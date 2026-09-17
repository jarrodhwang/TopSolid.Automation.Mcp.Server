# Sketch2D tools - 0.5.3

## Changes driven by the 2026-09-16 13:34 log

The model created the requested part and circle, but chose an unspecified 50 mm radius. It approximated the parabola with a handful of polyline points, then received `Successive segments do not share same vertex` when creating an open profile. Its retry added more points and could not solve the topology problem.

The updated drawing path keeps open lines, arcs, polylines and nonperiodic splines as native segments. It calls `CreateProfile` only for closed contours. It does not add an artificial closing edge, silently create a section, or treat a null profile handle as failure when native open segments exist. Sections remain explicit opt-in.

## Available tools

| Tool | Purpose |
|---|---|
| `topsolid_get_sketch2d_context` | Friendly/internal names, exact sketch handles, local frames/planes and topology counts in one paged read; exact-name filtering preserves duplicate matches. |
| `topsolid_read_sketch2d_geometry` | Batched vertices/segments/profiles, analytic lines/circles and native curve samples. Circle results include their native center vertex. Samples are not spline controls. Unbounded axes are not sampled with invented limits. |
| `topsolid_transform_sketch2d_points` | Convert up to 128 local reference points into another sketch frame through world space. Reject off-plane points instead of silently projecting. |
| `topsolid_create_sketches2d` | Up to 8 sketches and 32 total primitives in one confirmed transaction, bounded to 512 segments/control vertices. A failure attempts rollback of the whole batch. |
| `topsolid_create_sketch_profiles` | The same primitives and placement options for one new/existing sketch. |
| `topsolid_create_contour2d`, `topsolid_append_sketch_contour` | Connected line/arc contours; open contours remain native segments. |

Primitives: **circle, rectangle, line, circular arc, polyline, uniform cubic B-spline, parabola**. The parabola uses four mathematically derived cubic controls for the quadratic `x=t, y=t*t/(4*f)`, then verifies 17 native evaluations before commit. No sampled-polyline substitution is used. The native curve/commit path still requires the approved CAD fixture described below before it can be considered runtime-qualified on this installation.

## Coordinates and references

- Geometry is always expressed in the sketch's local frame. Input defaults to mm; native reads/receipts use metres. A part is `documentSpace=3d` even when its sketches are 2D.
- Principal planes accept an explicit **world origin** and in-plane rotation. Custom frames require orthogonal unit directions; invalid/mirrored native-2D frames are rejected.
- `placement=reference` uses **reference-local X/Y/normal offsets**, starting at the selected native anchor. It defaults to `referenceMode=associative`, matching the user's preference to follow subsequent reference changes.
- Associative placement supplies native `SmartPlane3D`, item-based `SmartPoint2D/3D`, and item-based `SmartDirection2D/3D` inputs. It uses the actual native sketch axes returned by TopSolid, checked against their local analytic geometry. No item labels are fabricated.
- In a 3D document, XY offsets use hidden native `CreateOffsetPoint` entities. Their parent operation definitions are read back to verify the original point/direction links. Geometry and helper entities are created in the same confirmed transaction.
- Supported associative frame rotation is a multiple of **90 degrees**. Normal offset must be zero. Native 2D documents additionally require zero XY offset. Unsupported combinations fail before confirmation; the server does not downgrade to a snapshot.
- The anchor must resolve to an existing vertex: a vertex, finite segment endpoint, circle center, or a line's already-defined native middle vertex. A sampled point without a native vertex is not an associative anchor.
- These links follow the reference **plane, anchor and orientation**. They do not copy a changing curve radius or create tangent, concentric or dimensional constraints. Local drawing dimensions remain explicit. Cross-document references are not accepted by this adapter.
- `referenceMode=snapshot` remains available only for explicitly requested fixed placement, including arbitrary in-plane rotations/custom frames. It is described as non-associative in the preview and result.

Previews include the resolved frame, native anchor/axis handles, dimensions, units and section choices. The same reference state is checked again when the confirmation token is consumed. Every document-local handle is rebased together after `EnsureIsDirty` changes the minor revision.

## Runtime issues found during inspection

1. `ItemLabel.Name` must be **null when absent**. The old parser replaced it with an empty string, causing native `Item name is empty` errors for valid returned topology handles. The parser now preserves absence, moniker and native name separately.
2. In installed SDK **7.20.400.107**, the two-argument `SmartPoint3D(ElementId, ItemLabel)` constructor produces `Type=Element`. The new reference path uses the documented full constructor with `SmartPoint3DType.Item` explicitly, and tests the resulting fields.
3. Native `IsSegmentConstruction` throws outside a sketch modification session in this installation. Read-only geometry tools no longer invoke it or open a modification session to obtain it. They report the unavailable state explicitly.
4. Sketch X/Y axes have infinite parameter ranges. Geometry reads return their analytic axes and label unbounded sampling; they do not send NaN parameters to TopSolid.
5. The web reference includes `CreateAxisByTwoPoints`, but the matched installed assembly does not expose it. It is not used or advertised. Unsupported arbitrary linked rotation is rejected instead.

## Studio behavior and performance

The initial tool subset now recognizes plural sketches, parabolas, splines, arcs and Korean sketch terms. Batch creation, sketch context, geometry reading and coordinate conversion are available in the first model request. A short follow-up retains the previous request's workflow context. The model is instructed to ask one combined question for missing dimensions/placement and to preserve explicit reference semantics.

One live local `gpt-oss:20b` test of “Create 2 sketches ... circle ... parabola” asked for dimensions without proposing any change. Its model response took **53.72 seconds** in that run. This is not a sub-10-second model-latency claim. The final Release reference preview took approximately **0.036 seconds**, with zero native writes. Batching reduces model/confirmation round trips; provider inference still determines much of the total latency.

## Validation and remaining native qualification

Both Debug (the configured MCP executable) and Release build with zero warnings/errors. Final checks passed: **5,952 server checks**, **970 protocol checks**, and **30/30 Release application groups**, including WPF behavior and native read-only previews. The manifest contains **168 tools**, and all **270 declared API source pages** resolve locally. Provider transport tests use fixtures; the separate local-model result above is the live inference evidence. No new live cloud inference or raster UI validation is claimed.

Evidence is under `artifacts/sketch2d-0.5.3`. The default server tests cover the reported open-path failure route, analytic parabola controls under several units/rotations/focal signs, coordinate conversion, frame readback, invalid/foreign references, native label roundtrip, confirmed-write metadata, batch bounds and default sections. Application tests cover tool selection and the normal confirmation/provider loop.

Live read-only checks use the already-open part: friendly-name lookup, frame reads, curve samples, local/world coordinate conversion, vertex/circle-center associative previews, and unchanged document ID/dirty state/sketch inventory. They do **not** prove native creation or subsequent regeneration.

The explicitly gated test in `NativeSketchValidation.cs` implements [this reviewable plan](sketch2d-native-validation-plan.json): create a dedicated part, draw circle/open path/parabola, create a linked sketch, move only the fixture reference by 10 mm, and verify that the linked sketch follows. It retains receipts and never runs from the default suite. **Execution requires the user's explicit approval; passing a plan hash is not itself permission.** No live creation/follow claim should be made until this test succeeds.

## Source contracts reviewed

- Installed *TopSolid'Design Automation Guide*, printed pages 31 and 34-38: Smart objects, local sketch frames, 2D sketches in 3D documents, vertex/segment/profile/section topology and modification lifecycle.
- Locally cached official TopSolid 7.20 references for `ISketches2D` creation/read APIs, `Plane3D`, `Frame2D`, `ItemLabel`, Smart geometry constructors, `IGeometries3D.CreateOffsetPoint` / `GetOffsetPointCreation`, and `IElements.GetParent`.
- Compiled public calls and constructor semantics checked against `C:\Program Files\TOPSOLID\TopSolid 7.20\bin\TopSolid.Kernel.Automating.dll` version 7.20.400.107. No generic reflection execution or API website lookup occurs in the production sketch workflow.

## Practical trade-offs

Native associative point/axis references preserve the user's placement intent and avoid repeated AI coordinate calculations, at the cost of a few hidden helper entities. Strict validation rejects unsupported operations instead of silently creating a fixed approximation. Pure geometry planning, placement/reference handling, native execution and schemas remain separate; the desktop continues to access TopSolid exclusively through MCP.
