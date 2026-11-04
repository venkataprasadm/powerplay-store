using IplStore.Application.Abstractions;
using IplStore.Application.Contracts;
using IplStore.Application.Paging;
using IplStore.Domain.Common;
using IplStore.Domain.Exceptions;

namespace IplStore.Application.Services;

public sealed class OrderService
{
    private readonly IOrderRepository _orders;

    public OrderService(IOrderRepository orders)
    {
        _orders = orders;
    }

    public async Task<OrderDto> GetAsync(string userId, string orderId, CancellationToken ct)
    {
        var order = await _orders.GetAsync(orderId, ct);

        // Another user's order looks exactly like a missing one, so order ids cannot be probed.
        if (order is null || order.UserId != userId)
        {
            throw new NotFoundException("Order not found.");
        }

        return OrderDto.From(order);
    }

    public async Task<Page<OrderDto>> ListAsync(
        string userId, DateTimeOffset? from, DateTimeOffset? to, int? pageSize, string? pageToken, CancellationToken ct)
    {
        if (from is not null && to is not null && from >= to)
        {
            throw new ValidationException("from", "'from' must be earlier than 'to'.");
        }

        var query = new OrderQuery(userId, from, to, PageToken.NormalizeSize(pageSize), PageToken.Decode(pageToken));
        var slice = await _orders.ListAsync(query, ct);

        var items = slice.Items.Select(OrderDto.From).ToList();
        var nextToken = PageToken.ForNextPage(slice, order => new KeysetCursor(Timestamp.Format(order.CreatedUtc), order.Id));
        return new Page<OrderDto>(items, nextToken);
    }
}
