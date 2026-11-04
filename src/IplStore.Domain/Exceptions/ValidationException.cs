namespace IplStore.Domain.Exceptions;

/// <summary>The input is invalid (HTTP 400). Carries the messages for each field.</summary>
public sealed class ValidationException : AppException
{
    public ValidationException(string field, string message, string code = "validation_failed")
        : base(code, message)
    {
        Errors = new Dictionary<string, string[]> { [field] = [message] };
    }

    public ValidationException(IReadOnlyDictionary<string, string[]> errors, string code = "validation_failed")
        : base(code, "One or more validation errors occurred.")
    {
        Errors = errors;
    }

    public IReadOnlyDictionary<string, string[]> Errors { get; }
}
