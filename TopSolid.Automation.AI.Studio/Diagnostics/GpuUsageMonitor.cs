using System.Runtime.InteropServices;
using System.Text.RegularExpressions;

namespace TopSolid.Automation.AI.Studio.Diagnostics;

internal sealed record GpuCounterValue(string Instance, double Value);
internal sealed record GpuAdapterSample(string Id, double? UtilizationPercent, double? DedicatedBytes, double? SharedBytes);
internal sealed record GpuUsageSample(DateTimeOffset Timestamp, IReadOnlyList<GpuAdapterSample> Adapters, string? Error = null)
{
    public static GpuUsageSample Unavailable(string? error = null) => new(DateTimeOffset.Now, [], error);
}

/// <summary>Combines process engine counters by physical engine, then reports the busiest engine.
/// Adapter memory counters avoid double-counting memory shared by several processes.</summary>
internal static class GpuCounterReducer
{
    private static readonly Regex Adapter = new(@"(?<adapter>luid_0x[0-9a-f]+_0x[0-9a-f]+_phys_\d+)", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
    private static readonly Regex Engine = new(@"_eng_(?<engine>\d+)(?:_|$)", RegexOptions.CultureInvariant);

    public static GpuUsageSample Reduce(IEnumerable<GpuCounterValue> engines, IEnumerable<GpuCounterValue> dedicated,
        IEnumerable<GpuCounterValue> shared, string? error = null)
    {
        var adapterIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var engineTotals = new Dictionary<(string Adapter, string Engine), double>();
        foreach (var value in engines)
        {
            var adapter = Adapter.Match(value.Instance); var engine = Engine.Match(value.Instance);
            if (!adapter.Success || !engine.Success || !double.IsFinite(value.Value) || value.Value < 0) continue;
            var id = adapter.Groups["adapter"].Value.ToLowerInvariant();
            adapterIds.Add(id);
            var key = (id, engine.Groups["engine"].Value);
            engineTotals[key] = engineTotals.GetValueOrDefault(key) + value.Value;
        }
        var dedicatedByAdapter = ReadMemory(dedicated, adapterIds);
        var sharedByAdapter = ReadMemory(shared, adapterIds);
        var samples = adapterIds.OrderBy(id => id, StringComparer.Ordinal).Take(32).Select(id =>
        {
            var utilization = engineTotals.Where(pair => pair.Key.Adapter == id).Select(pair => pair.Value).ToArray();
            return new GpuAdapterSample(id, utilization.Length == 0 ? null : Math.Clamp(utilization.Max(), 0, 100),
                dedicatedByAdapter.TryGetValue(id, out var dedicatedBytes) ? dedicatedBytes : null,
                sharedByAdapter.TryGetValue(id, out var sharedBytes) ? sharedBytes : null);
        }).ToArray();
        return new GpuUsageSample(DateTimeOffset.Now, samples, error);
    }

    private static Dictionary<string, double> ReadMemory(IEnumerable<GpuCounterValue> values, HashSet<string> adapterIds)
    {
        var result = new Dictionary<string, double>(StringComparer.OrdinalIgnoreCase);
        foreach (var value in values)
        {
            var match = Adapter.Match(value.Instance);
            if (!match.Success || !double.IsFinite(value.Value) || value.Value < 0) continue;
            var id = match.Groups["adapter"].Value.ToLowerInvariant(); adapterIds.Add(id);
            // Duplicate PDH instances must not inflate adapter-level memory.
            result[id] = Math.Max(result.GetValueOrDefault(id), value.Value);
        }
        return result;
    }
}

internal interface IGpuCounterReader : IDisposable
{
    void Prime();
    GpuUsageSample Read();
}

/// <summary>One worker and query per visible GPU tab. Cancellation never closes a query during a native read.</summary>
internal static class GpuUsageMonitor
{
    internal static Task RunAsync(Action<GpuUsageSample> publish, CancellationToken cancellationToken,
        Func<IGpuCounterReader>? readerFactory = null, TimeSpan? interval = null) => Task.Run(async () =>
    {
        try
        {
            cancellationToken.ThrowIfCancellationRequested();
            using var reader = (readerFactory ?? (() => new PdhGpuCounterReader()))();
            reader.Prime();
            while (true)
            {
                await Task.Delay(interval ?? TimeSpan.FromSeconds(2), cancellationToken).ConfigureAwait(false);
                var sample = reader.Read();
                cancellationToken.ThrowIfCancellationRequested();
                publish(sample);
            }
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested) { }
        catch (Exception error) when (error is InvalidOperationException or ExternalException or DllNotFoundException or EntryPointNotFoundException)
        {
            if (!cancellationToken.IsCancellationRequested) publish(GpuUsageSample.Unavailable(error.Message));
        }
    }, CancellationToken.None);
}

/// <summary>Local Windows WDDM counters; no vendor SDK, external process, elevation or new package.
/// References: https://learn.microsoft.com/windows/win32/api/pdh/nf-pdh-pdhgetformattedcounterarrayw
/// https://devblogs.microsoft.com/directx/gpus-in-the-task-manager/ </summary>
internal sealed class PdhGpuCounterReader : IGpuCounterReader
{
    private const uint MoreData = 0x800007D2;
    private const uint DoubleFormat = 0x00000200;
    private const uint NoCap100 = 0x00008000;
    private const int MaximumBufferBytes = 16 * 1024 * 1024;
    private IntPtr query;
    private readonly IntPtr engines;
    private readonly IntPtr dedicated;
    private readonly IntPtr shared;
    private readonly string? initializationError;

    public PdhGpuCounterReader()
    {
        var status = PdhOpenQuery(null, UIntPtr.Zero, out query);
        if (status != 0) throw new InvalidOperationException($"PDH query: 0x{status:X8}");
        try
        {
            engines = Add(@"\GPU Engine(*)\Utilization Percentage");
            dedicated = Add(@"\GPU Adapter Memory(*)\Dedicated Usage");
            shared = Add(@"\GPU Adapter Memory(*)\Shared Usage");
            if (engines == IntPtr.Zero && dedicated == IntPtr.Zero && shared == IntPtr.Zero)
                initializationError = "Windows GPU performance counters are not available.";
        }
        catch { Dispose(); throw; }
    }

    private IntPtr Add(string path) => PdhAddEnglishCounter(query, path, UIntPtr.Zero, out var counter) == 0 ? counter : IntPtr.Zero;
    public void Prime() { if (initializationError == null) PdhCollectQueryData(query); }
    public GpuUsageSample Read()
    {
        ObjectDisposedException.ThrowIf(query == IntPtr.Zero, this);
        if (initializationError != null) return GpuUsageSample.Unavailable(initializationError);
        var status = PdhCollectQueryData(query);
        if (status != 0) return GpuUsageSample.Unavailable($"PDH collection: 0x{status:X8}");
        return GpuCounterReducer.Reduce(ReadValues(engines), ReadValues(dedicated), ReadValues(shared));
    }

    private static IReadOnlyList<GpuCounterValue> ReadValues(IntPtr counter)
    {
        if (counter == IntPtr.Zero) return [];
        // Dynamic process instances may change between size and data calls. Re-query size instead of trusting
        // a failed second call, as required by the PDH buffer contract.
        for (var attempt = 0; attempt < 3; attempt++)
        {
            uint size = 0, count = 0;
            var status = PdhGetFormattedCounterArray(counter, DoubleFormat | NoCap100, ref size, out count, IntPtr.Zero);
            if (status != MoreData || size == 0 || size > MaximumBufferBytes) return [];
            var buffer = Marshal.AllocHGlobal(checked((int)size));
            try
            {
                status = PdhGetFormattedCounterArray(counter, DoubleFormat | NoCap100, ref size, out count, buffer);
                if (status == MoreData) continue;
                if (status != 0) return [];
                var stride = Marshal.SizeOf<FormattedCounterItem>();
                if ((ulong)count * (ulong)stride > size) return [];
                var values = new List<GpuCounterValue>(checked((int)count));
                for (var index = 0; index < count; index++)
                {
                    var item = Marshal.PtrToStructure<FormattedCounterItem>(buffer + index * stride);
                    if (item.Value.Status > 1 || !double.IsFinite(item.Value.DoubleValue)) continue;
                    var name = Marshal.PtrToStringUni(item.Name);
                    if (name != null) values.Add(new GpuCounterValue(name, item.Value.DoubleValue));
                }
                return values;
            }
            finally { Marshal.FreeHGlobal(buffer); }
        }
        return [];
    }

    public void Dispose()
    {
        if (query == IntPtr.Zero) return;
        PdhCloseQuery(query); query = IntPtr.Zero;
    }

    [StructLayout(LayoutKind.Explicit)]
    private struct FormattedCounterValue
    {
        [FieldOffset(0)] public uint Status;
        [FieldOffset(8)] public double DoubleValue;
    }
    [StructLayout(LayoutKind.Sequential)]
    private struct FormattedCounterItem
    {
        public IntPtr Name;
        public FormattedCounterValue Value;
    }
    [DllImport("pdh.dll", CharSet = CharSet.Unicode, EntryPoint = "PdhOpenQueryW")]
    private static extern uint PdhOpenQuery(string? source, UIntPtr data, out IntPtr query);
    [DllImport("pdh.dll", CharSet = CharSet.Unicode, EntryPoint = "PdhAddEnglishCounterW")]
    private static extern uint PdhAddEnglishCounter(IntPtr query, string path, UIntPtr data, out IntPtr counter);
    [DllImport("pdh.dll")]
    private static extern uint PdhCollectQueryData(IntPtr query);
    [DllImport("pdh.dll", CharSet = CharSet.Unicode, EntryPoint = "PdhGetFormattedCounterArrayW")]
    private static extern uint PdhGetFormattedCounterArray(IntPtr counter, uint format, ref uint size, out uint count, IntPtr buffer);
    [DllImport("pdh.dll")]
    private static extern uint PdhCloseQuery(IntPtr query);
}
