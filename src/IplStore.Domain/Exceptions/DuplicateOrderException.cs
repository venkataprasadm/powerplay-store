namespace IplStore.Domain.Exceptions;

/// <summary>Thrown by storage when an order with the same id already exists.</summary>
public sealed class DuplicateOrderException : Exception
{
    public DuplicateOrderException(string orderId)
        : base($"Order {orderId} already exists.")
    {
    }
}
