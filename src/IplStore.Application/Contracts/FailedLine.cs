namespace IplStore.Application.Contracts;

/// <summary>A cart line that cannot be fulfilled: how many were asked for and how many are available.</summary>
public sealed record FailedLine(string ProductId, string ProductName, int Requested, int Available);
