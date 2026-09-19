using System.IO;
using Newtonsoft.Json.Linq;
using TopSolid.Automation.AI.Studio.Mcp;
using TopSolid.Automation.Mcp.Contracts;

namespace TopSolid.Automation.AI.Studio.Preview;

/// <summary>Owns a downloaded snapshot, with exact-file cleanup rather than recursive deletion.</summary>
internal sealed class PreviewFilePayload(JObject metadata, string? file) : IDisposable
{
    internal JObject Metadata { get; } = metadata;
    internal string? FilePath { get; } = file;
    public void Dispose()
    {
        if (FilePath == null) return;
        try { File.Delete(FilePath); Directory.Delete(Path.GetDirectoryName(FilePath)!); }
        catch (IOException) { } catch (UnauthorizedAccessException) { }
    }
}

internal static class PreviewFileTransfer
{
    /// <summary>No whole-export array/string, Int32 file offsets, or native document file-size limit.</summary>
    internal static async Task<PreviewFilePayload> DownloadAsync(IGraphicPreviewClient client, JObject target,
        CancellationToken token, IProgress<double>? progress = null)
    {
        var request = (JObject)target.DeepClone(); request["chunked"] = true; request["fileBacked"] = true;
        var metadata = await client.GetGraphicPreviewAsync(request, token).ConfigureAwait(false);
        if ((string?)metadata["status"] != "ready") return new(metadata, null);
        var id = (string?)metadata["transferId"];
        PreviewFilePayload? payload = null;
        try
        {
            var length = (long?)metadata["byteLength"];
            if (id != null && (!Guid.TryParseExact(id, "N", out _) || length is null or <= 0)) throw new InvalidDataException("Invalid preview transfer.");
            var format = (string?)metadata["format"];
            if (format is not ("stl" or "glb")) throw new InvalidDataException("Unsupported preview format.");
            // These are format field widths, not a small product/file-size policy.
            var formatMaximum = format == "stl" ? 84L + uint.MaxValue * 50L : uint.MaxValue;
            if (length > formatMaximum) throw new InvalidDataException("Preview exceeds its file format's length field.");
            var directory = Path.Combine(Path.GetTempPath(), "TopSolid-Studio-stream-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(directory);
            payload = new(metadata, Path.Combine(directory, "snapshot." + format));
            if (length.HasValue && new DriveInfo(Path.GetPathRoot(directory)!).AvailableFreeSpace - (256L << 20) < length.Value)
                throw new IOException("Insufficient temporary disk space for this preview.");
            await using (var output = new FileStream(payload.FilePath!, FileMode.CreateNew, FileAccess.Write, FileShare.Read,
                1024 * 1024, FileOptions.Asynchronous | FileOptions.SequentialScan))
            {
                if (id == null)
                {
                    var inline = (string?)metadata["data"] ?? throw new InvalidDataException("Missing preview data.");
                    if (inline.Length > GraphicPreviewQuality.MaximumRpcLineCharacters) throw new InvalidDataException("Oversized inline preview.");
                    await output.WriteAsync(Convert.FromBase64String(inline), token).ConfigureAwait(false);
                    metadata.Remove("data");
                }
                else
                {
                    var buffer = new byte[GraphicPreviewQuality.ChunkBytes];
                    for (long offset = 0; offset < length;)
                    {
                        var chunk = await client.GetGraphicPreviewAsync(new JObject { ["action"] = "read", ["transferId"] = id, ["offset"] = offset }, token).ConfigureAwait(false);
                        var encoded = (string?)chunk["data"];
                        var expected = (int)Math.Min(buffer.Length, length!.Value - offset);
                        if ((string?)chunk["transferId"] != id || (long?)chunk["offset"] != offset || encoded == null ||
                            encoded.Length > (buffer.Length + 2) / 3 * 4 || !Convert.TryFromBase64String(encoded, buffer, out var count) || count != expected)
                            throw new InvalidDataException("Invalid or incomplete preview chunk.");
                        await output.WriteAsync(buffer.AsMemory(0, count), token).ConfigureAwait(false);
                        offset += count; progress?.Report((double)offset / length.Value);
                    }
                }
            }
            return payload;
        }
        catch { payload?.Dispose(); throw; }
        finally
        {
            if (id != null)
            {
                // Do not let cleanup keep a closed/superseded viewport waiting behind a long CAD call.
                using var cleanup = new CancellationTokenSource(TimeSpan.FromSeconds(2));
                try { await client.GetGraphicPreviewAsync(new JObject { ["action"] = "release", ["transferId"] = id }, cleanup.Token).ConfigureAwait(false); }
                catch (Exception error) when (error is IOException or InvalidOperationException or OperationCanceledException) { }
            }
        }
    }
}
