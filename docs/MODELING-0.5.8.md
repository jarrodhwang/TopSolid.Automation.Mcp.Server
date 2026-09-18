# Sketches, shapes, appearance and parameters — 0.5.8

## What the supplied log showed

Reviewed `TopSolid-AI-current-log-20260917-131135.json`. Its contents were treated as evidence, not instructions.

1. A request for a 100 mm diameter, 350 mm high cylinder selected the rectangular extrusion tool. The returned volume, 0.0035 m³, was a box volume. The expected cylinder volume is approximately 0.002748893572 m³.
2. Creating `Color_Red` created a parameter; it did not color a shape. Later the model incorrectly denied that Automation supports shape coloring.
3. Four independently created lines had separate endpoint vertices and returned `profile:null`. Their visual rectangle was not a connected native profile, so revolution failed. Attempts to invent profile/section labels could not repair it.
4. The model deleted working geometry before attempting its replacement. A later failure left the original shape gone.
5. Many native tool calls were already below one second. Tool discovery, incorrect proposals, repeated model requests, final prose and user confirmation added most of the visible time. Increasing native parallelism is inappropriate for TopSolid's single modification context.

## Implemented workflows

| Tool or existing workflow | Behavior |
|---|---|
| `topsolid_create_cylinder` | Creates a circular extrusion or a 360° revolution of a connected radial/axial rectangle, with optional color, in one confirmed transaction. Supports arbitrary world axis and base center. Checks profile geometry, validity and expected cylinder volume. |
| Cylinder `replaceShapes` | Only for an explicitly requested replacement. Validates the new shape and color before deleting the listed old shapes, within the same transaction. Rechecks the replacement after deletion. Old source sketches remain. |
| `topsolid_set_entity_colors` | Applies actual element display color to color-modifiable sketches, shapes, surfaces and other elements; checks native readback. Does not create a Color parameter. |
| `topsolid_color_shape_faces` | Validates exact face handles, creates a native coloring operation, and checks face-color readback. Element color and face-specific overrides are distinct. |
| `topsolid_create_sketch3d_curves` | Up to 32 lines, connected polylines, circles, arcs and uniform cubic B-splines, with at most 512 points/control vertices, in one new 3D sketch. World coordinates; explicit arc/circle normal; closed profiles share vertices; open paths remain open. |
| 2D `kind=slot` | Closed outline with two tangent lines and two analytic semicircles. `length` is overall end-to-end length, greater than `width`. Center, rotation and native curve readback are checked. This draws an outline; it does not cut a pocket. |
| Extrude/revolve/loft previews | Validate native profile membership, closedness for solids, and embedding in a 3D document before approval. A no-profile error explains the separate-line problem and the supported connected contour inputs. |
| `topsolid_get_modeling_guide` | Local recipes and explicit limits for cylinder, 2D/3D sketch, features, colors and parameters. No network or TopSolid query. |

Named colors are resolved by the server: `red`, `blue`, `green`, `lime`, `yellow`, `orange`, `purple`, `magenta`, `cyan`, `black`, `white`, `gray`. Use `color:{name:"red"}` for ordinary colors. Exact `color:{r:255,g:0,b:0}` remains available. Mixing the two representations is rejected. Green is RGB (0,128,0), lime (0,255,0). Older `update_elements` and Color-parameter inputs retain their RGB contract.

Optional color is also available during 2D sketch batches, 3D sketch creation and shape creation/batches. No sketch sections are created.

### Absolute cylinder example

After resolving the actual document ID, propose:

```json
{
  "documentId": "<live document revision>",
  "diameter": 100,
  "height": 350,
  "method": "revolve",
  "units": "mm",
  "origin": { "x": 0, "y": 0, "z": 0 },
  "axisDirection": { "x": 0, "y": 0, "z": 1 },
  "color": { "name": "red" }
}
```

`method` defaults to `extrude`, the base center to the world origin and the axis to +Z. Diameter is not radius. The confirmation displays these defaults and the prepared geometry. Omit `replaceShapes` for ordinary creation; an empty replacement list is not a deletion instruction.

## Parameters are native value providers

Absolute values remain the default. Parameter-driven modeling is used only when requested:

| Feature | Literal input | Native parameter alternative | Required parameter type |
|---|---|---|---|
| Extrusion, including batches | `length` in root length units | `lengthParameter` | Real / Length |
| Revolution, including batches | `angleDegrees` | `angleParameter` | Real / Angle |
| Through drilling | `diameter` in root length units | `diameterParameter` | Real / Length |
| Extruded cylinder | `height` in root length units | `heightParameter` | Real / Length |

Supply exactly one literal or reference. Parameters retain SI values (metres/radians). The server checks existence, validity, type, unit and the current feature range, then passes **`SmartReal(ElementId)`**, retaining the native dependency. It never reads a parameter and substitutes its current value as an alleged link. A parameter currently equal to 360° stays a reference; only a literal full turn uses the API's documented null-angle convention.

Create/find a named parameter once, optionally define its formula with the parameter-expression tools, then pass its returned child parameter handle to the feature. Parameter operations remain separate from parameter entities, PDM IDs and topology labels. A changed document revision rebases document-local parameter references together with the feature inputs.

**Limits:** verified sketch vertex/radius inputs are ordinary coordinates and doubles. This release does not create dimensional sketch constraints, parameter-driven cylinder diameter, or parameter-driven revolved-cylinder sketch height. It rejects unsupported linking instead of silently creating a snapshot. Generic editing of extrusion/revolution definitions is not exposed by the reviewed creation interfaces; supported later dimension changes use driving parameters. Live dependency regeneration still needs native qualification.

## Tool access and performance

- Cylinder, color and true 3D sketch requests receive the relevant tool schemas immediately, including Korean requests. Cylinder requests avoid loading unrelated generic sketch-building schemas for local models.
- Explicitly requesting no parameters avoids loading parameter-editing tools. Ordinary color requests use appearance tools.
- For an explicit current/active-document modeling request, Studio performs one MCP context read before inference. Named/PDM destinations keep their exact lookup workflow. The real read is retained as a paired tool exchange, not inserted into system instructions. It never approves a write.
- Gemini receives Google's documented marker for client-generated context calls only; genuine model thought signatures are preserved. [Google's thought-signature contract](https://ai.google.dev/gemini-api/docs/generate-content/thought-signatures).
- Invalid tool proposals are still bounded to three repairs. Failed or uncertain submitted writes are not retried automatically. Every change uses the existing confirmation and rollback paths.

### Measured model planning

Final runs used a synthetic document, real catalog and real inference; all valid proposals were declined. These are **not native creation times, cold-start guarantees, or measurements of Gemma 31B**.

| Saved model | Extruded red cylinder | Revolved red cylinder, Korean request |
|---|---:|---:|
| `gemini-flash-lite-latest` | 1.14 s / 1 model request | 0.79 s / 1 model request |
| `gemma4:e4b`, warm, fast mode | 1.17 s / 1 model request | 4.68 s / 3 model requests |

Each final run used one synthetic active-document read and reached one correct declined proposal. The local Korean run repaired a missing diameter and an empty optional replacement list before a valid proposal. Earlier failures (unnecessary ID question and magenta RGB for red) drove the context and named-color changes; their evidence is retained. These few runs do not establish success rates across arbitrary prompts.

## API scope and remaining operations

The reviewed [IShapes](https://help.topsolid.com/7.20/en/TopSolid%27Automation/api/kernel/TopSolid.Kernel.Automating.IShapes.html) interface provides extrusion, revolution, loft and face-color creation. [IElements](https://help.topsolid.com/7.20/en/TopSolid%27Automation/api/kernel/TopSolid.Kernel.Automating.IElements.html) provides element appearance and editability checks. [CreateProfile](https://help.topsolid.com/7.20/en/TopSolid%27Automation/api/kernel/TopSolid.Kernel.Automating.ISketches2D.CreateProfile.html) requires connected native vertices. Runtime lookup uses the bundled `TopSolid.Automation` corpus.

No verified public creation method was found for 3D fillet/chamfer, Boolean union/subtraction/intersection, fused boss/pocket, general surface trim or driven sketch constraints in the reviewed 7.20 interfaces/corpus. They are **not implemented or advertised as executable tools**. A separate extrusion is not a Boolean boss; a slot outline is not a pocket. Verified internal-API implementations would be additional work.

## Validation and practical limits

- Debug and Release: zero warnings/errors, SDK 7.20.400.107. The exact configured Debug bundled server was rebuilt.
- **14,072 offline server checks**, **1,941 configured-Debug protocol checks**, **32/32 Release application groups**, including live read-only previews and WPF behavior.
- **186 tools / 22 categories / 128 inspection-reference tools / 58 confirmed actions**. All **398 declared API pages** resolve locally.
- Real open-part previews passed for extrusion/revolution cylinders, slots, 3D sketch and element coloring. A real no-profile sketch was rejected before approval. Document identity, dirty state, sketch inventory and shape inventory remained unchanged. The open part had no shapes at validation time, so live color preview used a sketch.
- Offline tests cover cylinder dimensions/orientation/volume, analytic slot geometry, arc planes, units, dependency-preserving SDK Smart objects, color readback/guard failures, schema validation and confirmation rejection.
- Topology labels round-trip omitted monikers as the SDK-required empty string while preserving absent names as null. This prevents valid returned vertex/face handles from failing during parsing.
- **Zero native writes.** New geometry, actual coloring, atomic replacement and parameter regeneration have not yet been executed in a user-approved native test. Compilation, contract tests and preview success are distinct from that qualification. B-spline native validation checks type, topology and finite samples; no public control-net reader is available.

Evidence: `artifacts/modeling-0.5.8`. Release bundle: `artifacts/TopSolid-AI-0.5.8` (Windows x64; existing .NET 10 Desktop and .NET Framework 4.8 runtimes required). No database, generic API invocation, additional permission subsystem or direct TopSolid dependency was added to Studio.
