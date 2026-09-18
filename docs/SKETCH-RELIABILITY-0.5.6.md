# Sketch reliability and local model latency — 0.5.6

Reviewed: `C:\Users\jarrod\Desktop\TopSolid-AI-current-log-20260917-011816.json` (Studio 0.5.5). Chat messages inside the export were evidence, not permission to replay CAD actions.

## Findings and changes

| Observed in the log | Implemented change |
|---|---|
| Extruding `HeartSketch` led to a second `HeartSectionSketch` with `createSection=true`. | Removed the section-creation tool and both `createSection`/`sectionMode` inputs. Sketch drawing has no `CreateSection` calls. Extrusion prefers the original sketch. |
| The final heart was an open polyline whose last point repeated the first. It looked closed but had no closed native profile. | Reject this ambiguous topology before confirmation. Added a computed closed heart with shared endpoint vertices and a real closed profile. |
| Several invalid arc/B-spline/reference arguments consumed repeated model rounds, eventually reaching quota errors. | Compact heart arguments avoid model-generated arc centers and control nets. Stop after three invalid proposals (initial attempt plus two repairs); none of those proposals executes. |
| Provider changes reset the conversation. A clarification about the requested heart then produced a circle. | Retain user intent and action receipts across provider/model/settings changes. Failed requests retain the user message. Provider-specific thoughts/signatures become portable historical text, without executable tool envelopes. New chat still clears history. |
| Finding a named part required project listing, document search and ID resolution in separate model rounds. | `topsolid_get_modeling_context(projectName,documentName)` combines that read, including current revision, native type and open state. |
| Model requests took up to 79.77 seconds; many native calls took about 0.04–0.3 seconds. | Smaller domain instructions and local schema selection; Gemma 4 thinking disabled by the optional fast mode; per-request inference metrics. |
| Sketch rotation and “do not save/create sections” text selected unrelated write schemas. | Local selection distinguishes document creation from drawing in an existing part, and excludes save/revolution tools for these negative/orientation instructions. |

The export also contains HTTP 404, 429 and 503 responses. These remain model availability, quota or provider service failures. Studio does not silently change the selected model or replay uncertain writes to hide them.

## Sketch contract

**Drawing tools create segments and closed profiles, with no new sections.** A profile connects segments; a section groups profiles. These are distinct TopSolid objects. The server checks section count after creation and attempts transaction rollback on an unexpected change. Appending preserves the existing section count; existing user-created sections are not deleted.

The following inputs/tools are intentionally retired, including explicit `false`/`none` values:

- `createSection`
- `sectionMode`
- `topsolid_create_sketch_section`

Old callers receive an actionable validation error. Existing-section inspection and extrusion input remain supported for existing models. The complete local API reference still includes the documented section APIs; reference documentation does not grant generic invocation access.

`topsolid_extrude_sketch` accepts the original sketch handle through the documented `SmartSection3D(ElementId)` constructor. This is a reference wrapper, not a call to persist an `ISketches2D` section. The combined rectangle/extrusion tool uses this route too. It must not redraw a sketch merely to extrude it. Geometry with no suitable closed profile still requires a deliberate repair, not an invented closing edge.

### Heart drawing

```json
{
  "documentId": "<current revision from a live tool>",
  "name": "HeartSketch",
  "placement": "xy",
  "width": 100,
  "height": 80,
  "center": {"x": 10, "y": 20, "z": 30},
  "rotationDegrees": 0
}
```

Call `topsolid_create_heart_sketch`. Lengths are mm; center is the **world bounding-box center before rotation**, and the notch points along local +Y at zero rotation. The server defines a symmetric decorative heart with six cubic spans. Endpoints attain the exact unrotated width/height bounds. Adjacent spans share native vertices, including closure. Seventeen native samples per span must match the planned polynomial before commit. No section or save occurs.

The general batch tools also accept `kind=heart` with local `center`, `width`, `height`, `rotationDegrees`. They retain the existing associative reference placement/anchor options. This is a defined decorative shape, not a fit to an unspecified engineering drawing. Existing arbitrary spline, contour and reference workflows remain accessible.

Reviewed contracts are in the bundled official 7.20 cache and installed 7.20.400.107 SDK: `ISketches2D.CreateProfile`, `CreateBSplineSegment`, `CreateBuildingOperation`, `GetSectionCount`, `SmartSection3D(ElementId)`, `IShapes.CreateExtrudedShape`, `IPdm.SearchDocumentByName`, `IDocuments.GetDocument` and `GetOpenDocuments`. No runtime web lookup was added.

## Named document lookup

The context tool reads working-project names once, searches only the uniquely matched project, and returns current backing document IDs plus PDM IDs. Document candidates include their owner, so duplicate names are not collapsed. It does not open or create documents. Incomplete results, duplicate projects/documents, external documents without a native backing revision, or read errors prevent choosing a unique target. The query is bounded at 1,000 project names and 64 document candidates and reports incompleteness rather than silently selecting the first item.

## Local inference and conversation behavior

- Local initial exposure: at most ten schemas including the selector, with a 16,000-character selection budget (one required large schema is permitted). The measured heart request needs six schemas, 3,834 schema characters and 4,914 message characters.
- Category discovery happens locally in Studio. `studio_select_tools(category)` lists names/descriptions; `studio_select_tools(names)` loads exact schemas. Local expansion is capped at sixteen total schemas. No native CAD call or approval is implied. Cloud models retain their existing 24-initial/96-maximum limits.
- **Fast local replies** uses `think:false` for Gemma 4 and `think:"low"` for GPT-OSS. Unchecking it restores model defaults. Other model families receive no new thinking option. The existing persisted setting key is retained for compatibility. No global Ollama configuration, context size, downloads or service restarts were changed.
- Ollama's response metrics now expose load, input-processing and generation duration, prompt/cached/generated token counts and generation throughput. Missing metrics remain absent. See [Ollama chat metrics](https://docs.ollama.com/api/chat) and [thinking controls](https://docs.ollama.com/capabilities/thinking).
- The model should use supplied names directly, ask only for missing design values, respect explicitly delegated choices, and let the existing exact confirmation dialog obtain approval. New prompts preserve the user's language and prior shape/plane when a clarification changes only one detail.
- Provider migration retains historical receipts and opaque object IDs. Historical text is explicitly not proof of current state or authorization to repeat an operation. Confirmation tokens are never sent to the model.

## Measured evidence and limits

The live local model was **`gemma4:e4b`**, on Ollama **0.34.0**. Its manifest advertises tool calling and boolean thinking support. TopSolid was closed; the actual MCP status reported unavailable. The benchmark used real Ollama inference and real MCP tool discovery, but **synthetic named-document receipts and a fixture preview**. CAD execution is disabled in the harness and the preview is always declined.

| Final run | Time to valid, declined heart proposal | Model requests | Context reads | Initial prompt tokens |
|---|---:|---:|---:|---:|
| Model already loaded, new prompt | 1.853 s | 2 | 1 fixture read | 1,992 |
| Repeat prompt, cache warm | 1.038 s | 2 | 1 fixture read | 1,992 |

Both proposals preserved the exact plane, dimensions, world center and rotation and contained no section arguments. These times exclude native geometry, actual PDM lookup, human review and saving. They are not an end-to-end CAD latency guarantee. An earlier unsuccessful run is retained as `initial-clarification-failure.json`: cold loading alone took 5.30 seconds, and that run asked an unnecessary name clarification. It is not presented as a successful cold benchmark.

Validation:

- Debug and Release builds: no warnings/errors.
- 10,966 offline server assertions; 1,053 protocol checks against the log's configured Debug bundled executable.
- 31/31 Release application groups, including provider migration, interrupted intent, bounded invalid proposals, exact heart geometry/topology planning, confirmation rejection, local discovery, provider adapters, UI elapsed time/blue replies and unchanged saved settings.
- 172 tools in 21 categories: 122 inspection/reference tools and 50 confirmed actions. All 275 declared source pages resolve in the local reference.
- **No native writes were executed.** Live heart construction, direct-sketch extrusion and new combined PDM reads remain pending a running TopSolid session and, for writes, explicit confirmation. Existing associative regeneration remains a separate unverified native behavior. No new raster UI validation is claimed.

Evidence: `artifacts/sketch-reliability-0.5.6`. Bundle: `artifacts/TopSolid-AI-0.5.6`. The exact configured Debug Studio/server outputs were rebuilt as well.

## Practical quality trade-offs

Functional correctness uses TopSolid's profile/section and object/revision distinctions, with numerical and native readback guards. Reliability favors rollback and bounded repair over repeated uncertain writes. Performance removes model work and network round trips; it does not promise that every model/hardware combination completes within ten seconds. Maintainability keeps CAD code in the server and provider/context selection in Studio. Confirmation, credential storage and trace redaction remain in force. Smaller initial catalogs may add a discovery round for unusual operations, while common workflows preload their tools. Retiring section arguments is an intentional client-contract change. Deployment remains Windows x64 with the existing .NET 10, .NET Framework 4.8 and TopSolid 7.20 requirements.
