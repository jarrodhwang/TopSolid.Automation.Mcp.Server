# TopSolid object identity and CRUD — 0.5.0

This release adds **10 tools**, giving **163 tools in 21 categories: 117 read/reference tools and 46 confirmed actions**. TopSolid calls remain inside the separate MCP console server. The Studio chat receives typed identifiers and results through MCP.

## Source review

The installed `TopSolid'Design Automation Guide.pdf` has 60 PDF pages and identifies itself as **EN v7.11**, copyright 2017. The installation directory name does not make this a 7.20 edition. The review covered the guide, with particular attention to printed pages 9–10 (application modification), 12–15 (PDM and revisions), 18–19 (documents and dirty revisions), 22–23 (elements, names and topology), 26–27 (parameters), and 34–38 (sketch workflows).

Source SHA-256: `0bda2e62f486b3630ebfb340ef758ecd085d28d98949c07cb7a8894affb7ea6d`.

Bindings were checked against the cached official **7.20** contracts and compiled against installed **7.20.400.107** assemblies. The [coverage report](api/COVERAGE.md) lists the contracts for each actual tool. Runtime API references resolve to bundled files under `TopSolid.Automation`; the server does not browse the website to choose or execute a function.

## Identifier and name model

| Concept | Meaning in TopSolid | Tool behavior |
|---|---|---|
| `PdmObjectId` | A managed object such as a project, folder or document | Preserve the opaque ID. Read its PDM type, owner, state and name; never parse a GUID from its string. |
| `PdmMajorRevisionId` | A major revision of a PDM object | Enumerate through `IPdm.GetMajorRevisions`. Keep the revision ID separate from revision text. |
| `PdmMinorRevisionId` | A minor revision within a major revision | Enumerate through `IPdm.GetMinorRevisions`; use the minor revision to resolve a historical document. |
| `DocumentId` | One document revision | Use `GetDocument` for the latest backing revision or `GetMinorRevisionDocument` for an explicit minor revision. Check existence and document type. |
| `ElementId` | An element identifier within a particular `DocumentId` | Preserve both fields. Elements can be entities, operations or other kinds; classify through the native interfaces. |
| `ElementItemId` | An `ElementId` plus an `ItemLabel` | Preserve the label and element together when selecting a face, edge, sketch profile or section. A face is not a standalone document element. |
| `typeGuid` | A document/element **type** identifier | Shared by instances of that type; never use it as the identity of the selected object. |
| Universal identifier | Document `(domain, name)` pair | Separate from names and type GUIDs. Useful for locating definitions transferred between PDMs; set deliberately and check for an existing match. |
| PDM name | The object's PDM name | Return beside its PDM ID. Duplicate names remain separate candidates. |
| Element `name` / `friendlyName` | Internal name / displayed name | Return both. A translated system label does not replace its untranslated internal name for `SearchByName`. |

The official [document contract](https://help.topsolid.com/7.20/en/TopSolid%27Automation/api/kernel/TopSolid.Kernel.Automating.IDocuments.html) defines the PDM/revision mappings and the separate type/universal identifier methods. The [element contract](https://help.topsolid.com/7.20/en/TopSolid%27Automation/api/kernel/TopSolid.Kernel.Automating.IElements.html) distinguishes internal names, friendly names, unique names and editability.

`HasName` is not permission to rename. Rename tools now check **`IsRenamable`**, reject changes to reserved system names, and check for collisions where `HasUniqueName` applies. They verify the value after `SetName` and roll back the document modification on mismatch.

Names are selection aids. Friendly-name lookup returns all exact matches with `resolution: notFound | unique | ambiguous`. The model must select a real handle, or ask the user to distinguish candidates, before proposing a change. Queries do not silently pick the first duplicate.

## Correction: projects and libraries have backing documents here

The previous 0.4.3 implementation assumed that creation-date ordering depended on the separate PDM Explorer service. Native inspection of this 7.20 installation disproved that restriction:

1. `Documents.GetDocument(projectPdmId)` returned a real `TopSolid.Kernel.DB.Projects.ProjectDocument`.
2. `Documents.Exists` succeeded and `Documents.GetPdmObject` returned the original project ID.
3. `Parameters.GetCreationDateParameter` returned an existing `DateTime` parameter.
4. `Parameters.GetDateTimeValue` provided the creation date.

The same path worked for library projects. A PDM object classified as `WorkingProject` or `LibraryProject` can therefore have a backing metadata document. This does **not** mean that it is a part, sketch or assembly document. Tools inspect the returned document type rather than guessing from the PDM classification.

Project/library listing now uses this verified kernel path when dates are requested. It sorts all rows before paging and returns `creationDateSource: backingDocument.creationDateParameter`. Name-only lists avoid the extra date queries. No Explorer connection or modification-date substitution is used. Missing parameters, invalid mappings or ambiguous dates remain explicit; a failed chronological query does not return an unsorted list as if sorting succeeded. Dates preserve the API's `DateTime.Kind`; the server does not invent a timezone for unspecified values.

An initial native run sorted **51 projects in 645.89 ms** and **66 libraries in 966.03 ms**. The final packaged-server repeat returned the same totals in **124.91 ms** and **94.30 ms** with the native session already warm. These are MCP query measurements from this session, not model response-time guarantees. The additional project already existed in the user's session; this release's validation did not create it.

## Added tools

| Tool | Purpose |
|---|---|
| `topsolid_get_object_model` | Compact offline reference for the identifier/name hierarchy and modification rules. |
| `topsolid_inspect_document_identities` | Batch document-to-PDM, major/minor revision, type and universal-ID mappings. |
| `topsolid_resolve_pdm_documents` | Batch PDM-to-backing-document mappings with existence and reverse-mapping checks. |
| `topsolid_find_pdm_documents` | Exact PDM name or universal-ID lookup, optional project scope, all returned candidates. |
| `topsolid_list_pdm_document_revisions` | Major/minor relationships, revision text and historical document IDs for a TopSolid PDM document. |
| `topsolid_find_named_elements` | Exact internal/friendly-name matches within one document revision, with type and native classification. |
| `topsolid_set_document_universal_id` | Confirmed set/remove of the domain/name pair, collision check and readback. |
| `topsolid_update_pdm_objects` | Confirmed batch name/description updates on exact ordinary project, folder or document IDs. |
| `topsolid_delete_pdm_documents` | Confirmed PDM document deletion, excluding projects/folders and permanent purge. |
| `topsolid_restore_pdm_documents` | Confirmed restoration of deleted PDM documents using retained IDs; not restoration of an older revision. |

The existing inspect/list-element tools also return internal name, friendly name, type GUID, owner, `hasName`, `hasUniqueName`, `hasSystemName`, `isRenamable`, `isEntity` and `isOperation`. Transform tools reject non-entities. Parameter lists distinguish internal and friendly names.

## CRUD boundaries

| Scope | Create | Read | Update | Delete / restore |
|---|---|---|---|---|
| PDM | Existing project/folder/document creation; document extension or template must come from live data | Named lists, metadata, lookup, revision mapping | New batch name/description changes | New document deletion/restoration; no project/folder deletion or purge |
| Document revision | Resolve the document returned by PDM creation | Exact revision identity and properties | Content transaction; new universal-ID edit; save remains separate | Deleting PDM documents and deleting their contained elements are different operations |
| Elements/entities | Existing typed point, parameter, sketch, shape and assembly tools | Named batches, classification and exact lookup | Existing batch edits/transforms with stronger name and entity checks | Existing `DeleteSeveral` element tool checks deletability within a document transaction |
| Topology items | Produced by typed native geometry tools | Face/edge/profile/section handles include their parent element | Pass the actual native item handle to supported operations | No generic guessed face/edge deletion wrapper |

### Document-content changes

1. Prepare the exact target and synchronized document group for human confirmation.
2. Recheck the preview immediately before execution; reject a changed target.
3. Start a document modification and call `EnsureIsDirty(ref documentId)`.
4. Rebase target element/item handles to the returned document revision. Preserve external source-document references.
5. Execute the typed operation and check its result. Sketch edits use their documented nested modification scope.
6. Commit on success; attempt rollback on failure. Saving is a separate action.

Receipts now include the original/current `DocumentId`, `PdmObjectId`, `PdmMinorRevisionId` and `saved: false`. Clients must continue with the returned current revision. Geometry conversion keeps TopSolid SI values and sketch-local frames explicit.

### Persistent PDM changes

PDM creation, metadata, deletion and restoration are outside the document geometry undo transaction. Each new action requires confirmation, exact IDs and an unchanged target preview. A batch stops at its first failure. Receipts retain completed IDs/properties, the uncertain item and the number not attempted; the server neither claims rollback nor automatically retries.

Deletion is restricted to TopSolid or external PDM document objects. It uses native `IPdm.DeleteSeveral` and checks existence/state afterward. Restoration requires the deleted state and uses `IPdm.Restore`. Native reference, ownership and permission constraints still apply.

## Efficiency and practical quality review

| Quality | Implementation and trade-off |
|---|---|
| Functional suitability | TopSolid identity domains and native type/editability predicates determine behavior. This is a selected typed tool set, not a claim to wrap the entire software. |
| Reliability | Single-use confirmations, target revalidation, revision rebasing, readback and explicit partial PDM outcomes. No automatic retry of writes. |
| Performance | Up to 100 identities per read request and 32 objects per write request. Detailed results use bounded pages and continuation offsets. Native per-object queries still occur inside the server where no bulk vendor API exists; the model does not need a round per object. |
| Maintainability | Shared `ObjectIdentity` conversion/guards and separate PDM/document/entity tool files; compiled calls with locally traceable source contracts. |
| Compatibility | Tested with installed TopSolid 7.20.400.107. The older guide is conceptual guidance; live behavior and current contracts are distinguished. |
| Security | Native access remains in MCP, with existing credential protection and redacted logs. New writes use the existing explicit human-confirmation path. |
| Usability | Return friendly labels alongside exact IDs, preserve ambiguous candidates, explain persistent effects, and retain actionable receipts. |
| Portability | Versioned Windows bundle, local reference corpus, no required PDM Explorer service. TopSolid/.NET/Windows prerequisites remain. |

For quality in use, batches reduce user/model effort; duplicate-name handling and clear failure receipts help users complete the intended task without selecting the wrong object. Missing dates, unloaded/invalid revisions and partial failures are represented explicitly. Friendly-name searching still scans a document's elements; it stops above 10,000 elements and asks for a narrower collection. It is not a global fuzzy search engine.

## Validation and limits

- Release build: **zero warnings/errors**.
- Offline server suite: **2,808 checks**, including ambiguous names, actual rename eligibility, universal-ID argument rules, deletion target/state restrictions, partial PDM outcomes, confirmation, revision rebasing and local API references.
- Stdio protocol suite: **860 checks**, run with `-SkipTopSolidQueries`; includes discovery, local source files, blocked unapproved actions, reference retrieval and malformed input.
- Application suite: **26/26 groups**, including cloud/local model-loop fixtures, confirmation/denial, persistent receipts, complete PDM inventory rendering and a real MCP process. These are not new authenticated cloud inference measurements.
- Native identity harness: **13 read-only calls** covering project/library creation-date ordering, backing documents, document/revision mappings, name lookup and entity/operation classification.
- **250 declared binding pages** found in the local 6,898-symbol / 3,335-page reference corpus.
- Versioned package: both executable versions are **0.5.0.0**. Published Studio DLL/server executable hashes match the tested Release outputs; the 13 native calls also passed using the packaged server. The unused PDM Explorer assembly is excluded from this bundle.
- **New native write paths were not executed. No document was opened, created, modified, saved, deleted or restored by this validation.** They are compiled and covered by offline guard/receipt tests; native geometry and persistent PDM mutation success are not proven.
- Full interactive Studio GUI testing was not repeated for this release. Existing GUI evidence belongs to its recorded release.

Reproduction commands and historical evidence are in [VALIDATION.md](../VALIDATION.md). Private live receipts remain under `artifacts/identity-review` and are excluded from the distributed bundle.
