using System.Numerics;

namespace TopSolid.Automation.AI.Studio.Preview.Streaming;

internal readonly record struct PreviewCamera(PreviewPoint Target, Vector3 TowardCamera, Vector3 Up, double Width, double AspectRatio);
internal sealed record PreviewResidencyPlan(IReadOnlyList<PreviewChunk> Detailed, IReadOnlyList<PreviewChunk> Proxies, long ResidentBytes);

/// <summary>Orthographic frustum selection. Nonresident visible tiles retain a bounds proxy, never silently disappear.</summary>
internal static class PreviewResidency
{
    internal static PreviewResidencyPlan Select(IReadOnlyList<PreviewChunk> chunks, PreviewCamera camera, long budget)
    {
        if (!double.IsFinite(camera.Width) || camera.Width <= 0 || !double.IsFinite(camera.AspectRatio) || camera.AspectRatio <= 0 || budget <= 0 ||
            !Finite(camera.TowardCamera) || !Finite(camera.Up) || camera.TowardCamera.LengthSquared() < 1e-12f || camera.Up.LengthSquared() < 1e-12f)
            throw new ArgumentException("Invalid preview camera or residency budget.");
        var forward = Vector3.Normalize(camera.TowardCamera);
        var cross = Vector3.Cross(camera.Up, forward);
        if (cross.LengthSquared() < 1e-12f) throw new ArgumentException("Invalid camera basis.");
        var right = Vector3.Normalize(cross); var up = Vector3.Cross(forward, right);
        var width = camera.Width / 2; var height = width / camera.AspectRatio;
        var candidates = new List<(PreviewChunk Chunk, double Priority)>();
        foreach (var chunk in chunks)
        {
            var relative = chunk.Bounds.Center.RelativeTo(camera.Target);
            var x = Vector3.Dot(relative, right); var y = Vector3.Dot(relative, up); var radius = chunk.Bounds.Radius;
            if (Math.Abs(x) - radius > width || Math.Abs(y) - radius > height) continue;
            // Keep large projected objects and objects around the view centre detailed first.
            var priority = radius / (1 + Math.Sqrt(x * (double)x + y * (double)y) / Math.Max(width, 1e-12));
            candidates.Add((chunk, priority));
        }
        candidates.Sort((a, b) => { var order = b.Priority.CompareTo(a.Priority); return order != 0 ? order : a.Chunk.Id.CompareTo(b.Chunk.Id); });
        var detailed = new List<PreviewChunk>(); var proxies = new List<PreviewChunk>(); long used = 0;
        foreach (var candidate in candidates)
        {
            if (candidate.Chunk.ResidentBytes <= budget - used) { detailed.Add(candidate.Chunk); used += candidate.Chunk.ResidentBytes; }
            else proxies.Add(candidate.Chunk);
        }
        return new(detailed, proxies, used);
    }
    private static bool Finite(Vector3 v) => float.IsFinite(v.X) && float.IsFinite(v.Y) && float.IsFinite(v.Z);
}
