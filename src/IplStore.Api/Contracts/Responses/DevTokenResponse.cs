namespace IplStore.Api.Contracts.Responses;

public sealed record DevTokenResponse(string AccessToken, string UserId, string Role, DateTimeOffset ExpiresAtUtc);
