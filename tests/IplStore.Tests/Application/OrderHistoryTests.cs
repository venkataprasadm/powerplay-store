namespace IplStore.Tests.Application;

public class OrderHistoryTests
{
    private static async Task<(Harness H, List<string> Ids)> With25Orders()
    {
        var h = await Harness.CreateAsync();
        var ids = new List<string>();
        for (var i = 0; i < 25; i++)
        {
            h.Clock.Advance(TimeSpan.FromHours(6));
            await h.Add("asha", "mi-cap", 1);
            var id = Harness.OrderId(); ids.Add(id);
            await h.Checkout.PlaceOrderAsync("asha", id, 549.00m, default);
        }
        return (h, ids);
    }

    [Fact]
    public async Task History_is_newest_first_and_pages_without_gaps()
    {
        var (h, ids) = await With25Orders();
        var seen = new List<string>(); string? token = null; var sizes = new List<int>();
        do { var p = await h.Orders.ListAsync("asha", null, null, 10, token, default); seen.AddRange(p.Items.Select(o => o.Id)); sizes.Add(p.Items.Count); token = p.NextPageToken; } while (token is not null);
        Assert.Equal([10, 10, 5], sizes);
        Assert.Equal(Enumerable.Reverse(ids), seen);
    }

    [Fact]
    public async Task Date_range_filters_by_utc_instants_with_inclusive_from_and_exclusive_to()
    {
        var (h, _) = await With25Orders();                     // orders at Start + 6h, 12h, ... (4 per day)
        var from = Harness.Start.AddDays(1);
        var to = Harness.Start.AddDays(2);
        var page = await h.Orders.ListAsync("asha", from, to, 50, null, default);
        Assert.Equal(4, page.Items.Count);
        Assert.All(page.Items, o => Assert.InRange(o.CreatedUtc, from, to.AddTicks(-1)));
        Assert.Equal(h.Clock.GetUtcNow().Offset, TimeSpan.Zero);          // stored in UTC
        await Assert.ThrowsAsync<ValidationException>(() => h.Orders.ListAsync("asha", to, from, null, null, default));
    }

    [Fact]
    public async Task Users_only_see_their_own_orders()
    {
        var (h, _) = await With25Orders();
        Assert.Empty((await h.Orders.ListAsync("ravi", null, null, null, null, default)).Items);
    }
}
