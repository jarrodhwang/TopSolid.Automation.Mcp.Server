namespace TopSolid.Automation.AI.Studio.Settings;

public sealed record PreviewDefaults(bool Edges = false, bool Part = true, bool Stock = true, bool Machine = true)
{
    internal static PreviewDefaults Current { get; set; } = new();
}
