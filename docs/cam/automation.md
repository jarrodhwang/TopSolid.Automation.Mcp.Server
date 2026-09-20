# CAM analysis, local preview and confirmed method execution

The Studio workflow treats color as the geometry exchange contract between AI planning and an existing CAM method. With CAM mode enabled, **“이 부품 형상 분석해줘”** resolves the exact document, workpiece and machining stage; analyzes verified facts against registered methods; opens a local colored preview with process proposals; applies reviewed preparation colors; and opens a separate method execution confirmation. Manual color preparation remains an advanced workflow.

## Registration and user flow

Settings → CAM Colors → CAM method registrations stores up to 32 methods in `cam-methods.json` alongside Studio settings. **PDM explorer** opens one icon tree: Projects / Libraries → project or library → folders → documents. Expand folders to read bounded pages; **Load more** retains native offsets even when a page contains no methods. Only CAM method documents can be selected. Browsing does not open documents. Selecting an unloaded method opens it through the existing Access approval policy, then verifies the saved PDM object and exact document revision. Registration does not execute the method. The expandable identity section also supports an explicit PDM identity. The native method document must remain available for verification and execution.

Each registration contains a display name, process, applicability conditions, prerequisite method keys, role/RGB palette, search scope, typed inputs, and six native execution options. Method RGB is an explicit registration contract: it is not extracted or inferred from the method's internals. The immutable Studio starter palette remains available; it does not certify that a particular native method recognizes those colors.

The review shows processes, methods, target counts and reasons beside the 3D preview. Select a process to see its preparation colors; exclude/reorder it, choose another registered method or edit its target roles in the expandable geometry contract. **Proceed with AI recommendation** accepts the original proposed selection/order. Original appearance comparison, rotation and zoom use the local cache. Uncertain targets and unverified face display mappings remain excluded with reasons, including when no process can be proposed.

After preparation, the execution dialog presents workpiece, stage, order, typed inputs and options. Its defaults enable only `KeepAssociativity` and `UseCuttingConditions`; `LaunchDeferred`, `ManualExecution`, `ReuseAnswers` and `SilentMode` start off. Cancelling this dialog keeps prepared colors. **“CAM 재개”** reloads the saved plan, validates it and asks for execution confirmation again. Clearing chat preserves context modes and the prepared plan.

## Contracts and trust boundaries

- `CamMethodDefinition`: exact PDM identity/revision, process/conditions, prerequisites, RGB/scope and typed input/options contract.
- `CamAutomationPlan`: exact document/workpiece/stages, verified geometry, ordered method snapshots, per-process color plans and receipts.
- `CamMethodExecutionResult`: operation identities and calculated/pending/deferred state, without saving.
- Existing `CamColorPlan`, `CamColorStandard` and `StudioContextOptions` remain shared by the manual workflow.

Studio supplies only aliases for returned geometry and registered methods to a proposal-only model tool. The model cannot invent native target handles or invoke native writes during planning. Unknown method/target/role keys, incomplete geometry reads and conflicting assignments are rejected. Attachments and method descriptions are reference data, not modification authorization. The final method tool and color-plan application tool are hidden from the generic model selector.

The MCP additions are `topsolid_get_cam_stages`, `topsolid_inspect_cam_method` and `topsolid_execute_cam_method`. The latter uses strict nested argument schemas and the normal one-use prepare/confirmation ticket. Existing color inspection and application tools are reused. Final method confirmation is mandatory under every Access policy; Access still controls color application normally.

Typed registered inputs bind to existing scalar parameters in the target document. Real values use native SI units and a declared `UnitType`, and are checked before/after setting. Unspecified cutting conditions are never invented. Additional questions implemented by the native method use its own dialog when native options permit it. Arbitrary method-specific geometry-parameter binding is not provided by this release.

## Native stages and transactions

The common stage service identifies modeling/machining stages by native IDs/types, not translated names. Modeling, sketches, design parameters and preparation colors enter the modeling stage. CAM editing/calculation/method execution enters the owning or selected machining stage. A mixed generic entity batch spanning multiple stages is rejected for separate review. Cylinder creation now supports an explicitly active MillTurn CAM document as well as a design part.

`SetWorkingStage` and insertion reset run after `StartModification` and `EnsureIsDirty`, in the same transaction as the edit. Success retains the working stage. Failure rolls back the edit and the stage change. [Native stage contract](https://help.topsolid.com/7.20/en/TopSolid%27Automation/api/kernel/TopSolid.Kernel.Automating.IOperations.SetWorkingStage.html).

Color preparation applies whole entities before explicit face overrides. Each native face-color operation contains faces of only one shape, as required by the installed API; all these native operations remain in **one reviewed batch and one transaction**. `IsColorModifiable` gates appearance support. `IsModifiable` is not used as a face-color gate: a feature-generated shape may return false while allowing native coloring. Included read-only shapes are excluded. Every RGB is read back exactly, and a mismatch fails the entire transaction.

Each method executes with native `ExecuteMethod` in its own transaction. Geometry, original/applied RGB, exact stages and saved method revision are rechecked before execution. Created operations must belong to the approved document/stage. [Native execution options](https://help.topsolid.com/7.20/en/TopSolid%27Automation/api/cam/TopSolid.Cam.NC.Kernel.Automating.MethodExecutionOptions.html).

Execution IDs are reserved in `%LOCALAPPDATA%/TopSolid.Automation.AI.Studio/cam-executions` before native execution. A repeated or uncertain attempt cannot execute again automatically. Failures stop subsequent processes and retain completed receipts. Deferred results are explicitly **not calculated**. There is no automatic save, NC generation, or automatic retry.

## Search scope and repeated colors

Existing matching RGB does not authorize geometry. The server checks every supported entity/face in the native document search scope, including inherited whole-shape colors, and rejects unapproved matches. Whole-shape approval covers its faces only for the approved role; explicit overrides still matter.

The installed `ExecuteMethod` API does not accept a typed workpiece-selection binding. A registered workpiece scope therefore requires an isolated document with exactly one verified native workpiece. For multiple workpieces, use a reviewed document-scope contract with no unintended matching geometry, or isolate the workpiece. The UI does not promise an unenforceable narrower search.

Per-process recoloring is planned independently. Changed lookup colors after an associative, deferred or uncalculated prior step are conservatively blocked. For a previously calculated, non-associative sequence, reviewed recoloring can proceed through a separate modeling transaction, then machining execution. Same-color steps do not imply new geometry authorization.

## Exact face preview and deployment

`TopSolid.Automation.CamPreview.AddIn` reads existing native face display facets, exact labels and definition-to-document transforms on TopSolid's UI thread. It does not regenerate display items, start modifications, switch stages, save or execute commands. The server verifies document/workpiece/face identity, before/after fingerprints and native bounds, then includes original colors. An ordinary GLB or an inferred mesh index is never used as a native face mapping.

The private `topsolid/graphicPreview` transport uses format `cam-faces-v1`; mesh bytes never enter model history. The current-user-only pipe is pinned to the selected TopSolid PID. Protocol 1 and exact native assembly version must match. Unavailable faces retain their native identity and exclusion reason, not synthetic mesh geometry. Original face overrides survive whole-shape recoloring in the local preview, matching native appearance behavior.

Limits: 20 inspected targets/page, 64 targets/model proposal, 32 faces/preview transfer, 250,000 triangles and 750,000 vertices/transfer, 64 MB transfer size, the existing renderer scene limit, and **256 faces / 32 whole entities per reviewed application**. Geometry analysis currently requires a complete workpiece inventory of at most 512 entries. Larger workpieces require an explicitly narrowed/split review; no silent application batching. Faceted workpieces without a verified native preparation-face reference are rejected.

Build/publish includes `CamPreviewAddIn` with the helper DLL, Newtonsoft.Json, installer and [registration instructions](../../TopSolid.Automation.CamPreview.AddIn/README.md). The external helper needs a TOPSOLID-issued developer add-in registration certificate. This is separate from the customer's TopSolid license and AI API key. No certificate is generated or bypassed. Missing/unloaded/mismatched helpers show a reason and block exact-face preparation.

## Validation on 2026-09-20

- Release solution and configured Debug Studio/server builds: zero warnings/errors.
- Release publish completed in `artifacts/cam-automation-publish`; packaged helper hash matches the built DLL, with installer, README and Newtonsoft.Json present. This is packaging evidence, not native add-in loading.
- Studio/provider/protocol suite: **55/55**; real configured MCP handshake and active-document read included.
- Offline server suite: **16,013 checks**, including exact scope/RGB rejection, argument schemas, stage routing, stale geometry, cross-shape grouping and rollback/readback.
- WPF fixtures: automation review/registration/execution, original comparison, partial mapping rejection, existing face overrides, six languages, light/dark themes and keyboard-capable controls; existing CAD/CAM/color UI suite also passed. These are mock geometry/provider tests, not proof of real AI machining judgment.
- Read-only live probe uses Studio's configured Debug MCP executable and TopSolid **7.20.400.107**. Native stages are resolved; unavailable faceted preparation geometry is reported explicitly. The native face helper is **not loaded**, so a real exact-face mesh preview is **not verified**.
- A distinct disposable CAM copy was created without saving/changing its source. A native coloring attempt on an unmodifiable included shape failed; both original color and working stage were verified restored. Receipt: `artifacts/cam-automation-live/stage-fixture-8f3146f9.json`. The final capability gate rejects these targets before coloring.
- A native owned cylinder was created in that disposable CAM copy using the configured MCP. Starting from machining, actual face coloring switched to modeling, exact RGB readback succeeded, `saved=false`, and **one Undo restored both color and machining stage**. Receipt: `artifacts/cam-automation-live/owned-shape-4d343c2e.json`. This proves stage/appearance behavior on an owned CAM shape, not method-workpiece selection. The original active document was restored; fixtures remain for inspection.
- **Still unverified:** registered helper loading, live trimmed/curved/repeated-instance mappings, large native mesh latency/memory, real method application/selection/calculation, multiple native machining stages and native method Undo. No test method was supplied and no real method was executed. Deployment qualification remains open.

Developer reproduction:

```powershell
dotnet build TopSolid.Automation.Mcp.Server.slnx -c Release
./TopSolid.Automation.Mcp.Server.Tests/bin/Release/net48/TopSolid.Automation.Mcp.Server.Tests.exe
./TopSolid.Automation.Tests/bin/Release/net10.0-windows/TopSolid.Automation.Tests.exe --server '<configured MCP executable>'
./TopSolid.Automation.Tests/bin/Release/net10.0-windows/TopSolid.Automation.Tests.exe --cam-automation-ui
./TopSolid.Automation.Tests/bin/Release/net10.0-windows/TopSolid.Automation.Tests.exe --cam-colors-ui
python scripts/CamAutomationChecks/probe.py
```

Native write scripts in `scripts/CamAutomationChecks` require explicit disposable-copy identities. Do not treat them as ordinary read-only tests. Use the [Korean questionnaire](automation-test-questions.ko.md) for product acceptance.
