using System.Security.Claims;
using IplStore.Domain.Exceptions;
using Microsoft.AspNetCore.Mvc;

namespace IplStore.Api.Controllers;

[ApiController]
[Produces("application/json")]
public abstract class ApiControllerBase : ControllerBase
{
    private const string IdempotencyKeyHeader = "Idempotency-Key";

    /// <summary>The signed-in user's id. It comes only from the validated token, never from the request body or URL.</summary>
    protected string UserId => User.FindFirstValue("sub") ?? throw new UnauthorizedAccessException();

    /// <summary>The client generated request id that makes a cart change safe to retry.</summary>
    protected string GetIdempotencyKey()
    {
        var headerValue = Request.Headers[IdempotencyKeyHeader].FirstOrDefault();
        if (!Guid.TryParse(headerValue, out var key))
        {
            throw new ValidationException(
                IdempotencyKeyHeader,
                "An Idempotency-Key header containing a GUID is required.",
                "idempotency_key_required");
        }

        return key.ToString("D");
    }

    protected void SetETagHeader(string? etag)
    {
        if (etag is not null)
        {
            Response.Headers.ETag = etag;
        }
    }
}
