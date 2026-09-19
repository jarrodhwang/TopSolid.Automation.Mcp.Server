# TopSolid Automation AI — 0.5.21

**0.5.21:** CAM tools use native pocket/name/type metadata and preview their referenced tool documents. Native GLB previews preserve surface colors and transparency, including large models; the viewport waits for complete initial geometry and rendered frames before displaying it. Light/dark backgrounds follow the installed TopSolid palette. Native data and regression checks passed; final live GPU/theme visual comparison remains unverified because Windows screen capture was unavailable. [Changes and validation](docs/TOOL-PREVIEW-FIX-20260918.md).

**0.5.20:** List requests automatically open searchable icon dialogs with read-only paging. Direct3D 11 rendering and parallel disk-backed STL tiles support large native previews; the open blade CAM document's 2,777,626 triangles were displayed on the NVIDIA GPU. Operation selection reuses the model and requests an exact-operation toolpath overlay. The installed TopSolid 7.20 API returns empty strings for 3D toolpath points, so this document shows an explicit coordinate-unavailable status; native toolpath display remains blocked. [Behavior, measured validation and remaining limits](docs/LIST-PREVIEW-0.5.20.md).

**0.5.19:** CAM selection cards use native operation names/types even for generic question choices. A larger right-hand tool area shows the actual pocket and definition (for example `T 1 : Ball Nose Mill D10 L25 SD10`) with one of 67 native tool-function icons. Display metadata is read once per distinct tool per page; unavailable tooling stays explicit, and operations without a tool have no tool area.

**0.5.18:** Document selection cards use native part, assembly, drafting, milling/turning and 2D document icons from receipt extensions/types, including when the assistant labels the choices as generic options. Unknown types use a neutral document icon; names and selected identities remain unchanged.

**0.5.17:** Studio verifies `TopSolid'Kernel Base (1000)` through the live TopSolid API before creating the main window or contacting an AI provider. Invalid or unverifiable licenses show a themed explanation and close Studio. Connection status now always opens from the toolbar and includes read-only license details: expiry, active state, type, user, status and versions. [Startup rules and test instructions](docs/LICENSE-STARTUP-0.5.17.md).

**0.5.16:** Main-window caption controls match the supplied TopSolid reference: a light gray strip, thin gray minimize/maximize/restore/close glyphs, compact spacing and rounded ends on the dark title band. Other dialogs retain the shared 0.5.15 appearance.

**0.5.15:** Shared TopSolid window chrome, toolbars and controls across approvals, questions, connection status, Developer and error review. Includes native OK/Cancel icons, live custom palettes, narrow selection/preview tabs and clear/reselect recovery. [Dialog behavior and test prompts](docs/DIALOG-THEME-0.5.15.md).

**0.5.14:** TopSolid mouse gestures (Ctrl + right or wheel-button drag to rotate; right drag to pan), neutral gray proposed geometry, thin black feature/silhouette edges and 0.05 mm / 5° preview tessellation. Native STL export supplies explicit precision without changing document settings; WPF keeps the Direct3D hardware path. [Controls, precision, GPU behavior and test prompts](docs/PREVIEW-CONTROLS-0.5.14.md).

**0.5.13:** Approval reviews use flat, grouped facts with compact coordinates and one details disclosure. CAM choices preserve the native operation name/number, and 132 exact NC class mappings select original TopSolid icons. Dedicated tool, cutting-condition, geometry, strategy, comment, multi-axis and properties icons replace generic milling icons. [Review fixes and test prompts](docs/REVIEW-AND-CAM-ICONS-0.5.13.md).

**0.5.12:** Native interactive 3D previews in approval and geometry-bearing question dialogs, using actual TopSolid glTF exports. Includes orbit/pan/zoom/fit, TopSolid colors/icons, proposed cylinder/rectangle geometry, and cancellation that preserves the MCP approval session. [Graphical review behavior and limits](docs/GRAPHIC-PREVIEW-0.5.12.md).

**0.5.11:** Assistant questions use searchable icon cards and validated text, integer, decimal, image and color inputs. Object selections retain live receipt identities and scope while displaying friendly names. Cancelling stops the workflow; answering never replaces change approval. Includes the connection-status JSON-text fallback fix. [Question dialog contract](docs/QUESTION-DIALOG-0.5.11.md).

**0.5.10:** User Mode resolves friendly names for numeric/opaque references; approval uses TopSolid icons and formatted change cards; visible activity covers AI and TopSolid work. CAM cutting conditions default to operation parameters, with paginated values/types/units/choices and editability checks before approval. [Contracts, log findings and API limits](docs/CAM-USER-MODE-0.5.10.md).

Minimal Windows desktop chat with cloud service presets, OpenAI-compatible/Anthropic/Ollama adapters, a separate MCP console server, and **187 tools in 22 categories: 128 inspection/reference tools and 59 confirmed actions**. All TopSolid access stays inside the server.

**0.5.9:** automatic sketch/shape names, unique suffixes for requested names, and a separate section action only for explicit requests. [Naming/section changes](docs/NAMING-AND-SECTIONS-0.5.9.md). Ordinary modeling needs no “no section” instruction.

**0.5.8:** verified cylinder workflows (extrude/revolve), analytic 2D slots, 3D curve batches, named/RGB element and face colors, native parameter references for feature dimensions, profile checks before confirmation and faster active-document context. [Log findings, examples, measured model timings and remaining API limits](docs/MODELING-0.5.8.md).

**0.5.7:** native entity/parameter concepts; typed value adapters for all 11 concrete ParameterTypes; 8 creation types; formula/reference creation and editing through verified parent operations; entity structure, property and folder tools; RGB/transparency edits; immediate compact local-model access. [Contracts, examples and validation limits](docs/ENTITIES-PARAMETERS-0.5.7.md).

**0.5.6:** section creation removed from sketch tools; direct extrusion from existing sketches; a computed smooth heart; one-call named project/document context; conversation preserved across model/provider changes; bounded invalid proposals; compact local tool exposure, optional fast Gemma 4 replies and inference timing metrics. [Log findings, contracts, measurements and native validation limits](docs/SKETCH-RELIABILITY-0.5.6.md).

**0.5.5:** native project/object check-in, one-confirmation batch saving, direct save/check-in commands without inference, server-computed stars and bounded ellipse approximations, compatible nested units, smaller model receipts, and separate tool/confirmation timings. [Log findings, scope, geometry contracts and measured performance](docs/LOG-REVIEW-0.5.5.md).

**0.5.4:** empty document creation by extension is the default (`useDefaultTemplate=false`). No loaded part or template lookup is required. The local catalog covers all 101 supplied extensions; context reads include active-command readiness, and retries retain the original workflow's tools. Specific templates remain available when explicitly requested. [Log findings, creation contracts and measured performance](docs/DOCUMENT-CREATION-0.5.4.md).

**0.5.3:** batch 2D sketch drawing, lines/arcs/B-splines/parabolas, explicit frames and offsets, native associative reference placement, friendly-name sketch context, coordinate conversion, and topology-label repair. Open paths stay open. Section opt-in from this earlier release was removed in 0.5.6. [Sketch contracts, supported reference limits and validation](docs/SKETCH2D-0.5.3.md).

**0.5.0:** TopSolid object/revision identity tools, duplicate-name lookup, confirmed PDM metadata/deletion/restoration, universal identifiers, stronger element guards, and native project/library creation-date ordering. The previous PDM Explorer requirement for ordering was incorrect for this installation; verified backing-document creation parameters now supply dates. See [object model, CRUD boundaries and validation](docs/TOPSOLID-OBJECT-MODEL-0.5.0.md).

**0.5.2:** configurable 15-minute AI wait, direct alphabetical PDM lists, one-call creation context, confirmed part creation, complete tool-name visibility, smaller inventory history and optional faster GPT-OSS thinking. [Log review, measurements and limits](docs/LOG-REVIEW-0.5.2.md). Sketch tools now omit sections entirely in 0.5.6; chat retains elapsed time and blue replies.

**0.4.0:** 25 new batch tools for detailed reads, parameters, element edits/deletion, 2D/3D points, multiple sketch profiles and multiple extrusion/revolution features. Pages return details together; write batches use one confirmation and one undoable transaction. Studio supplies at most 96 tool schemas per model request and can select other discovered schemas when needed. See [batch tools and validation](docs/BATCH_TOOLS.md).

**0.3.2:** project/library lists return `{pdmObjectId, name}` directly, eliminating one model call per name. Gemini model IDs are normalized; provider 404 details identify model access restrictions. A live Gemini 3.6 Flash run returned all 50 project and 66 library names in two MCP calls. See [PDM name-list validation](docs/PDM_NAME_LISTS.md).

```text
User → WPF Studio → selected AI model
                    ↕ tool requests / results
                 MCP client → console server → TopSolid Automation → TopSolid
```

## Build and open

Requires Windows x64, .NET 10 SDK/Windows Desktop runtime, .NET Framework 4.8 targeting pack/runtime, and a licensed TopSolid 7.18 or newer installation. Initial NuGet restore requires network access or a populated package cache. The shipped 7.18 profile covers the common Automating assemblies; Cae/Electrode tools require 7.20 and native modeling requires 7.20.326.

```powershell
dotnet build .\TopSolid.Automation.Mcp.Server.slnx -c Release
& .\TopSolid.Automation.AI.Studio\bin\Release\net10.0-windows\TopSolid.Automation.AI.Studio.exe
```

The build uses the matched SDK from `C:\Program Files\TOPSOLID\TopSolid 7.20\bin`. To use another SDK folder, supply `-p:TopSolidAutomationDirectory="D:\SDK\TopSolid7.20"`. Legacy DLLs in `Server.AddIn/TopSolid.Automation` are preserved but excluded from reference resolution. Tested client and host version: **7.20.400.107**. Runtime tool floors are advertised in each tool's `_meta.topsolid/minimumVersion` and enforced before execution.

A complete framework-dependent bundle is in **`artifacts/TopSolid-AI-0.5.21`**. Open `TopSolid.Automation.AI.Studio.exe` there; keep the entire folder, including `McpServer`. TopSolid must already be running and ready in the same Windows session with a valid Kernel Base license.

```powershell
dotnet publish .\TopSolid.Automation.AI.Studio -c Release -o .\artifacts\TopSolid-AI-0.5.21
```

## Use

1. Choose **Cloud API** or **Ollama**.
2. Choose a cloud service: OpenAI, Google Gemini, Anthropic, xAI, Meta Model API, Groq / Meta Llama, Mistral, DeepSeek or OpenRouter. The API URL fills automatically; each service keeps its own protected key and model. **Custom OpenAI-compatible** permits another URL. Ollama uses its server root, such as `http://localhost:11434`. See [cloud presets and Gemini correction](docs/CLOUD_SERVICES.md).
3. Use **List models** to retrieve model IDs, then select a model that supports tool calling. Model discovery and successful inference are separate checks.
4. **Save settings** persists configuration. **Save log** exports the current chat, completed conversations, configuration metadata, discovered MCP tools, tool calls/results, and the current application-session diagnostics as a redacted JSON file. The session log starts when Studio starts; the separate all-history files remain available for manual inspection. Ordinary chat also works with MCP disconnected.
5. Open TopSolid in the same Windows session, then **Connect MCP**. Discovery lists the available tools in the trace. MCP connection and actual TopSolid availability are reported separately.
6. Ask “Am I connected to TopSolid?” or “What document is currently open?” The model calls MCP and receives the actual result before answering.
7. Describe a document or modeling task with its dimensions. The AI resolves the PDM destination, creates an empty document by extension, opens it, and then creates geometry. Templates are used only when requested. Each change has its own confirmation. The model first obtains its exact document ID. Studio displays a **Confirm TopSolid change** dialog containing the target, units and arguments. **Cancel is the default**; only **Apply change** submits the operation.

Examples: “In the active part, create a 20 mm by 10 mm rectangle on XY at the origin,” or “Create a 20 × 10 × 5 mm rectangular extrusion in this part.” A 2D drawing/sketch document uses `placement=2d`; a planar sketch in a 3D document uses `xy`, `xz` or `yz`.

**More thinking** is available for local Ollama only; when enabled, the model's default thinking behavior is used. When unchecked, Gemma 4 thinking is disabled and GPT-OSS uses low thinking for faster replies. The option is disabled for Cloud API. Model/provider changes preserve conversation context. **New chat** clears it.

**Ctrl+Enter** sends. **Cancel** interrupts model requests and inspection calls. Once a confirmed CAD modification is dispatched, cancellation, disconnect and close wait for commit/rollback; forcibly terminating the server could leave TopSolid locked. Geometry changes are not saved automatically; saving is a separate confirmed action.

## Implemented scope

| Area | Capabilities |
|---|---|
| System/reference | Live connection/version; capability listing; offline search and retrieval of the complete locally bundled official API reference |
| Documents/PDM | Inspection plus confirmed project/folder/document creation, open, single/batch save, native object/project check-in, update, rebuild and rename; template and extension discovery |
| Entities/licenses | Named collections, containment/parent operations, shortcut and occurrence targets, element properties, entity folders/moves; rename, delete, appearance and translation; selection/licenses |
| Parameters | Typed values for Real, Integer, Boolean, Text, DateTime, Color, Tolerance, Enumeration, UserEnumeration, Code and Family; 8 creation types; batched formula/reference creation/editing, scoped lists, relay/constraint/choice inspection |
| Sketch2D/Sketch3D | Batched circles, rectangles, lines, arcs, polylines, cubic B-splines, parabolas, stars, smooth hearts and bounded ellipse approximations; explicit frames/offsets and native reference placement; named sketch context, topology/curve reads and coordinate conversion; 3D lines/polylines/circles/arcs/B-splines; analytic 2D slots; optional colors; fix/unfix; no new sections. New native creation/regeneration remains pending approved runtime qualification. |
| Design2D/Design3D | Shape/topology/volume inspection; cylinder creation/replacement, rectangular extrusion, profile-checked extrusion/revolution/loft, through drilling, face colors and optional native parameter references for feature dimensions |
| Assembly/tooling/drafting/electrode/CAE | Assembly insertion with fixed positioning and inclusion translation; existing occurrence/material/tool/view/page/electrode/CAE inspection |
| CAM | Existing setup/tool/operation/NC inspection; exact scalar parameter edits, calculation of one existing operation, bounded toolpath table reads, and confirmed NC generation/export for selected operations |

The new workflows follow the supplied manuals: **PDM document → native sketch/profile → shape → assembly (existing sections remain readable)**, with returned IDs carried through the model/tool loop. See [manual review and workflow mapping](docs/MANUALS_AND_WORKFLOWS.md).

See [every tool, category and source contract](docs/api/COVERAGE.md), [machine-readable MCP schemas](docs/api/mcp-tools.json), and [validation evidence](VALIDATION.md). A documented API is not automatically a callable tool. A discovered tool does not prove its TopSolid module is connected or licensed.

General fillet/pocket/chamfer/Boolean creation, dimensional sketch constraints, CAM strategy creation, and Wire/standalone PDM Explorer adapters remain extensions. Confirmed NC generation/export is limited to selected existing operations and the post-processor configured on the CAM document; the public SDK surface reviewed here does not provide a post-processor catalog, so no names are invented. CAM simulation and verification execution are exposed through the documented `ISimulation`/`IVerify` workflows; they start the native animation but do not generate NC, run a machine, or certify collision safety. The entire TopSolid UI is not exposed by the public Automation interfaces reviewed here; unverified commands are not advertised as tools. Technology folders for 2D/3D/4-axis/3+2/5-axis/MillTurn/Robot CAM map to the public SDK's shared services; they do not advertise invented technology-specific interfaces.

## Structure

```text
TopSolid.Automation.AI.Studio/
  AI/          provider adapters and provider-neutral messages
  Chat/        bounded model → tool → model loop and confirmation callback
  Mcp/         owned console process, discovery, calls, write-aware cancellation
  Settings/    persisted settings and Windows key protection
  Diagnostics/ flushed crash/error logging and redacted session export
  MainWindow / ChangeConfirmationWindow
TopSolid.Automation.Mcp.Contracts/   DTOs; no vendor references
TopSolid.Automation.Mcp.Server.AddIn/
  Automation/  connection, ID/value conversion and native modeling transactions
  Protocol/    MCP JSON-RPC over stdio
  Tools/       domain folders; explicit compiled registrations and schemas
    System/ Documents/ Pdm/ Entities/ License/
    Sketch2D/ Sketch3D/ Design2D/ Design3D/ Assembly/ Tooling/
    Drafting/ Electrode/ Cae/
    Cam/ Machine/ PartSetup/ Operation/ CuttingConditions/ Nc/ ...
TopSolid.Automation.Tests/              provider/client/chat/UI harness
TopSolid.Automation.Mcp.Server.Tests/   schema/approval/transaction/geometry harness
scripts/ApiReference/                   reference cache, SDK catalog and coverage report
```

`Server.AddIn` retains its original name; it runs as a console process outside TopSolid. New providers implement `IAiProvider`. New capabilities get typed schemas, a domain registration, verified public API contracts and appropriate transaction tests. There is no generic reflection/invoke tool, database or planning framework.

## Quality and trade-offs

| Area | Implementation / practical limit |
|---|---|
| Functionality | Exact discovered names, typed arguments and live results. Unsupported parameter/property types are explicit. No guessed CAD IDs. |
| Reliability | One STA owns Automation. Modification start/dirty revision/commit/rollback follow the guide. Single-use approvals bind exact arguments for two minutes. The target and its synchronized document group are rechecked before execution. Document-local handles are rebased after EnsureIsDirty creates a new revision. PDM partial failures retain created-object receipts. Interrupted model follow-ups retain CAD receipts. Synchronous all-history and per-application-session Studio diagnostics are flushed on every event, with WPF/global crash handlers and server fatal-error capture. |
| Performance | 16 model rounds, 24 tool calls per turn; batch pages default to 100 and return a continuation offset when the character budget is reached. Most vendor list APIs still fetch the whole list before output pagination. At most 96 schemas are sent to the model; selecting omitted tools costs a model round. Local models still need sufficient context. |
| Security | Saved keys use DPAPI CurrentUser and endpoint binding. Cloud redirects are rejected; HTTPS is required except loopback. No arbitrary API, arbitrary file, shell or machine-execution tools. Typed NC generation/export requires human confirmation and the export path is chosen explicitly by the user. Diagnostic exports omit API keys and redact configured credentials before persistence. |
| Compatibility / portability | WPF/.NET 10 frontend and .NET Framework 4.8 x64 server isolate vendor dependencies. Windows-only; remote MCP HTTP is not implemented. Module availability is checked when used. |
| Usability / quality in use | Visible calls/results and exact preview; default cancellation; distinguish model discovery, inference, MCP and TopSolid states. Failure messages identify whether a change's outcome is uncertain. |

Settings are in `%LOCALAPPDATA%\TopSolid.Automation.AI.Studio\settings.json`. Studio writes all-history diagnostics to `%LOCALAPPDATA%\TopSolid.Automation.AI.Studio\Logs\studio-YYYY-MM-DD.log` and each application launch to a separate `studio-current-YYYYMMDD-HHmmss-<processId>-<sessionId>.log`; the separate MCP process writes fatal/protocol errors to the all-history `mcp-server-YYYY-MM-DD.log`. If LocalAppData is unavailable, Studio falls back to `%TEMP%\TopSolid.Automation.AI.Studio\Logs`. WPF crashes, unobserved task failures, handled errors, MCP stderr and server failures are recorded automatically. **Save log** includes the complete current-session log from Studio startup to the export, plus the current in-memory chat/trace snapshot; it includes all-history file paths for manual inspection but does not embed historical daily-log contents. API keys are omitted and redacted. Chat and traces remain bounded in memory. Cloud mode transmits chat and returned TopSolid information to the configured model service. Ollama LAN HTTP is supported; use HTTPS or a trusted network. TopSolid calls cannot be safely hard-cancelled during a modification; a vendor hang remains an operational limitation.

## Reference work and tests

The cache includes **6,898 symbols / 3,335 API pages** from the official 7.20 reference, with source hashes and no missing indexed pages. Seven installed Automation assemblies were cataloged. Runtime MCP metadata and reference results identify bundle-relative `TopSolid.Automation/...` files; website URLs in the cache and source links are provenance only. All five supplied PDFs were indexed (**581 pages**), and their relevant PDM/sketch/shape/assembly/CAM workflows were reviewed. Their editions differ: the Design Automation guide is v7.11 (2017), the User’s Guide is v7.9 (2014), and the newer Basics tutorial is v7.20 Rev.01 (2026). Current contracts and installed assemblies remain the implementation authority. Document instructions are source material, not authorization to change workstation settings.

```powershell
dotnet run --project .\TopSolid.Automation.Tests -c Release -- --server .\artifacts\TopSolid-AI\McpServer\TopSolid.Automation.Mcp.Server.AddIn.exe --ui-smoke
& .\TopSolid.Automation.Mcp.Server.Tests\bin\Release\net48\TopSolid.Automation.Mcp.Server.Tests.exe
.\TopSolid.Automation.Mcp.Server.AddIn\scripts\Test-Protocol.ps1 -ServerPath .\artifacts\TopSolid-AI\McpServer\TopSolid.Automation.Mcp.Server.AddIn.exe
```

See [VALIDATION.md](VALIDATION.md) for what passed and what still needs real CAD/CAM fixtures. A concrete isolated native test is prepared in `scripts/LiveWorkflow/fixture-plan.json`; executing it requires explicit approval. See the validation report for its current execution status.

## Sources

- [TopSolid 7.20 Automation reference](https://help.topsolid.com/7.20/en/TopSolid%27Automation/ReferencesHomePage.html)
- [MCP stdio](https://modelcontextprotocol.io/specification/2025-03-26/basic/transports), [lifecycle](https://modelcontextprotocol.io/specification/2025-03-26/basic/lifecycle), [tools](https://modelcontextprotocol.io/specification/2025-03-26/server/tools)
- [OpenAI function calling](https://developers.openai.com/api/docs/guides/function-calling), [Ollama tool calling](https://docs.ollama.com/capabilities/tool-calling)
