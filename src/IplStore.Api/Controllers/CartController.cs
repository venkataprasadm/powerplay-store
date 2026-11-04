using IplStore.Api.Contracts.Requests;
using IplStore.Application.Contracts;
using IplStore.Application.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace IplStore.Api.Controllers;

/// <summary>The signed-in user's cart. Every change needs an Idempotency-Key header, so a retried request is applied once.</summary>
[Route("api/v1/cart")]
[Authorize]
public sealed class CartController : ApiControllerBase
{
    private readonly CartService _cart;

    public CartController(CartService cart)
    {
        _cart = cart;
    }

    [HttpGet]
    public async Task<ActionResult<CartDto>> Get(CancellationToken ct)
    {
        var cart = await _cart.GetAsync(UserId, ct);
        return Ok(cart);
    }

    [HttpPost("items")]
    public async Task<ActionResult<CartDto>> AddItem([FromBody] AddCartItemRequest request, CancellationToken ct)
    {
        var cart = await _cart.AddAsync(UserId, GetIdempotencyKey(), request.ProductId!, request.Quantity, ct);
        return Ok(cart);
    }

    [HttpPut("items/{productId}")]
    public async Task<ActionResult<CartDto>> SetQuantity(string productId, [FromBody] SetCartItemQuantityRequest request, CancellationToken ct)
    {
        var cart = await _cart.SetQuantityAsync(UserId, GetIdempotencyKey(), productId, request.Quantity, ct);
        return Ok(cart);
    }

    [HttpDelete("items/{productId}")]
    public async Task<ActionResult<CartDto>> RemoveItem(string productId, CancellationToken ct)
    {
        var cart = await _cart.RemoveAsync(UserId, GetIdempotencyKey(), productId, ct);
        return Ok(cart);
    }

    /// <summary>The shopper accepts the current prices after being told they changed.</summary>
    [HttpPost("accept-changes")]
    public async Task<ActionResult<CartDto>> AcceptChanges(CancellationToken ct)
    {
        var cart = await _cart.AcceptChangesAsync(UserId, GetIdempotencyKey(), ct);
        return Ok(cart);
    }
}
