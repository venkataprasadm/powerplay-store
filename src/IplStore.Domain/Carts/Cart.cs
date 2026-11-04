using IplStore.Domain.Catalogue;
using IplStore.Domain.Exceptions;

namespace IplStore.Domain.Carts;

public sealed record Cart
{
    public const int MinQuantity = 1;
    public const int MaxQuantityPerLine = 10;

    // Keeps a checkout transactional batch well under the Cosmos DB limit of 100 operations.
    public const int MaxLines = 20;

    public const int MaxProcessedRequests = 20;

    public required string UserId { get; init; }
    public IReadOnlyList<CartLine> Lines { get; init; } = [];
    public IReadOnlyList<ProcessedRequest> Processed { get; init; } = [];
    public string? ETag { get; init; }

    public static void ValidateQuantity(int quantity)
    {
        if (quantity is < MinQuantity or > MaxQuantityPerLine)
        {
            throw new ValidationException("quantity", $"Quantity must be between {MinQuantity} and {MaxQuantityPerLine}.", "quantity_limit");
        }
    }

    /// <summary>Adds a new line, or increases an existing one. The line keeps the price the shopper originally accepted.</summary>
    public Cart Add(Product product, int quantity)
    {
        ValidateQuantity(quantity);

        var existing = FindLine(product.Id);
        if (existing is null)
        {
            if (Lines.Count >= MaxLines)
            {
                throw new ValidationException("productId", $"A cart can hold at most {MaxLines} different products.");
            }

            var newLine = new CartLine(product.Id, product.Name, quantity, product.Price);
            return this with { Lines = [.. Lines, newLine] };
        }

        var newQuantity = existing.Quantity + quantity;
        ValidateQuantity(newQuantity);
        return ReplaceLine(existing with { Quantity = newQuantity });
    }

    public Cart SetQuantity(string productId, int quantity)
    {
        ValidateQuantity(quantity);

        var existing = FindLine(productId) ?? throw new NotFoundException("That product is not in the cart.");
        return ReplaceLine(existing with { Quantity = quantity });
    }

    public Cart Remove(string productId)
    {
        if (FindLine(productId) is null)
        {
            throw new NotFoundException("That product is not in the cart.");
        }

        var remaining = Lines.Where(line => line.ProductId != productId).ToList();
        return this with { Lines = remaining };
    }

    /// <summary>The shopper accepts the current name and price of every product that still exists.</summary>
    public Cart AcceptPrices(IReadOnlyDictionary<string, Product> currentProducts)
    {
        var accepted = new List<CartLine>(Lines.Count);
        foreach (var line in Lines)
        {
            if (currentProducts.TryGetValue(line.ProductId, out var product))
            {
                accepted.Add(line with { AcceptedUnitPrice = product.Price, ProductName = product.Name });
            }
            else
            {
                accepted.Add(line);
            }
        }

        return this with { Lines = accepted };
    }

    public ProcessedRequest? FindProcessed(string requestId)
    {
        return Processed.FirstOrDefault(processed => processed.RequestId == requestId);
    }

    /// <summary>Records the request id with the resulting lines. Only the most recent ids are kept, to bound the document size.</summary>
    public Cart Record(string requestId, DateTimeOffset now, string? fingerprint = null)
    {
        var entry = new ProcessedRequest(requestId, Lines, now, fingerprint);
        return this with { Processed = [.. Processed.TakeLast(MaxProcessedRequests - 1), entry] };
    }

    private CartLine? FindLine(string productId)
    {
        return Lines.FirstOrDefault(line => line.ProductId == productId);
    }

    private Cart ReplaceLine(CartLine replacement)
    {
        var lines = Lines
            .Select(line => line.ProductId == replacement.ProductId ? replacement : line)
            .ToList();

        return this with { Lines = lines };
    }
}
