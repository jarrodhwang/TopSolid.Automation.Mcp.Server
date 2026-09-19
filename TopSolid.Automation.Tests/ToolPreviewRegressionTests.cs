using System.Windows.Media;
using Newtonsoft.Json.Linq;
using TopSolid.Automation.AI.Studio.Chat;
using TopSolid.Automation.AI.Studio.AI;
using TopSolid.Automation.AI.Studio.Preview;
using TopSolid.Automation.AI.Studio.Preview.Streaming;
using TopSolid.Automation.Mcp.Contracts;

namespace TopSolid.Automation.Tests;

internal static class ToolPreviewRegressionTests
{
    internal static async Task NativeGeometry(string file)
    {
        var watch = System.Diagnostics.Stopwatch.StartNew();
        using var source = await GlbChunkSource.OpenAsync(file, PreviewResources.Detect(), CancellationToken.None, true);
        long triangles = 0;
        await Parallel.ForEachAsync(source.Chunks, new ParallelOptions { MaxDegreeOfParallelism = 4 }, async (chunk, token) =>
        {
            var mesh = await source.ReadAsync(chunk.Id, token);
            Check.True(mesh.Positions.All(v => float.IsFinite(v.X) && float.IsFinite(v.Y) && float.IsFinite(v.Z)), "Nonfinite native vertices");
            Check.Equal(mesh.Positions.Length, mesh.Normals.Length, "Missing native normals");
            Interlocked.Add(ref triangles, mesh.Indices.Length / 3);
        });
        Check.Equal(source.TriangleCount, triangles, "Paged native model was truncated");
        var report = new JObject { ["file"] = Path.GetFullPath(file), ["triangles"] = triangles, ["chunks"] = source.Chunks.Length,
            ["colors"] = new JArray(source.Chunks.Select(c => c.Color!.Value.ToString()).Distinct()), ["seconds"] = watch.Elapsed.TotalSeconds,
            ["peakWorkingSetMiB"] = System.Diagnostics.Process.GetCurrentProcess().PeakWorkingSet64 / 1048576d };
        File.WriteAllText(Path.ChangeExtension(file, ".geometry-check.json"), report.ToString()); Console.WriteLine(report);
    }
    internal static async Task Run()
    {
        var row = JObject.Parse("{documentId:'cam-owner',id:11064,name:'internal tool',toolDisplayName:'T 1 : Side Mill D10 L25 SD10',toolDefinitionName:'Side Mill D10 L25 SD10',toolFunction:'SideMill',toolPreviewDocumentId:'library-tool'}");
        var snapshot = row.DeepClone(); var sources = new QuestionSources();
        sources.Capture("tools", "topsolid_list_cam_tools", new JObject { ["documentId"] = "cam-owner" },
            new McpToolResult { StructuredContent = new JObject { ["items"] = new JArray(row) } });
        var question = sources.Create(JObject.Parse("{question:'Tools',kind:'select',itemKind:'element',sources:[{toolCallId:'tools',path:'/items'}]}"));
        Check.Equal("T 1 : Side Mill D10 L25 SD10", question.Choices[0].Label, "Native tool name or dimensions were redacted");
        Check.Equal("tool", question.Choices[0].Kind, "Model-provided item kind erased native tool metadata");
        Check.True(question.Choices[0].IconKey?.Contains("side", StringComparison.OrdinalIgnoreCase) == true, "Side mill uses a generic/wrong tool icon");
        Check.Equal("library-tool", (string)question.PreviewTargetFor(question.Choices[0].Key)!["documentId"]!, "Tool click previews the machining document");
        Check.True(question.DocumentPreview == null, "Tool list preloads the CAM model");
        Check.True(JToken.DeepEquals(row, snapshot), "Presentation changed a tool receipt");
        Check.True(PreviewTarget.FromChoice(JObject.Parse("{sourceTool:'topsolid_list_cam_tools',value:{documentId:'cam-owner',id:1},sourceArguments:{documentId:'cam-owner'}}"), "tool") == null,
            "Missing tool reference silently falls back to its CAM owner");
        Check.True(ListPresentation.DirectTools("현재 가공 문서의 공구 리스트 보여줘").SequenceEqual(["topsolid_list_cam_tools"]), "Attached log's tool-list request still needs model retries");
        var toolMcp = new FakeMcpClient { Tools = [new() { Name = "topsolid_list_cam_tools", Annotations = new JObject { ["readOnlyHint"] = true } }] };
        toolMcp.OnCall = _ => Task.FromResult(new McpToolResult { StructuredContent = new JObject { ["items"] = new JArray(
            JObject.Parse("{name:'T1',toolDisplayName:'T 1 : Side Mill D10 L25 SD10',toolPreviewDocumentId:'library-tool'}")) } });
        using var toolModel = new FakeAiProvider(); var toolPreviewOpened = 0;
        var toolSession = new ChatSession(toolModel, toolMcp)
        {
            AskUserAsync = (q, _) => Task.FromResult<QuestionAnswer?>(q.Answer(selectedKeys: [q.Choices[0].Key])),
            ShowGraphicPreviewAsync = (target, _) => { Check.Equal("library-tool", (string?)target["documentId"], "Tool preview opened the CAM owner instead of the definition document"); toolPreviewOpened++; return Task.CompletedTask; }
        };
        var toolAnswer = await toolSession.SendAsync("T1 공구를 선택해서 실제 공구 정의 문서의 3D 프리뷰를 보여줘.", CancellationToken.None);
        Check.True(toolPreviewOpened == 1 && toolModel.CompletionCount == 0 && toolAnswer.Contains("preview", StringComparison.OrdinalIgnoreCase), "Attached log's T1 request still became raw text or used model inference");
        var mcp = new FakeMcpClient { Tools = [new() { Name = "topsolid_get_active_document", Annotations = new JObject { ["readOnlyHint"] = true } }] };
        mcp.OnCall = _ => Task.FromResult(new McpToolResult { StructuredContent = JObject.Parse("{document:{documentId:'active-cam',name:'CAM'}}") });
        using var model = new FakeAiProvider(); var opened = 0;
        var session = new ChatSession(model, mcp) { ShowGraphicPreviewAsync = (target, _) => {
            Check.Equal("active-cam", (string)target["documentId"]!, "Graphical preview changed its resolved active document"); opened++; return Task.CompletedTask; } };
        await session.SendAsync("Show a graphical preview of the active TopSolid document", CancellationToken.None);
        Check.True(opened == 1 && model.CompletionCount == 0 && mcp.Calls.Count == 1, "Log's explicit preview request was answered as text instead of opening the viewport");
        await session.SendAsync("현재 활성 부품의 형상을 3D로 미리 보여줘. 어떤 것도 수정하지 마.", CancellationToken.None);
        Check.True(opened == 2 && model.CompletionCount == 0 && mcp.Calls.Count == 2,
            $"Read-only Korean active-shape preview was mistaken for a mutation (opened={opened}, model={model.CompletionCount}, calls={mcp.Calls.Count})");
        Check.True(!GraphicPreviewRequest.Matches("delete current document and show graphical preview") &&
            !GraphicPreviewRequest.Matches("Show a graphical preview of the active TopSolid document named Blade"), "Qualified or mutating request was hijacked by preview shortcut");

        var material = JObject.Parse("{pbrMetallicRoughness:{baseColorFactor:[0.7529411764705882,0.5019607843137255,0,0.2]},alphaMode:'BLEND'}");
        var color = GlbPreviewReader.ReadColor(material, true);
        Check.Equal(Color.FromArgb(51, 192, 128, 0), color, "TopSolid source RGB/alpha changed during decoding");
        Check.Equal(51 / 255f, GpuPreviewRenderer.Material(color).DiffuseColor.Alpha, "GPU discards native transparency");
        Check.Equal((byte)188, GlbPreviewReader.ReadColor(JObject.Parse("{pbrMetallicRoughness:{baseColorFactor:[0.5,0,0,1]}}"), false).R, "Standard linear glTF factors did not use the sRGB transfer function");

        var folder = Path.Combine(Path.GetTempPath(), "tool-preview-tests-" + Guid.NewGuid().ToString("N")); Directory.CreateDirectory(folder);
        var file = Path.Combine(folder, "test.glb");
        var invalidFile = Path.Combine(folder, "invalid.glb");
        try
        {
            var bytes = GraphicPreviewTests.Fixture(root => {
                root["materials"] = new JArray(material); root["meshes"]![0]!["primitives"]![0]!["material"] = 0;
            });
            await File.WriteAllBytesAsync(file, bytes);
            using var source = await GlbChunkSource.OpenAsync(file, PreviewResources.FromHardware(1L << 30, 1L << 30, 2), CancellationToken.None, true);
            Check.Equal(12L, source.TriangleCount, "Paged GLB loses triangles");
            Check.Equal(color, source.Chunks[0].Color!.Value, "Paged GLB loses material colors/transparency");
            var mesh = await source.ReadAsync(0, CancellationToken.None);
            Check.Equal(36, mesh.Indices.Length, "Paged triangle indices changed");
            Check.True(Math.Abs(source.Bounds.Max.X-source.Bounds.Min.X-40) < .001 && Math.Abs(source.Bounds.Max.Z-source.Bounds.Min.Z-20) < .001,
                "Paged GLB transform or SI/up-axis conversion changed the model");
            using var cancelled = new CancellationTokenSource(); cancelled.Cancel();
            await Check.ThrowsAsync<OperationCanceledException>(() => source.ReadAsync(0, cancelled.Token));
            foreach (var invalid in new Action<JObject>[] {
                root => root["buffers"]![0]!["uri"] = "https://example.invalid/geometry.bin",
                root => root["accessors"]![0]!["byteOffset"] = long.MaxValue,
                root => root["nodes"]![0]!["children"] = new JArray(0) })
            {
                await File.WriteAllBytesAsync(invalidFile, GraphicPreviewTests.Fixture(invalid));
                await Check.ThrowsAsync<InvalidDataException>(async () => {
                    using var rejected = await GlbChunkSource.OpenAsync(invalidFile, PreviewResources.Detect(), CancellationToken.None);
                });
            }
        }
        finally { File.Delete(file); File.Delete(invalidFile); Directory.Delete(folder); }
    }
}
