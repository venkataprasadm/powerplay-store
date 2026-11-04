namespace IplStore.Tests.Application;

public class CatalogTests
{
    [Fact]
    public async Task First_page_matches_the_walkthrough_and_links_to_the_next()
    {
        var h = await Harness.CreateAsync();
        var page = await h.Catalog.SearchAsync(null, null, null, 2, null, default);
        Assert.Equal(["mi-jersey", "csk-cap"], page.Items.Select(i => i.Id));
        Assert.Equal(6, page.TotalCount);
        Assert.NotNull(page.NextPageToken);
        Assert.Equal(1999.00m, page.Items[0].Price);
        Assert.Equal(499.00m, page.Items[1].Price);
    }

    [Fact]
    public async Task Walking_every_page_returns_each_product_once_in_a_stable_order()
    {
        var h = await Harness.CreateAsync();
        var seen = new List<string>(); string? token = null;
        do { var p = await h.Catalog.SearchAsync(null, null, null, 4, token, default); seen.AddRange(p.Items.Select(i => i.Id)); token = p.NextPageToken; } while (token is not null);
        Assert.Equal(6, seen.Count);
        Assert.Equal(seen.Distinct(), seen);
    }

    [Fact]
    public async Task Large_catalogue_pages_in_order_without_gaps_or_repeats()
    {
        var h = await Harness.CreateAsync();
        for (var i = 0; i < 120; i++)
            await h.Store.CreateIfAbsentAsync(new Product { Id = $"extra-{i:000}", Name = $"Extra {i}", Type = ProductType.Other, FranchiseId = "dc", FranchiseName = "Delhi Capitals", Price = 10m, Stock = 1, CreatedUtc = Harness.Start.AddMinutes(i / 7) }, default);
        var seen = new List<string>(); string? token = null; var pages = 0;
        do { var p = await h.Catalog.SearchAsync(null, null, null, 50, token, default); seen.AddRange(p.Items.Select(i => i.Id)); token = p.NextPageToken; pages++; } while (token is not null);
        Assert.Equal(126, seen.Count);
        Assert.Equal(3, pages);
        Assert.Equal(seen.Distinct().Count(), seen.Count);
    }

    [Fact]
    public async Task A_product_added_while_paging_does_not_shift_or_duplicate_items()
    {
        var h = await Harness.CreateAsync();
        var first = await h.Catalog.SearchAsync(null, null, null, 3, null, default);
        await h.Store.CreateIfAbsentAsync(new Product { Id = "new-arrival", Name = "Aaa First Alphabetically", Type = ProductType.Other, FranchiseId = "dc", FranchiseName = "Delhi Capitals", Price = 10m, Stock = 1, CreatedUtc = Harness.Start }, default);
        // An item that sorts BEFORE the cursor must not appear later (it would shift every following page under offset paging).
        await h.Store.CreateIfAbsentAsync(new Product { Id = "old-arrival", Name = "Old", Type = ProductType.Other, FranchiseId = "dc", FranchiseName = "Delhi Capitals", Price = 10m, Stock = 1, CreatedUtc = Harness.Start.AddYears(-5) }, default);
        var second = await h.Catalog.SearchAsync(null, null, null, 10, first.NextPageToken, default);
        Assert.DoesNotContain("old-arrival", second.Items.Select(i => i.Id));
        Assert.Empty(first.Items.Select(i => i.Id).Intersect(second.Items.Select(i => i.Id)));
        Assert.Contains("new-arrival", second.Items.Select(i => i.Id));
    }

    [Fact]
    public async Task Page_size_is_capped_and_must_be_positive()
    {
        var h = await Harness.CreateAsync();
        Assert.Equal(6, (await h.Catalog.SearchAsync(null, null, null, 500, null, default)).Items.Count);
        await Assert.ThrowsAsync<ValidationException>(() => h.Catalog.SearchAsync(null, null, null, 0, null, default));
        await Assert.ThrowsAsync<ValidationException>(() => h.Catalog.SearchAsync(null, null, null, -5, null, default));
        Assert.Equal(StoreOptions.MaxPageSize, PageToken.NormalizeSize(10_000));
    }

    [Fact]
    public async Task Type_and_franchise_filters_combine()
    {
        var h = await Harness.CreateAsync();
        var page = await h.Catalog.SearchAsync(null, "Cap", "csk", null, null, default);
        var only = Assert.Single(page.Items);
        Assert.Equal("CSK Cap", only.Name);
        Assert.Equal(499.00m, only.Price);
        Assert.True(only.InStock);
        Assert.Equal(1, page.TotalCount);
    }

    [Theory]
    [InlineData("jers", 2)] [InlineData("JERSEY", 2)] [InlineData("  csk ", 2)] [InlineData("nothing-matches", 0)]
    public async Task Name_search_is_case_insensitive_and_partial(string q, int expected)
    {
        var h = await Harness.CreateAsync();
        Assert.Equal(expected, (await h.Catalog.SearchAsync(q, null, null, null, null, default)).Items.Count);
    }

    [Fact]
    public async Task Text_type_and_franchise_all_apply_together()
    {
        var h = await Harness.CreateAsync();
        Assert.Single((await h.Catalog.SearchAsync("jersey", "jersey", "mi", null, null, default)).Items);
        Assert.Empty((await h.Catalog.SearchAsync("jersey", "cap", "mi", null, null, default)).Items);
    }

    [Fact]
    public async Task Bad_filters_and_tokens_are_validation_errors()
    {
        var h = await Harness.CreateAsync();
        await Assert.ThrowsAsync<ValidationException>(() => h.Catalog.SearchAsync(null, "hat", null, null, null, default));
        await Assert.ThrowsAsync<ValidationException>(() => h.Catalog.SearchAsync(null, "7", null, null, null, default));
        await Assert.ThrowsAsync<ValidationException>(() => h.Catalog.SearchAsync(null, null, null, null, "not-a-token", default));
        await Assert.ThrowsAsync<ValidationException>(() => h.Catalog.SearchAsync(new string('x', 101), null, null, null, null, default));
    }

    [Fact] public async Task Unknown_product_is_not_found() => await Assert.ThrowsAsync<NotFoundException>(async () => await (await Harness.CreateAsync()).Catalog.GetAsync("nope", default));

    [Fact]
    public async Task Admin_update_with_the_current_etag_changes_price_and_records_an_audit_entry()
    {
        var h = await Harness.CreateAsync();
        var before = await h.Catalog.GetAsync("csk-cap", default);
        var after = await h.Catalog.UpdateAsync("csk-cap", before.Etag, 549.00m, null, "festival pricing", "admin-1", default);
        Assert.Equal(549.00m, after.Price);
        Assert.NotEqual(before.Etag, after.Etag);
        var audit = Assert.Single((await h.Catalog.GetAuditAsync("csk-cap", null, null, default)).Items);
        Assert.Equal((AuditField.Price, 499.00m, 549.00m, "festival pricing", "admin-1"), (audit.Field, audit.OldValue, audit.NewValue, audit.Reason, audit.Actor));
        Assert.Equal(h.Clock.GetUtcNow(), audit.AtUtc);
    }

    [Fact]
    public async Task Changing_price_and_stock_together_writes_one_audit_entry_each()
    {
        var h = await Harness.CreateAsync();
        var p = await h.Catalog.GetAsync("rcb-flag", default);
        await h.Catalog.UpdateAsync("rcb-flag", p.Etag, 349.00m, 150, "reprice and recount", "admin", default);
        var audit = (await h.Catalog.GetAuditAsync("rcb-flag", null, null, default)).Items;
        Assert.Equal([AuditField.Price, AuditField.Stock], audit.Select(a => a.Field).OrderBy(f => f));
    }

    [Fact]
    public async Task Stale_etag_is_rejected_with_precondition_failed_and_nothing_is_applied()
    {
        var h = await Harness.CreateAsync();
        var stale = (await h.Catalog.GetAsync("csk-cap", default)).Etag;
        await h.Admin("csk-cap", price: 520m);
        await Assert.ThrowsAsync<PreconditionFailedException>(() => h.Catalog.UpdateAsync("csk-cap", stale, 999m, null, "late write", "admin", default));
        Assert.Equal(520m, (await h.Catalog.GetAsync("csk-cap", default)).Price);
    }

    [Fact]
    public async Task Missing_if_match_is_precondition_required()
    {
        var h = await Harness.CreateAsync();
        await Assert.ThrowsAsync<PreconditionRequiredException>(() => h.Catalog.UpdateAsync("csk-cap", null, 1m, null, "no header", "admin", default));
    }

    [Fact]
    public async Task Two_admins_updating_from_the_same_version_only_one_wins()
    {
        var h = await Harness.CreateAsync();
        var etag = (await h.Catalog.GetAsync("mi-jersey", default)).Etag;
        var attempts = Enumerable.Range(0, 10).Select(i => Task.Run(async () =>
        {
            try { await h.Catalog.UpdateAsync("mi-jersey", etag, 1000m + i, null, "race", $"admin-{i}", default); return true; }
            catch (PreconditionFailedException) { return false; }
        })).ToArray();
        var results = await Task.WhenAll(attempts);
        Assert.Equal(1, results.Count(r => r));
        Assert.Single((await h.Catalog.GetAuditAsync("mi-jersey", null, null, default)).Items);
    }

    [Theory]
    [InlineData(499.001, null)] [InlineData(0.0, null)] [InlineData(-5.0, null)] [InlineData(null, -1)] [InlineData(null, 1_000_001)]
    public async Task Invalid_price_or_stock_is_rejected(double? price, int? stock)
    {
        var h = await Harness.CreateAsync();
        var etag = (await h.Catalog.GetAsync("csk-cap", default)).Etag;
        await Assert.ThrowsAsync<ValidationException>(() => h.Catalog.UpdateAsync("csk-cap", etag, price is null ? null : (decimal)price, stock, "bad input", "admin", default));
    }

    [Fact]
    public async Task Reason_is_mandatory_and_something_must_change()
    {
        var h = await Harness.CreateAsync();
        var etag = (await h.Catalog.GetAsync("csk-cap", default)).Etag;
        await Assert.ThrowsAsync<ValidationException>(() => h.Catalog.UpdateAsync("csk-cap", etag, 1m, null, "  ", "admin", default));
        await Assert.ThrowsAsync<ValidationException>(() => h.Catalog.UpdateAsync("csk-cap", etag, null, null, "nothing to do", "admin", default));
    }

    [Fact]
    public async Task Audit_trail_is_newest_first_and_pages()
    {
        var h = await Harness.CreateAsync();
        for (var i = 1; i <= 5; i++) { h.Clock.Advance(TimeSpan.FromMinutes(1)); await h.Admin("csk-cap", stock: 100 + i, reason: $"count {i}"); }
        var first = await h.Catalog.GetAuditAsync("csk-cap", 2, null, default);
        var second = await h.Catalog.GetAuditAsync("csk-cap", 2, first.NextPageToken, default);
        Assert.Equal(["count 5", "count 4"], first.Items.Select(a => a.Reason));
        Assert.Equal(["count 3", "count 2"], second.Items.Select(a => a.Reason));
    }
}
