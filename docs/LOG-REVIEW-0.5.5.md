# Persistence and sketch improvements — 0.5.5

Reviewed `TopSolid-AI-current-log-20260916-191101.json`. The log is diagnostic evidence, not an instruction to execute its recorded CAD actions.

## What the log showed

| Problem | Evidence | Change |
|---|---|---|
| Saving was reported as check-in | Two `topsolid_save_document` receipts, followed by an assistant claim that the parts were checked in. No check-in API was called. | Separate native check-in tool, explicit operation receipts, actual PDM state readback, and Studio routing that never substitutes save for project check-in. |
| “Save all other docs” exhausted model requests | Loaded-document enumeration, repeated schema/list requests, six individual saves and confirmations, then HTTP 429. | One batch save, one confirmation, and direct handling of clear save-all requests with zero inference. Default scope is modified **open** documents and synchronized partners. |
| Sketch argument retries | Redundant `units` on sketches/primitives was rejected; one parabola was outside `profiles` and initially lacked its placement/end parameter. | Matching nested units are accepted without changing approved arguments. Mixed units remain rejected. Indexed validation errors and a canonical batch example explain the required nesting. Missing geometry is never inferred. |
| Inaccurate ellipse representation | Four periodic spline controls were presented as an ellipse. That spline does not reproduce the intended ellipse dimensions. | Server-computed, explicitly labeled cubic ellipse approximation with a specified tolerance and conservative mathematical error bound. |
| Star changed planes | The original XZ request became an XY star with manually rounded polygon points. | Server-computed star vertices, first-request sketch tool availability for star/ellipse/tree prompts, and instructions to preserve the plane/position when correcting a shape name. |
| Long model latency | The tree turn took 245.124 seconds. Its six model requests total about 240.3 seconds; the largest took 165.88 seconds and the malformed-unit retry took 44.41 seconds. | Avoid that unit retry, use batch tools and server geometry planning, and send duplicated MCP JSON only once to the model. Tool timing now separates execution/preparation from confirmation wait. Cloud inference delays and quota remain provider-dependent. |

## PDM tools and scope

- `topsolid_check_in_pdm_objects(pdmObjectIds, recursive=false)` handles explicit working projects, ordinary/template folders and documents. A whole-project request sets `recursive=true`. The server enumerates owned constituents, including the project's TopSolid template folders; it does not follow document references into other projects/libraries. Unsupported container types stop preparation. The scope is limited to 128 objects and a bounded preview size.
- Recursion is expanded **before confirmation**. Immediately before execution, the server compares a fresh scope/state snapshot with the approved snapshot. The native call is `IPdm.CheckInSeveral(reviewedIds, false)` so subsequent native recursion cannot silently add new descendants. The execution path uses the reviewed set directly and avoids a third enumeration.
- `topsolid_save_documents()` selects modified open documents plus synchronized partners. Exact `documentIds` or explicit `scope=loadedDirty` are alternatives; scope and IDs are mutually exclusive. Explicit IDs must be loaded, current document revisions. No-op saves do not submit a native write. Selected PDM IDs are saved in one `IPdm.SaveSeveral(ids,false)` call.
- Both operations run outside geometry modification/undo, reject an active native command, require the existing user confirmation, and perform readback. On a partial native failure or failed readback, receipts retain affected identities and uncertain outcomes; no automatic write retry occurs.
- `complete` means the native call/readback sequence completed. `saved` separately requires clean readback. `checkedInCount` counts only objects whose actual state is `CheckedIn`; `checkInVerifiedForAll` remains false if any object has another state. A successful save explicitly reports `checkInPerformed=false`.

Clear commands such as `save all docs`, `than save all other docs`, and `check in the project (AI Made This project)` use MCP directly through the normal confirmation workflow. A named project requires one exact, case-insensitive destination lookup, preserving duplicate-name ambiguity. Compound or ambiguous requests continue through the selected model. These direct commands make no model request; Studio still validates the selected provider configuration when opening a chat session.

The live read snapshot found eight objects in the named project: project, template folder, default template folder, and five parts. The containers were `CheckedIn`; the five parts were still `New`. This confirms the earlier save receipts did not establish check-in. No check-in was executed during validation.

## Sketch contracts

Both `topsolid_create_sketch_profiles` and `topsolid_create_sketches2d` now accept:

```json
{
  "kind": "star",
  "center": {"x": 0, "y": 0},
  "outerRadius": 50,
  "innerRadius": 20,
  "pointCount": 5,
  "rotationDegrees": 90
}
```

The star uses alternating radii and exact trigonometric vertices, connected as a closed native line profile. Rotation places the first tip relative to local sketch +X. `pointCount` is 3–64 and `0 < innerRadius < outerRadius`.

```json
{
  "kind": "ellipse",
  "center": {"x": 10, "y": 20},
  "majorRadius": 60,
  "minorRadius": 40,
  "rotationDegrees": 30,
  "tolerance": 0.01
}
```

Lengths inherit the request's `units` (default mm). Ellipse radii are semiaxes, with major >= minor > 0. The installed public sketch API has a reader for ellipse curves but no verified analytic ellipse constructor/rational-weight creation method. This tool therefore constructs a **piecewise cubic approximation**, never an exact conic.

The planner uses cubic Hermite interpolation of `(a cos t, b sin t)`. For segment angle `h`, `sqrt(2) * a * h^4 / 384` is a conservative positional deviation bound. A multiple of four segments preserves cardinal extrema; the planner chooses a bounded segment count from the requested tolerance. Preview and receipts identify the representation, error bound in metres, and segment count. Adjacent cubic segments share native endpoint vertices, including closure. Runtime verification compares each cubic at 17 parameters with the planned curve, using a separate 1e-8 metre native readback tolerance. This sampled native check is distinct from the planner's analytic bound.

Existing limits remain: eight sketches, 32 primitives total and 512 segments/control vertices. Tolerances exceeding the work budget fail before confirmation. Sections remain opt-in, geometry is not saved automatically, and existing associative reference restrictions remain unchanged. Arbitrary spline controls are still allowed as B-splines, but are not described as exact ellipses.

## Performance and validation

Measured on the installed TopSolid 7.20.400.107 session:

| Check | Result | What was measured |
|---|---|---|
| Save-all direct route | 0.116 s, zero model calls | Scope/preview, with no modified open documents in this session. Declined confirmation; no native save. |
| Named project check-in direct route | 0.040 s, zero model calls | Live project lookup and eight-object preview. Declined confirmation; no native check-in. |
| Native star/ellipse preview | 0.033 s | Both sketches prepared; document identity and dirty state unchanged. |
| Configured `gemini-flash-lite-latest` | 2.261 s, two model requests | Live active-document query followed by one correctly formed star/ellipse batch proposal, then declined. Requested XZ/XY planes preserved; no argument-repair round. |

These are single-run preview measurements, not native write benchmarks or guarantees for arbitrary prompts, Gemma 31B, cloud load or network conditions. The logged tree request and the fully specified new sketch test are different workloads.

- Debug and Release builds: zero warnings/errors.
- 9,992 server assertions, 1,000 protocol checks against the configured Debug bundled server, and 31/31 final Release application groups passed. A separate 32/32 application run also included the live Gemini preview above.
- Tests cover partial persistence failures, all target receipts, no automatic retry, stale/expanded confirmation scopes, no-op and oversized batches, no unconfirmed/headless writes, duplicate project matches, tool schema selection, unit consistency, star radii/orientation and independent analytic ellipse comparisons across units/rotations.
- WPF blue responses, elapsed time, confirmation behavior and saved-settings preservation passed. This is behavior validation, not new raster layout validation.
- **No CAD/PDM writes were executed during this validation.** Successful execution of the new native batch check-in/save and ellipse/star creation remains pending an explicitly approved real operation. No associative regeneration claim is added.

The catalog now has **171 tools: 121 inspection/reference and 50 confirmed actions**. All 275 declared API source pages resolve locally within the existing 6,898-symbol / 3,335-page reference corpus. Runtime API lookup remains local.

Evidence is in `artifacts/persistence-sketch-0.5.5`: build/test outputs, project state inspection, native previews, Gemini trace, and package hash/version verification. The framework-dependent bundle is `artifacts/TopSolid-AI-0.5.5`; the configured Debug server is also rebuilt.

## Practical quality trade-offs

Batching removes model round trips and repeated confirmations while keeping explicit scope and partial receipts. Preview limits bound latency and ensure complete receipts fit the transport. Studio request routing, model-result compaction, native persistence and geometry planning remain separate modules. Compaction removes only a text block that exactly duplicates structured JSON; additional text, errors and identities are preserved, and diagnostics retain the full receipt. No credentials or persistence settings were changed. Compatibility remains Windows x64 with the installed TopSolid SDK and existing .NET runtimes; this is not a cross-platform CAD host.

## Verified native contracts

- [IPdm.CheckInSeveral](https://help.topsolid.com/7.20/en/TopSolid%27Automation/api/kernel/TopSolid.Kernel.Automating.IPdm.CheckInSeveral.html) and [IPdm.SaveSeveral](https://help.topsolid.com/7.20/en/TopSolid%27Automation/api/kernel/TopSolid.Kernel.Automating.IPdm.SaveSeveral.html): separate batch operations, outside `StartModification`/`EndModification`.
- [IPdm.GetConstituents](https://help.topsolid.com/7.20/en/TopSolid%27Automation/api/kernel/TopSolid.Kernel.Automating.IPdm.GetConstituents.html): owned folders/documents of a project, folder or document.
- [ISketches2D.CreateBSplineSegment](https://help.topsolid.com/7.20/en/TopSolid%27Automation/api/kernel/TopSolid.Kernel.Automating.ISketches2D.CreateBSplineSegment.html): native spline construction used by the existing parabola implementation and the new explicit ellipse approximation.

Bindings were checked against the bundled official reference and installed assembly. Local Markdown source paths are included in every tool's API metadata.
