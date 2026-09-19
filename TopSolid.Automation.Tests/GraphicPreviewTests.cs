using System.Diagnostics;
using System.Text;
using System.Windows.Media;
using System.Windows.Media.Media3D;
using System.Windows.Input;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using TopSolid.Automation.AI.Studio.Mcp;
using TopSolid.Automation.AI.Studio.Preview;

namespace TopSolid.Automation.Tests;

internal static class GraphicPreviewTests
{
    internal static async Task Run()
    {
        var scene = GlbPreviewReader.Read(Fixture(), CancellationToken.None);
        Check.True(scene.Surfaces.IsFrozen && scene.Edges.IsFrozen, "Preview geometry must be frozen before crossing threads");
        Check.Equal(12, scene.Triangles, "Native triangle count changed");
        Check.True(Math.Abs(scene.Bounds.SizeX - 40) < .001 && Math.Abs(scene.Bounds.SizeY - 30) < .001 && Math.Abs(scene.Bounds.SizeZ - 20) < .001, "glTF metres/Y-up conversion changed physical dimensions");
        foreach (var mutation in new Action<JObject>[] {
            r => r["buffers"]![0]!["uri"] = "https://example.test/geometry.bin",
            r => r["accessors"]![0]!["count"] = int.MaxValue,
            r => r["accessors"]![0]!["byteOffset"] = 999999,
            r => r["nodes"]![0]!["children"] = new JArray(0),
            r => r["extensionsRequired"] = new JArray("KHR_draco_mesh_compression"),
            r => r["meshes"]![0]!["primitives"]![0]!["mode"] = 3,
            r => r["nodes"]![0]!["matrix"] = new JArray(1, 2),
            r => r["accessors"]![1]!["componentType"] = 5126 })
        {
            try { GlbPreviewReader.Read(Fixture(mutation), CancellationToken.None); throw new Exception("Malformed GLB accepted"); }
            catch (Exception error) when (error is InvalidDataException or OverflowException) { }
        }
        foreach (var bytes in new[] { new byte[2], Fixture()[..^1], new byte[GlbPreviewReader.MaximumBytes + 1] })
        { try { GlbPreviewReader.Read(bytes, CancellationToken.None); throw new Exception("Invalid length accepted"); } catch (InvalidDataException) { } }
        var instanced = Fixture(root => {
            var node = root["nodes"]![0]!;
            root["nodes"] = new JArray(Enumerable.Range(0, 200).Select(_ => node.DeepClone()));
            root["scenes"]![0]!["nodes"] = new JArray(Enumerable.Range(0, 200));
        }, unusedVertices: 2000);
        try { GlbPreviewReader.Read(instanced, CancellationToken.None); throw new Exception("Instanced vertex amplification accepted"); } catch (InvalidDataException) { }
        using (var cancelled = new CancellationTokenSource())
        { cancelled.Cancel(); await Check.ThrowsAsync<OperationCanceledException>(() => Task.Run(() => GlbPreviewReader.Read(Fixture(), cancelled.Token))); }
        var proposal = CylinderProposal(); var snapshot = proposal.DeepClone(); var cylinder = ProposalGeometry.Build(proposal, CancellationToken.None);
        Check.True(Math.Abs(cylinder.Bounds.SizeX - 80) < .001 && Math.Abs(cylinder.Bounds.SizeZ - 160) < .001, "Proposed cylinder ignored native resolved dimension");
        Check.True(JToken.DeepEquals(proposal, snapshot), "Graphics changed an approval proposal");
        Check.Equal(288, cylinder.Triangles, "Cylinder must meet the 0.05 mm / 5 degree contract");
        PreviewAppearanceAndPrecision();
        proposal["arguments"]!["axisDirection"] = JObject.Parse("{x:1,y:0,z:0}");
        Check.True(Math.Abs(ProposalGeometry.Build(proposal, CancellationToken.None).Bounds.SizeX - 160) < .001, "Cylinder axis not respected");
        proposal["arguments"]!["units"] = "cm"; proposal["arguments"]!["diameter"] = 8;
        Check.True(Math.Abs(ProposalGeometry.Build(proposal, CancellationToken.None).Bounds.SizeY - 80) < .001, "Centimetre input changed the proposed physical diameter");
        var rectangle = ProposalGeometry.Build(JObject.Parse("{toolName:'topsolid_create_extruded_rectangle',arguments:{x:2,y:3,z:4,width:4,height:3,depth:2,units:'cm'}}"), CancellationToken.None);
        Check.True(rectangle.Bounds.X == 20 && rectangle.Bounds.Y == 30 && rectangle.Bounds.Z == 40 && rectangle.Bounds.SizeX == 40 && rectangle.Bounds.SizeY == 30 && rectangle.Bounds.SizeZ == 20,
            "Rectangular extrusion preview lost origin, units or dimension ordering");
        var ambiguous = JObject.Parse("{target:{documents:[{documentId:'one'},{documentId:'two'}]}}");
        Check.True(PreviewTarget.FromProposal(ambiguous) == null, "Ambiguous preview silently selected a document");
        Check.True(PreviewTarget.FromChoice(JObject.Parse("{value:'123',sourceArguments:{}}"), "document") == null, "Numeric ID became a document handle");
        Check.True(PreviewTarget.FromChoice(JObject.Parse("{value:{documentId:'part'},sourceArguments:{documentId:'wrong'}}"), "shape")?["documentId"]?.ToString() == "part", "Selected receipt lost its document scope");
        await PreviewCancellation();
    }

    private static void PreviewAppearanceAndPrecision()
    {
        foreach (var radius in new[] { .01, 1d, 40, 1000, 100000 })
        {
            var segments = PreviewQuality.CircleSegments(radius);
            Check.True(360d / segments <= 5 && radius * (1 - Math.Cos(Math.PI / segments)) <= .050000001,
                "Curved preview exceeded the requested angular or chord tolerance");
        }
        try { PreviewQuality.CircleSegments(1e9); throw new Exception("Unachievable tolerance silently degraded"); } catch (InvalidDataException) { }
        var scene = ProposalGeometry.Build(CylinderProposal(), CancellationToken.None);
        Color SurfaceColor(PreviewScene s) => ((SolidColorBrush)((DiffuseMaterial)((MaterialGroup)((GeometryModel3D)s.Surfaces.Children[0]).Material).Children[0]).Brush).Color;
        Check.Equal(Color.FromRgb(192, 192, 192), SurfaceColor(scene), "Unrequested orange preview tint returned");
        var colored = CylinderProposal(); colored["arguments"]!["color"] = JObject.Parse("{r:12,g:96,b:180}");
        Check.Equal(Color.FromRgb(12, 96, 180), SurfaceColor(ProposalGeometry.Build(colored, CancellationToken.None)), "Explicit requested color was lost");
        foreach (var scale in new[] { .01, .1, 10d })
        {
            var edges = scene.CreateEdges(new Vector3D(1, -1, 1), scale);
            Check.True(edges.IsFrozen, "Edge camera batch must be frozen");
            var first = (GeometryModel3D)edges.Children[0]; var mesh = (MeshGeometry3D)first.Geometry;
            Check.True(Math.Abs((mesh.Positions[1] - mesh.Positions[0]).Length / scale - 1) < 1e-8, "Edge grew thicker when zooming");
            Check.Equal(Colors.Black, ((SolidColorBrush)((DiffuseMaterial)first.Material).Brush).Color, "Edges must remain black independent of lighting");
        }
        Check.True(scene.CreateEdges(new Vector3D(1, 0, 0), 1).Children.Count > 0, "Curved silhouette lost its outline");
        foreach (var modifiers in new[] { ModifierKeys.None, ModifierKeys.Shift, ModifierKeys.Control })
        {
            Check.Equal(PreviewDrag.None, PreviewNavigation.Gesture(MouseButton.Left, modifiers), "Left drag must never move the camera");
            Check.Equal(PreviewDrag.Orbit, PreviewNavigation.Gesture(MouseButton.Middle, modifiers), "Wheel-button drag must rotate");
        }
        Check.Equal(PreviewDrag.Pan, PreviewNavigation.Gesture(MouseButton.Right, ModifierKeys.None), "Right drag must pan");
        Check.Equal(PreviewDrag.Orbit, PreviewNavigation.Gesture(MouseButton.Right, ModifierKeys.Control), "Ctrl + right drag must rotate");
        var orbitDelta = PreviewNavigation.OrbitDelta(new System.Windows.Vector(10, -5));
        Check.True(orbitDelta.Horizontal < 0 && orbitDelta.Vertical < 0, "Orbit must follow TopSolid's horizontal inverse and vertical screen-drag direction");
        var stl = StlPreviewReader.Read(StlFixture(), CancellationToken.None);
        Check.True(stl.Triangles == 12 && stl.Bounds.SizeX == 40 && stl.Bounds.SizeY == 30 && stl.Bounds.SizeZ == 20, "STL millimetres/Z-up conversion changed dimensions");
        foreach (var bytes in new[] { new byte[2], StlFixture()[..^1], new byte[StlPreviewReader.MaximumBytes + 1], StlFixture() })
        {
            if (bytes.Length == StlFixture().Length) BitConverter.GetBytes(float.NaN).CopyTo(bytes, 96);
            try { StlPreviewReader.Read(bytes, CancellationToken.None); throw new Exception("Malformed STL accepted"); } catch (InvalidDataException) { }
        }
    }

    internal static byte[] StlFixture()
    {
        Point3D[] points = [new(0,0,0), new(40,0,0), new(40,30,0), new(0,30,0), new(0,0,20), new(40,0,20), new(40,30,20), new(0,30,20)];
        int[] indices = [0,2,1, 0,3,2, 4,5,6, 4,6,7, 0,1,5, 0,5,4, 1,2,6, 1,6,5, 2,3,7, 2,7,6, 3,0,4, 3,4,7];
        using var stream = new MemoryStream(); using var writer = new BinaryWriter(stream);
        writer.Write(new byte[80]); writer.Write(12u);
        for (var i = 0; i < indices.Length; i += 3)
        {
            writer.Write(0f); writer.Write(0f); writer.Write(0f);
            for (var v = 0; v < 3; v++) { var p = points[indices[i + v]]; writer.Write((float)p.X); writer.Write((float)p.Y); writer.Write((float)p.Z); }
            writer.Write((ushort)0);
        }
        return stream.ToArray();
    }

    internal static JObject CylinderProposal() => JObject.Parse("""
        {toolName:'topsolid_create_cylinder',confirmationToken:'fixture-approval',effect:'Create a cylinder in the selected part.',
        inputLengthUnits:'mm',target:{documentId:'fixture-part',name:'Mounting pin',height:{mode:'parameter',currentValueSI:0.16,name:'Pin length'}},
        arguments:{documentId:'fixture-part',diameter:80,heightParameter:{documentId:'fixture-part',id:7899},method:'extrude',units:'mm'}}
        """);

    internal static JObject Result(string id = "fixture-part") => new() { ["status"] = "ready", ["documentId"] = id, ["name"] = "Mounting plate",
        ["format"] = "glb", ["upAxis"] = "Y", ["units"] = "m", ["data"] = Convert.ToBase64String(Fixture()) };

    internal static byte[] Fixture(Action<JObject>? change = null, int unusedVertices = 0)
    {
        // An uncompressed, embedded glTF cube in metres, with native-style root transform.
        float[] points = [0,0,0, .04f,0,0, .04f,.03f,0, 0,.03f,0, 0,0,.02f, .04f,0,.02f, .04f,.03f,.02f, 0,.03f,.02f];
        uint[] indices = [0,2,1, 0,3,2, 4,5,6, 4,6,7, 0,1,5, 0,5,4, 1,2,6, 1,6,5, 2,3,7, 2,7,6, 3,0,4, 3,4,7];
        points = indices.SelectMany(i => new[] { points[i * 3], points[i * 3 + 1], points[i * 3 + 2] }).ToArray();
        indices = Enumerable.Range(0, 36).Select(i => (uint)i).ToArray();
        if (unusedVertices > 0) Array.Resize(ref points, points.Length + unusedVertices * 3);
        var root = JObject.Parse("""
            {asset:{version:'2.0'},buffers:[{byteLength:576}],bufferViews:[{buffer:0,byteOffset:0,byteLength:432},{buffer:0,byteOffset:432,byteLength:144}],
            accessors:[{bufferView:0,componentType:5126,count:36,type:'VEC3'},{bufferView:1,componentType:5125,count:36,type:'SCALAR'}],
            meshes:[{primitives:[{attributes:{POSITION:0},indices:1}]}],
            nodes:[{mesh:0,matrix:[1,0,0,0,0,0,-1,0,0,1,0,0,0,0,0,1]}],scene:0,scenes:[{nodes:[0]}]}
            """);
        var dataBytes = points.Length * 4 + indices.Length * 4;
        root["buffers"]![0]!["byteLength"] = dataBytes; root["bufferViews"]![0]!["byteLength"] = points.Length * 4;
        root["bufferViews"]![1]!["byteOffset"] = points.Length * 4; root["accessors"]![0]!["count"] = points.Length / 3;
        change?.Invoke(root); var json = Encoding.UTF8.GetBytes(root.ToString(Formatting.None)); var padded = (json.Length + 3) / 4 * 4;
        using var output = new MemoryStream(); using var writer = new BinaryWriter(output);
        writer.Write(0x46546C67); writer.Write(2); writer.Write(12 + 8 + padded + 8 + dataBytes); writer.Write(padded); writer.Write(0x4E4F534A);
        writer.Write(json); for (var i = json.Length; i < padded; i++) writer.Write((byte)32);
        writer.Write(dataBytes); writer.Write(0x004E4942); foreach (var p in points) writer.Write(p); foreach (var index in indices) writer.Write(index);
        return output.ToArray();
    }

    private static async Task PreviewCancellation()
    {
        await using var client = new StdioMcpClient(); var started = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        client.Diagnostic += text => { if (text == "fixture preview started") started.TrySetResult(); };
        var previous = Environment.GetEnvironmentVariable("TOPSOLID_MCP_TEST_FIXTURE");
        try { Environment.SetEnvironmentVariable("TOPSOLID_MCP_TEST_FIXTURE", "1"); await client.ConnectAsync(Path.Combine(AppContext.BaseDirectory, "TopSolid.Automation.Tests.exe"), CancellationToken.None); }
        finally { Environment.SetEnvironmentVariable("TOPSOLID_MCP_TEST_FIXTURE", previous); }
        using var stop = new CancellationTokenSource(); var request = client.GetGraphicPreviewAsync(new JObject { ["documentId"] = "fixture" }, stop.Token);
        await started.Task.WaitAsync(TimeSpan.FromSeconds(5)); stop.Cancel();
        await Check.ThrowsAsync<OperationCanceledException>(() => request);
        Check.True(client.IsConnected, "Closing a graphic preview destroyed the MCP approval session");
        var next = await client.GetGraphicPreviewAsync(new JObject { ["documentId"] = "next" }, CancellationToken.None);
        Check.Equal("next", (string)next["documentId"]!, "Drained preview response was reused for another selection");
        Check.True(client.IsConnected, "Transport was not usable after preview cancellation");
        var large = await client.GetGraphicPreviewAsync(new JObject { ["documentId"] = "large-preview" }, CancellationToken.None);
        Check.True(((string)large["data"]!).Length > 4 * 1024 * 1024 && client.IsConnected, "A bounded precision mesh exceeded the old MCP line limit");
    }

    internal static async Task Live(string server, string? documentId = null)
    {
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(55));
        await using var client = new StdioMcpClient(); await client.ConnectAsync(server, timeout.Token);
        async Task<JObject> Read(string name, JObject args)
        {
            var result = await client.CallToolAsync(name, args, timeout.Token); Check.True(!result.IsError, "Native read failed");
            return JObject.Parse((string)result.Content[0]["text"]!);
        }
        var before = await Read("topsolid_get_document_info", documentId == null ? new JObject() : new JObject { ["documentId"] = documentId }); var doc = (JObject)before["document"]!;
        var target = new JObject { ["documentId"] = doc["documentId"]!.DeepClone() };
        var listArgs = (JObject)target.DeepClone(); listArgs["kind"] = "shapes"; listArgs["limit"] = 100;
        var shapes = await Read("topsolid_list_named_elements", listArgs); var watch = Stopwatch.StartNew();
        var payload = await client.GetGraphicPreviewDataAsync(target, timeout.Token); var preview = payload.Metadata; var exportMs = watch.Elapsed.TotalMilliseconds;
        Check.Equal("ready", (string)preview["status"]!, "Live native export was unavailable");
        var bytes = payload.Bytes!; watch.Restart();
        var stl = (string?)preview["format"] == "stl";
        var scene = await Task.Run(() => stl ? StlPreviewReader.Read(bytes, timeout.Token) : GlbPreviewReader.Read(bytes, timeout.Token)); var parseMs = watch.Elapsed.TotalMilliseconds;
        Check.True(scene.Triangles > 0 && scene.Surfaces.IsFrozen, "Live geometry did not reach the frozen renderer");
        var edgeTimes = new List<double>();
        for (var i = 0; i < 24; i++)
        {
            watch.Restart();
            var edges = scene.CreateEdges(new Vector3D(Math.Cos(i * Math.PI / 12), Math.Sin(i * Math.PI / 12), .5), scene.Bounds.SizeX / 500);
            Check.True(edges.IsFrozen && edges.Children.Count <= 7, "Edge batch escaped its interaction budget");
            edgeTimes.Add(watch.Elapsed.TotalMilliseconds);
        }
        edgeTimes.Sort();
        var after = await Read("topsolid_get_document_info", target);
        Check.True(JToken.DeepEquals(before["document"], after["document"]), "Preview changed document identity or dirty state");
        Check.True(JToken.DeepEquals(shapes, await Read("topsolid_list_named_elements", listArgs)), "Preview changed native shape inventory");
        var output = Path.GetFullPath("artifacts/graphic-preview-0.5.14"); Directory.CreateDirectory(output); File.WriteAllBytes(Path.Combine(output, "live-part." + (stl ? "stl" : "glb")), bytes);
        var report = new JObject { ["name"] = preview["name"]!.DeepClone(), ["bytes"] = bytes.Length, ["triangles"] = scene.Triangles,
            ["exportMilliseconds"] = exportMs, ["parseMilliseconds"] = parseMs, ["widthMm"] = scene.Bounds.SizeX, ["depthMm"] = scene.Bounds.SizeY,
            ["heightMm"] = scene.Bounds.SizeZ, ["documentAndInventoryUnchanged"] = true, ["hardwareTierCapability"] = RenderCapability.Tier >> 16,
            ["edgePreparationMedianMilliseconds"] = edgeTimes[12], ["edgePreparationP95Milliseconds"] = edgeTimes[22],
            ["processPeakWorkingSetMiB"] = Process.GetCurrentProcess().PeakWorkingSet64 / 1048576d,
            ["format"] = preview["format"], ["linearToleranceMm"] = preview["linearToleranceMm"], ["angularToleranceDegrees"] = preview["angularToleranceDegrees"] };
        File.WriteAllText(Path.Combine(output, "live-read-preview.json"), report.ToString()); Console.WriteLine(report);
    }
}
