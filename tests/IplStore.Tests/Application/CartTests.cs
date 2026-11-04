namespace IplStore.Tests.Application;

public class CartTests
{
    [Fact]
    public async Task Walkthrough_totals_and_double_submitted_add_is_applied_once()
    {
        var h = await Harness.CreateAsync();
        var c1 = await h.Add("asha", "mi-jersey", 2);
        Assert.Equal(3998.00m, c1.Total);
        var c2 = await h.Add("asha", "csk-cap", 3);
        Assert.Equal(1497.00m, c2.Lines.Single(l => l.ProductId == "csk-cap").LineTotal);
        Assert.Equal(5495.00m, c2.Total);

        var key = Harness.Key();                                   // double click: same request id sent twice
        var first = await h.Add("asha", "kkr-signed-photo", 1, key);
        var second = await h.Add("asha", "kkr-signed-photo", 1, key);
        Assert.Equal(10494.00m, first.Total);
        Assert.Equal(first.Total, second.Total);
        Assert.Equal(1, second.Lines.Single(l => l.ProductId == "kkr-signed-photo").Quantity);
    }

    [Theory]
    [InlineData(1)] [InlineData(10)]
    public async Task Quantity_at_the_boundaries_is_accepted(int qty) =>
        Assert.Equal(qty, (await (await Harness.CreateAsync()).Add("u", "csk-cap", qty)).Lines[0].Quantity);

    [Theory]
    [InlineData(0)] [InlineData(11)] [InlineData(-1)]
    public async Task Quantity_outside_one_to_ten_is_rejected(int qty) =>
        Assert.Equal("quantity_limit", (await Assert.ThrowsAsync<ValidationException>(async () => await (await Harness.CreateAsync()).Add("u", "csk-cap", qty))).Code);

    [Fact]
    public async Task Repeated_adds_cannot_exceed_ten_in_total()
    {
        var h = await Harness.CreateAsync();
        await h.Add("u", "csk-cap", 6);
        await Assert.ThrowsAsync<ValidationException>(() => h.Add("u", "csk-cap", 5));
        Assert.Equal(10, (await h.Add("u", "csk-cap", 4)).Lines[0].Quantity);
    }

    [Fact]
    public async Task Quantity_cannot_exceed_available_stock_but_may_equal_it()
    {
        var h = await Harness.CreateAsync();                         // KKR photo stock is 2
        var ex = await Assert.ThrowsAsync<ConflictException>(() => h.Add("u", "kkr-signed-photo", 3));
        Assert.Equal("insufficient_stock", ex.Code);
        Assert.Equal(2, (await h.Add("u", "kkr-signed-photo", 2)).Lines[0].Quantity);
        await Assert.ThrowsAsync<ConflictException>(() => h.Add("u", "kkr-signed-photo", 1));   // 2 + 1 > 2
    }

    [Fact]
    public async Task Out_of_stock_product_cannot_be_added()
    {
        var h = await Harness.CreateAsync();
        await h.Admin("kkr-signed-photo", stock: 0);
        Assert.Equal("insufficient_stock", (await Assert.ThrowsAsync<ConflictException>(() => h.Add("u", "kkr-signed-photo", 1))).Code);
    }

    [Fact] public async Task Unknown_product_is_not_found() => await Assert.ThrowsAsync<NotFoundException>(async () => await (await Harness.CreateAsync()).Add("u", "ghost", 1));

    [Fact]
    public async Task Change_quantity_and_remove()
    {
        var h = await Harness.CreateAsync();
        await h.Add("u", "mi-jersey", 2);
        var changed = await h.Cart.SetQuantityAsync("u", Harness.Key(), "mi-jersey", 5, default);
        Assert.Equal(5, changed.Lines[0].Quantity);
        await Assert.ThrowsAsync<ValidationException>(() => h.Cart.SetQuantityAsync("u", Harness.Key(), "mi-jersey", 11, default));
        await Assert.ThrowsAsync<NotFoundException>(() => h.Cart.SetQuantityAsync("u", Harness.Key(), "csk-cap", 1, default));
        Assert.Empty((await h.Cart.RemoveAsync("u", Harness.Key(), "mi-jersey", default)).Lines);
        await Assert.ThrowsAsync<NotFoundException>(() => h.Cart.RemoveAsync("u", Harness.Key(), "mi-jersey", default));
    }

    [Fact]
    public async Task Replaying_a_request_id_returns_the_original_result_even_after_later_changes()
    {
        var h = await Harness.CreateAsync();
        var k1 = Harness.Key();
        await h.Add("u", "mi-jersey", 2, k1);
        await h.Cart.SetQuantityAsync("u", Harness.Key(), "mi-jersey", 5, default);
        var replay = await h.Add("u", "mi-jersey", 2, k1);
        Assert.Equal(2, replay.Lines[0].Quantity);                                          // original outcome
        Assert.Equal(5, (await h.Cart.GetAsync("u", default)).Lines[0].Quantity);          // and nothing was applied again
    }

    [Fact]
    public async Task Ten_simultaneous_copies_of_one_request_apply_exactly_once()
    {
        var h = await Harness.CreateAsync();
        var key = Harness.Key();
        var results = await Task.WhenAll(Enumerable.Range(0, 10).Select(_ => Task.Run(() => h.Add("u", "mi-jersey", 2, key))));
        Assert.All(results, r => Assert.Equal(2, r.Lines.Single().Quantity));
        Assert.Equal(2, (await h.Cart.GetAsync("u", default)).Lines.Single().Quantity);
    }

    [Fact]
    public async Task Simultaneous_different_requests_on_one_cart_are_all_kept()
    {
        var h = await Harness.CreateAsync();
        var ids = new[] { "mi-jersey", "csk-cap", "rcb-flag", "csk-jersey", "mi-cap" };
        await Task.WhenAll(ids.Select(id => Task.Run(() => h.Add("u", id, 1))));
        Assert.Equal(ids.Order(), (await h.Cart.GetAsync("u", default)).Lines.Select(l => l.ProductId).Order());
    }

    [Fact]
    public async Task Carts_are_per_user()
    {
        var h = await Harness.CreateAsync();
        await h.Add("asha", "mi-jersey", 1);
        Assert.Empty((await h.Cart.GetAsync("ravi", default)).Lines);
    }

    [Fact]
    public async Task A_price_change_is_flagged_and_must_be_accepted()
    {
        var h = await Harness.CreateAsync();
        await h.Add("asha", "csk-cap", 2);
        await h.Admin("csk-cap", price: 549.00m);
        var flagged = await h.Cart.GetAsync("asha", default);
        Assert.True(flagged.RequiresAcceptance);
        Assert.False(flagged.CanCheckout);
        Assert.Equal(1098.00m, flagged.Total);
        Assert.Equal(998.00m, flagged.AcceptedTotal);
        Assert.True(flagged.Lines[0].PriceChanged);

        var accepted = await h.Cart.AcceptChangesAsync("asha", Harness.Key(), default);
        Assert.False(accepted.RequiresAcceptance);
        Assert.True(accepted.CanCheckout);
        Assert.Equal(549.00m, accepted.Lines[0].AcceptedUnitPrice);
    }

    [Fact]
    public async Task A_stock_drop_below_the_cart_quantity_is_flagged()
    {
        var h = await Harness.CreateAsync();
        await h.Add("asha", "kkr-signed-photo", 2);
        await h.Admin("kkr-signed-photo", stock: 1);
        var view = await h.Cart.GetAsync("asha", default);
        Assert.True(view.Lines[0].InsufficientStock);
        Assert.False(view.CanCheckout);
    }

    [Fact]
    public async Task A_cart_holds_at_most_twenty_distinct_products()
    {
        var h = await Harness.CreateAsync();
        for (var i = 0; i < 25; i++)
            await h.Store.CreateIfAbsentAsync(new Product { Id = $"bulk-{i:00}", Name = $"Bulk {i}", Type = ProductType.Other, FranchiseId = "dc", FranchiseName = "Delhi Capitals", Price = 5m, Stock = 5, CreatedUtc = Harness.Start }, default);
        for (var i = 0; i < Cart.MaxLines; i++) await h.Add("u", $"bulk-{i:00}", 1);
        await Assert.ThrowsAsync<ValidationException>(() => h.Add("u", "bulk-20", 1));
    }

    [Fact]
    public async Task Reusing_a_request_id_for_a_different_change_is_rejected_and_changes_nothing()
    {
        var h = await Harness.CreateAsync();
        var key = Harness.Key();
        await h.Add("asha", "csk-cap", 1, key);
        var ex = await Assert.ThrowsAsync<ConflictException>(() => h.Add("asha", "csk-cap", 3, key));
        Assert.Equal("idempotency_key_reused", ex.Code);
        await Assert.ThrowsAsync<ConflictException>(() => h.Cart.RemoveAsync("asha", key, "csk-cap", default));
        Assert.Equal(1, (await h.Cart.GetAsync("asha", default)).Lines.Single().Quantity);
    }

    [Fact]
    public async Task Cart_lines_carry_type_and_franchise_for_display()
    {
        var h = await Harness.CreateAsync();
        var line = (await h.Add("asha", "kkr-signed-photo", 1)).Lines.Single();
        Assert.Equal(ProductType.SignedPhotograph, line.ProductType);
        Assert.Equal("kkr", line.FranchiseId);
    }
}
