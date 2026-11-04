namespace IplStore.Application.Paging;

/// <summary>A page returned to API clients. <see cref="TotalCount"/> is only set where it is cheap to compute.</summary>
public sealed record Page<T>(IReadOnlyList<T> Items, string? NextPageToken, int? TotalCount = null);
