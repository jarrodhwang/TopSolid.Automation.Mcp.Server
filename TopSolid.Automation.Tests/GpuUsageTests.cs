using TopSolid.Automation.AI.Studio.Diagnostics;

namespace TopSolid.Automation.Tests;

internal static class GpuUsageTests
{
    internal static async Task Run()
    {
        const string adapter = "luid_0x00000000_0x00000001_phys_0";
        const string secondAdapter = "luid_0x00000000_0x00000002_phys_0";
        var sample = GpuCounterReducer.Reduce(
            [new("pid_1_" + adapter + "_eng_0_engtype_3D", 30), new("pid_2_" + adapter + "_eng_0_engtype_3D", 20),
             new("pid_1_" + adapter + "_eng_1_engtype_Copy", 40), new("pid_1_" + secondAdapter + "_eng_0_engtype_3D", 95),
             new("pid_2_" + secondAdapter + "_eng_0_engtype_3D", 10), new("invalid", 999),
             new("pid_3_" + adapter + "_eng_0_engtype_3D", double.NaN)],
            [new(adapter, 4096), new(adapter, 4096), new(secondAdapter, 0)], [new(adapter, 1024)]);
        Check.Equal(2, sample.Adapters.Count, "GPU aggregation created an adapter from an invalid instance");
        var gpu = sample.Adapters.Single(item => item.Id == adapter);
        Check.Equal(50d, gpu.UtilizationPercent, "GPU engines must sum process usage per engine, then take the busiest engine");
        Check.Equal(4096d, gpu.DedicatedBytes, "Adapter memory duplicated shared allocations");
        Check.Equal(1024d, gpu.SharedBytes, "Shared adapter memory lost");
        var other = sample.Adapters.Single(item => item.Id == secondAdapter);
        Check.Equal(100d, other.UtilizationPercent, "Transient counter rounding over 100% was not bounded");
        Check.Equal(0d, other.DedicatedBytes, "A real zero memory sample must remain distinguishable from unavailable");
        Check.True(other.SharedBytes == null, "Missing shared-memory counter must not claim zero usage");
        Check.Equal(0, GpuCounterReducer.Reduce([], [], []).Adapters.Count, "Unavailable counters fabricated a GPU");

        var reader = new FakeReader(sample);
        using var cancellation = new CancellationTokenSource();
        var publishes = 0;
        var work = GpuUsageMonitor.RunAsync(_ => { Interlocked.Increment(ref publishes); cancellation.Cancel(); }, cancellation.Token,
            () => reader, TimeSpan.FromMilliseconds(5));
        await work.WaitAsync(TimeSpan.FromSeconds(5));
        Check.True(reader.Primed && reader.Disposed && reader.Reads == 1 && publishes == 1, "GPU cancellation did not stop and dispose its owned query");

        using var cancelled = new CancellationTokenSource(); cancelled.Cancel();
        var opened = false;
        await GpuUsageMonitor.RunAsync(_ => throw new Exception("Unexpected sample"), cancelled.Token,
            () => { opened = true; return new FakeReader(sample); });
        Check.True(!opened, "A cancelled GPU panel opened a counter query");

        GpuUsageSample? failure = null;
        await GpuUsageMonitor.RunAsync(value => failure = value, CancellationToken.None,
            () => throw new InvalidOperationException("Counters unavailable"));
        Check.True(failure != null && failure.Adapters.Count == 0 && failure.Error == "Counters unavailable", "Counter startup failure was not represented as unavailable");
    }

    private sealed class FakeReader(GpuUsageSample sample) : IGpuCounterReader
    {
        public bool Primed { get; private set; }
        public bool Disposed { get; private set; }
        public int Reads { get; private set; }
        public void Prime() => Primed = true;
        public GpuUsageSample Read() { Reads++; return sample; }
        public void Dispose() => Disposed = true;
    }
}
