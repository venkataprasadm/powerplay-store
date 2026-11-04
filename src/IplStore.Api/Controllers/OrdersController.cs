using IplStore.Api.Contracts.Requests;
using IplStore.Api.Hosting;
using IplStore.Api.Security;
using IplStore.Application.Contracts;
using IplStore.Application.Paging;
using IplStore.Application.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http.Timeouts;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

namespace IplStore.Api.Controllers;

[Route("api/v1/orders")]
[Authorize]
public sealed class OrdersController : ApiControllerBase
{
    private readonly CheckoutService _checkout;
    private readonly OrderService _orders;

    public OrdersController(CheckoutService checkout, OrderService orders)
    {
        _checkout = checkout;
        _orders = orders;
    }

    /// <summary>
    /// Places the order. PUT with a client generated id is idempotent by nature: the same id always names the same order.
    /// Returns 201 the first time, and 200 with Idempotent-Replayed: true for a retry.
    /// </summary>
    [HttpPut("{orderId:guid}")]
    [EnableRateLimiting(RateLimitPolicies.Checkout)]
    [RequestTimeout(RequestTimeoutPolicies.Checkout)]
    public async Task<ActionResult<OrderDto>> Place(Guid orderId, [FromBody] PlaceOrderRequest request, CancellationToken ct)
    {
        var result = await _checkout.PlaceOrderAsync(UserId, orderId.ToString("D"), request.ExpectedTotal!.Value, ct);

        if (result.Replayed)
        {
            Response.Headers["Idempotent-Replayed"] = "true";
            return Ok(result.Order);
        }

        return CreatedAtAction(nameof(Get), new { orderId }, result.Order);
    }

    [HttpGet("{orderId:guid}")]
    public async Task<ActionResult<OrderDto>> Get(Guid orderId, CancellationToken ct)
    {
        var order = await _orders.GetAsync(UserId, orderId.ToString("D"), ct);
        return Ok(order);
    }

    /// <summary>Newest first. <paramref name="from"/> is inclusive and <paramref name="to"/> is exclusive (UTC).</summary>
    [HttpGet]
    public async Task<ActionResult<Page<OrderDto>>> List(
        [FromQuery] DateTimeOffset? from,
        [FromQuery] DateTimeOffset? to,
        [FromQuery] int? pageSize,
        [FromQuery] string? pageToken,
        CancellationToken ct)
    {
        var page = await _orders.ListAsync(UserId, from, to, pageSize, pageToken, ct);
        return Ok(page);
    }
}
