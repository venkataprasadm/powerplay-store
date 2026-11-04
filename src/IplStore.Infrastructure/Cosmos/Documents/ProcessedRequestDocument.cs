using IplStore.Domain.Carts;
using IplStore.Domain.Common;

namespace IplStore.Infrastructure.Cosmos.Documents;

internal sealed class ProcessedRequestDocument
{
    public string RequestId { get; set; } = "";
    public string AtUtc { get; set; } = "";

    /// <summary>Absent on entries written before this field existed. Such entries replay without the reuse check.</summary>
    public string? Fingerprint { get; set; }

    public List<CartLineDocument> ResultLines { get; set; } = [];

    public static ProcessedRequestDocument FromDomain(ProcessedRequest request)
    {
        return new ProcessedRequestDocument
        {
            RequestId = request.RequestId,
            AtUtc = Timestamp.Format(request.AtUtc),
            Fingerprint = request.Fingerprint,
            ResultLines = request.ResultLines.Select(CartLineDocument.FromDomain).ToList(),
        };
    }

    public ProcessedRequest ToDomain()
    {
        var resultLines = ResultLines.Select(line => line.ToDomain()).ToList();
        return new ProcessedRequest(RequestId, resultLines, Timestamp.Parse(AtUtc), Fingerprint);
    }
}
