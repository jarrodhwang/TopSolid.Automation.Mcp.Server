using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using TopSolid.Automation.AI.Studio.AI;
using TopSolid.Automation.AI.Studio.Chat;
using TopSolid.Automation.AI.Studio.Localization;
using TopSolid.Automation.Mcp.Contracts;

namespace TopSolid.Automation.Tests;

internal static class ResponsePresentationTests
{
    internal const string PartId = "849af2ed-3417-42ca-a6f6-b643513ad790";
    internal const string RevisionId = "4bc2b7d8-c195-4285-9a73-f034315b7069";
    internal const string UnknownId = "5763c938-a9c1-4c97-98ef-56e1a47a3af6";
    internal const string PartName = "Mounting plate / 장착판";

    public static async Task Run()
    {
        var language = StudioStrings.CurrentLanguage;
        StudioStrings.Apply("en");
        try
        {
            var presenter = new FriendlyResponsePresenter();
            var data = new JObject { ["name"] = PartName, ["pdmObjectId"] = PartId, ["documentId"] = RevisionId,
                ["created"] = true, ["complete"] = false, ["width"] = 32.5, ["date"] = "2026-09-17",
                ["partialChange"] = new JObject { ["saved"] = false, ["message"] = "Save failed; do not retry automatically.",
                    ["failedDocumentId"] = UnknownId, ["name"] = "Plate backup" },
                ["warnings"] = new JArray("Not saved to PDM.", "Keep the existing geometry."), ["color"] = "#0080D7" };
            var receipt = new JObject { ["isError"] = true, ["structuredContent"] = data,
                ["content"] = new JArray(new JObject { ["type"] = "text", ["text"] = data.ToString(Formatting.None) }) };
            var original = receipt.ToString(Formatting.Indented);
            presenter.ObserveToolResult(original);
            var friendly = presenter.Present(original);
            AssertFriendly(friendly);
            foreach (var important in new[] { PartName, "Complete: No", "32.5", "2026-09-17", "Save failed", "Not saved to PDM.", "Keep the existing geometry.", "#0080D7", "Error: Yes" })
                Check.True(friendly.Contains(important), "Presentation lost outcome or design facts: " + important);
            Check.Equal(1, friendly.Split(PartName).Length - 1, "Duplicate text/structured receipt was displayed twice");
            Check.Equal(original, presenter.Present(original, developerMode: true), "Dev Mode changed its raw response");
            Check.Equal(original, receipt.ToString(Formatting.Indented), "Presentation mutated the receipt");

            Check.Equal($"Created \"{PartName}\".", presenter.Present($"Created \"{PartName}\".\nPDM ID: {PartId}\nDocument ID: {RevisionId}"), "Creation receipt should only show its name");
            Check.Equal(PartName, presenter.Present("Document ID: " + RevisionId), "Reference-only response became empty");
            Check.Equal("Saved (" + PartName + ").", presenter.Present("Saved (documentId: " + RevisionId + ")."), "Inline reference punctuation obscured the verified name");
            foreach (var format in new[] { "D", "N", "B", "P", "X" })
            {
                var id = Guid.Parse(PartId).ToString(format);
                var output = presenter.Present("Opened " + id + ".");
                AssertFriendly(output);
                Check.True(output.Contains(PartName), "Verified name was not substituted for GUID format " + format);
            }
            var prose = presenter.Present($"Read urn:uuid:{RevisionId}.\nDocument ID: {UnknownId} — failed to save.\n" +
                $"[Plate](topsolid://document/{PartId})\n| Name | pdmObjectId |\n| Plate | `{PartId}` |\n" +
                "documentId: opaque-revision-handle\n" + "Partial receipt: " + data.ToString(Formatting.None));
            AssertFriendly(prose);
            Check.True(prose.Contains("failed to save") && prose.Contains("Save failed"), "Identity removal hid a failure");
            Check.True(!prose.Contains("opaque-revision-handle") && !prose.Contains("topsolid://"), "Technical references survived presentation");
            Check.True(presenter.Present(UnknownId).Contains("name unavailable"), "Unknown IDs must not receive an invented name");

            // Same-named objects remain separate internally; a local entity must not rename its parent document.
            presenter.Observe(new JObject { ["name"] = "Sketch A", ["element"] = new JObject { ["documentId"] = UnknownId, ["id"] = 42 } });
            var shapeReference = presenter.Present(new JObject { ["replaceShapes"] = new JArray(new JObject { ["documentId"] = UnknownId, ["id"] = 42 }) }.ToString());
            Check.True(shapeReference.Contains("Sketch A"), "Compound entity handle did not use its own verified name");
            Check.True(!presenter.Present(UnknownId).Contains("Sketch A"), "Entity name was incorrectly assigned to its parent document");
            presenter.Observe(new JObject { ["name"] = "Child part", ["projectId"] = UnknownId });
            Check.True(!presenter.Present(UnknownId).Contains("Child part"), "Part name was incorrectly assigned to its parent project");
            presenter.Observe(new JObject { ["name"] = "Fixture project", ["pdmObjectId"] = UnknownId });
            var references = presenter.Present(new JObject { ["sourceDocumentId"] = RevisionId, ["ownerId"] = UnknownId,
                ["documentIds"] = new JArray(PartId, RevisionId) }.ToString());
            Check.True(references.Contains("Source Document: " + PartName) && references.Contains("Owner: Fixture project"), "Multiple referenced objects were collapsed or mislabeled");
            Check.True(references.Contains("Documents:") || references.Contains("Document:"), "Referenced object collection was dropped");
            var sameNames = presenter.Present(new JObject { [PartId] = new JObject { ["width"] = 19 }, [RevisionId] = new JObject { ["width"] = 29 } }.ToString());
            Check.True(sameNames.Contains("19") && sameNames.Contains("29"), "Duplicate friendly names caused a result to be overwritten");
            var proposal = new JObject { ["toolName"] = "topsolid_create_rectangle2d", ["confirmationToken"] = "secret-token",
                ["target"] = new JObject { ["name"] = PartName, ["documentId"] = RevisionId },
                ["arguments"] = new JObject { ["documentId"] = RevisionId, ["width"] = 25, ["height"] = 10 },
                ["effect"] = "Add native geometry; no save." };
            var proposalBefore = proposal.ToString();
            var review = presenter.Review(proposal).ToString();
            AssertFriendly(review);
            Check.True(!review.Contains("secret-token") && review.Contains("25") && review.Contains("no save"), "Review omitted design facts or exposed authorization metadata");
            Check.Equal(proposalBefore, proposal.ToString(), "Friendly review modified the approved target or arguments");
            presenter.Observe(JObject.Parse("{\"name\":{},\"documentId\":{},\"isError\":{}}"));
            AssertFriendly(presenter.Present("{\"name\":{},\"documentId\":{},\"isError\":{},\"content\":[]}"));
            AssertFriendly(presenter.Present("```json\n" + proposalBefore + "\n```"));
            var malformed = new string('{', 20000) + UnknownId;
            AssertFriendly(presenter.Present(malformed));
            presenter.Clear();
            Check.True(!presenter.Present(PartId).Contains(PartName), "Clearing the conversation retained stale aliases");
            presenter.Observe(new JObject { ["name"] = "ID Plate", ["pdmObjectId"] = PartId });
            Check.Equal("ID Plate", presenter.Present(PartId), "Friendly name containing ID was altered");
            Check.Equal("Selected ID Plate.", presenter.Present("Selected ID Plate."), "Friendly name was mistaken for an unlabeled reference");
            StudioStrings.Apply("ko");
            Check.True(presenter.Present(UnknownId).Contains("이름 확인 불가"), "Missing-name fallback ignored interface language");
            StudioStrings.Apply("en");
            NumericAndCamIdentityPresentation();
            CamApprovalReferenceNamesStaySeparate();
            await ModeInstructionsAndHistory();
        }
        finally { StudioStrings.Apply(language); }
    }

    private static void NumericAndCamIdentityPresentation()
    {
        const string revision = "19_zdjluv3akwnezg3eh7ehlclnce&15_0_8";
        const string otherRevision = "19_otherdocument&15_0_8";
        static JObject Handle(string document, int id) => new() { ["documentId"] = document, ["id"] = id };
        var presenter = new FriendlyResponsePresenter();
        presenter.Observe(new JObject { ["documentId"] = revision, ["name"] = "Blade machining" });
        presenter.Observe(new JObject { ["element"] = Handle(revision, 11791), ["name"] = "$ToolFunction4", ["friendlyName"] = "Ball end mill Ø8" });
        presenter.Observe(new JObject { ["element"] = Handle(revision, 7899), ["friendlyName"] = "Blade blank" });
        presenter.Observe(new JObject { ["operation"] = new JObject { ["element"] = Handle(revision, 12790) }, ["operationName"] = "Finish blade",
            ["tool"] = Handle(revision, 11791), ["part"] = Handle(revision, 7899) });
        presenter.Observe(new JObject { ["element"] = Handle(revision, 208), ["friendlyName"] = "Five-axis mill" });

        var examples = new[]
        {
            "사용된 공구 ID: `11791` (공구 기능 4)\n대상 파트 ID: `7899`",
            "- **공구 요소 ID:** `11791`, `7899`, `7725`",
            "Machine (`208`) uses tool 11791 and part 7899.",
            "머신(`208`)을 기준으로 조회했습니다.",
            "ElementId: 11791\nPart ID: 7899 — failed to update.",
            "| Tool ID | Part ID | Feed |\n| --- | --- | --- |\n| 11791 | 7899 | 11791 mm/min |",
            "Opened `" + revision + "`. [Blade](topsolid://document/" + Uri.EscapeDataString(revision) + "/11791)",
            new JObject { ["operation"] = new JObject { ["element"] = Handle(revision, 12790) }, ["tool"] = Handle(revision, 11791), ["part"] = Handle(revision, 7899) }.ToString()
        };
        foreach (var original in examples)
        {
            var output = presenter.Present(original);
            Check.True(!output.Contains(revision) && !output.Contains("7899") && !output.Contains("12790") && !output.Contains("7725") && !output.Contains("208"), "Numeric/opaque identity leaked: " + output);
            Check.True(!output.Replace("11791 mm/min", "").Contains("11791"), "Tool number leaked or colliding feed was replaced: " + output);
            Check.Equal(original, presenter.Present(original, developerMode: true), "Dev Mode must keep exact numeric IDs");
        }
        Check.True(presenter.Present(examples[0]).Contains("Ball end mill Ø8") && presenter.Present(examples[0]).Contains("Blade blank"), "Korean ID labels did not resolve native friendly names");
        Check.True(presenter.Present(examples[4]).Contains("failed to update"), "Suppression lost an operational failure");
        Check.True(presenter.Present(examples[5]).Contains("11791 mm/min"), "ID column formatting hid a same-valued feed");
        Check.True(presenter.Present(examples[7]).Contains("Finish blade"), "ElementExId wrapper lost its name");
        var failedCell = presenter.Present("| Part ID | State |\n| --- | --- |\n| 7899 — failed to update | unchanged |");
        Check.True(failedCell.Contains("Blade blank") && failedCell.Contains("failed to update"), "Formatting an identity table cell hid its failure detail");
        presenter.Observe(new JObject { ["elementEx"] = new JObject { ["preparationId"] = UnknownId }, ["friendlyName"] = "Prepared finishing pass" });
        Check.True(presenter.Present(new JObject { ["preparationId"] = UnknownId }.ToString()).Contains("Prepared finishing pass"), "Uncreated ElementExId could not use a verified native name");
        Check.True(presenter.Present("{\"part\":7899,\"tool\":11791}").Contains("Blade blank"), "Scalar object reference was exposed instead of its native name");

        var measurements = "Width: 11791 mm; count: `7899`; feed: `11791` mm/min; angle: 208 degrees; depth: 0.12.\n| Value | Count |\n| --- | --- |\n| `11791` | `7899` |";
        Check.Equal(measurements, presenter.Present(measurements), "Unrelated real numbers were mistaken for IDs");
        Check.Equal("Blade blank", presenter.Present("7899"), "Standalone known identity did not use its verified name");
        presenter.Observe(new JObject { ["element"] = Handle(revision, 7712), ["friendlyName"] = "Tool 4" });
        Check.True(presenter.Present("Tool ID: 7712").Contains("Tool 4"), "Legitimate numbered friendly name was mistaken for an ID");
        var scalarFacts = new JObject { ["width"] = 11791, ["feed"] = 7899, ["solid"] = true, ["grid"] = 208, ["currentValue"] = 0.08, ["requestedValue"] = 0.12 };
        var facts = presenter.Present(scalarFacts.ToString());
        Check.True(facts.Contains("11791") && facts.Contains("7899") && facts.Contains("Solid: Yes") && facts.Contains("Grid: 208") && facts.Contains("0.08") && facts.Contains("0.12"), "Structured design facts were mistaken for identity suffixes");

        // Observing an unnamed collision is enough to prohibit an unscoped alias.
        presenter.Observe(new JObject { ["element"] = Handle(otherRevision, 11791) });
        Check.True(!presenter.Present("Tool ID: 11791").Contains("Ball end mill"), "Unscoped local ID received another document's name");
        Check.True(presenter.Present(Handle(revision, 11791).ToString()).Contains("Ball end mill Ø8"), "Scoped handle could not resolve after a collision");
        Check.True(!presenter.Present(Handle(otherRevision, 11791).ToString()).Contains("Ball end mill"), "Unnamed handle inherited another document's name");
        presenter.Observe(new JObject { ["element"] = Handle(otherRevision, 11791), ["friendlyName"] = "Roughing cutter" });
        Check.True(presenter.Present(Handle(otherRevision, 11791).ToString()).Contains("Roughing cutter"), "Second document lost its own alias");

        // Parameter Name is part of the identity: dozens of parameters can share an owner.
        var owner = Handle(revision, 12788);
        presenter.Observe(new JObject { ["element"] = owner.DeepClone(), ["friendlyName"] = "Rough pocket" });
        var feed = new JObject { ["element"] = owner.DeepClone(), ["name"] = "FeedPerTooth@CuttingConditions|Strategy" };
        var speed = new JObject { ["element"] = owner.DeepClone(), ["name"] = "SpindleSpeed@CuttingConditions|Strategy" };
        presenter.Observe(new JObject { ["parameter"] = feed.DeepClone(), ["name"] = feed["name"]!.DeepClone(), ["displayName"] = "Feed per tooth", ["value"] = 0.12 });
        presenter.Observe(new JObject { ["parameter"] = speed.DeepClone(), ["name"] = speed["name"]!.DeepClone(), ["displayName"] = "Spindle speed", ["value"] = 10000 });
        Check.True(presenter.Present(owner.ToString()).Contains("Rough pocket"), "CAM parameter names overwrote their owning operation");
        Check.True(presenter.Present(new JObject { ["parameter"] = feed.DeepClone() }.ToString()).Contains("Feed per tooth"), "Native parameter display name was replaced by its technical Name");
        Check.True(!presenter.Present(new JObject { ["parameter"] = speed.DeepClone() }.ToString()).Contains("Feed per tooth"), "Parameters sharing an element received the same alias");
        Check.True(!presenter.Present(revision).Contains("Rough pocket"), "Element/parameter alias renamed its document");

        var approval = new JObject { ["target"] = new JObject { ["name"] = "Finish blade", ["element"] = Handle(revision, 12790) },
            ["arguments"] = new JObject { ["element"] = Handle(revision, 12790), ["parameterId"] = feed.DeepClone(), ["currentValue"] = 0.08, ["value"] = 0.12, ["unit"] = "mm/tooth" } };
        var before = approval.ToString();
        var review = presenter.Review(approval).ToString();
        Check.True(!review.Contains(revision) && !review.Contains("12790") && !review.Contains("12788") && review.Contains("Feed per tooth") && review.Contains("0.08") && review.Contains("0.12"), "Approval exposed handles or lost machining facts: " + review);
        Check.Equal(before, approval.ToString(), "Presentation changed execution or approval data");
        presenter.Observe(new JObject { ["target"] = new JObject { ["element"] = Handle(revision, 12790), ["friendlyName"] = "Finish blade" },
            ["arguments"] = new JObject { ["element"] = Handle(revision, 12790), ["name"] = "Unapproved rename" } });
        Check.True(presenter.Present(Handle(revision, 12790).ToString()).Contains("Finish blade"), "Proposed arguments renamed a target before approval or execution");
        presenter.Observe(new JObject { ["documentId"] = revision, ["parameterId"] = 9981, ["parameterName"] = "Coolant mode" });
        Check.True(presenter.Present(revision).Contains("Blade machining"), "Numeric parameter reference renamed its document");
        Check.True(presenter.Present(new JObject { ["toolName"] = "Ball end mill Ø8" }.ToString()).Contains("Ball end mill Ø8"), "Native CAM tool name was mistaken for an MCP tool identifier");

        var bounded = new FriendlyResponsePresenter();
        bounded.Observe(new JObject { ["element"] = Handle(revision, 42), ["friendlyName"] = "First sketch" });
        bounded.Observe(new JObject { ["element"] = Handle(otherRevision, 42) });
        for (var i = 0; i < 4100; i++) bounded.Observe(new JObject { ["parameter"] = new JObject { ["element"] = owner.DeepClone(), ["name"] = "Parameter" + i }, ["displayName"] = "Parameter " + i });
        bounded.Observe(new JObject { ["element"] = Handle(revision, 42), ["friendlyName"] = "First sketch" });
        Check.True(!bounded.Present("Element ID: 42").Contains("First sketch"), "Evicting aliases incorrectly cleared cross-document ambiguity");

        var context = new JObject { ["element"] = Handle(revision, 9991) };
        presenter.ObserveToolResult("{\"value\":\"not a name\"}", context, "topsolid_get_cam_parameter_value");
        Check.True(!presenter.Present(context.ToString()).Contains("not a name"), "Scalar parameter text was treated as a target name");
        presenter.ObserveToolResult("{\"isError\":true,\"content\":[{\"type\":\"text\",\"text\":\"{\\\"value\\\":\\\"failed read\\\"}\"}]}", context, "topsolid_get_element_name");
        Check.True(!presenter.Present(context.ToString()).Contains("failed read"), "Failed name lookup created an alias");
        presenter.ObserveToolResult("{\"value\":\"Fixture clamp\"}", context, "topsolid_get_element_friendly_name");
        Check.True(presenter.Present(context.ToString()).Contains("Fixture clamp"), "Scalar name receipt could not use exact request handle context");
        presenter.Clear();
        Check.True(!presenter.Present("Tool ID: 11791").Contains("Ball end mill"), "Conversation clear retained numeric aliases");
    }

    private static void CamApprovalReferenceNamesStaySeparate()
    {
        const string document = "19_pocketmachining&15_0_8";
        var operation = new JObject { ["documentId"] = document, ["id"] = 11791 };
        var parameterHandle = new JObject { ["element"] = operation.DeepClone(), ["name"] = "CuttingSpeed@CuttingConditions" };
        var parameter = new JObject { ["parameter"] = parameterHandle.DeepClone(), ["name"] = "CuttingSpeed@CuttingConditions",
            ["localizedName"] = "Cutting speed", ["displayName"] = "Cutting speed", ["valueType"] = "Real", ["unitType"] = "Velocity", ["displayValue"] = "120 m/min" };
        var target = new JObject { ["documentId"] = document, ["name"] = "Pocket machining", ["type"] = "TopSolid.Cam.NC.MillTurn.Document",
            ["operation"] = operation.DeepClone(), ["operationName"] = "Finish pocket", ["parameterName"] = "Cutting speed", ["parameter"] = parameter };
        var proposal = new JObject { ["target"] = target, ["arguments"] = new JObject { ["documentId"] = document, ["element"] = operation.DeepClone(),
            ["name"] = "CuttingSpeed@CuttingConditions", ["realValueSI"] = 2.5 } };
        var original = proposal.ToString();
        var presenter = new FriendlyResponsePresenter();
        presenter.Observe(new JObject { ["documentId"] = document, ["name"] = "Pocket machining" });
        presenter.Observe(new JObject { ["element"] = operation.DeepClone(), ["friendlyName"] = "Finish pocket" });
        presenter.Observe(proposal);
        var review = presenter.Review(proposal);
        Check.Equal("Pocket machining", (string)review["target"]!["name"]!, "CAM target header used a related parameter name instead of its document name");
        Check.Equal("Cutting speed", (string)review["target"]!["parameter"]!["name"]!, "Nested CAM parameter lost its native display/localized name");
        Check.Equal("Pocket machining", presenter.Present(document), "CAM approval parameter renamed the cached document");
        Check.Equal("Finish pocket", presenter.Present(operation.ToString()), "CAM approval parameter renamed the cached operation");
        Check.True(presenter.Present(new JObject { ["parameter"] = parameterHandle.DeepClone() }.ToString()).Contains("Cutting speed"), "CAM parameter received its document's alias");

        // The same related-name rule applies when a preview carries only a handle.
        target["parameter"] = parameterHandle.DeepClone();
        presenter.Observe(proposal);
        Check.Equal("Pocket machining", presenter.Present(document), "Minimal parameter reference renamed the cached document");
        Check.Equal("Finish pocket", presenter.Present(operation.ToString()), "Minimal parameter reference renamed its owning operation");
        Check.True(presenter.Present(new JObject { ["parameter"] = parameterHandle.DeepClone() }.ToString()).Contains("Cutting speed"), "Minimal parameter reference replaced its localized alias with a parent name");
        target["parameter"] = parameter;
        Check.Equal(original, proposal.ToString(), "Presentation altered the native approval payload");
    }

    private static async Task ModeInstructionsAndHistory()
    {
        ChatSession? session = null;
        var raw = "Opened " + PartName + " (documentId: " + RevisionId + ").";
        using var provider = new FakeAiProvider { Reply = (round, _, _) =>
        {
            if (round == 1) { session!.DeveloperMode = true; return Task.FromResult(FakeAiProvider.ToolReply(FakeAiProvider.StatusCall())); }
            return Task.FromResult(new AiReply { Content = raw });
        } };
        var client = new FakeMcpClient { OnCall = _ => Task.FromResult(new McpToolResult
        { StructuredContent = new JObject { ["name"] = PartName, ["documentId"] = RevisionId } }) };
        session = new ChatSession(provider, client);
        var result = await session.SendAsync("Read TopSolid status.", CancellationToken.None);
        Check.Equal(raw, result, "ChatSession must retain the original response for diagnostics and future tools");
        foreach (var request in provider.Requests)
            Check.True(request[0].Content.Contains("USER-FACING RESPONSES"), "Presentation mode changed within an active turn");
        Check.True(provider.Requests[1].Any(m => m.Role == "tool" && m.Content.Contains(RevisionId)), "User mode removed identifiers from model tool context");
        Check.True(session.GetConversationSnapshot().SelectMany(t => t).Any(m => m.Content == raw), "History lost the raw answer");
        await session.SendAsync("Continue.", CancellationToken.None);
        Check.True(!provider.Requests[^1][0].Content.Contains("USER-FACING RESPONSES"), "Dev Mode retained user-only response instructions");
    }

    internal static void AssertFriendly(string text)
    {
        foreach (var id in new[] { PartId, RevisionId, UnknownId })
            foreach (var format in new[] { "D", "N", "B", "P", "X" })
                Check.True(!text.Contains(Guid.Parse(id).ToString(format), StringComparison.OrdinalIgnoreCase), "User presentation exposed an internal identity: " + text);
        Check.True(!text.Contains("pdmObjectId", StringComparison.OrdinalIgnoreCase) && !text.Contains("documentId", StringComparison.OrdinalIgnoreCase), "User presentation exposed internal property names: " + text);
    }
}
