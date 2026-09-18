using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using TopSolid.Automation.AI.Studio.Chat;
using TopSolid.Automation.AI.Studio.Mcp;

namespace TopSolid.Automation.Tests;

/// <summary>Opt-in reads of the active CAM document. No prepare or confirmed-call path.</summary>
internal static class CamReadOnlyTests
{
    internal static async Task Run(string executable)
    {
        await using var client = new StdioMcpClient();
        using var timeout = new CancellationTokenSource(TimeSpan.FromMinutes(4));
        await client.ConnectAsync(executable, timeout.Token);
        var output = Path.GetFullPath("artifacts/cam-user-mode-0.5.10");
        Directory.CreateDirectory(output);
        var record = new JObject { ["capturedAtUtc"] = DateTimeOffset.UtcNow.ToString("O"), ["server"] = executable, ["nativeWrites"] = 0 };
        var calls = new JArray(); record["reads"] = calls;
        async Task<JObject> Read(string name, JObject args)
        {
            Check.True(client.Tools.Single(t => t.Name == name).Annotations["readOnlyHint"]?.Value<bool>() == true,
                "Live CAM fixture permits only declared read-only tools");
            var started = System.Diagnostics.Stopwatch.StartNew();
            var result = await client.CallToolAsync(name, args, timeout.Token);
            calls.Add(new JObject { ["tool"] = name, ["arguments"] = args.DeepClone(), ["seconds"] = started.Elapsed.TotalSeconds,
                ["modelCharacters"] = ToolResultContext.Serialize(result).Length });
            Check.True(!result.IsError, name + ": " + result.Content);
            return result.StructuredContent ?? JObject.Parse((string)result.Content[0]["text"]!);
        }
        try
        {
            var before = await Read("topsolid_get_document_info", new JObject());
            record["documentBefore"] = before;
            var document = before["document"] as JObject ?? throw new InvalidOperationException("Open a CAM document to run this optional read-only fixture.");
            var documentId = (string)document["documentId"]!;
            var operationArgs = new JObject { ["documentId"] = documentId, ["limit"] = 100 };
            var operations = await Read("topsolid_list_cam_operation_summaries", operationArgs);
            record["operations"] = operations;
            var rows = operations["items"] as JArray ?? throw new InvalidOperationException("CAM operations were not returned.");
            Check.True(rows.Count > 0, "The active document has no CAM operations; no fixture data will be created.");
            var operation = rows[Math.Min(1, rows.Count - 1)]["operation"]!.DeepClone();
            // ElementEx identities preserve their exact server shape; the schema
            // accepts the nested element for ordinary document operations.
            var element = operation["element"] ?? operation;
            var items = new JArray(); record["parameters"] = items;
            var seen = new HashSet<int>();
            var offset = 0;
            int? total = null;
            while (true)
            {
                Check.True(seen.Add(offset), "CAM parameter pagination repeated an offset");
                var page = await Read("topsolid_list_cam_parameters", new JObject { ["element"] = element.DeepClone(), ["offset"] = offset, ["limit"] = 100 });
                total ??= (int?)page["total"];
                Check.True(total == (int?)page["total"], "CAM parameter inventory changed during the read");
                foreach (var row in (JArray)page["items"]!) items.Add(row.DeepClone());
                Check.True((int)calls.Last!["modelCharacters"]! <= 64000, "CAM page exceeds Studio's model result budget");
                if ((bool?)page["hasMore"] != true) break;
                offset = (int?)page["nextOffset"] ?? throw new InvalidOperationException("CAM page omitted nextOffset");
            }
            Check.Equal(total ?? 0, items.Count, "CAM parameter pagination did not return the whole operation");
            record["parameterCount"] = items.Count;
            record["failedRows"] = items.Count(r => (bool?)r["isError"] == true);
            record["metadataErrorRows"] = items.Count(r => r["metadataErrors"] is JObject e && e.HasValues);
            record["editableRows"] = items.Count(r => (bool?)r["editSupported"] == true);
            Check.True(items.All(r => (bool?)r["isError"] != true), "Some CAM parameter rows failed; inspect the saved evidence");
            var details = new JArray(); record["cuttingConditionSamples"] = details;
            var preferred = new[] { "Feedrate@CuttingConditions", "CuttingSpeed@CuttingConditions", "ToothFeedrate@CuttingConditions", "SpindleRate@CuttingConditions|Tool" };
            foreach (var row in items.OfType<JObject>().Where(r => ((string?)r["name"] ?? "").Contains("Cutting", StringComparison.OrdinalIgnoreCase)
                || ((string?)r["name"] ?? "").Contains("Feed", StringComparison.OrdinalIgnoreCase)
                || ((string?)r["name"] ?? "").Contains("Spindle", StringComparison.OrdinalIgnoreCase))
                .OrderBy(r => { var index = Array.IndexOf(preferred, (string?)r["name"]); return index < 0 ? int.MaxValue : index; }).Take(8))
            {
                details.Add(await Read("topsolid_get_cam_parameter_value", new JObject { ["element"] = element.DeepClone(), ["name"] = row["name"]!.DeepClone() }));
            }
            var after = await Read("topsolid_get_document_info", new JObject { ["documentId"] = documentId });
            Check.True(JToken.DeepEquals(document, after["document"]), "Read-only CAM inspection changed document identity or dirty state");
            Check.True(JToken.DeepEquals(operations, await Read("topsolid_list_cam_operation_summaries", operationArgs)), "Operation state changed during read-only inspection");
            record["documentAndOperationsUnchanged"] = true;
            record["passed"] = true;
            Console.WriteLine($"PASS live CAM reads: {items.Count} parameters, {details.Count} cutting-condition samples; document and operation state unchanged; zero native writes.");
        }
        catch (Exception error) { record["error"] = error.Message; throw; }
        finally { await File.WriteAllTextAsync(Path.Combine(output, "live-cam-reads.json"), record.ToString(Formatting.Indented)); }
    }
}
