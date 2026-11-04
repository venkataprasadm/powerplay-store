using IplStore.Api.Security;
using IplStore.Application.Contracts;
using IplStore.Application.Paging;
using IplStore.Application.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

namespace IplStore.Api.Controllers;

[Route("api/v1/products")]
public sealed class ProductsController : ApiControllerBase
{
    private readonly CatalogService _catalog;

    public ProductsController(CatalogService catalog)
    {
        _catalog = catalog;
    }

    [HttpGet]
    [EnableRateLimiting(RateLimitPolicies.Search)]
    public async Task<ActionResult<Page<ProductDto>>> Search(
        [FromQuery] string? q,
        [FromQuery] string? type,
        [FromQuery] string? franchise,
        [FromQuery] int? pageSize,
        [FromQuery] string? pageToken,
        CancellationToken ct)
    {
        var page = await _catalog.SearchAsync(q, type, franchise, pageSize, pageToken, ct);
        return Ok(page);
    }

    [HttpGet("{id}")]
    public async Task<ActionResult<ProductDto>> Get(string id, CancellationToken ct)
    {
        var product = await _catalog.GetAsync(id, ct);
        SetETagHeader(product.Etag);
        return Ok(product);
    }
}
