namespace IplStore.Domain.Exceptions;

/// <summary>The caller's If-Match precondition no longer holds (HTTP 412).</summary>
public sealed class PreconditionFailedException : AppException
{
    public PreconditionFailedException(string message)
        : base("precondition_failed", message)
    {
    }
}
