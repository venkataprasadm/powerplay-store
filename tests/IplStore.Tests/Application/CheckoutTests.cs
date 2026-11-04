namespace IplStore.Tests.Application;

public class CheckoutTests
{
    private static async Task<Harness> AshaWithWalkthroughCart()
    {
        var h = await Harness.CreateAsync();
        await h.Add("asha", "mi-jersey", 2);
        await h.Add("asha", "csk-cap", 3);
        await h.Add("asha", "kkr-signed-photo", 1);
        return h;
    }

    [Fact]
    public async Task Walkthrough_order_confirms_deducts_stock_audits_and_clears_the_cart()
    {
        var h = await AshaWithWalkthroughCart();
        var id = Harness.OrderId();
        var result = await h.Checkout.PlaceOrderAsync("asha", id, 10494.00m, default);

        Assert.False(result.Replayed);
        Assert.Equal(10494.00m, result.Order.Total);
        Assert.Equal(OrderStatus.Confirmed, result.Order.Status);
        Assert.Equal((48, 97, 1), (await h.StockOf("mi-jersey"), await h.StockOf("csk-cap"), await h.StockOf("kkr-signed-photo")));
        Assert.Empty((await h.Cart.GetAsync("asha", default)).Lines);
        var audit = (await h.Catalog.GetAuditAsync("csk-cap", null, null, default)).Items.Single();
        Assert.Equal(($"order:{id}", "asha", 100m, 97m), (audit.Reason, audit.Actor, audit.OldValue, audit.NewValue));
    }

    [Fact]
    public async Task Retrying_with_the_same_order_id_returns_the_same_order_and_leaves_stock_alone()
    {
        var h = await AshaWithWalkthroughCart();
        var id = Harness.OrderId();
        var first = await h.Checkout.PlaceOrderAsync("asha", id, 10494.00m, default);
        var retry = await h.Checkout.PlaceOrderAsync("asha", id, 10494.00m, default);
        Assert.True(retry.Replayed);
        Assert.Equal((first.Order.Id, first.Order.Total, first.Order.CreatedUtc), (retry.Order.Id, retry.Order.Total, retry.Order.CreatedUtc));
        Assert.Equal(first.Order.Lines, retry.Order.Lines);
        Assert.Equal((48, 97, 1), (await h.StockOf("mi-jersey"), await h.StockOf("csk-cap"), await h.StockOf("kkr-signed-photo")));
        Assert.Single((await h.Orders.ListAsync("asha", null, null, null, null, default)).Items);
    }

    [Fact]
    public async Task Ten_simultaneous_retries_of_one_order_create_one_order_and_deduct_once()
    {
        var h = await AshaWithWalkthroughCart();
        var id = Harness.OrderId();
        var results = await Task.WhenAll(Enumerable.Range(0, 10).Select(_ => Task.Run(() => h.Checkout.PlaceOrderAsync("asha", id, 10494.00m, default))));
        Assert.Equal(1, results.Count(r => !r.Replayed));
        Assert.All(results, r => Assert.Equal(id, r.Order.Id));
        Assert.Equal(48, await h.StockOf("mi-jersey"));
        Assert.Single((await h.Orders.ListAsync("asha", null, null, null, null, default)).Items);
    }

    [Fact]
    public async Task Asha_and_Ravi_race_for_the_last_photo_exactly_one_order_is_created()
    {
        var h = await Harness.CreateAsync();
        await h.Admin("kkr-signed-photo", stock: 1);
        await h.Add("asha", "mi-jersey", 2); await h.Add("asha", "csk-cap", 3); await h.Add("asha", "kkr-signed-photo", 1);
        await h.Add("ravi", "kkr-signed-photo", 1);

        async Task<(bool Ok, string? Code, IReadOnlyList<FailedLine>? Failed)> Try(string user, decimal total)
        {
            try { await h.Checkout.PlaceOrderAsync(user, Harness.OrderId(), total, default); return (true, null, null); }
            catch (ConflictException ex) { return (false, ex.Code, (ex.Details as OutOfStockDetails)?.FailedLines); }
        }
        var outcomes = await Task.WhenAll(Task.Run(() => Try("asha", 10494.00m)), Task.Run(() => Try("ravi", 4999.00m)));

        Assert.Equal(1, outcomes.Count(o => o.Ok));
        var loser = outcomes.Single(o => !o.Ok);
        Assert.Equal("out_of_stock", loser.Code);
        Assert.Equal("kkr-signed-photo", loser.Failed!.Single().ProductId);
        Assert.Equal(0, await h.StockOf("kkr-signed-photo"));
        var loserUser = (await h.Orders.ListAsync("asha", null, null, null, null, default)).Items.Count == 1 ? "ravi" : "asha";
        Assert.Empty((await h.Orders.ListAsync(loserUser, null, null, null, null, default)).Items);   // no order for the loser
        Assert.NotEmpty((await h.Cart.GetAsync(loserUser, default)).Lines);                           // and the cart is kept
    }

    [Fact]
    public async Task Fifty_shoppers_for_five_photos_never_oversell()
    {
        var h = await Harness.CreateAsync();
        await h.Admin("kkr-signed-photo", stock: 5);
        for (var i = 0; i < 50; i++) await h.Add($"u{i}", "kkr-signed-photo", 1);

        var outcomes = await Task.WhenAll(Enumerable.Range(0, 50).Select(i => Task.Run(async () =>
        {
            try { await h.Checkout.PlaceOrderAsync($"u{i}", Harness.OrderId(), 4999.00m, default); return "ok"; }
            catch (ConflictException ex) { return ex.Code; }
        })));

        Assert.Equal(5, outcomes.Count(o => o == "ok"));
        Assert.Equal(45, outcomes.Count(o => o == "out_of_stock"));
        Assert.Equal(0, await h.StockOf("kkr-signed-photo"));
    }

    [Fact]
    public async Task Heavy_contention_on_a_plentiful_product_keeps_stock_and_orders_consistent()
    {
        var h = await Harness.CreateAsync();                         // MI jersey stock 50
        for (var i = 0; i < 20; i++) await h.Add($"u{i}", "mi-jersey", 2);       // 20 x 2 = 40 <= 50, so nobody should ever be told out of stock
        var outcomes = await Task.WhenAll(Enumerable.Range(0, 20).Select(i => Task.Run(async () =>
        {
            try { await h.Checkout.PlaceOrderAsync($"u{i}", Harness.OrderId(), 3998.00m, default); return "ok"; }
            catch (ConflictException ex) { return ex.Code; }
        })));
        var ok = outcomes.Count(o => o == "ok");
        Assert.Equal(50 - 2 * ok, await h.StockOf("mi-jersey"));                          // every unit sold is accounted for by an order
        Assert.All(outcomes.Where(o => o != "ok"), o => Assert.Equal("concurrency_conflict", o));   // only honest "retry" failures, never oversell
        Assert.True(ok >= 8);                                                          // retries guarantee progress: each lost round means someone else committed
    }

    [Fact]
    public async Task One_failing_line_rejects_the_whole_order_and_deducts_nothing()
    {
        var h = await Harness.CreateAsync();
        await h.Add("asha", "mi-jersey", 2);
        await h.Add("asha", "kkr-signed-photo", 2);
        await h.Admin("kkr-signed-photo", stock: 1);                 // someone else took one
        var auditBefore = (await h.Catalog.GetAuditAsync("mi-jersey", null, null, default)).Items.Count;

        var ex = await Assert.ThrowsAsync<ConflictException>(() => h.Checkout.PlaceOrderAsync("asha", Harness.OrderId(), 8998.00m, default));
        Assert.Equal("out_of_stock", ex.Code);
        var failed = ((OutOfStockDetails)ex.Details!).FailedLines;
        var line = Assert.Single(failed!);
        Assert.Equal(("kkr-signed-photo", 2, 1), (line.ProductId, line.Requested, line.Available));
        Assert.Equal(50, await h.StockOf("mi-jersey"));              // the healthy line was NOT deducted
        Assert.Equal(1, await h.StockOf("kkr-signed-photo"));
        Assert.Equal(2, (await h.Cart.GetAsync("asha", default)).Lines.Count);   // cart kept
        Assert.Empty((await h.Orders.ListAsync("asha", null, null, null, null, default)).Items);
        Assert.Equal(auditBefore, (await h.Catalog.GetAuditAsync("mi-jersey", null, null, default)).Items.Count);
    }

    [Fact]
    public async Task A_stale_product_version_at_commit_leaves_no_partial_state()
    {
        var h = await Harness.CreateAsync();
        var mi = (await h.Store.GetAsync("mi-jersey", default))!;
        var csk = (await h.Store.GetAsync("csk-cap", default))!;
        await h.Admin("csk-cap", price: 510m);                       // csk is now newer than the copy we hold
        var order = new Order { Id = Harness.OrderId(), UserId = "u", CreatedUtc = Harness.Start, Lines = [new("mi-jersey", "MI Jersey", 1, 1999m), new("csk-cap", "CSK Cap", 1, 499m)] };
        var changes = new[] { mi, csk }.Select(p => new StockChange(p.Deduct(1), new AuditEntry(Guid.NewGuid().ToString("N"), p.Id, AuditField.Stock, p.Stock, p.Stock - 1, "t", "u", Harness.Start))).ToList();

        await Assert.ThrowsAsync<ConcurrencyException>(() => h.Store.CommitAsync(order, changes, default));
        Assert.Equal(50, await h.StockOf("mi-jersey"));              // first change was rolled back with the failed second
        Assert.Null(await ((IOrderRepository)h.Store).GetAsync(order.Id, default));
    }

    [Fact]
    public async Task Order_lines_keep_the_purchase_price_after_later_price_changes()
    {
        var h = await Harness.CreateAsync();
        await h.Add("asha", "mi-jersey", 2); await h.Add("asha", "csk-cap", 3); await h.Add("asha", "kkr-signed-photo", 1);
        await h.Checkout.PlaceOrderAsync("asha", Harness.OrderId(), 10494.00m, default);
        await h.Admin("csk-cap", price: 549.00m);
        var order = (await h.Orders.ListAsync("asha", null, null, null, null, default)).Items.Single();
        Assert.Equal(10494.00m, order.Total);
        Assert.Equal(499.00m, order.Lines.Single(l => l.ProductId == "csk-cap").UnitPrice);
        Assert.Equal("CSK Cap", order.Lines.Single(l => l.ProductId == "csk-cap").ProductName);
        Assert.Equal(549.00m, (await h.Catalog.GetAsync("csk-cap", default)).Price);
    }

    [Fact]
    public async Task Checkout_after_a_price_change_requires_acceptance_of_the_new_total()
    {
        var h = await Harness.CreateAsync();
        await h.Add("asha", "csk-cap", 2);
        await h.Admin("csk-cap", price: 549.00m);

        var ex = await Assert.ThrowsAsync<ConflictException>(() => h.Checkout.PlaceOrderAsync("asha", Harness.OrderId(), 998.00m, default));
        Assert.Equal("cart_changed", ex.Code);
        Assert.Equal(100, await h.StockOf("csk-cap"));               // nothing deducted

        await h.Cart.AcceptChangesAsync("asha", Harness.Key(), default);
        var ok = await h.Checkout.PlaceOrderAsync("asha", Harness.OrderId(), 1098.00m, default);
        Assert.Equal(549.00m, ok.Order.Lines[0].UnitPrice);
    }

    [Fact]
    public async Task The_client_must_state_the_exact_total_it_showed()
    {
        var h = await Harness.CreateAsync();
        await h.Add("asha", "csk-cap", 2);
        Assert.Equal("cart_changed", (await Assert.ThrowsAsync<ConflictException>(() => h.Checkout.PlaceOrderAsync("asha", Harness.OrderId(), 1.00m, default))).Code);
        await Assert.ThrowsAsync<ValidationException>(() => h.Checkout.PlaceOrderAsync("asha", Harness.OrderId(), 998.001m, default));
    }

    [Fact]
    public async Task Empty_cart_cannot_check_out() =>
        Assert.Equal("cart_empty", (await Assert.ThrowsAsync<ConflictException>(async () => await (await Harness.CreateAsync()).Checkout.PlaceOrderAsync("asha", Harness.OrderId(), 0m, default))).Code);

    [Fact]
    public async Task An_order_id_cannot_be_claimed_by_a_second_user()
    {
        var h = await Harness.CreateAsync();
        await h.Add("asha", "csk-cap", 1); await h.Add("ravi", "csk-cap", 1);
        var id = Harness.OrderId();
        await h.Checkout.PlaceOrderAsync("asha", id, 499.00m, default);
        Assert.Equal("order_id_in_use", (await Assert.ThrowsAsync<ConflictException>(() => h.Checkout.PlaceOrderAsync("ravi", id, 499.00m, default))).Code);
        await Assert.ThrowsAsync<NotFoundException>(() => h.Orders.GetAsync("ravi", id, default));      // and cannot read it either
    }

    [Fact]
    public async Task Cart_flagged_with_missing_stock_cannot_be_checked_out_even_with_the_right_total()
    {
        var h = await Harness.CreateAsync();
        await h.Add("asha", "kkr-signed-photo", 2);
        await h.Admin("kkr-signed-photo", stock: 0);
        Assert.Equal("out_of_stock", (await Assert.ThrowsAsync<ConflictException>(() => h.Checkout.PlaceOrderAsync("asha", Harness.OrderId(), 9998.00m, default))).Code);
    }

    private static IShippingPolicy Paid() =>
        new ThresholdShippingPolicy(Microsoft.Extensions.Options.Options.Create(new ShippingOptions { FlatFee = 99m, FreeFrom = 999m }));

    [Fact]
    public async Task Cart_shows_the_shipping_and_grand_total_that_checkout_charges()
    {
        var h = await Harness.CreateAsync(Paid());
        var cart = await h.Add("ravi", "csk-cap", 1);
        Assert.Equal(499.00m, cart.Total);
        Assert.Equal(99.00m, cart.Shipping);
        Assert.Equal(598.00m, cart.GrandTotal);

        var placed = await h.Checkout.PlaceOrderAsync("ravi", Harness.OrderId(), cart.GrandTotal, default);
        Assert.Equal(99.00m, placed.Order.Shipping);
        Assert.Equal(598.00m, placed.Order.Total);
    }

    [Fact]
    public async Task A_client_that_ignores_shipping_is_told_the_real_total_and_nothing_is_charged()
    {
        var h = await Harness.CreateAsync(Paid());
        await h.Add("ravi", "csk-cap", 1);
        var ex = await Assert.ThrowsAsync<ConflictException>(() => h.Checkout.PlaceOrderAsync("ravi", Harness.OrderId(), 499.00m, default));
        Assert.Equal("cart_changed", ex.Code);
        Assert.Equal(598.00m, ((CartChangedDetails)ex.Details!).NewTotal);
        Assert.Equal(100, await h.StockOf("csk-cap"));
    }

    [Theory]
    [InlineData(998.99, 99.00)]   // just below the threshold
    [InlineData(999.00, 0.00)]    // exactly at the threshold ships free
    public async Task Free_shipping_starts_exactly_at_the_threshold(decimal price, decimal expectedShipping)
    {
        var h = await Harness.CreateAsync(Paid());
        await h.Admin("csk-cap", price: price);
        Assert.Equal(expectedShipping, (await h.Add("ravi", "csk-cap", 1)).Shipping);
    }

    [Fact]
    public void An_empty_cart_never_pays_shipping() => Assert.Equal(0m, Paid().Calculate(0m));
}
