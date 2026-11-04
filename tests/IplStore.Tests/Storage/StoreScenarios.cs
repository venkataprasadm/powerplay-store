namespace IplStore.Tests.Storage;

/// <summary>
/// One set of storage scenarios, run against both implementations. It proves the in-memory double that the fast tests rely on really behaves
/// like Cosmos DB for ETag compare-and-swap, atomic batches, duplicate detection and keyset ordering.
/// Cosmos runs only when COSMOS_TEST_CONNECTION is set (see README), otherwise those cases are reported as skipped.
/// </summary>
public static class StoreScenarios
{
    private static readonly DateTimeOffset T0 = new(2026, 1, 1, 0, 0, 0, TimeSpan.Zero);

    private static Product P(string id, string name = "Item", ProductType type = ProductType.Cap, string fr = "csk", int stock = 10, int seconds = 0) => new()
    {
        Id = id, Name = name, Type = type, FranchiseId = fr, FranchiseName = Franchises.Find(fr)!.Name, Price = 100m, Stock = stock, CreatedUtc = T0.AddSeconds(seconds),
    };

    private static AuditEntry Audit(string product, int from, int to, DateTimeOffset at, string reason = "t") =>
        new(Guid.NewGuid().ToString("N"), product, AuditField.Stock, from, to, reason, "tester", at);

    private static async Task<Product> Reload(StoreTestTarget s, string id) => (await s.Products.GetAsync(id, default))!;

    private static async Task Place(StoreTestTarget s, string orderId, string user, DateTimeOffset at, params string[] productIds)
    {
        var changes = new List<StockChange>();
        foreach (var id in productIds) { var p = await Reload(s, id); changes.Add(new StockChange(p.Deduct(1), Audit(id, p.Stock, p.Stock - 1, at, $"order:{orderId}"))); }
        var order = new Order { Id = orderId, UserId = user, CreatedUtc = at, Lines = productIds.Select(i => new OrderLine(i, i, 1, 100m)).ToList() };
        await s.Checkout.CommitAsync(order, changes, default);
    }

    public static readonly IReadOnlyDictionary<string, Func<StoreTestTarget, Task>> All = new Dictionary<string, Func<StoreTestTarget, Task>>
    {
        ["update_is_compare_and_swap_on_etag"] = async s =>
        {
            Assert.True(await s.Products.CreateIfAbsentAsync(P("a"), default));
            Assert.False(await s.Products.CreateIfAbsentAsync(P("a"), default));            // create is idempotent
            var v1 = await Reload(s, "a");
            var v2 = await s.Products.UpdateAsync(v1.WithPrice(150m), [Audit("a", 10, 10, T0)], default);
            Assert.NotEqual(v1.ETag, v2.ETag);
            await Assert.ThrowsAsync<ConcurrencyException>(() => s.Products.UpdateAsync(v1.WithPrice(999m), [Audit("a", 10, 10, T0)], default));
            Assert.Equal(150m, (await Reload(s, "a")).Price);
            Assert.Single((await s.Products.GetAuditAsync("a", null, 10, default)).Items);   // the rejected write left no audit row
        },

        ["batch_is_all_or_nothing_on_a_stale_etag"] = async s =>
        {
            await s.Products.CreateIfAbsentAsync(P("a"), default); await s.Products.CreateIfAbsentAsync(P("b"), default);
            var a = await Reload(s, "a"); var b = await Reload(s, "b");
            await s.Products.UpdateAsync(b.WithPrice(111m), [Audit("b", 10, 10, T0)], default);       // b is now newer than our copy
            var order = new Order { Id = Guid.NewGuid().ToString(), UserId = "u", CreatedUtc = T0, Lines = [new("a", "a", 1, 100m), new("b", "b", 1, 100m)] };
            await Assert.ThrowsAsync<ConcurrencyException>(() => s.Checkout.CommitAsync(order,
                [new(a.Deduct(1), Audit("a", 10, 9, T0)), new(b.Deduct(1), Audit("b", 10, 9, T0))], default));
            Assert.Equal(10, (await Reload(s, "a")).Stock);                                           // a was not deducted
            Assert.Null(await s.Orders.GetAsync(order.Id, default));
            Assert.Empty((await s.Products.GetAuditAsync("a", null, 10, default)).Items);
        },

        ["duplicate_order_id_rolls_back_the_whole_batch"] = async s =>
        {
            await s.Products.CreateIfAbsentAsync(P("a", stock: 5), default);
            var id = Guid.NewGuid().ToString();
            await Place(s, id, "u", T0, "a");
            Assert.Equal(4, (await Reload(s, "a")).Stock);
            await Assert.ThrowsAsync<DuplicateOrderException>(() => Place(s, id, "u", T0.AddSeconds(1), "a"));
            Assert.Equal(4, (await Reload(s, "a")).Stock);                                            // second deduction rolled back
            Assert.Single((await s.Products.GetAuditAsync("a", null, 10, default)).Items);
        },

        ["concurrent_commits_for_the_last_unit_only_one_wins"] = async s =>
        {
            await s.Products.CreateIfAbsentAsync(P("last", stock: 1), default);
            var snapshot = await Reload(s, "last");
            var results = await Task.WhenAll(Enumerable.Range(0, 8).Select(i => Task.Run(async () =>
            {
                var order = new Order { Id = Guid.NewGuid().ToString(), UserId = $"u{i}", CreatedUtc = T0, Lines = [new("last", "last", 1, 100m)] };
                try { await s.Checkout.CommitAsync(order, [new(snapshot.Deduct(1), Audit("last", 1, 0, T0))], default); return true; }
                catch (ConcurrencyException) { return false; }
            })));
            Assert.Equal(1, results.Count(r => r));
            Assert.Equal(0, (await Reload(s, "last")).Stock);
        },

        ["keyset_paging_and_filters"] = async s =>
        {
            await s.Products.CreateIfAbsentAsync(P("p1", "MI Jersey", ProductType.Jersey, "mi", seconds: 1), default);
            await s.Products.CreateIfAbsentAsync(P("p2", "CSK Cap", ProductType.Cap, "csk", seconds: 2), default);
            await s.Products.CreateIfAbsentAsync(P("p3", "CSK Jersey", ProductType.Jersey, "csk", seconds: 3), default);
            await s.Products.CreateIfAbsentAsync(P("p4", "MI Cap", ProductType.Cap, "mi", seconds: 3), default);   // same timestamp as p3: id breaks the tie
            await s.Products.CreateIfAbsentAsync(P("p5", "RCB Flag", ProductType.Flag, "rcb", seconds: 5), default);
            var seen = new List<string>(); KeysetCursor? after = null; bool more;
            do
            {
                var slice = await s.Products.SearchAsync(new ProductQuery(null, null, null, 2, after), default);
                seen.AddRange(slice.Items.Select(p => p.Id));
                after = slice.Items.Count == 0 ? null : new KeysetCursor(Timestamp.Format(slice.Items[^1].CreatedUtc), slice.Items[^1].Id);
                more = slice.HasMore;
            } while (more);
            Assert.Equal(["p1", "p2", "p3", "p4", "p5"], seen);
            Assert.Equal(5, await s.Products.CountAsync(new ProductQuery(null, null, null, 10, null), default));
            Assert.Equal(["p2"], (await s.Products.SearchAsync(new ProductQuery("csk", ProductType.Cap, "csk", 10, null), default)).Items.Select(p => p.Id));
            Assert.Equal(["p1", "p3"], (await s.Products.SearchAsync(new ProductQuery("JERS", null, null, 10, null), default)).Items.Select(p => p.Id));
            Assert.Equal(2, (await s.Products.GetManyAsync(["p1", "p5", "missing"], default)).Count);
        },

        ["orders_are_listed_newest_first_with_range_and_paging"] = async s =>
        {
            await s.Products.CreateIfAbsentAsync(P("a", stock: 100), default);
            var ids = new List<string>();
            for (var i = 0; i < 7; i++) { var id = Guid.NewGuid().ToString(); ids.Add(id); await Place(s, id, "asha", T0.AddHours(i), "a"); }
            await Place(s, Guid.NewGuid().ToString(), "ravi", T0.AddHours(3), "a");
            var seen = new List<string>(); KeysetCursor? after = null; bool more;
            do
            {
                var slice = await s.Orders.ListAsync(new OrderQuery("asha", null, null, 3, after), default);
                seen.AddRange(slice.Items.Select(o => o.Id));
                after = slice.Items.Count == 0 ? null : new KeysetCursor(Timestamp.Format(slice.Items[^1].CreatedUtc), slice.Items[^1].Id);
                more = slice.HasMore;
            } while (more);
            Assert.Equal(Enumerable.Reverse(ids), seen);
            var ranged = await s.Orders.ListAsync(new OrderQuery("asha", T0.AddHours(2), T0.AddHours(5), 10, null), default);
            Assert.Equal(ids.Skip(2).Take(3).Reverse(), ranged.Items.Select(o => o.Id));              // from inclusive, to exclusive
            var stored = await s.Orders.GetAsync(ids[0], default);
            Assert.Equal(100m, stored!.Total);
        },

        ["cart_create_replace_and_delete_use_etags"] = async s =>
        {
            var cart = new Cart { UserId = "asha", Lines = [new("a", "A", 2, 100m)] };
            var v1 = await s.Carts.SaveAsync(cart, default);
            Assert.NotNull(v1.ETag);
            await Assert.ThrowsAsync<ConcurrencyException>(() => s.Carts.SaveAsync(cart, default));    // create-if-absent
            var v2 = await s.Carts.SaveAsync(v1 with { Lines = [new("a", "A", 3, 100m)] }, default);
            await Assert.ThrowsAsync<ConcurrencyException>(() => s.Carts.SaveAsync(v1 with { Lines = [] }, default));   // stale
            var read = await s.Carts.GetAsync("asha", default);
            Assert.Equal(3, read!.Lines[0].Quantity);
            Assert.Equal(v2.ETag, read.ETag);
            await s.Carts.DeleteAsync("asha", default);
            await s.Carts.DeleteAsync("asha", default);                                                  // deleting twice is fine
            Assert.Null(await s.Carts.GetAsync("asha", default));
        },

        ["processed_request_ids_survive_a_round_trip"] = async s =>
        {
            var saved = await s.Carts.SaveAsync(new Cart { UserId = "u", Lines = [new("a", "A", 1, 10m)] }.Record("req-1", T0), default);
            var read = (await s.Carts.GetAsync("u", default))!;
            var done = read.FindProcessed("req-1")!;
            Assert.Equal(T0, done.AtUtc);
            Assert.Equal(saved.Lines, done.ResultLines);
        },
    };
}
