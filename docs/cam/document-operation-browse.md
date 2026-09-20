# Project, CAM document, and operation browsing

Unqualified CAM operation list requests now follow a local Studio workflow:

1. Select a working project from a paged, receipt-backed project list.
2. Browse that project's folders and select a CAM document. Non-CAM documents are filtered out. Back returns to the parent folder or project picker.
3. Display only operations belonging to the selected document, with its project/folder/document path in the dialog. Explicit current/active-document commands retain their direct behavior. Named document queries retain the existing resolver.

The 2026-09-20 conversation log showed `SelectionRequest` collecting document lists and CAM operation lists independently, before selecting a document. Their rows were merged into one picker. A subsequent project/document request repeated the same problem. `CamDocumentBrowseRequest` now handles these staged requests before the generic list and selection paths; selecting a parent immediately drives the next scoped read.

Document IDs and PDM IDs remain distinct. Every selection must match a displayed receipt. Operation reads always include the selected document ID, including continuation pages, and reject foreign document identities. Filtered-out pages still advance using raw server offsets. Folder visits and page counts are bounded; failures remain explicit instead of falling through to model inference. Reads do not recursively scan whole projects during normal browsing.

PDM child rows include `isLoaded`. If necessary, opening uses the existing prepared, confirmed action. The proposal must match the requested document, and the open receipt supplies any changed revision ID. Selecting a document alone is not authorization to modify it. No save, calculation, postprocessing, or simulation is part of browsing.

Validation on 2026-09-20:

- Debug build succeeded with no warnings or errors, updating the Studio and bundled MCP path configured in the attached log.
- 53/53 harness checks passed including the real configured MCP process. Staged regression coverage includes project paging, filtered document pages, nested folders, Back, empty folders, cancellation, malformed pagination, forged selections, foreign operation IDs, denied/tampered opening, and changed revision IDs.
- `--cam-document-browse-ui` rendered and inspected all four Korean dialog stages; Back, browse mode, document path, and unchanged settings checks passed. These are fixture renders without native geometry previews.
- `--cam-document-browse-live` traversed `TopSolid'Cam Presentation`, selected the already-loaded `5-axis retouched support plate`, and returned all 44 operations through the actual workflow callbacks. No model calls or native writes; active and selected document state were unchanged. Evidence: `artifacts/cam-document-browse/live.json`.
- Opening an unloaded native document was covered by the confirmation/revision harness, not executed in the live read-only fixture.
