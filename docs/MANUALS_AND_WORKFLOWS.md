# Manual review and executable workflow mapping

All five user-supplied PDFs were extracted page by page and indexed: **581 pages**. Review concentrated on PDM/document lifecycle, sketch frames/topology/constraints, shape creation, assembly positioning, and the complete ten-page CAM Automation guide. Relevant figures were rendered and inspected. This is a workflow review, not a claim to have validated every manual command in TopSolid.

## Sources and editions

| Local manual under the TopSolid 7.20 installation | Pages | Edition shown inside the PDF |
|---|---:|---|
| `Help/en/TopSolid'Design Tutorial/TopSolid'Design Tutorial.pdf` | 76 | Discover tutorial, copyright 2009 |
| `Help/en/TopSolid'Design Automation Guide.pdf` | 60 | EN v7.11, 2017 |
| `Help/en/TopSolid'Cam Automation Guide.pdf` | 10 | CAM Automation Introduction; no explicit revision stated |
| `Help/en/TopSolid'Design User's Guide.pdf` | 372 | EN v7.9, 2014 |
| `Help/en/TopSolid'Design Tutorial.pdf` | 63 | Design Basics v7.20, Rev.01, 2026 |

The two tutorial paths contain different documents. Installation location alone does not establish a manual's edition. Full paths, page counts, outlines and SHA-256 are recorded locally in `artifacts/manuals/manifest.json`. Extracted manual content is research material and is not bundled with Studio. Instructions to delete, check in, configure PDM, send email, or change settings in the tutorials were treated as source content, not user authorization.

Current signatures were checked against the [official 7.20 Automation reference](https://help.topsolid.com/7.20/en/TopSolid%27Automation/ReferencesHomePage.html) and compiled against installed 7.20.400.107 assemblies. The complete reference index remains searchable from MCP. See [coverage](api/COVERAGE.md) for each executable tool's source links.

## How the manuals changed this implementation

PDF page numbers below count from the first PDF page, including covers.

| Workflow and source pages | Implemented behavior | Boundary |
|---|---|---|
| PDM/projects/templates: User's Guide pp.23–32; Basics pp.6–10 | Discover projects, folders, templates and real document extensions; create a project/folder/document; explicitly open and save | PDM creation/open/save are outside the application geometry transaction, per current API remarks. Partial creation receipts survive setup failures. |
| Document revisions: Automation Guide pp.22–23 | Explicit target IDs; EnsureIsDirty inside the transaction; rebase nested element/profile/section handles to the new document revision | Source assembly definitions keep their original revision. Model instructions require using returned IDs for subsequent calls. |
| Synchronized documents: User's Guide pp.66–67 | Preview the affected synchronization group; recheck target state/group immediately before execution | Preview traversal is bounded to 50 documents. Changed scope invalidates approval. |
| Local sketch frames: Automation Guide pp.38–40; User's Guide pp.90–91 | Separate native 2D documents from planar sketches in 3D; XY/XZ/YZ placement, explicit origin, SI conversion; plane inspection | Contour coordinates are local to the sketch; world positions are not guessed from images. |
| Profiles and sections: Automation Guide pp.39–42; User's Guide pp.92–93 | Native line/arc contour creation, append to a sketch, existing section inspection, returned topology handles | Closed contours repeat their starting point. Since 0.5.6, drawing tools create no sections and reject section-creation options. Extrusion accepts the original sketch. Native closure and length are checked; input and output segment counts need not match. |
| Constraints: User's Guide pp.94–98; Basics pp.11–12, 49–50 | Fixed/unfixed segment or vertex; existing scalar document parameter edits with exact type/unit checks | This does not create arbitrary geometric/dimensional sketch constraints or edit every dimension shown in the UI. |
| Shapes: Basics p.13; User's Guide pp.110–113 | Extrude/revolve an existing section or whole sketch; loft existing profiles; through drilling on a part shape; native solid volume readback | Whole-sketch sections use TopSolid's implicit profile closure. General pocket/fillet/chamfer/Boolean creation has no verified public creation adapter here. |
| Assembly: Discover pp.60–62; User's Guide pp.134–138; Basics pp.38–46 | Include existing part/assembly definitions, fixed rigid positioning and translation; inspect resulting occurrences/transforms | No generic family-driver mapping, arbitrary mating constraint solver or in-place editing workflow. |
| User interaction: User's Guide pp.39–45 | Read the user's single selected entity/operation; display immutable action preview and tool receipts in Studio | No arbitrary native command invocation. A selection may be empty when zero or multiple items are selected. |
| CAM inspection/update: CAM Guide pp.3–8 | Exact existing parameter IDs, scalar parameter edits, one-operation calculation and bounded toolpath reads | Bound/feedrate/spindle/tool/geometry parameters require dedicated adapters. Toolpath revolution speeds are rpm, as specified by IToolPath; other geometric/angular quantities use their documented units. |
| CAM creation/simulation examples: CAM Guide pp.9–10 | Guide reviewed; `ISimulation`/`IVerify` actions follow the documented modification transaction | Simulation starts for one existing operation; verification starts for one or all operations. The native animation is not NC generation, machine execution, or collision-safety certification. Strategy creation, postprocessing and machine/NC execution remain unexposed. |

## Example Studio requests

- “Create a new part called Bracket in this project, then open it.” The model first obtains a real destination ID and template or extension; each action is confirmed.
- “Create an 80 by 50 mm rectangle in the active part and extrude it by 20 mm.” The model obtains the current document, creates a sketch, passes its returned sketch handle directly to extrusion, and uses the new revision IDs. No section is created.
- “Extrude the sketch I selected by 15 mm along positive Z.” The model reads the user's selection and can pass the whole sketch to extrusion.
- “Set this Length parameter to 25 mm.” The model reads type/current value, proposes `0.025` SI metres with `unitType=Length`, and waits for confirmation.
- “Include this part in the assembly at X=120 mm and fix it there.” Source and target revisions are resolved before the inclusion preview.
- “Recalculate this CAM operation and show its first 25 toolpath rows.” Calculation requires confirmation; the subsequent read is inspection. Neither is NC generation or a collision-safety assertion.

## Why this does not claim the entire TopSolid UI

Manual commands describe product workflows; they do not establish callable Automation functions. The public reference exposes broad inspection and a narrower set of direct creation APIs. This server exposes typed, compiled adapters for verified methods and reports unimplemented workflows. Extending the remaining domains requires a documented/public or separately verified internal contract, an explicit target/effect preview, correct transaction handling, and representative native fixtures.

The current executable surface has **192 tools**, including **64 confirmed actions**. The [0.4 batch expansion](BATCH_TOOLS.md) adds detailed lists, parameter/element/point edits, explicit deletion, multiple sketch profiles and multiple extrusion/revolution features. Provider protocol, MCP protocol, settings, and vendor access remain separate. See [validation](../VALIDATION.md) for the precise runtime evidence.
