using System.Diagnostics.Metrics;

namespace IplStore.Application.Common;

/// <summary>Metrics hooks. Export them with any OpenTelemetry or Prometheus listener on the meter named "IplStore".</summary>
public sealed class StoreMetrics : IDisposable
{
    public const string MeterName = "IplStore";

    private readonly Meter _meter = new(MeterName);

    public StoreMetrics()
    {
        OrdersPlaced = _meter.CreateCounter<long>("ipl_orders_placed");
        CheckoutRejected = _meter.CreateCounter<long>("ipl_checkout_rejected");
        ConcurrencyConflicts = _meter.CreateCounter<long>("ipl_concurrency_conflicts");
        IdempotentReplays = _meter.CreateCounter<long>("ipl_idempotent_replays");
    }

    public Counter<long> OrdersPlaced { get; }

    public Counter<long> CheckoutRejected { get; }

    public Counter<long> ConcurrencyConflicts { get; }

    public Counter<long> IdempotentReplays { get; }

    public void Dispose()
    {
        _meter.Dispose();
    }
}
