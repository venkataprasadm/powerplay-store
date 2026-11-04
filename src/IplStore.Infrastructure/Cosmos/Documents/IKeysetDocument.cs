namespace IplStore.Infrastructure.Cosmos.Documents;

/// <summary>A document listed with keyset paging on (createdUtc, id), the store container's composite index.</summary>
internal interface IKeysetDocument
{
    string Id { get; }

    string CreatedUtc { get; }
}
