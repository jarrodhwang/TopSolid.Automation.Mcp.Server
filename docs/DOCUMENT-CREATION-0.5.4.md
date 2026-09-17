# Document creation by extension — 0.5.4

## Behavior

Ordinary document requests now create an **empty native document by extension**, without a template. No existing part needs to be loaded or opened first.

| Request | MCP/native route |
|---|---|
| Empty part | `topsolid_create_part_document(ownerId,name)` → `IPdm.CreateDocument(ownerId,".TopPrt",false)` |
| Other native document | `topsolid_create_document(ownerId,name,extension)`; omitted `useDefaultTemplate` means **false** |
| Requested specific template | `topsolid_create_document(ownerId,name,templateId)` → `IPdm.CreateDocumentWithTemplate`; omit extension and the default-template flag |
| Explicitly requested configured default template | Supply extension and `useDefaultTemplate=true` |
| Project (`.TopPrj`) | `topsolid_create_project`, using the project API |
| External file (`.bin`, `.pdf`, `.png`, `.topfud`, `.txt`, `.xml`) | Requires source-file import; rejected by empty native-document creation |

The local `topsolid_list_document_types` catalog contains all **101 supplied extensions**: 94 native document identifiers, one project identifier and six external formats. It works without a TopSolid connection. `.TopPdf` is a native identifier; `.pdf` is an external format. Explicit installation-specific `.Top...` identifiers are accepted as inputs and validated by the native host. Catalog membership does not establish module licensing or successful creation of every document type.

Preview and creation receipts expose `creationMode` (`empty`, `defaultTemplate`, or `specificTemplate`), extension and template choice. A requested template remains available, but the model is directed to browse templates only when the user requests one. Confirmation remains required. Creation is persistent PDM work outside geometry undo; it does not automatically open, save or check in the document. Partial-creation receipts are retained and writes are not automatically retried.

## Findings from the supplied 18:30 log

- The first request ran **nine model rounds** and ended after **9.85 s** with no part. The server required a loaded `PartDocument` to establish `.TopPrt`, then the model searched template projects and libraries. This prerequisite was an application restriction, not the documented creation contract.
- Repeated plain-part requests hit the same unnecessary prerequisite. The later generic creation call explicitly sent `useDefaultTemplate=true`; it did not follow the user's no-template preference.
- An active native command caused a creation failure. The new context reports command readiness. Both the confirmation preview and execution check `ActiveCommandName` / `ActiveCommandFullName`; they stop before creation and tell the user to finish or cancel that command. They never cancel it automatically. A native command starting after the final check can still cause a host rejection.
- A short “now try again” lost the original workflow's supplied schemas, adding selector rounds for opening and sketching. Tool exposure now retains up to eight recent user turns as local selection context, within the existing 24-schema initial budget.
- The final attached receipts report successful part creation, opening, circle creation and a native parabola with 17 readback samples and no sections. This is user-provided runtime evidence for those operations. It does **not** establish associative regeneration. The final values were radius 50 mm and focal length 25 mm despite absent sizes in the original request; the existing prompt continues to require a combined question for missing dimensions and visible confirmation of geometry.

## Fewer calls

`topsolid_get_document_creation_context` now returns the destination matches, selected extension, no-template defaults and readiness together. It does not inspect loaded documents, enumerate their native types, resolve sample revisions, or browse templates. The part action does not repeat any type discovery during preview/execution. Known parts need no catalog query.

The installed host's `SearchProjectByName` returned no result for the capitalization used in the log. The implementation therefore compares names from `GetProjects(true,false)` in one MCP request, preserving case-insensitive matching and every duplicate. The public API still requires an internal `GetName` per project. The 1,000-project bound reports an incomplete search instead of claiming uniqueness. No persistent name cache introduces stale destination identities.

The direct, explicit quoted-name part request uses one destination lookup and the existing confirmation path with no model inference. Compound design requests continue through the AI so missing geometry information can be resolved.

## Validation

- Debug and Release: **zero warnings/errors**. The configured Debug bundled executable was rebuilt and protocol-tested.
- **6,106 server checks**, **977 protocol checks**, **30/30 Release application groups** passed. Coverage includes cold/no-loaded-type planning, no-template defaults, explicit template opt-in, ambiguous/incomplete destinations, busy-command rejection, external-file rejection, complete catalog pagination, confirmation/partial receipts and retry schema retention.
- **169 MCP tools**: 121 inspection/reference tools and 48 confirmed actions. All **272 declared API source pages** resolve locally.
- Live Release lookup: **0.155 s**. Direct named-part lookup plus declined preview: **0.009 s**. Empty previews passed for `.TopPrt`, `.TopAsm`, `.Top2D` and `.TopMillTurn` with no template. Case-variant project lookup passed.
- One live configured **`gemini-flash-lite-latest`** run reached the correct empty-part proposal in **2.37 s**: three model rounds, one native PDM read and one declined preview, with no template/loaded-document/library/API-reference searches. One round was a redundant schema selection by the model. These are single-run measurements, not guarantees for other models, networks or datasets.
- All live changes were declined or only prepared. **No new native document was created in this validation**, and timing excludes actual creation and human review. Saved settings were unchanged. No new raster UI validation was performed.

Evidence: `artifacts/document-creation-0.5.4/{server-tests,protocol-tests,application-tests}.txt`, `live-preview.json`, `live-model.json`, and `package-verification.json`.

## API sources and boundaries

- [IPdm.CreateDocument](https://help.topsolid.com/7.20/en/TopSolid%27Automation/api/kernel/TopSolid.Kernel.Automating.IPdm.CreateDocument.html): extension begins with a dot; the boolean selects whether to use an available default template; creation must be outside `StartModification` / `EndModification`.
- [IPdm.CreateDocumentWithTemplate](https://help.topsolid.com/7.20/en/TopSolid%27Automation/api/kernel/TopSolid.Kernel.Automating.IPdm.CreateDocumentWithTemplate.html): explicit native template route, verified against the local reference and installed assembly.
- [IApplication.ActiveCommandName](https://help.topsolid.com/7.20/en/TopSolid%27Automation/api/kernel/TopSolid.Kernel.Automating.IApplication.ActiveCommandName.html) and [ActiveCommandFullName](https://help.topsolid.com/7.20/en/TopSolid%27Automation/api/kernel/TopSolid.Kernel.Automating.IApplication.ActiveCommandFullName.html): read-only command state; null when idle.

Public bindings compile against the installed **7.20.400.107** Automation assembly. Extension identifiers came from the user's installation list and supplied runtime log. Runtime tools use the bundled local references; neither creation nor the local catalog searches the website. The desktop continues to access TopSolid exclusively through MCP.
