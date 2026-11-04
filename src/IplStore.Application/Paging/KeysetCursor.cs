namespace IplStore.Application.Paging;

/// <summary>Position in a list ordered by (sort key, id): "continue after this item".</summary>
public sealed record KeysetCursor(string SortKey, string Id);
