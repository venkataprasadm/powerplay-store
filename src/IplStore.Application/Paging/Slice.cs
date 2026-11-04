namespace IplStore.Application.Paging;

/// <summary>One page read from storage, plus whether more items exist after it.</summary>
public sealed record Slice<T>(IReadOnlyList<T> Items, bool HasMore);
