using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Text;
using Newtonsoft.Json.Linq;

namespace TopSolid.Automation.AI.Studio.Preview;

internal static class OcctPreviewImporter
{
    internal static string WorkerPath => Path.Combine(AppContext.BaseDirectory, "OcctPreview", "TopSolid.OcctPreview.exe");
    internal static bool IsAvailable => File.Exists(WorkerPath);
    internal static async Task<PreviewFilePayload> ImportAsync(string input, CancellationToken token)
    {
        if (!IsAvailable) throw new IOException("The optional OCCT preview runtime is not installed.");
        if (Path.GetExtension(input).ToLowerInvariant() is not (".step" or ".stp" or ".iges" or ".igs" or ".brep"))
            throw new InvalidDataException("Unsupported neutral CAD format.");
        var directory = Path.Combine(Path.GetTempPath(), "TopSolid-Studio-occt-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        var output = new PreviewFilePayload(new JObject { ["format"] = "stl", ["units"] = "mm", ["upAxis"] = "Z", ["kernel"] = "OCCT 7.9.3" },
            Path.Combine(directory, "display.stl"));
        try
        {
            var start = new ProcessStartInfo(WorkerPath) { UseShellExecute = false, CreateNoWindow = true,
                RedirectStandardOutput = true, RedirectStandardError = true, WorkingDirectory = Path.GetDirectoryName(WorkerPath)! };
            start.ArgumentList.Add(Path.GetFullPath(input)); start.ArgumentList.Add(output.FilePath!);
            start.ArgumentList.Add(PreviewQuality.LinearToleranceMm.ToString(CultureInfo.InvariantCulture));
            start.ArgumentList.Add(PreviewQuality.AngularToleranceDegrees.ToString(CultureInfo.InvariantCulture));
            using var process = Process.Start(start) ?? throw new IOException("Could not start OCCT preview worker.");
            // Drain both streams independently; OCCT may emit verbose import diagnostics.
            var stdout = DrainAsync(process.StandardOutput); var stderr = DrainAsync(process.StandardError);
            try { await process.WaitForExitAsync(token).ConfigureAwait(false); }
            catch (OperationCanceledException)
            {
                try { if (!process.HasExited) process.Kill(entireProcessTree: true); } catch (InvalidOperationException) { }
                await process.WaitForExitAsync(CancellationToken.None).ConfigureAwait(false); throw;
            }
            await stdout.ConfigureAwait(false); var error = await stderr.ConfigureAwait(false);
            if (process.ExitCode != 0 || !File.Exists(output.FilePath))
                throw new InvalidDataException("OCCT preview import failed: " + error);
            return output;
        }
        catch { output.Dispose(); throw; }
    }
    private static async Task<string> DrainAsync(StreamReader reader)
    {
        var text = new StringBuilder(); var buffer = new char[2048]; int count;
        while ((count = await reader.ReadAsync(buffer).ConfigureAwait(false)) != 0)
            if (text.Length < 8192) text.Append(buffer, 0, Math.Min(count, 8192 - text.Length));
        return text.ToString();
    }
}
