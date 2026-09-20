using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using TopSolid.Kernel.Automating;

namespace TopSolid.Automation.Mcp.Server.AddIn.Automation
{
    // Original stock is hidden in the CAM export. Read its included part-setting
    // definition and occurrence transform, without opening or modifying a document.
    internal static class CamStockPreview
    {
        internal static int Append(DocumentId document, string file, string directory)
        {
            var target = Asset.Read(file);
            var nodes = (JArray)target.Json["nodes"];
            var scene = target.Json["scenes"][(int?)target.Json["scene"] ?? 0];
            var categories = ((JArray)scene["nodes"]).Values<int>().Select(i => nodes[i])
                .Single(n => (string)n["name"] == "EntityFilterTypes");
            var stocks = new JArray();
            var exports = new Dictionary<string, Asset>();
            foreach (var setting in TopSolidHost.Elements.GetElements(document).Where(e =>
                TopSolidHost.Elements.GetTypeFullName(e).EndsWith("PartSettingOccurrenceEntity", StringComparison.Ordinal)).Take(64))
            {
                var children = TopSolidHost.Elements.GetConstituents(setting);
                var originals = children.Where(e => TopSolidHost.Elements.GetTypeFullName(e).EndsWith(".StockOccurrenceEntity", StringComparison.Ordinal))
                    .SelectMany(e => TopSolidHost.Elements.GetConstituents(e)).ToArray();
                if (originals.Length == 0) continue;
                var definition = TopSolidHost.Entities.GetOccurrenceSource(setting).DocumentId;
                if (definition.IsEmpty || !TopSolidHost.Documents.Exists(definition)) continue;
                var transform = TopSolidHost.Geometries3D.GetOccurrenceDefinitionTransform(setting);
                var placement = new[] { transform.R00, transform.R10, transform.R20, 0d, transform.R01, transform.R11, transform.R21, 0d,
                    transform.R02, transform.R12, transform.R22, 0d, transform.Tx, transform.Ty, transform.Tz, 1d };
                // Perspective/scaled occurrences require a different transform contract.
                if (transform.Px != 0 || transform.Py != 0 || transform.Pz != 0 || transform.Si != 1) continue;
                if (!exports.TryGetValue(definition.PdmDocumentId, out var source))
                {
                    var name = "stock-" + exports.Count;
                    var path = Path.Combine(directory, name + ".glb");
                    try
                    {
                        var dirty = TopSolidHost.Documents.IsDirty(definition);
                        TopSolidHost.Documents.zExportToTopglTF(definition, directory, name, true, false, false, true, true, true, true, 0, Color.Empty);
                        if (TopSolidHost.Documents.IsDirty(definition) != dirty) throw new InvalidOperationException("Stock definition changed during export.");
                        source = Asset.Read(path); exports.Add(definition.PdmDocumentId, source);
                    }
                    finally { if (File.Exists(path)) File.Delete(path); }
                }
                var sourceIds = originals.Select(TopSolidHost.Entities.GetOccurrenceSource)
                    .Where(e => e.DocumentId.Equals(definition)).Select(e => e.Id).ToArray();
                var selected = FindShapes(source.Json, sourceIds);
                if (selected.Count == 0) continue;
                var meshOffset = target.AppendGeometry(source);
                foreach (var shape in selected)
                {
                    stocks.Add(nodes.Count);
                    nodes.Add(new JObject { ["mesh"] = shape.Mesh + meshOffset,
                        ["matrix"] = new JArray(Multiply(placement, shape.Matrix)) });
                }
            }
            if (stocks.Count == 0) return 0;
            ((JArray)categories["children"]).Add(nodes.Count);
            nodes.Add(new JObject { ["name"] = "OriginalStock", ["children"] = stocks });
            var combined = file + ".stock";
            try { target.Write(combined); File.Copy(combined, file, true); }
            finally { if (File.Exists(combined)) File.Delete(combined); }
            return stocks.Count;
        }

        private sealed class Shape
        {
            internal int Mesh;
            internal double[] Matrix;
        }
        private static List<Shape> FindShapes(JObject root, int[] ids)
        {
            var nodes = (JArray)root["nodes"];
            var scene = root["scenes"][(int?)root["scene"] ?? 0];
            var parts = ((JArray)scene["nodes"]).Values<int>().Where(i => (string)nodes[i]["name"] == "Parts");
            var found = new List<Shape>(); var seen = new HashSet<string>(); var visited = new HashSet<int>();
            foreach (var i in parts) Visit(i, Identity(), 0);
            return found;
            void Visit(int i, double[] parent, int depth)
            {
                if (depth > 64 || i < 0 || i >= nodes.Count || !visited.Add(i)) throw new InvalidDataException("Invalid stock hierarchy.");
                var n = nodes[i];
                if (n["translation"] != null || n["rotation"] != null || n["scale"] != null) throw new InvalidDataException("Unsupported native stock transform.");
                var matrix = n["matrix"] is JArray m ? m.Values<double>().ToArray() : Identity();
                var world = Multiply(parent, matrix);
                // The exporter appends the native entity ID to shape names. Match the
                // Automation source ID, not a user-editable name such as 'stock'.
                if (n["mesh"] != null && ids.Any(id => ((string)n["name"] ?? "").EndsWith(" <" + id + ">", StringComparison.Ordinal)))
                {
                    var key = n["mesh"] + ":" + new JArray(world).ToString(Formatting.None);
                    if (seen.Add(key)) found.Add(new Shape { Mesh = (int)n["mesh"], Matrix = world });
                }
                foreach (var child in (JArray)n["children"] ?? new JArray()) Visit((int)child, world, depth + 1);
            }
        }
        private static double[] Identity() => new[] { 1d, 0, 0, 0, 0, 1, 0, 0, 0, 0, 1, 0, 0, 0, 0, 1 };
        private static double[] Multiply(double[] a, double[] b)
        {
            if (a.Length != 16 || b.Length != 16 || a.Concat(b).Any(v => double.IsNaN(v) || double.IsInfinity(v))) throw new InvalidDataException("Invalid stock matrix.");
            var c = new double[16];
            for (var col = 0; col < 4; col++) for (var row = 0; row < 4; row++)
                for (var k = 0; k < 4; k++) c[col * 4 + row] += a[k * 4 + row] * b[col * 4 + k];
            return c;
        }
        private sealed class Asset
        {
            internal JObject Json;
            private byte[] binary;
            internal static Asset Read(string file)
            {
                using (var r = new BinaryReader(File.OpenRead(file)))
                {
                    if (r.BaseStream.Length > 128 * 1024 * 1024 || r.ReadInt32() != 0x46546c67 || r.ReadInt32() != 2 || r.ReadInt32() != r.BaseStream.Length) throw new InvalidDataException("Invalid stock GLB.");
                    var length = r.ReadInt32();
                    if (length < 2 || length > 16 * 1024 * 1024 || r.ReadInt32() != 0x4e4f534a) throw new InvalidDataException("Invalid stock JSON.");
                    JObject json;
                    using (var reader = new JsonTextReader(new StringReader(Encoding.UTF8.GetString(r.ReadBytes(length)))) { MaxDepth = 64 }) json = JObject.Load(reader);
                    length = r.ReadInt32();
                    if (length < 0 || r.ReadInt32() != 0x004e4942 || length != r.BaseStream.Length - r.BaseStream.Position) throw new InvalidDataException("Invalid stock buffer.");
                    if (json["buffers"] is not JArray buffers || buffers.Count != 1 || buffers[0]["uri"] != null) throw new InvalidDataException("External stock buffer.");
                    return new Asset { Json = json, binary = r.ReadBytes(length) };
                }
            }
            internal int AppendGeometry(Asset source)
            {
                var views = (JArray)Json["bufferViews"]; var accessors = (JArray)Json["accessors"];
                var meshes = (JArray)Json["meshes"]; var materials = (JArray)Json["materials"];
                var viewOffset = views.Count; var accessorOffset = accessors.Count; var materialOffset = materials.Count; var meshOffset = meshes.Count;
                var offset = binary.Length;
                if ((long)offset + source.binary.Length > 128 * 1024 * 1024) throw new InvalidDataException("Stock geometry exceeds preview limit.");
                foreach (var item in ((JArray)source.Json["bufferViews"]).OfType<JObject>())
                { var v = (JObject)item.DeepClone(); v["byteOffset"] = checked(((int?)v["byteOffset"] ?? 0) + offset); views.Add(v); }
                foreach (var item in ((JArray)source.Json["accessors"]).OfType<JObject>())
                { var a = (JObject)item.DeepClone(); if (a["bufferView"] != null) a["bufferView"] = (int)a["bufferView"] + viewOffset; accessors.Add(a); }
                foreach (var item in (JArray)source.Json["materials"]) materials.Add(item.DeepClone());
                foreach (var item in ((JArray)source.Json["meshes"]).OfType<JObject>())
                {
                    var mesh = (JObject)item.DeepClone();
                    foreach (var p in ((JArray)mesh["primitives"]).OfType<JObject>())
                    {
                        foreach (var attribute in ((JObject)p["attributes"]).Properties()) attribute.Value = (int)attribute.Value + accessorOffset;
                        if (p["indices"] != null) p["indices"] = (int)p["indices"] + accessorOffset;
                        if (p["material"] != null) p["material"] = (int)p["material"] + materialOffset;
                        if (p["extensions"]?["KHR_draco_mesh_compression"] is JObject draco) draco["bufferView"] = (int)draco["bufferView"] + viewOffset;
                    }
                    meshes.Add(mesh);
                }
                var joined = new byte[offset + source.binary.Length]; Array.Copy(binary, joined, offset); Array.Copy(source.binary, 0, joined, offset, source.binary.Length); binary = joined;
                Json["buffers"][0]["byteLength"] = binary.Length;
                return meshOffset;
            }
            internal void Write(string file)
            {
                var json = Encoding.UTF8.GetBytes(Json.ToString(Formatting.None)); var padded = (json.Length + 3) & ~3;
                using (var w = new BinaryWriter(File.Create(file)))
                {
                    w.Write(0x46546c67); w.Write(2); w.Write(checked(28 + padded + binary.Length)); w.Write(padded); w.Write(0x4e4f534a); w.Write(json);
                    for (var i = json.Length; i < padded; i++) w.Write((byte)32);
                    w.Write(binary.Length); w.Write(0x004e4942); w.Write(binary);
                }
            }
        }
    }
}
