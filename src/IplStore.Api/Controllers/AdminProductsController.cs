using IplStore.Api.Contracts.Requests;
using IplStore.Api.Security;
using IplStore.Application.Contracts;
using IplStore.Application.Paging;
using IplStore.Application.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace IplStore.Api.Controllers;

[Route("api/v1/admin/products")]
[Authorize(Policy = AuthorizationPolicies.Admin)]
public sealed class AdminProductsController : ApiControllerBase
{
    private readonly CatalogService _catalog;

    public AdminProductsController(CatalogService catalog)
    {
        _catalog = catalog;
    }

    /// <summary>Changes price and/or stock. The If-Match header must carry the ETag the admin saw (412 if stale, 428 if missing).</summary>
    [HttpPatch("{id}")]
    public async Task<ActionResult<ProductDto>> Update(
        string id,
        [FromBody] UpdateProductRequest request,
        [FromHeader(Name = "If-Match")] string? ifMatch,
        CancellationToken ct)
    {
        var updated = await _catalog.UpdateAsync(id, ifMatch, request.Price, request.Stock, request.Reason, UserId, ct);
        SetETagHeader(updated.Etag);
        return Ok(updated);
    }

    [HttpGet("{id}/audit")]
    public async Task<ActionResult<Page<AuditEntryDto>>> GetAudit(
        string id,
        [FromQuery] int? pageSize,
        [FromQuery] string? pageToken,
        CancellationToken ct)
    {
        var page = await _catalog.GetAuditAsync(id, pageSize, pageToken, ct);
        return Ok(page);
    }
}
