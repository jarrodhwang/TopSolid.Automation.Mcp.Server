# Native graphical review — 0.5.12

Approval dialogs and receipt-backed document/element/operation questions now contain an interactive native WPF 3D viewport. It uses the supplied TopSolid blue/gray gradient, coordinate-axis colors, millimetre scale, original TopSolid toolbar icons, and the Studio light/dark palette. Wide windows show facts/cards beside the viewport; narrow windows stack them and refit the camera.

## Behavior

- Orbit with a left drag, pan with Shift-drag or the middle button, zoom with the wheel, and fit with the toolbar or a double-click. Keyboard arrows orbit, `+`/`−` zoom, `F` fits and `Home` restores the isometric direction. Top/front/right views and edge visibility are also available.
- Current document geometry comes from the installed TopSolid **glTF exporter** through `IDocuments.ExportWithOptions`. The active document is never substituted for a selected target. Unloaded documents are not opened; the dialog asks the user to open them in TopSolid. Names stay visible while identities remain internal.
- The geometry is a **document snapshot**. Selecting an operation, face or edge shows its owning document; it does not isolate or highlight that individual entity. The viewport states this explicitly. Geometry picking is not used to answer a question; the existing receipt-backed cards remain the authoritative selection.
- Cylinder and extruded-rectangle approvals also offer **Proposed geometry**, built from the exact prepared arguments. A cylinder height bound to a parameter uses the value returned by native preflight. Proposed geometry uses TopSolid orange unless the request specifies RGB. These are tessellated primitive previews, not an execution of the CAD action. Existing shapes to be replaced remain visible only in the current-document view.
- Other actions show current geometry and retain the normal change facts. A CAM parameter change is not presented as simulated machining. Questions without a geometry-bearing document remain compact.
- Loading, unavailable exporter, unloaded document, changed document and oversized-model states leave the approval/answer controls usable. A preview never approves, applies, saves or checks in a change.

## Architecture and practical trade-offs

The ADS [`GraphicView`](https://ads.topsolid.com/_doc/TopSolidHelp720/html/731ca1e1-a7b7-f854-8f59-2132473e89af.htm) constructors take an existing graphic view or TopSolid `DocumentUserInterface`/`DocumentWindow`. The Automation view API exposes cameras and screenshots rather than those UI objects. This standalone .NET 10 process therefore uses a WPF viewport instead of attempting to load/reparent the in-process TopSolid editor. The existing .NET Framework 4.8 helper remains the sole owner of vendor Automation assemblies.

| Quality concern | Implementation and limit |
| --- | --- |
| Functional correctness | glTF node hierarchy, affine transforms, mirror winding, inverse-transpose normals, metre-to-millimetre and Y-up-to-Z-up conversion; exact prepared primitive dimensions. Unsupported data rejects the whole preview instead of displaying a partial model. |
| Reliability | Selection requests are debounced and tagged by generation. Late replies cannot replace a newer selection. Closing/switching a dialog cancels display while the native read drains under the MCP request gate, preserving pending approval tokens. Native read timeout remains 30 seconds. |
| Performance | A single WPF viewport with frozen geometry/materials, bounded mesh batches, no per-frame mesh updates, no 3D software hit testing, and no idle rendering timer. Only the camera and small 2D compass/scale change during interaction. Native WPF hardware acceleration is used where the device supports it. |
| Bounds | Maximum native GLB is 2 MiB; expanded surface geometry is capped at 100,000 triangles and 300,000 positions, including repeated instances and unused vertices, with 512 primitive meshes and 2,048 scene nodes. Feature edges are omitted beyond 10,000 segments. Oversized/unsupported models must be reviewed in TopSolid. Export itself is a vendor operation; these limits bound ingestion/rendering, not the vendor's internal memory or export time. |
| Security and privacy | Private `topsolid/graphicPreview` RPC is not a model tool. Binary geometry does not enter AI messages, chat history, or model logs. No caller-supplied export path, external glTF buffers, URI loads, textures, scripts, skins or animations. Only a generated temporary file is read and deleted. No new external renderer package or browser runtime. |
| Usability/accessibility | Friendly names, light/dark/Korean text, native icons, keyboard camera controls, accessible toolbar labels, visible loading and failure states, and responsive layout. Approval facts remain unchanged by camera or mode changes. |
| Fidelity/compatibility | Bounded uncompressed triangle GLB subset; opaque base-color/diffuse/specular display and derived feature edges. It does not reproduce TopSolid's complete material/rendering engine, toolpaths, post-change regeneration or native sub-entity picking. Graphics isolation lets CAD actions continue through the existing approval contract. |

The renderer follows [Microsoft's WPF 3D performance guidance](https://learn.microsoft.com/en-us/dotnet/desktop/wpf/graphics-multimedia/maximize-wpf-3d-performance), the [glTF 2.0 binary/scene format](https://registry.khronos.org/glTF/specs/2.0/glTF-2.0.html), and the public [TopSolid export contract](https://help.topsolid.com/7.20/en/TopSolid%27Automation/api/kernel/TopSolid.Kernel.Automating.IDocuments.ExportWithOptions.html). Export runs outside a modification transaction as required by that contract.

## Verification

- Release and Debug solution builds: zero warnings/errors. Exact configured Debug MCP executable rebuilt.
- **39/39 application test groups**, **15,225 server checks**, **2,124 protocol checks**. New cases cover malformed/oversized GLB, external buffers, transform dimensions, immutable approval proposals, actual transport cancellation/draining, and selected receipt scope.
- Native WPF dialog fixtures cover camera changes, proposed/current switching, light/dark and Korean, narrow layout, stale replies, clearing a selection, and a failed preview leaving approval available. Rendered images were inspected. These offscreen captures explicitly use software rendering and are not GPU performance evidence.
- A live read-only export of `테스트파트` produced a 1,740-byte, 12-triangle GLB, approximately 40 × 40 × 40 mm. The measured run exported in 68.6 ms and parsed/built frozen meshes in 62.8 ms. Document identity, dirty state and shape inventory were unchanged. Hardware capability tier was 2; this is capability evidence, not a frame-rate benchmark. No complex-assembly/GPU throughput benchmark or AI-provider inference was performed.
- Evidence: `artifacts/graphic-preview-0.5.12`; inspected dialogs: `artifacts/ui-redesign/graphic-*.png`. Complete framework-dependent bundle: `artifacts/TopSolid-AI-0.5.12`.

No user CAD action was executed during verification. The pre-existing confirmation requirement remains in effect for actual changes.
