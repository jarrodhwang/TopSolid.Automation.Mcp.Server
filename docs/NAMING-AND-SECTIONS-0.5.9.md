# Automatic names and explicit sections — 0.5.9 source changes

## Findings from the supplied log

Reviewed `TopSolid-AI-current-log-20260917-145523.json` as diagnostic evidence, not instructions.

- The red 100 × 350 mm cylinder succeeded, with the expected volume and red color readback. The blue slot also succeeded. Both receipts say `sectionCreated:false` despite the user not mentioning sections.
- The next cylinder failed because its helper sketch was forcibly named `Cylinder profile`, which already existed. This was a naming defect, not a parameter/section requirement.
- A later cylinder creation bound its height to a parameter successfully. The parameter setter returned 0.2 m. The final HTTP 429 prevented the model from completing its response; the log does not contain a subsequent shape-volume read proving regeneration.

## Resulting behavior

1. **Normal requests require no section wording.** Drawing tools still create zero sections and reject legacy `createSection`/`sectionMode` arguments. Extrusion/revolution uses the original sketch handle directly.
2. **Explicit sections use a separate tool.** `topsolid_create_sketch_section` accepts verified closed profiles from one existing sketch and requires a separate prepared action. Studio hides it from ordinary tool exposure and category/name selection. Only an explicit section-creation request in the current user message exposes it; old conversation requests do not enable it for later ordinary modeling. English/Korean request detection is deliberately conservative.
3. **Omitted geometry names stay omitted.** The server no longer forces `Cylinder` or `Cylinder profile`. The supporting cylinder sketch keeps TopSolid's automatic name even when the user names the resulting shape. Studio is instructed to omit sketch/shape/point names unless requested.
4. **Requested new names are unique.** An unused requested name is preserved. Collisions get `_1`, `_2`, etc. An occupied `Cylinder_2` proceeds to `Cylinder_3`, rather than `Cylinder_2_1`. Comparisons are conservatively case-insensitive, and suffixes stay within schema length limits.
5. **Batches reserve names together.** Sketches, feature batches, point entities, literal/smart parameters and entity folders share the same policy. Existing entities are never renamed, overwritten or silently reused to satisfy a new creation request. PDM/document naming and explicit renaming tools retain their separate contracts.
6. **Actual names are reviewable.** Preparation reports requested/assigned names by input path. The server rechecks that allocation before execution and refuses a changed allocation. SetName readback is checked inside the native modification. Receipts retain the mapping; subsequent operations should use returned handles and names. Formula text is not automatically rewritten.

## Performance and API choices

No name snapshot is loaded when names are omitted. Explicit names load the document's internal names once per preparation/recheck, then allocate all batch suffixes in memory. This avoids extra model rounds and per-collision native retries. The snapshot is not cached across changes, so another edit cannot leave a stale name reservation.

`SearchByName` alone is insufficient because it excludes elements whose names are not natively required to be unique. The allocator uses `GetElements` and `GetName`; display names are returned through `GetFriendlyName`. Automatic names retain TopSolid's own localized display behavior; translated friendly names are not treated as object IDs.

Sources: [SearchByName](https://help.topsolid.com/7.20/en/TopSolid%27Automation/api/kernel/TopSolid.Kernel.Automating.IElements.SearchByName.html), [GetElements](https://help.topsolid.com/7.20/en/TopSolid%27Automation/api/kernel/TopSolid.Kernel.Automating.IElements.GetElements.html), [SetName](https://help.topsolid.com/7.20/en/TopSolid%27Automation/api/kernel/TopSolid.Kernel.Automating.IElements.SetName.html), [CreateSection](https://help.topsolid.com/7.20/en/TopSolid%27Automation/api/kernel/TopSolid.Kernel.Automating.ISketches2D.CreateSection.html). Runtime references remain bundled locally.

## Validation already completed

- **14,286 offline server checks passed**, including duplicate names, batch reservations, numeric suffixes, case variants, name-length limits, no naming calls for omitted names, readback mismatch and name changes after confirmation.
- **2,004 protocol checks passed**; **187 tools / 22 categories / 128 read tools / 59 confirmed actions**. All **399 declared API pages** resolve locally.
- Live **read-only** naming previews passed: automatic cylinder names about **0.002 s**, an existing-name collision about **0.030 s**, two duplicate sketch names about **0.045 s** in the open part. These are individual preview measurements, not end-to-end model/creation guarantees.
- Document identity, dirty state and sketch/shape inventories stayed unchanged. **No native writes were performed in this task.** The new suffix application and explicit section execution have not been qualified by an approved native write.
- An earlier application run passed **31/32 groups**. Its only failure was the local instruction-length budget; the instructions were subsequently shortened. Additional section-exposure/dispatch tests were added. These latest Studio edits still need the final application test run.

## Build handoff

The user requested that the other active session handle further builds and packaging. No further build or app restart is performed here. Concurrent Studio UI/provider work is preserved. No new complete release bundle is claimed by this task.

The attached log configured:

`TopSolid.Automation.AI.Studio/bin/Debug/net10.0-windows/McpServer/TopSolid.Automation.Mcp.Server.AddIn.exe`

The building session should rebuild Debug and Release after its UI changes are complete, then run the server checks, `scripts/Test-Protocol.ps1` against the configured Debug server, and application tests with `--ui-behavior --live-modeling-preview`. Regenerate API coverage after the final tool-description edits. Keep confirmation behavior and the section exposure restriction intact during integration.

Evidence from this task is in `artifacts/naming-0.5.9`. Server source/test builds passed with zero warnings/errors before the final description-only edits. A later full solution build encountered the other session's unfinished UI files; it is not evidence of a completed app build.

Simple user prompts need no technical caveats:

- “Create a red cylinder, diameter 100 mm and height 350 mm, in the active part.”
- “Create another cylinder with the same dimensions.”
- “Create two rectangle sketches named Test, each 40 × 20 mm, on XY at X=0 and X=60 mm.”
