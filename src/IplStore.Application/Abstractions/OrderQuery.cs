using IplStore.Application.Paging;

namespace IplStore.Application.Abstractions;

/// <summary>A user's order history. <see cref="From"/> is inclusive and <see cref="To"/> is exclusive.</summary>
public sealed record OrderQuery(string UserId, DateTimeOffset? From, DateTimeOffset? To, int PageSize, KeysetCursor? After);
