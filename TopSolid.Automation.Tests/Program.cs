namespace TopSolid.Automation.Tests;

internal static class Program
{
    private static async Task<int> Main(string[] args)
    {
        if (args.Length == 4 && args[0] == "--live-native-toolpath") { await NativeToolpathLiveTests.Run(args[1], args[2], int.Parse(args[3])); return 0; }
        if (args.Length == 1 && args[0] == "--toolpath-preview-ui") { UiShellTests.ToolpathOnly = true; await UiShellTests.Run(); return 0; }
        if (args.Length == 2 && args[0] == "--cam-context") { await CamWorkflowImprovementTests.Context(args[1]); return 0; }
        if (args.Length == 2 && args[0] == "--live-cam-context") { await CamWorkflowImprovementTests.LiveContext(args[1]); return 0; }
        if (args.Length == 2 && args[0] == "--cam-context-ui") { UiShellTests.CamContextPath=args[1]; await UiShellTests.Run(); return 0; }
        if (args.Length == 1 && args[0] == "--topsolid-connection-settings") { await TopSolidConnectionTests.Settings(); return 0; }
        if (args.Length == 1 && args[0] == "--topsolid-connection-ui") { await TopSolidConnectionUiTests.Run(); return 0; }
        if (args.Length == 2 && args[0] == "--topsolid-connection-live") { await TopSolidConnectionTests.Live(args[1]); return 0; }
        if (Environment.GetEnvironmentVariable("TOPSOLID_MCP_TEST_FIXTURE") == "1") return await MutationTransportTests.RunFixture();
        if (args.Length == 2 && args[0] == "--live-tool-preview") { await LiveToolPreviewReview.Run(args[1]); return 0; }
        if (args.Length == 2 && args[0] == "--live-tool-data") { await LiveToolPreviewReview.ValidateData(args[1]); return 0; }
        if (args.Length == 2 && args[0] == "--verify-native-glb") { await ToolPreviewRegressionTests.NativeGeometry(args[1]); return 0; }
        if (args.Length == 1 && args[0] == "--workstation-preview") { await WorkstationPreviewTests.Run(); return 0; }
        if (args.Length == 2 && args[0] == "--live-licenses")
        { await LicenseTests.Live(args[1]); return 0; }
        if (args.Length is 1 or 2 && args[0] == "--ui-shell" && (args.Length == 1 || args[1] == "--no-ui-render"))
        { await UiShellTests.Run(render: args.Length == 1); return 0; }
        if (args.Length == 2 && args[0] == "--live-cam-reads")
        { await CamReadOnlyTests.Run(args[1]); return 0; }
        if (args.Length == 2 && args[0] == "--live-cam-selection")
        { await CamSelectionTests.Live(args[1]); return 0; }
        if (args.Length == 2 && args[0] == "--live-log-review-reads")
        { await CamReadOnlyTests.Run(args[1], "artifacts/conversation-log-review-20260918", "CuttingConditions"); return 0; }
        if (args.Length is 2 or 3 && args[0] == "--live-graphic-preview")
        { await GraphicPreviewTests.Live(args[1], args.Length == 3 ? args[2] : null); return 0; }
        if (args.Length is 1 or 3 && args[0] == "--gpu-preview")
        { await PreviewRuntimeTests.Gpu(args.Length == 3 ? args[1] : null, args.Length == 3 ? args[2] : null); return 0; }
        if (args.Length == 3 && args[0] == "--live-paged-preview")
        { await PreviewRuntimeTests.LivePaged(args[1], args[2]); return 0; }
        string? serverPath = null;
        string? liveUiOllamaModel = null;
        string? approvedNativePlan = null;
        string? liveModelList = null;
        if (args.Length == 3 && args[0] == "--local-heart-benchmark") {
            await LocalHeartBenchmark.Run(args[1], args[2]); return 0;
        }
        if (args.Length == 3 && args[0] == "--modeling-model-benchmark") {
            await ModelingModelBenchmark.Run(args[1], args[2]); return 0;
        }
        var uiSmoke = false;
        var uiBehavior = false;
        var livePdmNames = false;
        var livePdmOllama = false;
        var livePdmFast = false;
        string? livePdmModel = null;
        string? liveAccessModel = null;
        var liveSketchPreview = false;
        var liveModelingPreview = false;
        string? liveSketchModel = null;
        string? approvedSketchPlan = null;
        var liveCreationPreview = false;
        var liveCreationModel = false;
        var livePersistencePreview = false;
        var livePersistenceSketchModel = false;
        for (var index = 0; index < args.Length; index++)
        {
            if (args[index] == "--server" && index + 1 < args.Length) serverPath = args[++index];
            else if (args[index] == "--ui-smoke") uiSmoke = true;
            else if (args[index] == "--ui-behavior") uiBehavior = true;
            else if (args[index] == "--live-pdm-names") livePdmNames = true;
            else if (args[index] == "--live-pdm-fast") livePdmFast = true;
            else if (args[index] == "--live-sketch-preview") liveSketchPreview = true;
            else if (args[index] == "--live-modeling-preview") liveModelingPreview = true;
            else if (args[index] == "--live-creation-preview") liveCreationPreview = true;
            else if (args[index] == "--live-creation-model") liveCreationModel = true;
            else if (args[index] == "--live-persistence-preview") livePersistencePreview = true;
            else if (args[index] == "--live-persistence-sketch-model") livePersistenceSketchModel = true;
            else if (args[index] == "--live-sketch-model" && index + 1 < args.Length) liveSketchModel = args[++index];
            else if (args[index] == "--approved-sketch-plan" && index + 1 < args.Length) approvedSketchPlan = args[++index];
            else if (args[index] == "--live-access-model" && index + 1 < args.Length) liveAccessModel = args[++index];
            else if (args[index] == "--live-pdm-ollama") { livePdmNames = true; livePdmOllama = true; }
            else if (args[index] == "--live-pdm-model" && index + 1 < args.Length) livePdmModel = args[++index];
            else if (args[index] == "--live-model-list" && index + 1 < args.Length) liveModelList = args[++index];
            else if (args[index] == "--approved-native-plan" && index + 1 < args.Length) approvedNativePlan = args[++index];
            else if (args[index] == "--live-ui-ollama" && index + 1 < args.Length)
            {
                liveUiOllamaModel = args[++index];
                uiSmoke = true;
            }
            else
            {
                Console.Error.WriteLine("Usage: TopSolid.Automation.Tests.exe [--server <MCP-server.exe>] [--ui-smoke] [--live-ui-ollama <model>] [--live-model-list <saved-service-id>] [--approved-native-plan <explicitly-approved-plan-sha256>]");
                return 2;
            }
        }
        if (approvedNativePlan != null && liveUiOllamaModel == null) throw new ArgumentException("An approved native test requires an explicitly selected live Ollama model.");
        var cases = new List<(string Name, Func<Task> Run)>
        {
            ("Cloud HTTP request -> MCP tool -> tool result -> final answer", ProviderTests.OpenAiToolLoop),
            ("Ollama HTTP request -> MCP tool -> tool result -> final answer", ProviderTests.OllamaToolLoop),
            ("Cloud and Ollama model discovery", ProviderTests.ModelDiscovery),
            ("Local and cloud model selector icon mapping preserves model IDs", ModelIconTests.Run),
            ("Cloud presets, Gemini endpoint, authentication and thought-signature replay", CloudServiceTests.PresetRoutesAndGemini),
            ("Anthropic Messages tool loop, parallel results, pagination and truncation", CloudServiceTests.AnthropicToolLoopAndPagination),
            ("Per-service encrypted credentials, model persistence and legacy migration", CloudServiceTests.ProfilePersistence),
            ("Malformed provider tool arguments do not reach MCP", ProviderTests.MalformedArgumentsNeverExecute),
            ("Provider failures, incomplete replies, duplicate IDs, cancellation", ProviderTests.ProviderErrorsAndCancellation),
            ("Disconnected text chat, conversation history, clear", ChatTests.TextConversationAndClear),
            ("Unknown and malformed calls become model-visible errors", ChatTests.InvalidCallsAreReturnedToModel),
            ("Multiple tool calls, error results, visible traces", ChatTests.MultipleToolsAndToolFailure),
            ("Cancellation during a tool call and next-turn recovery", ChatTests.CancelDuringToolThenRecover),
            ("Repeated model/tool calls stop at round limit", ChatTests.RepeatedToolsAreBounded),
            ("Oversized tool batches fail before any tool executes", ChatTests.OversizedToolBatchIsRejectedBeforeDispatch),
            ("Excessive tool output becomes a bounded tool error", ChatTests.LargeToolOutputIsBounded),
            ("Duplicate tool IDs fail before any tool executes", ChatTests.DuplicateCallIdsAreRejectedBeforeDispatch),
            ("Persistent diagnostics and structured export redact credentials", DiagnosticLogTests.PersistentLogAndExportRedaction),
            ("DPAPI settings roundtrip, no plaintext key, endpoint binding", SettingsTests.SecureRoundTrip),
            ("TopSolid targets, encrypted gateway tokens and invalid-target recovery", TopSolidConnectionTests.Settings),
            ("Endpoint validation and cloud credential transport safety", SettingsTests.EndpointSafety)
            ,("Configurable model timeout, cancellation and GPT-OSS latency option", TimeoutTests.Run)
            ,("Modeling approval, denial, tampered preview, headless rejection", ConfirmationTests.ApprovalAndDenial)
            ,("Declined modeling request is not repeatedly prompted", ConfirmationTests.DeclinedChangesAreNotRepeated)
            ,("Committed change receipt survives failed model follow-up", ConfirmationTests.ChangeReceiptSurvivesFailedFollowUp)
            ,("Mutation completes before cancellation/disconnect; no forced kill on broken transport", MutationTransportTests.Run)
        };
        cases.Add(("Sketch context migration, bounded repair and local latency metrics", SketchReliabilityTests.Run));
        cases.Add(("CAM selection dialogs, display names, pagination and failed-question recovery", CamSelectionTests.Run));
        cases.Add(("Local CAM parameter editing, SI units, revision continuity and ordered tool groups", CamWorkflowImprovementTests.Run));
        cases.Add(("Preview disk transfer, cancellation, identity validation and toolpath coordinates", PreviewRuntimeTests.Run));
        cases.Add(("Official CAM operation labels, module identity and source provenance", CamOperationNamesTests.Run));
        cases.Add(("Conversation-log regression: complete CAM pages, parameter selection, exact active target and compact history", ConversationLogRegressionTests.Run));
        cases.Add(("Measured developer dashboard timings and formatted details", DeveloperDashboardTests.MeasuredTurnDurationsAndReadableDetails));
        cases.Add(("Permission modes and bounded text/image attachment delivery", UiWorkflowTests.Run));
        cases.Add(("TopSolid saved and custom theme parsing", ThemeTests.Run));
        cases.Add(("Kernel Base startup gate, rejected queries and cancellation", LicenseTests.Gate));
        cases.Add(("License protocol fields and strict fail-closed parsing", LicenseTests.Transport));
        cases.Add(("Independent Studio and AI response languages", LocalizationTests.Run));
        cases.Add(("Friendly user responses and unchanged developer receipts, target identities and model history", ResponsePresentationTests.Run));
        cases.Add(("Connection readiness, slow/pending states, quota preservation and translated failures", ConnectionHealthTests.Run));
        cases.Add(("Receipt-backed question choices, typed inputs, images, cancellation and separate approval", UserQuestionTests.Run));
        cases.Add(("Automatic list dialogs, exact selection scope, empty results and read-only pagination", ListPresentationTests.Run));
        cases.Add(("Explicit project/document/operation/tool/sketch selection dialogs use receipts without inference", SelectionRequestTests.Run));
        cases.Add(("Operation toolpath requests resolve an operation and open the read-only preview", ToolpathPreviewTests.Run));
        cases.Add(("Native tool names/icons/library previews and paged GLB color/transparency", ToolPreviewRegressionTests.Run));
        cases.Add(("NC generation selects operations, configured post-processor, generates, and exports", NcGenerationTests.Run));
        cases.Add(("Native GLB/STL, 0.05 mm / 5 degree precision, neutral surfaces, thin edges, navigation and preview cancellation", GraphicPreviewTests.Run));
        if (serverPath != null)
            cases.Add(("Real MCP process initialize, discover, status, errors, disconnect",
                () => ProcessTests.ServerHandshakeAndStatus(serverPath)));
        if (liveModelList != null)
            cases.Add(("Live configured cloud model listing (no inference)", () => CloudServiceTests.LiveModelList(liveModelList)));
        if (livePdmNames)
            cases.Add(("Live model all PDM friendly names", () => PdmNamesTests.Live(serverPath ?? throw new ArgumentException("--server is required"), livePdmModel, livePdmOllama)));
        if (uiSmoke || uiBehavior)
            cases.Add((uiSmoke ? "WPF window initialization, MCP buttons, rendered layout, settings unchanged" : "WPF chat behavior, themed replies, elapsed time and unchanged settings (no raster validation)",
                () => UiSmokeTests.Run(serverPath, liveUiOllamaModel, approvedNativePlan, render: uiSmoke)));
        var failures = 0;
        cases.Add(("Complete PDM inventory rendering and transient HTTP retry", InventoryTests.Run));
        cases.Add(("Direct PDM creation preserves confirmation, exact identities and partial receipts", PdmFastPathTests.Run));
        cases.Add(("Batch persistence scope, check-in receipts, confirmation and zero model rounds", PersistenceTests.Run));
        if (livePersistencePreview) cases.Add(("Live batch save and project check-in previews without writes", () => PersistenceTests.LivePreview(serverPath ?? throw new ArgumentException("--server required"))));
        if (livePersistenceSketchModel) cases.Add(("Live model star and ellipse batch through a declined preview", () => PersistenceTests.LiveSketchModel(serverPath ?? throw new ArgumentException("--server required"))));
        if (livePdmFast) cases.Add(("Live direct PDM read and declined creation latency", () => PdmFastPathTests.Live(serverPath ?? throw new ArgumentException("--server required"))));
        if (liveAccessModel != null) cases.Add(("Live model document creation tool access without execution", () => LiveToolAccessTests.Run(serverPath ?? throw new ArgumentException("--server required"), liveAccessModel)));
        if (liveSketchPreview) cases.Add(("Live sketch reference reads and previews without changes", () => LiveSketchTests.Preview(serverPath ?? throw new ArgumentException("--server required"))));
        if (liveModelingPreview) cases.Add(("Live modeling and color previews without changes", () => ModelingPreviewTests.Run(serverPath ?? throw new ArgumentException("--server required"))));
        if (liveSketchModel != null) cases.Add(("Live model asks for unspecified sketch dimensions", () => LiveSketchTests.ModelClarification(serverPath ?? throw new ArgumentException("--server required"), liveSketchModel)));
        if (approvedSketchPlan != null) cases.Add(("Explicitly approved native sketch fixture", () => NativeSketchValidation.Run(serverPath ?? throw new ArgumentException("--server required"), approvedSketchPlan)));
        if (liveCreationPreview) cases.Add(("Live extension-only document previews without writes", () => LiveDocumentCreationTests.Preview(serverPath ?? throw new ArgumentException("--server required"))));
        if (liveCreationModel) cases.Add(("Live model empty document creation with declined preview", () => LiveDocumentCreationTests.Model(serverPath ?? throw new ArgumentException("--server required"))));
        cases.Add(("Bounded tool catalog, schema selection and MCP dispatch", ToolExposureTests.CatalogSelectionAndDispatch));
        foreach (var test in cases)
        {
            try
            {
                await test.Run().WaitAsync(test.Name.StartsWith("Explicitly approved native", StringComparison.Ordinal) ? Timeout.InfiniteTimeSpan : liveUiOllamaModel != null && test.Name.StartsWith("WPF", StringComparison.Ordinal)
                    ? TimeSpan.FromMinutes(7) : test.Name.StartsWith("Live model", StringComparison.Ordinal) ? TimeSpan.FromMinutes(5) : TimeSpan.FromSeconds(55));
                Console.WriteLine("PASS " + test.Name);
            }
            catch (Exception exception)
            {
                failures++;
                Console.Error.WriteLine("FAIL " + test.Name + Environment.NewLine + exception);
            }
        }
        Console.WriteLine($"{cases.Count - failures}/{cases.Count} checks passed.");
        return failures == 0 ? 0 : 1;
    }
}
