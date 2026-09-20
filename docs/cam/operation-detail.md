# Operation Detail

Click an operation in the shared **CAM operations** dialog and choose **Detail**. Browsing, selecting operations, and showing a toolpath use the same header, search, grouping, preview, clear-selection action, and Detail action. Only the workflow footer differs: Close for browsing, Cancel/Continue for selection.

A normal click selects one operation. In workflows accepting multiple operations, Ctrl/Shift extends the selection. Each selected operation shows a Detail icon immediately to the right of its checkmark. The icon opens that row's exact operation, even when several operations are selected; its tooltip identifies that operation. Basic operation lists, scenario lists and summary lists resolve to the same exact native target. An explicitly requested toolpath starts with its verified operation selected. Unresolved workflow pickers never preselect a target.

Settings → Appearance contains preview defaults for newly opened dialogs. Edges default off; CAM part, stock, and machine default on. Save persists the defaults. Per-dialog toolbar toggles remain independent, and machine-element visibility remains export-specific. Part and stock can be hidden independently, including an empty view with all layers hidden.

The operation name and tool stay above eight Detail pages: Favorites, Tools, Cutting conditions, Geometry, Strategy, Comments (WCS), Multi axis, and Properties.

The dialog follows every `topsolid_list_cam_parameters` page. It displays all parameters exposed by the Automation API, including native subcategories, read-only values, and failed reads. Unknown categories appear under Properties. Search filters the current page; drafts remain intact when searching or changing pages. Stars are persistent Studio favorites, independent of TopSolid's own favorites.

Editable native scalars use numeric/text fields or the exact native enum/boolean choices. Lengths are entered in millimetres and angles in degrees; other dimensions explicitly use the returned SI unit type. **Review changes** uses the same prepare/confirmation/write/read-back workflow as the chat parameter editor. The user's Studio permission setting applies. Writes update the document but do not save it or calculate toolpaths.

Computed parameters, composite references such as Tool/Geometry/FeedRate/SpindleRate, missing metadata, and duplicate names belonging to different native owners cannot be changed through the current setter. Their values remain visible. The UI does not invent setters, enum choices, limits, or geometry references. Full native-dialog parity is constrained by the published Automation API.

Before writing, parameters are read again. Changed values, types, units, formulas or references require another review. Each accepted server revision is propagated to the parent operation selection. Multiple edits use individual native transactions: a declined or failed change stops the sequence, reports completed changes, refreshes all values, and retains valid unapplied drafts. There is no automatic write retry or implied batch rollback.

## Verification

Run `dotnet run --project scripts/OperationDetailChecks/OperationDetailChecks.csproj -c Debug` for deterministic coverage and English/Korean light/dark screenshots. This includes the operation-dialog Detail button, all eight pages, pagination, ambiguous parameter owners, SI conversion, native choices, confirmation decline, revision continuity, stale edits, and favorites.

The optional `--live <MCP-server.exe>` argument performs read-only inspection of the first open CAM document. It requires exactly one supported local TopSolid instance and records data/screenshots under `artifacts/operation-detail-checks`. It never invokes a native write.

On 2026-09-20, the configured Debug server returned 547 parameters from the open five-axis multi-blade operation; 333 had editable metadata. Document identity/dirty state remained unchanged. The focused harness and the existing 51-check regression suite passed. Native modification of the user's machining document was not exercised by these tests.
