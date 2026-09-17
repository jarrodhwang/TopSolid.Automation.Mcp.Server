# TopSolid Automation MCP console server — 0.3

A separate .NET Framework 4.8 x64 console process. All vendor calls stay here; Studio contains no Automation API references. `Server.AddIn` is the retained project name, not an in-process installation requirement.

## Runtime and SDK

Run `TopSolid.Automation.Mcp.Server.AddIn.exe` without arguments. Keep its entire build/publish folder. Studio launches it with redirected UTF-8 stdin/stdout/stderr. Open TopSolid manually in the same Windows session.

Build uses `$(ProgramW6432)\TOPSOLID\TopSolid 7.20\bin`; override with `-p:TopSolidAutomationDirectory="path"`. References use HintPath ahead of all legacy candidate files. Original DLLs under `TopSolid.Automation` remain intact but are not used. Production references Kernel, Design, Drafting, CAM NC, Electrode, CAE and the Kernel SX dependency. All directly referenced Automation modules and SX match 7.20.400.107; supporting installation dependencies retain their own vendor versions. No `.Host.dll` internal implementation assembly is referenced.

`TopSolidHost.Connect(false, 5, uniqueClientName)` connects to an existing host. Its Boolean result means automatic startup, not connection success. The server verifies `IsConnected` and makes a live `Application.Version` query. The version is decoded using the documented decimal layout and returned as `hostVersionText`.

The base host must be ready and version 7.20 or newer. Modeling requires 7.20.326 or newer. This is not certification of all 7.20 patch levels; the compiled and live-tested SDK/host is 7.20.400.107. Optional module hosts connect lazily. A listed tool does not prove a module/license is available.

## Tool organization

There are **153 registered tools / 21 categories**, including **42 confirmed action tools**. Domain files are under `Tools/Pdm`, `Entities`, `Documents`, `License`, `Sketch2D`, `Sketch3D`, `Design2D`, `Design3D`, `Assembly`, `Tooling`, `Drafting`, `Electrode`, `Cae`, and `Cam/*`. Shared schema, result conversion and registration avoid duplication. Batch tool files sit alongside each domain; native sketch/shape construction lives in `Automation/SketchBatchChanges.cs` and `ShapeBatchChanges.cs`. See [batch scope and evidence](../docs/BATCH_TOOLS.md).

See the [complete coverage report](../docs/api/COVERAGE.md) and [tool schemas](../docs/api/mcp-tools.json). Every bound operation is a static call compiled against the vendor SDK. No reflection dispatcher or arbitrary API invocation exists.

CAM technology folders preserve the requested organization. Existing operations are inspected and individually calculated through shared public CAM services. Scalar CAM parameter changes require exact names/types and confirmation. Strategy creation, NC postprocessing/transmission and simulation execution are not exposed. Wire and standalone PDM Explorer references are cached; their host adapters are not implemented. PDM tools use `TopSolidHost.Pdm`.

## Protocol

MCP **2025-03-26**, JSON-RPC 2.0, one UTF-8 JSON message per line. Supported methods: `initialize`, `notifications/initialized`, `ping`, `tools/list`, `tools/call`, plus the declared experimental `topsolid/prepare` confirmation extension. Initialization is required before tools; `initialize` cannot be batched. Other valid JSON-RPC batches are serial. All tool schemas fit one discovery page.

Stdout contains protocol messages only. Vendor diagnostics are redirected to stderr. One leading UTF-8 BOM from .NET Framework clients is accepted. Input is bounded at 1 MiB characters, JSON depth 64; duplicate properties are rejected. Tool schemas reject unknown fields and validate nested object/array types, finite numeric bounds, enums, string lengths, required fields and geometry constraints. Pages default to 25 and cap at 100. Results above 60,000 characters return a bounded error. Vendor list methods generally still materialize the full list before paging.

Fatal server failures and protocol/tool exceptions are flushed as JSON-line all-history diagnostics to `%LOCALAPPDATA%\TopSolid.Automation.AI.Studio\Logs\mcp-server-YYYY-MM-DD.log`; the same records are sent to stderr for Studio capture. This file is independent of the WPF window, so a server crash remains inspectable after the client reports the lost connection. Studio's **Save log** export records this file's path but keeps its historical contents separate from the current application-session export.

`content[0].text` contains JSON data. Unavailable status is a successful check with `connected=false`; no active document is a successful explicit empty result. Execution failures use `isError=true`; malformed requests, unknown tools and approval failures use JSON-RPC errors. Multi-property reads are not atomic snapshots; document changes can invalidate IDs during inspection.

## Human confirmation extension

A trusted MCP client must perform this sequence for each change call:

1. Send `topsolid/prepare` with `{ "name": "topsolid_create_rectangle2d", "arguments": { ... } }`.
2. The server validates schema/geometry and reads the explicit target plus all synchronized documents (bounded to 50). It returns the target name/type/ID, exact arguments, units/effect, expiry, and `confirmationToken`. No modification is started.
3. Show that proposal to the user. Studio uses an immutable dialog with **Cancel as default**. Keep the token outside the model's messages, tools and trace.
4. After approval, send `tools/call` with the identical `name`/`arguments` and `"_meta": { "confirmationToken": "returned token" }`.

The token is random, single-use, tied to the tool and exact JSON arguments, expires after two minutes, and is consumed before execution. The complete target snapshot is rechecked immediately before execution. Changed target state or synchronization membership invalidates approval. Missing, expired, changed or replayed proposals cannot execute. The model has no prepare tool and cannot satisfy approval with a `confirmed=true` argument. An MCP client that does not implement this extension can use inspection tools; its writes are rejected.

This protects the model/tool boundary and accidental bypass. It is not authentication against a malicious local client: the process owner controls the session and must implement genuine human approval. No elaborate permission system is introduced.

## Document actions, native modeling and recovery

PDM create/open/save APIs explicitly run **outside** StartModification/EndModification. Their previews state that those actions are not geometry transactions. If PDM creation succeeds and naming/resolution fails, the error includes a `partialChange` receipt with the created PDM ID. Do not create a duplicate.

Contour tools preserve local sketch coordinates, validate line/arc continuity and radii, and read back native closure and curve length. Native segment counts are recorded without requiring exact input count equality because TopSolid can sew/simplify profiles. Extrusion/revolution/loft return native shape handles and solid volume. Scalar parameters require their actual type and, for reals, exact unit type with SI values. Arbitrary dimensional constraints are not exposed.



- Explicit document revision ID required; writes never follow a later active-document change.
- Input lengths default to mm; cm/m are supported and converted to SI metres.
- `placement=2d` uses `CreateSketchIn2D`; XY/XZ/YZ use `CreateSketchIn3D` on a plane through the supplied origin.
- Rectangle/circle create native vertices, segments, profile, section and building operation. 3D polylines create native 3D sketch geometry; the open-polyline cleanup uses the 7.20.326 contract. Rectangular extrusion creates a native extruded shape along +Z.
- `StartModification` must succeed before writes. `EnsureIsDirty(ref document)` runs inside the transaction; the resulting revision ID is returned.
- Sketch modification scopes always end. Successful application modification commits/updates with `EndModification(true,true)`; failure attempts `EndModification(false,false)`.
- A rollback failure reports uncertainty. Saving is a separate confirmed action. Explicit document-element and sketch-item deletion is available with confirmation and rollback handling. PDM deletion, check-in and automatic retries are not exposed.

All Automation access runs serially on one STA. Inspection requests can be discarded by stopping the owned server after cancellation/timeout. **Never hard-kill the server while a modification may be active.** Studio disables cancellation/close during an active change and waits for commit/rollback. On a broken write transport it reports an unknown outcome and avoids forcibly stopping the child. This cannot recover every vendor/process failure; users must inspect TopSolid before retrying an uncertain change.

## Evidence and reproducibility

- [Full reference cache report](../docs/api/reference-cache-report.json): 6,898 symbols, 3,335 pages, zero missing indexed pages.
- [SDK catalog](../docs/api/assemblies-7.20.json): exported signatures/fields and SHA-256 for seven Automation assemblies.
- [Bindings](../docs/api/binding-verification.json): all declared source pages exist in the official reference.
- `../scripts/ApiReference/cache_reference.py` rebuilds the complete index/article cache; the compressed article resource supports offline reference tools.
- `../scripts/ApiReference/Export-AssemblyCatalog.ps1` refreshes installed SDK metadata using Windows PowerShell/.NET Framework.
- `../scripts/ApiReference/generate_read_tools.py` regenerates only its reviewed allowlist of 76 simple inspection bindings. It does not wrap the entire API.
- `../scripts/ApiReference/write_coverage_report.py` verifies declared reference URLs and rebuilds category documentation from actual MCP discovery.

The five supplied PDFs (581 pages) were indexed and relevant workflows reviewed. See [manual mapping](../docs/MANUALS_AND_WORKFLOWS.md) for edition differences, source pages, implemented workflows and API gaps. Manual setup/deletion/check-in instructions were not treated as user commands.

Run `scripts/Test-Protocol.ps1` after building; `-SkipTopSolidQueries` checks only the protocol. The separate `TopSolid.Automation.Mcp.Server.Tests` harness validates confirmation, rollback, geometry conversion and serialization without connecting to TopSolid. See [VALIDATION.md](../VALIDATION.md) for actual runtime coverage and remaining geometry/CAM validation.

