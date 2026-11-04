using System.Text.RegularExpressions;
using IplStore.Application.Abstractions;
using IplStore.Application.Contracts;
using IplStore.Application.Paging;
using IplStore.Domain.Catalogue;
using IplStore.Domain.Common;
using IplStore.Domain.Exceptions;
using Microsoft.Extensions.Logging;

namespace IplStore.Application.Services;

public sealed partial class CatalogService
{
    private const int MaxSearchTextLength = 100;
    private const int MinReasonLength = 3;
    private const int MaxReasonLength = 200;
    private const string StaleProductMessage = "The product was changed by someone else. Reload and try again.";

    private readonly IProductRepository _products;
    private readonly TimeProvider _clock;
    private readonly ILogger<CatalogService> _logger;

    public CatalogService(IProductRepository products, TimeProvider clock, ILogger<CatalogService> logger)
    {
        _products = products;
        _clock = clock;
        _logger = logger;
    }

    public async Task<Page<ProductDto>> SearchAsync(
        string? q, string? type, string? franchise, int? pageSize, string? pageToken, CancellationToken ct)
    {
        var text = string.IsNullOrWhiteSpace(q) ? null : q.Trim();
        if (text is { Length: > MaxSearchTextLength })
        {
            throw new ValidationException("q", $"Search text must be at most {MaxSearchTextLength} characters.");
        }

        var productType = ParseProductType(type);
        var franchiseId = string.IsNullOrWhiteSpace(franchise) ? null : franchise.Trim().ToLowerInvariant();
        var after = PageToken.Decode(pageToken);
        var query = new ProductQuery(text, productType, franchiseId, PageToken.NormalizeSize(pageSize), after);

        var slice = await _products.SearchAsync(query, ct);

        // The count is a cheap aggregate inside one partition. Later pages skip it because it never changes the answer.
        int? totalCount = after is null ? await _products.CountAsync(query, ct) : null;

        var items = slice.Items.Select(ProductDto.From).ToList();
        var nextToken = PageToken.ForNextPage(slice, product => new KeysetCursor(Timestamp.Format(product.CreatedUtc), product.Id));
        return new Page<ProductDto>(items, nextToken, totalCount);
    }

    public async Task<ProductDto> GetAsync(string id, CancellationToken ct)
    {
        var product = await LoadAsync(id, ct);
        return ProductDto.From(product);
    }

    /// <summary>
    /// Admin change of price and/or stock. The caller must send the ETag of the version they saw (If-Match).
    /// A stale version is rejected, never merged.
    /// </summary>
    public async Task<ProductDto> UpdateAsync(
        string id, string? ifMatch, decimal? price, int? stock, string? reason, string actor, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(ifMatch))
        {
            throw new PreconditionRequiredException("An If-Match header with the product's ETag is required.");
        }

        var trimmedReason = reason?.Trim();
        if (trimmedReason is null || trimmedReason.Length < MinReasonLength || trimmedReason.Length > MaxReasonLength)
        {
            throw new ValidationException("reason", $"A reason of {MinReasonLength} to {MaxReasonLength} characters is required.");
        }

        if (price is null && stock is null)
        {
            throw new ValidationException("body", "Provide a price, a stock level, or both.");
        }

        var current = await LoadAsync(id, ct);
        if (!ETagMatches(ifMatch, current.ETag))
        {
            throw new PreconditionFailedException(StaleProductMessage);
        }

        var updated = current;
        var audit = new List<AuditEntry>();
        var now = _clock.GetUtcNow();

        if (price is not null)
        {
            // Validated even when the value is unchanged.
            updated = updated.WithPrice(price.Value);
            if (updated.Price != current.Price)
            {
                audit.Add(CreateAuditEntry(id, AuditField.Price, current.Price, updated.Price, trimmedReason, actor, now));
            }
        }

        if (stock is not null)
        {
            updated = updated.WithStock(stock.Value);
            if (updated.Stock != current.Stock)
            {
                audit.Add(CreateAuditEntry(id, AuditField.Stock, current.Stock, updated.Stock, trimmedReason, actor, now));
            }
        }

        if (audit.Count == 0)
        {
            return ProductDto.From(current);
        }

        try
        {
            var saved = await _products.UpdateAsync(updated, audit, ct);

            var changes = string.Join(", ", audit.Select(entry => $"{entry.Field} {entry.OldValue}->{entry.NewValue}"));
            _logger.LogInformation("Product {ProductId} updated by {Actor}: {Changes}", id, actor, changes);

            return ProductDto.From(saved);
        }
        catch (ConcurrencyException)
        {
            throw new PreconditionFailedException(StaleProductMessage);
        }
    }

    public async Task<Page<AuditEntryDto>> GetAuditAsync(string id, int? pageSize, string? pageToken, CancellationToken ct)
    {
        // Makes an unknown product a 404 instead of an empty list.
        await LoadAsync(id, ct);

        var slice = await _products.GetAuditAsync(id, PageToken.Decode(pageToken), PageToken.NormalizeSize(pageSize), ct);

        var items = slice.Items.Select(AuditEntryDto.From).ToList();
        var nextToken = PageToken.ForNextPage(slice, entry => new KeysetCursor(Timestamp.Format(entry.AtUtc), entry.Id));
        return new Page<AuditEntryDto>(items, nextToken);
    }

    private async Task<Product> LoadAsync(string id, CancellationToken ct)
    {
        if (!ProductIdPattern().IsMatch(id))
        {
            throw new NotFoundException("Product not found.");
        }

        var product = await _products.GetAsync(id, ct);
        return product ?? throw new NotFoundException("Product not found.");
    }

    private static ProductType? ParseProductType(string? type)
    {
        if (string.IsNullOrWhiteSpace(type))
        {
            return null;
        }

        // Match by name only. Enum.TryParse would also accept numbers such as "1".
        var names = Enum.GetNames<ProductType>();
        var match = names.FirstOrDefault(name => name.Equals(type.Trim(), StringComparison.OrdinalIgnoreCase));
        if (match is null)
        {
            throw new ValidationException("type", $"Type must be one of: {string.Join(", ", names)}.");
        }

        return Enum.Parse<ProductType>(match);
    }

    private static AuditEntry CreateAuditEntry(
        string productId, AuditField field, decimal oldValue, decimal newValue, string reason, string actor, DateTimeOffset now)
    {
        return new AuditEntry(Guid.NewGuid().ToString("N"), productId, field, oldValue, newValue, reason, actor, now);
    }

    private static bool ETagMatches(string ifMatchHeader, string? etag)
    {
        return etag is not null && NormalizeETag(ifMatchHeader) == NormalizeETag(etag);
    }

    private static string NormalizeETag(string value)
    {
        return value.Trim().Replace("W/", "").Trim('"');
    }

    [GeneratedRegex("^[a-z0-9-]{1,64}$")]
    private static partial Regex ProductIdPattern();
}
