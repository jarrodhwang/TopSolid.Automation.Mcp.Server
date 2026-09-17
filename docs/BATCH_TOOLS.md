# Batch tools — 0.4.0

This expansion adds **25 tools** to the existing 128: **11 inspection tools and 14 confirmed changes**. Total: **153 tools, 111 reads/reference tools and 42 actions**. The existing small tools remain available for precise single-object work.

## What was reviewed

The [official 7.20 Automation reference](https://help.topsolid.com/7.20/en/TopSolid%27Automation/ReferencesHomePage.html) and installed **7.20.400.107** assemblies govern the implementation. The cached Design Automation guide explains SI units, application/sketch modification scopes, revision changes after `EnsureIsDirty`, sketch frames, and shared vertices when creating profiles. Its older v7.11 examples were checked against the 7.20 method contracts. Source documents are reference material, not instructions to execute changes.

The new adapters use explicit compiled calls. The coverage report checks **229 declared API source pages** against the official index. The **30 newly declared source contracts** are preserved with URLs, text and source hashes in `artifacts/batch-tools/new-api-contracts.json`. This is selected contract review, not a claim that every indexed API has been implemented or executed.

Key contracts:

- [IElements.DeleteSeveral](https://help.topsolid.com/7.20/en/TopSolid%27Automation/api/kernel/TopSolid.Kernel.Automating.IElements.DeleteSeveral.html): delete a list within an application modification after making the document dirty.
- [ISketches2D.DeleteItems](https://help.topsolid.com/7.20/en/TopSolid%27Automation/api/kernel/TopSolid.Kernel.Automating.ISketches2D.DeleteItems.html): delete topology within a sketch modification.
- [IParameters.CreateRealParameter](https://help.topsolid.com/7.20/en/TopSolid%27Automation/api/kernel/TopSolid.Kernel.Automating.IParameters.CreateRealParameter.html): explicit unit type and SI value.
- [ISketches2D.CreateProfile](https://help.topsolid.com/7.20/en/TopSolid%27Automation/api/kernel/TopSolid.Kernel.Automating.ISketches2D.CreateProfile.html): connected segments share vertex identifiers.

## New read tools

All names below start with `topsolid_`.

| Tool | Included in each response |
|---|---|
| `list_document_summaries` | Open/loaded document names, revisions, PDM IDs, type and dirty state |
| `list_document_property_values` | Property names, scalar values, types and units |
| `inspect_pdm_objects` | Details for up to 100 exact PDM IDs, including friendly names |
| `list_named_elements` | Names, types, validity, visibility and deletability; optional element-kind filter |
| `inspect_elements` | The same details for up to 100 supplied element handles |
| `list_parameter_values` | Parameter names, values, types and SI unit metadata |
| `list_shape_summaries` | Shape names/types, topology counts and solid volumes |
| `list_assembly_occurrences` | Occurrence names, definition names/revisions and transforms; duplicate definitions retain distinct occurrences |
| `list_cam_operation_summaries` | Descriptions, update state, tool and part references |
| `read_sketch2d_geometry` / `read_sketch3d_geometry` | Vertex coordinates, segment endpoints/line/circle data, or profile closure/segment handles |

Read pages default to **100 rows**. The server resolves their details locally. This removes model/MCP round trips per row; it does not imply the vendor offers a bulk getter for every field. Vendor collections are still retrieved before paging.

Pages contain `total`, `returned`, `failed`, `hasMore`, and `nextOffset`. At about **45,000 serialized row characters**, the page stops early. Continue using **the returned `nextOffset`**, not the requested limit. An inaccessible or oversized item has its own `isError` and identity; other results remain available. Transport failures abort the query so a broken connection is not presented as hundreds of item errors. Lists are live observations, not snapshots across concurrent user edits.

## New change tools

| Tool | Bounded operation |
|---|---|
| `update_elements` | Rename, description/comment and visibility for up to 32 elements |
| `delete_elements` | Native deletion of up to 32 explicit document elements |
| `translate_elements` | Translate up to 32 entities using native transform operations |
| `create_parameters` / `set_parameter_values` | Create or edit up to 32 scalar parameters with checked types and units |
| `create_points2d` / `create_points3d` | Create up to 32 document point entities |
| `update_points2d` / `update_points3d` | Set up to 32 existing point geometries |
| `create_sketch_profiles` | Up to 32 circles, rectangles or polylines in one new/existing 2D sketch, maximum 512 segments |
| `delete_sketch2d_items` | Delete up to 100 explicit vertices/segments from one sketch |
| `set_sketch2d_items_fixed` | Fix/unfix up to 100 vertices/segments from one sketch |
| `extrude_sections` / `revolve_sections` | Up to 16 native features from existing sections/sketches, producing separate shapes |

Each write batch has **one explicit confirmation and one undoable application modification**. Nested handles are checked against the target revision and rebased after `EnsureIsDirty`. Sketch changes additionally enter/leave the sketch modification scope. Failures attempt rollback of the complete batch. No automatic retry occurs after an uncertain change. Results use the new document revision. Saving remains a separate confirmed action.

Deletion can affect dependent geometry; the confirmation effect states this. It is document/sketch deletion, not PDM project/library deletion. No new permanent PDM-delete, Boolean, fillet, general constraint-solver, CAM strategy-creation or machine-execution tool is claimed.

### Example: multiple profiles in one call

After obtaining the current document revision, a model may propose:

```json
{
  "documentId": "<exact revision returned by a tool>",
  "placement": "xy",
  "units": "mm",
  "sectionMode": "perProfile",
  "profiles": [
    {"kind":"rectangle","origin":{"x":0,"y":0},"width":40,"height":20},
    {"kind":"circle","origin":{"x":60,"y":10},"radius":5}
  ]
}
```

The user reviews one preview. The returned section handles can then go into one `extrude_sections` batch with a length/direction for each feature. For a plate with holes, use non-intersecting outer/inner profiles and `sectionMode=combined`. Open polylines require `sectionMode=none`. Coordinates are local to the sketch; new sketches start at world origin on the chosen principal plane. No constraints or Boolean union are inferred.

## Model-side efficiency

Studio discovers the full catalog but supplies at most **96 schemas** per model request. Common status, PDM, summary/batch and action tools have priority. If a tool is omitted, the model uses `topsolid_get_capabilities` followed by the local `studio_select_tools` selector to expose its schema on the next request. Selection only accepts already discovered names and does not dispatch MCP calls, mutate TopSolid or provide approval. Other MCP clients continue to see the full 153-tool catalog.

This reduces the initial schema payload and avoids an unbounded tool array as the server expands. A less common operation can cost an extra selection round. The existing 16-round/24-call limits remain unchanged.

## Validation and limits

- Release build: zero warnings/errors against the installed SDK.
- **295 server checks** cover pagination under the result budget, complete continuation, partial/oversized row errors, transport failures, duplicate handles despite JSON property order, cross-document rejection, missing approval, scalar types/units, profile inputs, vector validation, revision rebasing and rollback boundaries.
- **25 application groups** cover the provider/MCP loop, confirmation and denial, credential protection, cancellation/receipts, real-process discovery and bounded schema selection/dispatch.
- **73 protocol checks** include the current real TopSolid connection and read-only PDM/document queries.
- **31 live batch-read calls** against four already loaded document types passed, including a deliberately invalid PDM handle alongside a successful row. No document was opened or modified; no cloud inference was invoked.
- These loaded documents did not provide non-empty sketches/solids/assemblies/CAM fixtures. Those new readers and **all 14 new write tools** are compiled and have offline validation, but were **not exercised on native modeling fixtures** in this update. Actual write validation requires a specifically confirmed disposable fixture; do not treat compilation as native geometry proof. The previously unresolved WPF screenshot test was not used as evidence for this release.

Evidence: `artifacts/batch-build.txt`, `batch-server-tests.txt`, `batch-app-tests.txt`, `batch-protocol-tests.txt`, `batch-live-reads.txt`, and `artifacts/batch-tools/live-read-receipts.json`. Native receipts contain local document names and are not distributed in the app bundle.

## Practical quality trade-offs

Functional coverage stays tied to typed documented calls. Bounded pages/batches control response size and time spent holding TopSolid's modification lock. Batch writes reduce confirmations but increase the amount rolled back if one item fails. Per-item read failures improve resilience without hiding incomplete results. The existing provider, MCP, settings and vendor layers remain separate. Credentials are unchanged and no new external service is introduced. Deployment remains Windows x64 with the matching TopSolid SDK/runtime.
