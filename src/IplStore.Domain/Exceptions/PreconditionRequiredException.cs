namespace IplStore.Domain.Exceptions;

/// <summary>A conditional header is required but missing (HTTP 428).</summary>
public sealed class PreconditionRequiredException : AppException
{
    public PreconditionRequiredException(string message)
        : base("precondition_required", message)
    {
    }
}
