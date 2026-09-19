using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Newtonsoft.Json.Linq;
using TopSolid.Automation.Mcp.Contracts;

namespace TopSolid.Automation.Mcp.Server.AddIn.Automation
{
    /// <summary>Session-owned, short-lived export files. Only random capabilities cross the RPC boundary.</summary>
    internal sealed class PreviewTransferStore : IDisposable
    {
        private sealed class Entry { internal string File; internal DateTime Expires; }
        private readonly Dictionary<string, Entry> files = new Dictionary<string, Entry>(StringComparer.Ordinal);
        internal string Add(string file)
        {
            Prune();
            while (files.Count >= 2) Remove(files.Keys.First());
            var id = Guid.NewGuid().ToString("N");
            files.Add(id, new Entry { File = file, Expires = DateTime.UtcNow.AddMinutes(3) }); return id;
        }
        internal JObject Handle(JObject request)
        {
            Prune(); var id = (string)request["transferId"]; var action = (string)request["action"];
            if (id == null || !files.TryGetValue(id, out var entry) ||
                request.Properties().Any(p => p.Name != "action" && p.Name != "transferId" && p.Name != "offset"))
                throw new ArgumentException("Invalid or expired preview transfer.");
            if (action == "release") { Remove(id); return new JObject { ["status"] = "released" }; }
            if (action != "read" || request["offset"]?.Type != JTokenType.Integer) throw new ArgumentException("Invalid preview chunk request.");
            var offset = (long)request["offset"];
            using (var stream = File.OpenRead(entry.File))
            {
                if (offset < 0 || offset >= stream.Length) throw new ArgumentException("Invalid preview chunk offset.");
                var bytes = new byte[(int)Math.Min(GraphicPreviewQuality.ChunkBytes, stream.Length - offset)]; stream.Position = offset;
                var read = 0; while (read < bytes.Length) { var n = stream.Read(bytes, read, bytes.Length - read); if (n == 0) throw new EndOfStreamException(); read += n; }
                entry.Expires = DateTime.UtcNow.AddMinutes(3);
                return new JObject { ["transferId"] = id, ["offset"] = offset, ["data"] = Convert.ToBase64String(bytes) };
            }
        }
        private void Prune() { foreach (var id in files.Where(p => p.Value.Expires < DateTime.UtcNow).Select(p => p.Key).ToArray()) Remove(id); }
        private void Remove(string id)
        {
            if (!files.TryGetValue(id, out var entry)) return; files.Remove(id);
            try { File.Delete(entry.File); Directory.Delete(Path.GetDirectoryName(entry.File)); }
            catch (IOException) { } catch (UnauthorizedAccessException) { }
        }
        public void Dispose() { foreach (var id in files.Keys.ToArray()) Remove(id); }
    }
}
