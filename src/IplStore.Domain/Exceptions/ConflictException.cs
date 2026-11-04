namespace IplStore.Domain.Exceptions;

/// <summary>The request is valid but conflicts with the current state (HTTP 409).</summary>
public sealed class ConflictException : AppException
{
    public ConflictException(string code, string message, object? details = null)
        : base(code, message)
    {
        Details = details;
    }

    /// <summary>Extra members added to the problem details body, for example the lines that are out of stock.</summary>
    public object? Details { get; }
}
