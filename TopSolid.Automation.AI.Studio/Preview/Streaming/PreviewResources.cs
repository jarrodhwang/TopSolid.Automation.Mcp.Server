namespace TopSolid.Automation.AI.Studio.Preview.Streaming;

/// <summary>Working-set budgets, never source file size or total model complexity limits.</summary>
internal sealed record PreviewResources(long CpuResidentBytes, long GpuResidentBytes, int Workers)
{
    internal static PreviewResources Detect(long dedicatedVideoMemory = 0)
        => FromHardware(GC.GetGCMemoryInfo().TotalAvailableMemoryBytes, dedicatedVideoMemory, Environment.ProcessorCount);

    internal static PreviewResources FromHardware(long availableRam, long dedicatedVideoMemory, int processors)
    {
        if (availableRam <= 0 || dedicatedVideoMemory < 0 || processors < 1) throw new ArgumentOutOfRangeException();
        // Leave memory for TopSolid, the OS and the inference model. These are residency budgets,
        // not allocation requests. Missing adapter information must not be reported as measured VRAM.
        var cpu = Math.Max(64L << 20, availableRam / 3);
        var gpu = dedicatedVideoMemory > 0 ? dedicatedVideoMemory * 3 / 5 : Math.Min(cpu / 2, 1L << 30);
        return new(cpu, Math.Max(32L << 20, gpu), Math.Max(1, processors - 1));
    }
}
