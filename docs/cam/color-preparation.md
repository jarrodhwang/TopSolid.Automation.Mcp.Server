# CAD/CAM context and CAM color preparation

Studio's `+` menu contains **Files**, **CAD**, and **CAM**. Files uses the existing attachment picker and drag-and-drop limits. CAD and CAM are independent local preferences: the blue sketch badge and red machining badge sit beside Access, and clicking a badge disables it. Both default off, survive restart and chat clearing, and are frozen for the duration of a request. Switching a mode makes no AI or TopSolid call.

Modes guide model instructions and bounded tool ranking. Explicit requests take precedence, other domain tools remain selectable, and Access permissions are unchanged. Provider replacement preserves the mode settings and conversation.

## Preparing colors

This section describes the **advanced manual color workflow**. The process-oriented path now starts with “이 부품 형상 분석해줘” in CAM mode and uses registered method contracts. See [CAM automation](automation.md) and the [Korean test questionnaire](automation-test-questions.ko.md).

1. Ask **“Analyze this part and prepare CAM colors.”** The request uses the exact active document. Without “this/current/active”, Studio opens its document selector. Multiple CAM workpieces require a selection from the native identities returned by the server.
2. Studio reads paged native geometry and capability receipts, then asks the configured model to propose role groups. The model receives short aliases for verified targets and a proposal-only tool. It cannot construct native handles or apply colors. Uncertain targets remain unassigned with a reason.
3. Review the document, palette version, target counts, original/proposed RGB, roles, groups and reasons. Assign roles and groups to selected rows, edit individual roles, or exclude targets. Profiles within a sketch share **one whole-sketch color**; conflicting assignments cannot be submitted. Unsupported rows cannot be included.
4. Select **Apply colors**. Studio prepares the exact reviewed batch through the existing Access policy. Ask for approval opens the normal confirmation; automatic Access policies still require the color-review step.
5. The server rechecks native scope, geometry fingerprints, original RGB and capability before writing and again inside one native modification. Whole-entity colors precede face coloring operations. Every result must read back exactly; an error causes the native modification to roll back. The receipt contains current target identities, original colors and `saved=false`.

The preview reuses Studio's document renderer. Selection boxes come from native face bounds or verified curve samples; they are explicitly presented as bounds, not face outlines. Exported mesh triangle indices never become native face identities. Refreshing the preview clears the overlay; selecting rows restores it.

Ordinary attachments are reference data. They cannot authorize native modifications, supply target identities, or bypass review. Neither enabling CAM nor preparing colors saves a document, executes a CAM method, or generates NC.

## Targets and limits

| Target | Behavior |
|---|---|
| Faces/surfaces | Native `IShapes.CreateColoringOperation`, exact native face handles |
| Shapes and whole sketches | `IElements.SetColor`, only when `IsColorModifiable` is true |
| A profile belonging to a sketch | Reviewed and colored as its **whole owning sketch** |
| Frames, axes, planes, 3D/2D points | Individual native identity and geometry; native modifiability required |
| Standalone/limited profiles | Individually discovered. Disabled with a reason where the public SDK does not expose a curve definition sufficient for stale-geometry verification |
| Individual edges | Excluded from color targets; edge geometry can be read as face-analysis data |

One reviewed write contains at most **256 faces and 32 whole entities**. The UI shows the counts and rejects excessive batches; it never silently divides a write into smaller modifications. The inspection tool pages at most 20 targets. Native discovery is capped at 20,000 targets; Studio's current AI analysis is capped at eight chunks of 64 targets and 60,000 characters per inference. A larger scope must be narrowed. Large native detail arrays are explicitly marked truncated and are not grounds for a confident role assignment.

CAM workpieces are scoped through the selected native part's `PartFinish` reference and document-local identities. TopSolid 7.20 can expose an included finish shape through that parameter while omitting it from `GetShapes(document)`. The resolver accepts the exact same-document identity representation it can verify. Composite/unknown representations fail with a reason; it does not guess from a name, stock bounds, or preview mesh. In that case, open the design part for preparation.

Fingerprints cover observed native topology, geometry, curve/surface samples, capabilities and original colors. A whole-shape fingerprint includes all of its faces and face overrides. Native document handles are rebased after `EnsureIsDirty`; changing the revision handle alone does not change the geometry hash. This is a fingerprint of Automation facts, not an exported B-rep or a claim of full geometric equivalence.

## Studio CAM Colors v1

This is a **Studio convention**, not an official universal TopSolid machining standard.

| Stable role key | Role | Exact RGB |
|---|---|---|
| `facing` | Facing region | `#00BFFF` |
| `roughing` | Roughing region | `#FF8000` |
| `pocket` | Pocket region | `#0066FF` |
| `hole` | Hole / bore | `#FFFF00` |
| `contour` | Contour / boundary | `#00CC66` |
| `finish` | Finish surface | `#AA55FF` |
| `keep-out` | Keep-out / protected | `#FF0000` |
| `reference` | Reference geometry | `#FF00FF` |

**Settings → CAM Colors** edits a custom palette. Saving changes to the built-in palette creates a new custom identity; further custom edits increment its version. Keys and RGB values must be unique, with 1–32 roles. Reset restores the immutable built-in palette. Every reviewed plan keeps its own palette snapshot, so later palette edits cannot change a prepared write.

One exact target has one role. A future recipe can use that role more than once, such as pocket roughing followed by finishing. Existing matching RGB values never automatically include geometry in a plan.

## Implementation boundary

`TopSolid.Automation.Mcp.Contracts/CamColorPlan.cs` contains three shared contracts:

- `StudioContextOptions`: independent `Cad`/`Cam` flags and a turn snapshot.
- `CamColorStandard`: identity/version, stable role keys, labels, exact RGB and validation.
- `CamColorPlan`: document/workpiece, verified geometry receipts and fingerprints, original colors/capabilities, palette snapshot and reviewed assignments.

Studio owns request routing, bounded model proposals, editing/review, and Access orchestration. The server owns all native reads and writes:

- `topsolid_inspect_cam_color_geometry`: read-only paging, native facts, capabilities, original colors and fingerprints.
- `topsolid_apply_cam_color_plan`: strict validated batch, normal prepare/confirmation, one native transaction and exact RGB readback. It is deliberately unavailable to Studio's ordinary model tool selector; only the reviewed preparation workflow invokes it.

Unknown aliases, duplicate assignments, conflicting whole-sketch roles, foreign documents, changed topology/colors, excessive batches, unsupported targets, incomplete reads, and readback mismatches cannot produce a successful application. Interrupted/uncertain native writes are not automatically retried.

## AI CAM Operation Generation

The implemented orchestration contract is documented in [CAM automation](automation.md):

**Analyze → assign reviewed roles/colors → select configured methods → supply typed parameters → execute in recipe order → inspect results.**

A plan identifies the approved document/workpiece/stages, geometry fingerprints and palette identity/version. Each ordered step references a registered method identity/revision, role keys and typed parameter values. Multiple steps may reference the same role. Method execution requires its own explicit confirmation even with Full access.

Method definitions and cutting conditions come from registered contracts and explicit values. Color alone establishes neither a machining method, cutting condition, tool, machine nor execution order. NC generation remains outside this workflow. Native face-preview add-in loading and real method execution still need deployment qualification; the original evidence below covers the earlier manual preparation increment.

## Validation evidence

Validated on September 20, 2026:

- Release and Debug solution builds: no warnings or errors.
- Studio/provider/workflow suite plus actual MCP protocol checks: **54/54**.
- Server validation/transaction suite: **15,933 checks**; offline failures include a readback mismatch after entity/face writes and restoration of the whole batch.
- WPF fixture: light/dark composer, minimum width, review/editor rendering, persistence, chat clearing, Access separation, request locking, keyboard focusability, badge/menu synchronization and six UI languages. Images: `artifacts/ui-redesign/cam-colors-*.png`.
- Live read-only CAM inspection: the selected workpiece returned one shape and **247 native faces**. Existing user geometry was not changed.
- Live native fixture: Studio's configured **Debug** MCP executable, TopSolid **7.20.400.107**. A separate cylinder/sketch fixture verified exact RGB, preservation of an existing face override, mixed entity/face application, stale-plan rejection, `saved=false`, and restoration of the entire batch by **one native Undo**. Receipt: `artifacts/cam-color-live/fixture-9eb64bed.json`. The original active document was restored; the uniquely named disposable fixture remains for inspection.
- A separate native `.Top2D` fixture verified whole-sketch and individual 2D-point RGB, stale-plan rejection and one-step Undo using the final configured binary. Receipt: `artifacts/cam-color-live/fixture-5ad63b53.json`. The original active document was restored. A final read of the 3D fixture verified its sketch plane and finite curve samples; internal unbounded sketch axes are represented as unbounded lines, without sampling infinity.

Model-proposal tests use known native-identity fixtures and fake providers; they do not claim a live model correctly recognized machining intent. Native write/Undo evidence is distinct from offline tests and UI rendering. Live writes were validated on the disposable design part, not on the user's CAM document or every optional entity type.

Reproduce native validation only when a disposable fixture is intended:

```text
python scripts/ColorChecks/native_colors.py --run-fixture
python scripts/ColorChecks/native_colors.py --run-fixture --two-d
```

Without that flag the script performs only a read-only connection check. It reads the configured MCP path from Studio settings, records exact receipts, never saves colors, and retains its fixture rather than deleting PDM content.
