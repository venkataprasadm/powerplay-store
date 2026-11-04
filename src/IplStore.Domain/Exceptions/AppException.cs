namespace IplStore.Domain.Exceptions;

/// <summary>Base class for every expected business failure. The API maps each subclass to a problem details response.</summary>
public abstract class AppException : Exception
{
    protected AppException(string code, string message)
        : base(message)
    {
        Code = code;
    }

    /// <summary>Stable machine readable error code returned to clients, for example <c>out_of_stock</c>.</summary>
    public string Code { get; }
}
