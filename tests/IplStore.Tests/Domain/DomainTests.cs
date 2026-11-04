namespace IplStore.Tests.Domain;

public class DomainTests
{
    [Theory]
    [InlineData(499.00, true)] [InlineData(0.01, true)] [InlineData(499.001, false)]
    [InlineData(0, false)] [InlineData(-1, false)] [InlineData(10_000_000.00, true)] [InlineData(10_000_000.01, false)]
    public void Price_must_be_positive_with_at_most_two_decimals(double raw, bool valid) =>
        Assert.Equal(valid, Money.IsValidPrice((decimal)raw));

    [Theory]
    [InlineData(0, false)] [InlineData(1, true)] [InlineData(10, true)] [InlineData(11, false)] [InlineData(-3, false)]
    public void Cart_quantity_boundaries(int quantity, bool valid)
    {
        if (valid) Cart.ValidateQuantity(quantity);
        else Assert.Equal("quantity_limit", Assert.Throws<ValidationException>(() => Cart.ValidateQuantity(quantity)).Code);
    }

    private static Product P(int stock) => new()
    {
        Id = "p", Name = "P", Type = ProductType.Cap, FranchiseId = "mi", FranchiseName = "Mumbai Indians", Price = 10m, Stock = stock,
    };

    [Fact] public void Deducting_the_last_unit_reaches_exactly_zero() => Assert.Equal(0, P(1).Deduct(1).Stock);

    [Fact]
    public void Deducting_more_than_stock_is_rejected_and_never_goes_negative()
    {
        var ex = Assert.Throws<ConflictException>(() => P(1).Deduct(2));
        Assert.Equal("insufficient_stock", ex.Code);
    }

    [Fact] public void Deduct_zero_or_negative_is_invalid() { Assert.Throws<ValidationException>(() => P(5).Deduct(0)); Assert.Throws<ValidationException>(() => P(5).Deduct(-1)); }

    [Fact]
    public void Deduct_returns_a_new_instance_and_leaves_the_original_untouched()
    {
        var before = P(5);
        var after = before.Deduct(2);
        Assert.Equal(5, before.Stock);
        Assert.Equal(3, after.Stock);
    }

    [Theory]
    [InlineData(0, StockStatus.OutOfStock)] [InlineData(1, StockStatus.LowStock)] [InlineData(5, StockStatus.LowStock)] [InlineData(6, StockStatus.InStock)]
    public void Stock_status_thresholds(int stock, StockStatus expected) => Assert.Equal(expected, P(stock).StockStatus);

    [Fact]
    public void Adding_to_a_line_cannot_push_it_over_the_cap()
    {
        var cart = new Cart { UserId = "u" }.Add(P(100), 8);
        Assert.Throws<ValidationException>(() => cart.Add(P(100), 3));
        Assert.Equal(10, cart.Add(P(100), 2).Lines[0].Quantity);
    }

    [Fact]
    public void Timestamps_sort_as_text_in_time_order()
    {
        var a = DateTimeOffset.Parse("2026-01-01T00:00:00Z");
        var times = new[] { a, a.AddTicks(1), a.AddMilliseconds(100), a.AddSeconds(1), a.AddDays(400) };
        Assert.Equal(times.Select(Timestamp.Format), times.Select(Timestamp.Format).OrderBy(x => x, StringComparer.Ordinal));
        Assert.Equal(a.AddTicks(1), Timestamp.Parse(Timestamp.Format(a.AddTicks(1))));
    }

    [Fact]
    public void Order_totals_use_the_snapshot_prices()
    {
        var order = new Order { Id = "o", UserId = "u", Lines = [new("a", "A", 2, 1999.00m), new("b", "B", 3, 499.00m), new("c", "C", 1, 4999.00m)] };
        Assert.Equal(10494.00m, order.Total);
    }
}
