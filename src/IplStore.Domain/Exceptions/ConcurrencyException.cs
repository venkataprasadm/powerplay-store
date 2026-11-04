namespace IplStore.Domain.Exceptions;

/// <summary>Thrown by storage when an ETag check fails. Application code retries or translates it.</summary>
public sealed class ConcurrencyException : Exception
{
    public ConcurrencyException(string message)
        : base(message)
    {
    }
}
