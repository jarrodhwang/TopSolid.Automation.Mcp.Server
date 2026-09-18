using System.Diagnostics;
using Newtonsoft.Json.Linq;
using TopSolid.Automation.AI.Studio.Mcp;

namespace TopSolid.Automation.Tests;

internal static class ModelingPreviewTests
{
    // This fixture has no confirmed-call path. It can inspect an existing part
    // and prepare proposals, but it cannot create, recolor or delete geometry.
    internal static async Task Run(string executable)
    {
        await using var client = new StdioMcpClient();
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(50));
        await client.ConnectAsync(executable, timeout.Token);
        var record = new JObject { ["server"] = executable, ["nativeWrites"] = 0, ["capturedAtUtc"] = DateTimeOffset.UtcNow.ToString("O") };
        async Task<JObject> Read(string name, JObject args)
        {
            var result = await client.CallToolAsync(name, args, timeout.Token);
            Check.True(!result.IsError, name + ": " + result.Content);
            return result.StructuredContent ?? JObject.Parse((string)result.Content[0]["text"]!);
        }
        async Task Preview(string key, string tool, JObject args)
        {
            var watch = Stopwatch.StartNew(); var proposal = await client.PrepareToolAsync(tool, args, timeout.Token);
            Check.True(proposal["confirmationToken"] != null, "Preview must return an approval challenge"); proposal.Remove("confirmationToken");
            record[key] = new JObject { ["seconds"] = watch.Elapsed.TotalSeconds, ["proposal"] = proposal };
        }
        var info = await Read("topsolid_get_document_info", new JObject());
        var doc = info["document"] as JObject ?? throw new InvalidOperationException("A live preview needs an existing open part; none will be created automatically.");
        Check.True((string?)doc["typeFullName"] == "TopSolid.Cad.Design.DB.Documents.PartDocument", "This fixture requires an already open Design part.");
        var id = (string)doc["documentId"]!; record["documentBefore"] = doc.DeepClone();
        var contextArgs = new JObject { ["documentId"] = id, ["documentSpace"] = "3d", ["limit"] = 100 };
        var context = await Read("topsolid_get_sketch2d_context", contextArgs); record["sketchesBefore"] = context;
        var shapeArgs = new JObject { ["documentId"] = id, ["kind"] = "shapes", ["limit"] = 100 };
        var shapes = await Read("topsolid_list_named_elements", shapeArgs); record["shapesBefore"] = shapes;
        var p = JObject.Parse("{diameter:100,height:350,color:{r:255,g:0,b:0},name:'Preview only - never executed'}"); p["documentId"] = id;
        foreach (var method in new[] { "extrude", "revolve" }) { p["method"] = method; await Preview(method, "topsolid_create_cylinder", p); }
        p.Remove("name");
        await Preview("automaticNames", "topsolid_create_cylinder", p);
        Check.True(record["automaticNames"]!["proposal"]!["target"]!["naming"] == null && record["automaticNames"]!["proposal"]!["target"]!["sketchPlan"]!["name"]?.Type == JTokenType.Null, "Automatic cylinder injected a sketch/shape name");
        var existingName = ((JArray)context["items"]!).Select(row => (string?)row["internalName"]).FirstOrDefault(name => !string.IsNullOrEmpty(name) && !name.StartsWith('$') && name.Length <= 100);
        if (existingName != null) {
            p["name"] = existingName; await Preview("existingNameCollision", "topsolid_create_cylinder", p);
            var naming = record["existingNameCollision"]!["proposal"]!["target"]!["naming"]!["assignments"]![0]!;
            Check.True((string?)naming["requestedName"] == existingName && (string?)naming["assignedName"] != existingName && (bool)naming["adjusted"]!, "Native name collision was not resolved before confirmation");
        }
        var duplicateSketches = JArray.Parse("[{placement:'xy',name:'Naming preview only',profiles:[{kind:'circle',origin:{x:0,y:0},radius:10}]},{placement:'xy',name:'Naming preview only',profiles:[{kind:'circle',origin:{x:50,y:0},radius:10}]}]");
        await Preview("batchNameCollision", "topsolid_create_sketches2d", new JObject { ["documentId"] = id, ["documentSpace"] = "3d", ["sketches"] = duplicateSketches });
        var assigned = (JArray)record["batchNameCollision"]!["proposal"]!["target"]!["naming"]!["assignments"]!;
        Check.Equal(2, assigned.Select(v => (string)v["assignedName"]!).Distinct(StringComparer.OrdinalIgnoreCase).Count(), "Duplicate batch names reached confirmation");
        await Preview("slot", "topsolid_create_sketches2d", new JObject { ["documentId"] = id, ["documentSpace"] = "3d", ["sketches"] = JArray.Parse("[{placement:'xy',profiles:[{kind:'slot',center:{x:0,y:0},length:100,width:20,rotationDegrees:0}]}]") });
        await Preview("sketch3d", "topsolid_create_sketch3d_curves", new JObject { ["documentId"] = id, ["curves"] = JArray.Parse("[{kind:'circle',center:{x:0,y:0,z:0},normal:{x:0,y:0,z:1},radius:50}]") });
        var empty = ((JArray)context["items"]!).FirstOrDefault(row => (int?)row["profiles"] == 0);
        if (empty != null) {
            try {
                await client.PrepareToolAsync("topsolid_revolve_sketch", new JObject { ["documentId"] = id, ["sketch"] = empty["sketch"]!.DeepClone(), ["axisOrigin"] = JObject.Parse("{x:0,y:0,z:0}"), ["axisDirection"] = JObject.Parse("{x:0,y:0,z:1}"), ["angleDegrees"] = 360 }, timeout.Token);
                throw new InvalidOperationException("An empty-profile sketch unexpectedly passed feature preflight.");
            } catch (Exception ex) when (ex.Message.Contains("Separate line", StringComparison.Ordinal)) { record["noProfileRejectedBeforeConfirmation"] = true; record["profileError"] = ex.Message; }
        } else record["noProfileCheck"] = "No empty-profile sketch was present; covered by contract tests.";
        var colorTarget = ((JArray)shapes["items"]!).FirstOrDefault(row => (bool?)row["colorModifiable"] == true);
        if (colorTarget == null) {
            var sketches = await Read("topsolid_list_named_elements", new JObject { ["documentId"] = id, ["kind"] = "sketches2d", ["limit"] = 100 });
            colorTarget = ((JArray)sketches["items"]!).FirstOrDefault(row => (bool?)row["colorModifiable"] == true);
        }
        if (colorTarget != null) await Preview("elementColor", "topsolid_set_entity_colors", new JObject { ["documentId"] = id, ["elements"] = new JArray(colorTarget["element"]!.DeepClone()), ["color"] = JObject.Parse("{r:255,g:0,b:0}") });
        else record["colorPreview"] = "No color-modifiable shape/sketch was present.";
        var after = await Read("topsolid_get_document_info", new JObject { ["documentId"] = id });
        Check.True(JToken.DeepEquals(doc, after["document"]), "A preview changed document identity or dirty state");
        Check.True(JToken.DeepEquals(context, await Read("topsolid_get_sketch2d_context", contextArgs)), "A preview changed the sketch inventory");
        Check.True(JToken.DeepEquals(shapes, await Read("topsolid_list_named_elements", shapeArgs)), "A preview changed shape state");
        record["documentAndInventoryUnchanged"] = true;
        Directory.CreateDirectory("artifacts/naming-0.5.9"); File.WriteAllText("artifacts/naming-0.5.9/live-read-preview.json", record.ToString());
        Console.WriteLine("Live automatic naming, collision, batch, cylinder, slot, 3D sketch and appearance previews passed; zero native writes; document and inventories unchanged.");
    }
}
