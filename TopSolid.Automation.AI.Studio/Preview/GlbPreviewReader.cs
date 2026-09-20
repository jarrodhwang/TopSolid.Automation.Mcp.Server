using System.Buffers.Binary;
using System.IO;
using System.Text;
using System.Windows.Media;
using System.Windows.Media.Media3D;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace TopSolid.Automation.AI.Studio.Preview;

/// <summary>Bounded glTF 2.0 subset emitted by TopSolid. No external buffers, textures, URLs or executable content.</summary>
internal static class GlbPreviewReader
{
    internal const int MaximumBytes = TopSolid.Automation.Mcp.Contracts.GraphicPreviewQuality.MaximumGlbBytes;
    internal static PreviewScene Read(byte[] bytes, CancellationToken token, bool nativeColors = false, bool zUp = false, Action<int, PreviewScene>? nodeScene = null)
    {
        if (bytes.Length is < 28 or > MaximumBytes || U32(0) != 0x46546C67 || U32(4) != 2 || U32(8) != bytes.Length)
            throw new InvalidDataException("Invalid GLB header.");
        var jsonLength = checked((int)U32(12));
        if (jsonLength < 2 || jsonLength > bytes.Length - 28 || U32(16) != 0x4E4F534A) throw new InvalidDataException("Invalid GLB JSON chunk.");
        JObject root;
        using (var reader = new JsonTextReader(new StringReader(Encoding.UTF8.GetString(bytes, 20, jsonLength))) { MaxDepth = 48, DateParseHandling = DateParseHandling.None })
        { root = JObject.Load(reader, new JsonLoadSettings { DuplicatePropertyNameHandling = DuplicatePropertyNameHandling.Error }); if (reader.Read()) throw new InvalidDataException("Unexpected GLB JSON."); }
        var binHeader = checked(20 + jsonLength); var binStart = binHeader + 8;
        if (U32(binHeader + 4) != 0x004E4942 || U32(binHeader) != bytes.Length - binStart) throw new InvalidDataException("Invalid GLB binary chunk.");
        if ((string?)root["asset"]?["version"] != "2.0" || root["extensionsRequired"] is JArray required && required.Count > 0 ||
            root["buffers"] is not JArray buffers || buffers.Count != 1 || buffers[0]["uri"] != null ||
            root["skins"] is JArray skins && skins.Count > 0 || root["animations"] is JArray animations && animations.Count > 0)
            throw new InvalidDataException("Unsupported GLB data.");
        var binaryLength = (int?)buffers[0]["byteLength"] ?? -1;
        if (binaryLength < 0 || binaryLength > bytes.Length - binStart || bytes.Length - binStart - binaryLength > 3) throw new InvalidDataException("Invalid GLB buffer.");
        var nodes = root["nodes"] as JArray ?? throw new InvalidDataException("Missing scene nodes.");
        if (nodes.Count > 2048) throw new InvalidDataException("Too many scene nodes.");
        var meshes = new List<PreviewMesh>(); var visited = new HashSet<int>(); var triangles = 0; var vertices = 0;
        var components = new List<PreviewScene>();
        var scene = Entry("scenes", (int?)root["scene"] ?? 0);
        foreach (var node in scene["nodes"] as JArray ?? throw new InvalidDataException("Missing scene roots.")) Walk((int)node, Matrix3D.Identity, 0);
        return nodeScene == null ? PreviewScene.Build(meshes, token) : PreviewScene.Combine(components.ToArray());

        uint U32(int offset)
        { if (offset < 0 || offset > bytes.Length - 4) throw new InvalidDataException("Truncated GLB."); return BinaryPrimitives.ReadUInt32LittleEndian(bytes.AsSpan(offset, 4)); }
        JObject Entry(string array, int index)
        { if (root[array] is not JArray items || index < 0 || index >= items.Count || items[index] is not JObject entry) throw new InvalidDataException("Invalid GLB reference."); return entry; }
        void Walk(int index, Matrix3D parent, int depth)
        {
            token.ThrowIfCancellationRequested();
            if (depth > 64 || !visited.Add(index)) throw new InvalidDataException("Invalid scene hierarchy.");
            var node = Entry("nodes", index); var transform = Transform(node); transform.Append(parent);
            if (node["skin"] != null || node["weights"] != null) throw new InvalidDataException("Unsupported deformed mesh.");
            if (node["mesh"] is JValue meshIndex)
            {
                var firstMesh = meshes.Count;
                var mesh = Entry("meshes", (int)meshIndex);
                foreach (var p in mesh["primitives"] as JArray ?? throw new InvalidDataException("Missing primitives."))
                {
                    if (((int?)p["mode"] ?? 4) != 4 || p["targets"] != null || p["extensions"] != null) throw new InvalidDataException("Unsupported primitive.");
                    if (meshes.Count >= 512) throw new InvalidDataException("Too many display meshes.");
                    var positionAccessor = Accessor((int?)p["attributes"]?["POSITION"] ?? -1, "VEC3", 5126);
                    // Instancing may reuse a tiny buffer thousands of times. Bound expanded geometry,
                    // including unused positions, before allocating transformed arrays.
                    vertices = checked(vertices + positionAccessor.Count);
                    if (vertices > PreviewScene.MaximumTriangles * 3 || vertices > Math.Max(300000, binaryLength * 4L / 12)) throw new InvalidDataException("Expanded preview geometry is too large.");
                    var positions = new Point3D[positionAccessor.Count];
                    for (var i = 0; i < positions.Length; i++)
                    {
                        var at = positionAccessor.Offset + i * positionAccessor.Stride;
                        var world = transform.Transform(new Point3D(Float(at), Float(at + 4), Float(at + 8)));
                        positions[i] = zUp ? new Point3D(world.X * 1000, world.Y * 1000, world.Z * 1000)
                            : new Point3D(world.X * 1000, -world.Z * 1000, world.Y * 1000);
                    }
                    int[] indices;
                    if (p["indices"] != null)
                    {
                        var accessor = Accessor((int)p["indices"]!, "SCALAR", null);
                        if (accessor.Component is not (5121 or 5123 or 5125)) throw new InvalidDataException("Invalid index component.");
                        indices = new int[accessor.Count];
                        for (var i = 0; i < indices.Length; i++)
                        {
                            var at = accessor.Offset + i * accessor.Stride;
                            indices[i] = accessor.Component == 5121 ? bytes[at] : accessor.Component == 5123 ? BinaryPrimitives.ReadUInt16LittleEndian(bytes.AsSpan(at, 2)) : checked((int)U32(at));
                            if ((uint)indices[i] >= positions.Length) throw new InvalidDataException("Mesh index outside vertex buffer.");
                        }
                    }
                    else indices = Enumerable.Range(0, positions.Length).ToArray();
                    triangles = checked(triangles + indices.Length / 3);
                    if (indices.Length % 3 != 0 || triangles > PreviewScene.MaximumTriangles) throw new InvalidDataException("Invalid or oversized triangle data.");
                    if (transform.Determinant < 0) for (var i = 0; i < indices.Length; i += 3) (indices[i + 1], indices[i + 2]) = (indices[i + 2], indices[i + 1]);
                    Vector3D[]? normals = null;
                    if (p["attributes"]?["NORMAL"] != null)
                    {
                        var accessor = Accessor((int)p["attributes"]!["NORMAL"]!, "VEC3", 5126);
                        if (accessor.Count != positions.Length || !transform.HasInverse) throw new InvalidDataException("Invalid normal data.");
                        var inverse = transform; inverse.Invert();
                        normals = new Vector3D[positions.Length];
                        for (var i = 0; i < normals.Length; i++)
                        {
                            var at = accessor.Offset + i * accessor.Stride; var x = Float(at); var y = Float(at + 4); var z = Float(at + 8);
                            var n = new Vector3D(x * inverse.M11 + y * inverse.M12 + z * inverse.M13, x * inverse.M21 + y * inverse.M22 + z * inverse.M23, x * inverse.M31 + y * inverse.M32 + z * inverse.M33);
                            if (n.LengthSquared < 1e-24 || !double.IsFinite(n.LengthSquared)) throw new InvalidDataException("Invalid normal."); n.Normalize();
                            normals[i] = zUp ? n : new Vector3D(n.X, -n.Z, n.Y);
                        }
                    }
                    var color = ReadColor(p["material"] == null ? null : Entry("materials", (int)p["material"]!), nativeColors);
                    meshes.Add(new PreviewMesh(positions, indices, color, normals));
                }
                if (nodeScene != null && meshes.Count > firstMesh)
                {
                    var component = PreviewScene.Build(meshes.Skip(firstMesh).ToArray(), token);
                    components.Add(component); nodeScene(index, component);
                }
            }
            foreach (var child in node["children"] as JArray ?? []) Walk((int)child, transform, depth + 1);
        }
        float Float(int offset)
        { var number = BitConverter.Int32BitsToSingle(unchecked((int)U32(offset))); if (!float.IsFinite(number)) throw new InvalidDataException("Non-finite mesh value."); return number; }
        (int Offset, int Stride, int Count, int Component) Accessor(int index, string type, int? component)
        {
            var a = Entry("accessors", index); var view = Entry("bufferViews", (int?)a["bufferView"] ?? -1);
            var count = (int?)a["count"] ?? -1; var kind = (int?)a["componentType"] ?? -1;
            var width = kind switch { 5121 => 1, 5123 => 2, 5125 or 5126 => 4, _ => throw new InvalidDataException("Unsupported accessor component.") };
            var elementSize = width * (type == "VEC3" ? 3 : 1); var stride = (int?)view["byteStride"] ?? elementSize;
            var viewOffset = (int?)view["byteOffset"] ?? 0; var accessorOffset = (int?)a["byteOffset"] ?? 0; var viewLength = (int?)view["byteLength"] ?? -1;
            if ((string?)a["type"] != type || component.HasValue && kind != component || a["sparse"] != null || (bool?)a["normalized"] == true || (int?)view["buffer"] != 0 ||
                count <= 0 || count > PreviewScene.MaximumTriangles * 3 || stride < elementSize || stride > 252 || viewOffset < 0 || accessorOffset < 0 || viewLength < 0 ||
                (long)viewOffset + viewLength > binaryLength || (long)accessorOffset + (long)(count - 1) * stride + elementSize > viewLength)
                throw new InvalidDataException("Invalid accessor bounds.");
            return (checked(binStart + viewOffset + accessorOffset), stride, count, kind);
        }
    }
    internal static Color ReadColor(JObject? material, bool nativeColors)
    {
        if (material == null) return TopSolidPreviewPalette.Surface;
        var rgba = material["pbrMetallicRoughness"]?["baseColorFactor"] as JArray ?? new JArray(1, 1, 1, 1);
        if (rgba.Count != 4) throw new InvalidDataException("Invalid material color.");
        var values = rgba.Select(v => (double)v).ToArray();
        if (values.Any(v => !double.IsFinite(v) || v < 0 || v > 1)) throw new InvalidDataException("Invalid material color.");
        var mode = (string?)material["alphaMode"] ?? "OPAQUE";
        var alpha = mode switch { "OPAQUE" => 1, "BLEND" => values[3], "MASK" => values[3] >= ((double?)material["alphaCutoff"] ?? .5) ? 1 : 0,
            _ => throw new InvalidDataException("Invalid alpha mode.") };
        byte Channel(double v) => (byte)Math.Round(255 * (nativeColors ? v : v <= .0031308 ? 12.92 * v : 1.055 * Math.Pow(v, 1 / 2.4) - .055));
        return Color.FromArgb((byte)Math.Round(alpha * 255), Channel(values[0]), Channel(values[1]), Channel(values[2]));
    }
    internal static Matrix3D Transform(JObject node)
    {
        var m = Matrix3D.Identity;
        if (node["matrix"] is JArray array)
        {
            if (array.Count != 16 || node["translation"] != null || node["rotation"] != null || node["scale"] != null) throw new InvalidDataException("Invalid matrix.");
            var a = array.Select(t => (double)t).ToArray();
            if (a.Any(v => !double.IsFinite(v)) || a[3] != 0 || a[7] != 0 || a[11] != 0 || a[15] != 1) throw new InvalidDataException("Invalid affine matrix.");
            return new Matrix3D(a[0], a[1], a[2], a[3], a[4], a[5], a[6], a[7], a[8], a[9], a[10], a[11], a[12], a[13], a[14], a[15]);
        }
        var scale = Vector("scale", new Vector3D(1, 1, 1)); m.Scale(scale);
        if (node["rotation"] is JArray r)
        {
            if (r.Count != 4) throw new InvalidDataException("Invalid quaternion.");
            var a = r.Select(t => (double)t).ToArray(); if (a.Any(v => !double.IsFinite(v)) || Math.Abs(a.Sum(v => v * v) - 1) > .001) throw new InvalidDataException("Invalid rotation.");
            m.Rotate(new Quaternion(a[0], a[1], a[2], a[3]));
        }
        m.Translate(Vector("translation", new Vector3D())); return m;
        Vector3D Vector(string key, Vector3D fallback)
        {
            if (node[key] == null) return fallback;
            if (node[key] is not JArray a || a.Count != 3 || a.Any(v => !double.IsFinite((double)v))) throw new InvalidDataException("Invalid transform vector.");
            return new Vector3D((double)a[0], (double)a[1], (double)a[2]);
        }
    }
}
