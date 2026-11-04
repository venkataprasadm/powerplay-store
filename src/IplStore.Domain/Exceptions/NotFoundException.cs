namespace IplStore.Domain.Exceptions;

/// <summary>The resource does not exist (HTTP 404).</summary>
public sealed class NotFoundException : AppException
{
    public NotFoundException(string message)
        : base("not_found", message)
    {
    }
}
