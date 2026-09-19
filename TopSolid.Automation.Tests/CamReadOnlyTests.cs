using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using TopSolid.Automation.AI.Studio.Chat;
using TopSolid.Automation.AI.Studio.Mcp;
using TopSolid.Automation.AI.Studio.Appearance;

namespace TopSolid.Automation.Tests;

/// <summary>Opt-in reads of the active CAM document. No prepare or confirmed-call path.</summary>
internal static class CamReadOnlyTests
{
    internal static async Task Run(string executable, string outputDirectory = "artifacts/cam-review-0.5.13", string? category = null)
    {
        await using var client = new StdioMcpClient();
        using var timeout = new CancellationTokenSource(TimeSpan.FromMinutes(4));
        await client.ConnectAsync(executable, timeout.Token);
        var output = Path.GetFullPath(outputDirectory);
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
            var activeBefore = await Read("topsolid_get_active_document", new JObject());
            var target = new JObject();
            if (category != null && !((string?)activeBefore["document"]?["typeFullName"] ?? "").StartsWith("TopSolid.Cam.", StringComparison.Ordinal))
            {
                var documentOffset = 0;
                for (var pageIndex = 0; pageIndex < 24; pageIndex++)
                {
                    var open = await Read("topsolid_list_document_summaries", new JObject { ["scope"] = "open", ["offset"] = documentOffset, ["limit"] = 100 });
                    var cam = ((JArray?)open["items"])?.OfType<JObject>().FirstOrDefault(row => ((string?)row["type"] ?? "").StartsWith("TopSolid.Cam.", StringComparison.Ordinal));
                    if (cam != null) { target["documentId"] = cam["documentId"]!.DeepClone(); break; }
                    if ((bool?)open["hasMore"] != true) break;
                    var next = (int?)open["nextOffset"];
                    Check.True(next.HasValue && next > documentOffset, "Open-document continuation did not advance"); documentOffset = next!.Value;
                }
                Check.True(target["documentId"] != null, "No open CAM document is available for this read-only fixture.");
                record["targetSelection"] = "First verified open CAM document; the active document was not changed.";
            }
            var before = await Read("topsolid_get_document_info", target);
            record["documentBefore"] = before;
            var document = before["document"] as JObject ?? throw new InvalidOperationException("Open a CAM document to run this optional read-only fixture.");
            var documentId = (string)document["documentId"]!;
            var operationArgs = new JObject { ["documentId"] = documentId, ["limit"] = 100 };
            var operations = await Read("topsolid_list_cam_operation_summaries", operationArgs);
            record["operations"] = operations;
            var rows = operations["items"] as JArray ?? throw new InvalidOperationException("CAM operations were not returned.");
            Check.True(rows.Count > 0, "The active document has no CAM operations; no fixture data will be created.");
            var sources = new QuestionSources();
            sources.Capture("operations", "topsolid_list_cam_operation_summaries", operationArgs, new TopSolid.Automation.Mcp.Contracts.McpToolResult { StructuredContent = operations });
            var question = sources.Create(new JObject { ["question"] = "Inspect machining operations", ["kind"] = "select", ["itemKind"] = "operation",
                ["sources"] = new JArray(new JObject { ["toolCallId"] = "operations", ["path"] = "/items" }) });
            record["operationChoices"] = new JArray(question.Choices.Select(c => new JObject { ["label"] = c.Label, ["icon"] = c.IconKey }));
            for (var index = 0; index < rows.Count; index++)
            {
                Check.Equal(((string)rows[index]["operationName"]!).Trim(), question.Choices[index].Label, "Live CAM selection lost the native name or operation number");
                Check.True(!string.IsNullOrWhiteSpace((string?)rows[index]["operationType"]), "Live NC operation type was not read");
                Check.True(TopSolidIcons.OperationKey(rows[index]) != "operation", "Native fixture operation has no exact original icon mapping");
            }
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
                var parameterArguments = new JObject { ["element"] = element.DeepClone(), ["offset"] = offset, ["limit"] = 100 };
                if (category != null) parameterArguments["category"] = category;
                var page = await Read("topsolid_list_cam_parameters", parameterArguments);
                if (category != null) Check.True(((JArray)page["items"]!).All(row => ((JArray?)row["categories"])?.Any(value => (string?)value == category) == true), "Filtered CAM read included an unrelated category");
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
            Check.True(JToken.DeepEquals(activeBefore, await Read("topsolid_get_active_document", new JObject())), "Read-only CAM inspection changed the active document");
            record["documentAndOperationsUnchanged"] = true;
            record["passed"] = true;
            Console.WriteLine($"PASS live CAM reads: {items.Count} parameters, {details.Count} cutting-condition samples; document and operation state unchanged; zero native writes.");
        }
        catch (Exception error) { record["error"] = error.Message; throw; }
        finally { await File.WriteAllTextAsync(Path.Combine(output, "live-cam-reads.json"), record.ToString(Formatting.Indented)); }
    }
}
