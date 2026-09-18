# Validation — 2026-09-18, current workspace

## Studio/MCP 0.5.14 — TopSolid preview controls and precision

- Release/Debug solution builds passed with zero warnings/errors. **39/39 application groups**, **15,256 offline server checks**, and **2,094 protocol checks** (native queries disabled) passed. The configured Debug MCP executable was rebuilt and exercised.
- WPF light/dark/Korean fixtures verified Ctrl + right / wheel-button rotation, right-button pan, ignored left dragging, stable gestures/capture cancellation, explicit/default colors, proposed/native STL/GLB modes, thin black outlines and narrow layouts. Images were visually inspected; they use offscreen software rendering.
- Preview geometry tests cover 0.05 mm chord / 5° angular limits across several radii, sharp edges with screen-stable width, malformed/oversized STL and GLB, model/approval immutability, cancellation and maximum permitted base64 transport.
- Read-only native cube probe: 12 triangles, 684-byte binary STL, 40 × 40 × 40 mm; document identity, dirty state and shape inventory unchanged. Its existing 0.2 mm / 15° display setting remained unchanged while export options requested 0.05 mm / 5°.
- Read-only curved-tool probe: **225,484 triangles**, 11,274,284 bytes, 63 × 63 × 108 mm. Export/transport **1.26 s**, decode/build **0.73 s**; 24-direction CPU edge preparation median **11.65 ms**, p95 **23.58 ms**. Probe peak working set **562 MiB**. Document state, inventory and its 0.2 mm / 15° display setting were unchanged. No CAD writes.
- This host exposes NVIDIA RTX PRO 3000 Blackwell Laptop and Intel Graphics, with WPF hardware capability tier 2. Production retains the Windows-selected Direct3D hardware path; active NVIDIA/Radeon adapter use and GPU frame rate were not measured. Edge timings are CPU preparation measurements.
- Evidence: `artifacts/graphic-preview-0.5.14`, `artifacts/ui-redesign/graphic-*.png`. Bundle: `artifacts/TopSolid-AI-0.5.14`. [Precision, memory limits, GPU behavior and test prompts](docs/PREVIEW-CONTROLS-0.5.14.md).

## Studio/MCP 0.5.13 — flat reviews and native CAM icons

- Release/Debug builds passed with zero warnings/errors. **39/39 application groups**, **15,249 offline server checks**, and **2,094 protocol checks** (native queries disabled) passed.
- Separate read-only native CAM verification: seven exact operation names/numbers and NC type icon mappings; **649 parameters**, eight cutting-condition samples, zero failed rows. Document and operation states unchanged; zero native writes.
- WPF fixtures passed flat profile/coordinate layout, lazy detail disclosure, light/dark/Korean, compact action visibility, original operation/category images, all packaged icon resources, immutable approval/selection, cancellation and existing graphical preview behavior. Captures are offscreen software renders, not a GPU performance benchmark.
- The source icon registry provides 132 exact native class mappings. Unknown operation types/categories use neutral fallback symbols. General arguments and CAD parameters no longer use CAM milling icons.
- Evidence: `artifacts/cam-review-0.5.13`, `artifacts/ui-redesign/approval-sketch-flat-*.png`, and `question-cam-native-names-icons.png`. Bundle: `artifacts/TopSolid-AI-0.5.13`. [Behavior, trade-offs and test prompts](docs/REVIEW-AND-CAM-ICONS-0.5.13.md).

## Studio/MCP 0.5.12 — native graphical review

- Release/Debug builds passed with zero warnings/errors; configured Debug MCP output rebuilt. **39/39 application groups**, **15,225 server checks**, **2,124 protocol checks** passed.
- Read-only native GLB export and parsing passed for the open test part; approximately 40 mm cube, 12 triangles. Document identity, dirty state and shape inventory were unchanged. Measured export 68.6 ms, parse/frozen mesh build 62.8 ms; hardware capability tier 2. No CAD writes or large-model/GPU frame-rate claim.
- UI fixtures passed proposed/current modes, exact receipt scope, light/dark/Korean, responsive layout/refit, camera controls, stale replies, clearing selection, transport cancellation and independent approval on preview failure. Offscreen software-rendered dialogs were visually inspected.
- Native GPU-capable WPF viewport is separate from the in-process TopSolid editor. Previews show document context; exact sub-entity picking and CAM simulation are not included. Supported proposed geometry: cylinder and extruded rectangle. [Behavior, architecture and bounds](docs/GRAPHIC-PREVIEW-0.5.12.md).
- Evidence: `artifacts/graphic-preview-0.5.12`; bundle: `artifacts/TopSolid-AI-0.5.12`; UI images: `artifacts/ui-redesign/graphic-*.png`.

## Studio/MCP 0.5.11 — question dialogs

- Release and Debug solution builds succeeded with zero warnings/errors. **38/38 application groups passed**, including a real MCP handshake against the configured Debug bundle and native WPF behavior. The local server reported TopSolid connected.
- A live, read-only active-document result was converted to a question card. Its friendly name and the exact returned document identity were verified; **no native CAD changes were made**.
- Question regression cases cover immutable receipt-backed targets, duplicate names, rejected source paths, typed number/unit/culture and color validation, image parts and reference-data boundaries, separate change approval, cancellation, preserved answers after provider failure, direct project ambiguity, and the Ollama HTTP path where call IDs are absent from the wire.
- Offscreen WPF fixtures passed for search, persistent single/multiple selections, visible target summaries, clearing, numeric/color validation, image preview, modal answers and MainWindow cancellation. English/Korean and light/dark renders are under `artifacts/ui-redesign/question-*.png`; card and color renders were visually inspected.
- Framework-dependent bundle: `artifacts/TopSolid-AI-0.5.11`. Application test evidence: `artifacts/question-dialog-0.5.11/application-tests.txt`. Live AI-provider inference and native viewport geometry picking are not claimed. [Contract and limits](docs/QUESTION-DIALOG-0.5.11.md).

## Studio/MCP 0.5.10 — CAM parameters and User Mode

- Debug and Release builds passed with zero warnings/errors. **15,225 server checks**, **2,086 protocol checks** against the configured Debug bundled server, and **37/37 application groups** passed. The full offscreen UI suite also passed, including native-shaped CAM approval, whole-change scope, live language switching, loading phases, cancellation/failure cleanup, unchanged settings and display-only identity resolution.
- **187 tools / 22 categories / 128 inspection-reference tools / 59 confirmed actions.** All **412 declared API source pages** resolve in the local corpus. Existing tool names and execution identifiers are retained.
- Live read-only inspection of the already-open CAM document returned all **649 parameters** of the second operation, **zero failed inventory rows**, and eight detailed cutting-condition samples. Five native bound-value component faults remain explicit metadata errors. The largest serialized model receipt was **51,469 characters**, below the 64,000-character limit. Document identity/dirty state and operation summaries were unchanged; **no native writes were performed**.
- Parameter editing checks exact native types/units, read-only status and enum choices before approval. Readback verifies the requested literal definition as well as type/unit/value, so retaining an equal-valued formula cannot masquerade as a successful literal replacement. Runtime native writes/regeneration remain unqualified.
- User Mode resolves document/operation/parameter names separately, including opaque revision IDs and numeric local identities. Approval values are initially visible, with full metadata collapsed; proposed real values are explicitly labelled SI. The native-shaped approval and loading images were visually inspected in light/dark/minimum-size fixtures.
- The matched public CAM setter supports scalar Real/Integer/Boolean/Text. Composite FeedRate/SpindleRate/Bound values are inspectable with explicit write limitations. No live model inference or provider quota test was performed; the supplied log's HTTP 429 remains a provider-side limitation.
- Evidence: `artifacts/cam-user-mode-0.5.10` and `artifacts/ui-redesign/approval-cam-*.png`, `loading-*.png`. Local framework-dependent bundle: `artifacts/TopSolid-AI-0.5.10`. The exact configured Debug MCP path was rebuilt. [Contracts, findings and limits](docs/CAM-USER-MODE-0.5.10.md).

## Naming/section source changes — build handed to the other session

- Removed hard-coded cylinder/sketch names; explicit names are reserved uniquely before confirmation, including batches. Ordinary sketch tools create no sections; a separate action is exposed only for explicit section requests.
- **14,286 server checks**, **2,004 protocol checks**, and live read-only collision/batch previews passed. No CAD writes. **187 tools**, **399 locally verified API pages**.
- Final Studio test rerun and Debug/Release packaging are left to the other active session at the user's request. Concurrent UI edits are preserved. The last application run was **31/32**; its prompt-budget failure was shortened in source, with final integration testing still pending.
- [Findings, behavior, measurements and build handoff](docs/NAMING-AND-SECTIONS-0.5.9.md). Evidence: `artifacts/naming-0.5.9`. No new complete bundle is claimed here.

## Studio/MCP 0.5.8 — prior validation

- Debug and Release built with zero warnings/errors. **14,072 offline server assertions**, **1,941 protocol checks** against the exact configured Debug bundled server, and **32/32 Release application groups** passed.
- **186 tools / 22 categories / 128 inspection-reference tools / 58 confirmed actions.** All **398 declared API source pages** resolve in the local corpus.
- Added cylinder creation/replacement, actual element/face colors, analytic 2D slots, 3D curve batches, a local modeling guide and native SmartReal parameter references for extrusion length, revolution angle and drilling diameter. Absolute-value modeling remains the default. Profiles are validated before confirmation; no sketch sections are created.
- Live read-only previews succeeded in the already-open part, including rejection of a sketch without profiles. The document identity, dirty state and shape/sketch inventories were unchanged. The part had zero shapes; the live appearance preview used an existing sketch. No native writes occurred.
- Final real-inference runs with synthetic context and declined proposals: **Gemini Flash Lite 1.14 s / 0.79 s**, **warm Gemma 4 E4B 1.17 s / 4.68 s**, for extruded/revolved red cylinders. One active-document read per run. Local Korean revolution needed two bounded schema repairs. These are planning times, not creation timings or Gemma 31B results. Earlier failures are retained in the evidence folder.
- Gemini client-created read context uses the documented signature marker; model-generated signatures remain intact. Read context is stored as tool data. WPF behavior and unchanged saved settings passed; no raster UI claim.
- Native geometry/coloring/replacement and dependency regeneration still require user-approved runtime qualification. 3D fillet/chamfer/Boolean/boss/pocket, surface trim and dimensional sketch constraints remain unimplemented; no corresponding verified public creation method was found in the reviewed interfaces.
- Evidence: `artifacts/modeling-0.5.8`; bundle: `artifacts/TopSolid-AI-0.5.8`. [Detailed findings, contracts, examples and limits](docs/MODELING-0.5.8.md).

## Studio/MCP 0.5.7 — prior validation

- Debug and Release built with zero warnings/errors. **13,139 offline server assertions**, **1,698 protocol checks** against the configured Debug bundled executable, and **31/31 Release application groups** passed.
- **181 tools in 22 categories: 127 inspection/reference and 54 confirmed actions.** All **387 declared API source pages** resolve in the complete local reference corpus.
- Added entity structure/children/properties/folder tools and typed parameter context/choices/formula/reference tools. Eight literal creation types and all 11 concrete ParameterType value adapters are compiled and contract-tested. Single and batch setters share guards and readback.
- Tests exercised injected native-interface responses for unset values, enum keys beyond page 1, type/unit/relay/parent guards, dates, colors, tolerances, codes and family references; actual installed SDK Smart constructors for all four expression types; confirmation rejection, ownership cycles and local-model schema selection.
- The installed SmartInteger(string) constructor produced Type=Item in a test despite its documented Formula behavior. Explicit type constructors are used and verified. This is SDK object construction evidence, not live native parameter creation.
- Both real-process status/application paths reported TopSolid unavailable while it was closed. **No native CAD writes were performed.** Formula evaluation/regeneration, parameter edits and entity-folder/appearance changes still require a user-confirmed native trial. No new live model timing or raster UI validation is claimed.
- Existing WPF blue replies, elapsed time, provider/MCP loops, approval flow, credentials redaction and unchanged saved settings passed. Compact local workflows expose relevant entity/parameter schemas immediately without loading the full catalog.
- Evidence: `artifacts/entity-parameters-0.5.7`; framework-dependent bundle: `artifacts/TopSolid-AI-0.5.7`. Exact configured Debug and Release outputs were rebuilt. [Concepts, methods, examples and limits](docs/ENTITIES-PARAMETERS-0.5.7.md).

## Studio/MCP 0.5.6 — prior validation

- Debug and Release build with zero warnings/errors. **10,966 server assertions**, **1,053 protocol checks** against the configured Debug bundle and **31/31 Release application groups** passed.
- **172 tools: 122 inspection/reference and 50 confirmed actions.** All 275 declared source pages resolve locally.
- Section creation and its arguments were removed. Drawing preserves native section count, failing with rollback on unexpected changes. Heart topology, dimensions, rotation, strict old-field rejection, ambiguous target refusal, context migration and the three-invalid-proposal limit passed regression checks.
- Live **Gemma 4 e4b**, fast replies enabled: **1.853 s** and **1.038 s** to a valid declined heart proposal, each with two model requests and one synthetic document-context read. Six schemas; 1,992 initial input tokens. The model was already loaded. These are **fixture planning benchmarks, not native CAD execution timings**.
- TopSolid was closed and the real status tool reported unavailable. **No native writes were executed.** Native heart construction, direct-sketch extrusion and combined PDM lookup remain pending live validation; no new raster UI validation is claimed. WPF behavior, blue replies, elapsed time and unchanged saved settings passed.
- [Full findings, geometry contract, compatibility changes and limitations](docs/SKETCH-RELIABILITY-0.5.6.md). Evidence: `artifacts/sketch-reliability-0.5.6`; bundle: `artifacts/TopSolid-AI-0.5.6`. Configured Debug outputs were rebuilt too.

## Studio/MCP 0.5.5 — prior validation

- Debug and Release: zero warnings/errors. **9,992 server assertions**, **1,000 protocol checks** against the configured Debug bundled executable, and **31/31 final Release application groups** passed. An additional **32/32** run included the live Gemini preview.
- **171 tools: 121 inspection/reference, 50 confirmed actions.** All **275 declared source pages** resolve locally.
- Native batch check-in and saving use exact reviewed/rechecked scopes, separate operation receipts and state readback. Default save scope is modified open documents with synchronized partners. Clear save/check-in commands require no model inference and retain explicit confirmation. Partial failures are never automatically retried.
- Live preview measurements: save scope **0.116 s** (zero dirty open documents), named project check-in **0.040 s** (eight objects), star/ellipse preview **0.033 s**. The document identity and dirty state remained unchanged. The five parts in the project reported `New`, despite the earlier model's check-in claim.
- Configured **Gemini Flash Lite** produced the correctly positioned star/ellipse batch proposal in **2.261 s**, with two model requests and no argument-repair round. The harness declined it. This differs from the log's tree workload and is not a latency guarantee.
- **No native writes were performed.** Live save/check-in execution and new star/ellipse creation are not claimed as validated; those need an explicitly approved operation. Ellipse planning uses a documented, bounded cubic approximation, not an analytic conic. Existing associative regeneration remains unverified.
- Blue replies, elapsed time, confirmation, model adapters and unchanged settings passed behavior tests; no new raster layout validation. [Detailed log review and contracts](docs/LOG-REVIEW-0.5.5.md). Evidence: `artifacts/persistence-sketch-0.5.5`; bundle: `artifacts/TopSolid-AI-0.5.5`.

## Studio/MCP 0.5.4 — prior validation

- Debug and Release built with zero warnings/errors. **6,106 server checks**, **977 protocol checks** against the configured Debug bundled executable, and **30/30 Release application groups** passed.
- **169 tools: 121 inspection/reference tools and 48 confirmed actions.** All **272 declared source pages** resolve locally. The new local extension catalog covers the user's 101 identifiers with separate native-document, project and external-file routes.
- Empty native document creation defaults to extension plus `useDefaultTemplate=false`, with no loaded-part or template prerequisite. Explicit specific/default templates remain opt-in. Preview and execution check active commands; confirmation, partial receipts and no automatic write retries are preserved.
- Live Release context lookup: **0.155 s**; direct named-part lookup plus declined preview: **0.009 s**, with no inference. Empty previews passed for four native document types. The configured **Gemini Flash Lite** model reached an empty-part proposal in **2.37 s**, with one PDM read and three model rounds. The proposal was declined; these timings do not measure actual creation or human review.
- No native writes were executed during this validation. The user's supplied log separately records successful circle/parabola creation in 0.5.3; native associative regeneration remains unverified.
- WPF behavior, confirmation and settings-preservation checks passed; no new raster UI validation. Evidence: `artifacts/document-creation-0.5.4`. [Detailed log review and API contracts](docs/DOCUMENT-CREATION-0.5.4.md).

## Studio/MCP 0.5.3 — prior validation

- Debug and Release built with zero warnings/errors. **5,952 server checks**, **970 protocol checks** against the configured Debug bundled executable, and **30/30 Release application groups** passed.
- **168 tools: 120 inspection/reference tools and 48 confirmed actions.** All **270 declared API source pages** resolve in the local reference cache. The new sketch batch, context and coordinate-conversion tools are available in the initial sketch workflow selection.
- Live read-only checks in the already-open part verified native frames, topology handles, friendly-name lookup, curve samples, coordinate conversion and associative-reference previews. The final preview took **0.036 s**; the document ID, dirty state and sketch inventory were unchanged. No native writes were performed.
- A separate local **GPT-OSS 20B** run asked for missing circle/parabola dimensions in **53.72 s**, with zero proposed or executed writes. This does not establish Gemma/Gemini latency or successful native creation.
- Open paths no longer force a closed profile. New pure tests cover parabola controls, local/world coordinates, batch limits, reference validation, null topology labels and explicit native Smart-point item types. Sections remain opt-in. Reference placement defaults to associative links with the SDK limits documented in [Sketch2D contracts](docs/SKETCH2D-0.5.3.md).
- **Native creation and regeneration remain unverified.** The isolated [validation plan](docs/sketch2d-native-validation-plan.json) is implemented behind an explicit opt-in test. It requires user approval before creating a dedicated part, drawing the curves, creating a linked sketch and moving its reference to verify follow behavior. Preview success and compiled Smart inputs do not prove native dependency regeneration.
- WPF behavior checks passed, including blue replies, elapsed time, confirmation and unchanged saved settings. No raster screenshot validation is claimed.
- Evidence: `artifacts/sketch2d-0.5.3`. The framework-dependent release bundle is `artifacts/TopSolid-AI-0.5.3`. Published Studio/server hashes match the tested Release outputs; both report 0.5.3.0. The configured Debug bundled server also reports 0.5.3.0. Hash/version records are in `package-verification.json`.

## Studio/MCP 0.5.2 — prior validation

- Debug and Release built with zero warnings/errors. **2,872 server checks**, **876 protocol checks** against the configured Debug bundled executable, and **31/31 Release application groups** passed.
- **165 tools: 118 read/reference tools and 47 confirmed actions.** New tools provide one-call document creation context and confirmed native part creation from observed loaded part types. All **250 declared API source pages** resolve locally.
- The exact alphabetical request in the log completed in **0.227 s** with zero inference; all names and duplicate records are rendered from MCP. Chronological projects took **0.086 s**; explicit named-part lookup plus declined preview took **0.085 s**.
- Local **GPT-OSS 20B** handled the reported compound part/circle request through a declined part-creation preview in **16.82 s**, with one read and two model rounds. No native writes were executed. Human review, actual creation/modeling and Gemma 31B inference were not benchmarked.
- WPF behavior checks include the new timeout/thinking controls, blue replies, elapsed time and unchanged saved settings. No raster screenshot validation is claimed; the existing blank screenshot limitation remains.
- The final versioned bundle is `artifacts/TopSolid-AI-0.5.2`. [Log review and limits](docs/LOG-REVIEW-0.5.2.md); evidence in `artifacts/log-review-0.5.2`.

## Studio/MCP 0.5.1 — prior validation

- Both Debug (the path in the supplied log) and Release built with zero warnings/errors. **2,816 server checks**, **861 protocol checks**, and **29/29 application groups** with `--ui-behavior --live-pdm-fast` passed.
- Live connected-session PDM measurements: sorted project list **0.177 s**, both name lists **0.019 s**, declined project preview **0.003 s**, and named-part lookup plus declined preview **0.075 s**. Zero model requests and zero native writes. Execution after approval and human review time were not measured.
- WPF controls verified blue assistant replies, per-response elapsed time, the real Send path and unchanged saved settings. The separate raster screenshot check remains blank/failing; this release does not claim a passing screenshot.
- Sketches now default to no sections; explicit sections and combined solid creation remain supported. New geometry behavior is compiled/offline-tested, not proven through new native mutations.
- [Details, scope and evidence](docs/PDM-AND-CHAT-0.5.1.md); artifacts are under `artifacts/pdm-fast-0.5.1`.

## Studio/MCP 0.5.0 — prior validation

- **163 tools: 117 reads/reference tools and 46 confirmed actions.** Ten added tools implement TopSolid identity/revision lookup and confirmed PDM/document CRUD improvements. All **250 declared API source pages** resolve in the cached 7.20 reference.
- Release build: zero warnings/errors. **2,808 offline server checks**, **860 protocol checks** (`-SkipTopSolidQueries`), and **26/26 application test groups** passed. The application tests include a real MCP process; provider responses in this run use fixtures, not fresh authenticated cloud inference.
- **13 native read-only calls** verified backing project/library documents, creation-date ordering, PDM/document/revision mappings and element name/type classification. The initial date queries sorted **51 projects in 645.89 ms** and **66 libraries in 966.03 ms**; these are local query timings, not end-to-end model latency guarantees.
- The final **0.5.0.0 packaged server** passed the same 13 native reads. In that warm session the sort queries took **124.91 ms** and **94.30 ms**. Published Studio DLL/server executable hashes match the tested Release outputs; the bundle excludes the unused PDM Explorer assembly.
- The 0.4.3 assumption that dates require PDM Explorer was incorrect for this installation. The kernel API resolves backing project documents and exposes their creation-date parameters. Missing/ambiguous dates still fail explicitly; modification dates are never substituted.
- New persistent PDM writes and universal-ID edits were **not executed against native data**. No TopSolid document was opened or changed for these checks. Full interactive GUI testing was not repeated. Earlier GUI/native-change evidence below must not be read as validation of the new write paths.
- See [object model, sources and exact CRUD boundaries](docs/TOPSOLID-OBJECT-MODEL-0.5.0.md). Current evidence: `artifacts/identity-review/{server-tests,application-tests,protocol-tests}.txt` and private `live-reads.json`.

## Earlier diagnostic/export validation (0.4.0)

- Release build completed with zero warnings/errors. The final checks passed: **2,096 server assertions**, **635 protocol/live-read assertions**, **25 application checks**, and **27/27 WPF UI-smoke checks**.
- Save log regression coverage verifies separate all-history/current-session files, startup-to-export current-log content, exclusion of historical log content, structured chat/conversation/MCP export, and credential redaction.

## Studio/MCP 0.4.0 batch expansion

- **153 tools: 111 reads/reference tools and 42 confirmed actions**, adding 25 tools. All 229 declared API source pages were found in the official 7.20 index; the new compiled bindings target installed SDK 7.20.400.107.
- Release build: zero warnings/errors. **2,096 server checks**, **25 application groups**, **635 protocol checks**, and **27/27 WPF UI-smoke checks** passed. Tests include bounded schema selection, result-budget continuation, partial errors, revision rebasing, confirmation rejection, batch rollback boundaries, and local API-reference metadata/file resolution.
- **31 live batch-read calls** passed across four already loaded document types, including mixed valid/invalid PDM handles. No document was opened or changed and no external model was called.
- New native modeling/deletion execution and non-empty sketch/shape/assembly/CAM reads remain unverified without suitable confirmed fixtures. The final WPF smoke rendered successfully.
- See [new tools, limits and exact evidence](docs/BATCH_TOOLS.md). Earlier release results follow below.

## Studio/MCP 0.3.2 PDM names update

- **50 project names and 66 library names** retrieved natively; 17-item pagination verified complete totals and unique IDs.
- A real **Gemini 3.6 Flash** conversation with all 128 tools produced every one of the **116 names in two MCP calls**. 24/24 groups passed in that live run (`artifacts/pdm-names-tests.txt`). Saved settings were unchanged; no CAD mutation occurred.
- **215 server assertions** and **73 protocol/live-read assertions** passed with the updated named-object list contract and default 100-entry project/library pages. Live calls with empty arguments returned all 50 projects and 66 libraries. The regression suite covers Gemini model prefix normalization, array-wrapped 404 detail/redaction, and upgrading a saved bundled-server path.
- Latest application rerun passed 23 functional groups, including the real MCP process, but the WPF screenshot assertion failed twice with an empty render. Earlier UI evidence does not establish a passing render on this final rerun. Release build completed with zero warnings/errors.
- The saved Gemini 2.5 Flash model remains unavailable for this account according to Google's 404 response, even though discovery lists it. The successful test used an explicit in-memory model override. Select 3.6 Flash in Studio; no silent model fallback is implemented.
- See [scope and evidence](docs/PDM_NAME_LISTS.md). Earlier release evidence follows below.

## Studio 0.3.1 cloud-provider update

- **25/25 application groups passed**, including real MCP process/WPF controls and authenticated Gemini model discovery. Saved native Gemini `/v1beta` settings migrated in memory to `/v1beta/openai`; the existing key retrieved **58 model IDs**. User settings were not rewritten and no cloud inference or CAD data was sent.
- Nine service presets, per-service encrypted keys/models, Anthropic native Messages/model pagination, Gemini thought-signature replay, and endpoint migration passed regression checks. Other cloud services used synthetic HTTP contracts, not live authentication.
- Studio version is **0.3.1.0**; MCP server remains **0.3.0.0**. The earlier v0.3 server/native-validation evidence below still applies.
- See [cloud implementation and source contracts](docs/CLOUD_SERVICES.md), `artifacts/cloud-provider-tests.txt` and `artifacts/cloud-provider-ui-tests.txt`.

## Historical validation snapshot (before 0.5.0)

| Check | Evidence |
|---|---|
| Release build | Five projects, zero warnings/errors; installed TopSolid 7.20.400.107 SDK |
| Published package | Both executables report 0.4.3.0; published Studio/server binaries match the tested Release outputs by SHA-256 |
| Server regression tests | **2,096 assertions**: schema/confirmation, expiry/replay, changed targets, revision rebasing, partial PDM receipts, commit/rollback, line/arc geometry, SI/frame/translation math, serialization, local-reference metadata and reference registration |
| MCP protocol and live reads | **635 assertions**: lifecycle, tool discovery, local API-reference paths/files, write annotations, blocked unapproved calls, invalid geometry, reference retrieval, malformed input, batches, live reads and clean exit |
| Studio/provider/client/UI | **25/25 application checks**, plus **27/27 WPF UI-smoke checks**: both HTTP provider formats, tool loop, prepare rejection returned to the model, settings protection, confirmation/denial, retained receipts, mutation-aware shutdown, actual process and rendered WPF controls |
| Diagnostics/export | Persistent all-history and per-application-session Studio diagnostics, global crash/error handlers, bounded chat/conversation snapshot, MCP tool/result export, atomic current-session JSON export and credential-redaction regression test |
| Real local AI through WPF | `qwen3.5:9b-q4_K_M` on Ollama, with **128 discovered tools**: actual Send button → model → `topsolid_get_status` → MCP → TopSolid → final English response reporting 7.20.400.107 |
| Reference and SDK | 6,898 symbols / 3,335 cached official API pages; declared source links checked; seven Automation assemblies cataloged with 307 exported types |
| Manual review | All five PDFs indexed, 581 pages; relevant Design workflows and all ten CAM Automation pages reviewed; selected figures rendered and inspected |

The server accepts a single leading UTF-8 BOM used by some Windows PowerShell/.NET Framework clients. The protocol script was exercised using both the current PowerShell and Windows PowerShell.

### Live read snapshot

- TopSolid host/client: **7.20.400.107** (`720400107`).
- No active/open document; **29 loaded documents**, **50 working projects**, **66 libraries**, **11 active license records**.
- Loaded document information/elements and PDM object details were queried successfully.
- PDM metadata verified `.TopPrt`, `.TopAsm` and `.TopMillTurn` extensions without opening or changing those documents.
- TopSolid was initially closed in this turn; it was started for inspection. The MCP server itself continues to connect without auto-starting TopSolid.

Direct Automation modules and SX match 7.20.400.107. Supporting installation dependencies keep their own vendor versions. This is not certification for every 7.20 patch or workstation.

## Native change execution: awaiting explicit approval

The **28 action tools are implemented and compile against real vendor assemblies**. Their schema, geometry math, target/revision handling, confirmation and transaction boundaries are tested. **They have not yet been executed against real CAD/CAM documents in this expansion.** Build success and fake transaction tests do not prove native geometry or CAM results.

A reviewable live fixture is prepared in [fixture-plan.json](scripts/LiveWorkflow/fixture-plan.json):

- One new working project: `AI Studio MCP Validation 2026-09-15`.
- Three parts for an 80×50×20 mm extrusion, a revolved ring and a circular-profile loft; one assembly with two fixed inclusions.
- Line/arc contours, native volume checks, save/revision rebasing, and one actual AI-created circle through Studio's real confirmation window.
- Only newly created fixture documents may change. No deletion, check-in, CAM mutation, NC output or machine execution.
- Stop on failure and preserve receipts. No automatic retry of failed or uncertain changes.

Read-only preflight passed. The user was asked to approve this exact fixture because they explicitly requested confirmation before TopSolid changes. **No fixture project or document has been created yet.**

## Evidence files

- `artifacts/server-tests.txt`: latest server assertions.
- `artifacts/protocol-tests.txt`: protocol/live read results.
- `artifacts/test-results.txt`: real Ollama/WPF result and regression groups.
- `artifacts/live-ui-smoke.png`: actual local model response in the Studio visual tree.
- `artifacts/ui-smoke.png`, `confirmation-smoke.png`: latest own-window renders.
- `artifacts/native-workflow/preflight.json`: read-only fixture preflight and exact plan hash.
- `artifacts/manuals/manifest.json`: manual paths, page counts, hashes and outlines.
- `docs/api/binding-verification.json`, `mcp-tools.json`, `COVERAGE.md`: actual schemas, source checks and full tool coverage.

Default tests do not modify TopSolid. Confirmation/rollback tests use fake actions; transport failures use a separate fake MCP process. The WPF live status test contacts real Ollama and TopSolid but performs only inspection. UI tests verify saved application settings remain unchanged.

## Reproduce safe checks

```powershell
dotnet build .\TopSolid.Automation.Mcp.Server.slnx -c Release
& .\TopSolid.Automation.Mcp.Server.Tests\bin\Release\net48\TopSolid.Automation.Mcp.Server.Tests.exe
.\TopSolid.Automation.Mcp.Server.AddIn\scripts\Test-Protocol.ps1 -ServerPath .\TopSolid.Automation.Mcp.Server.AddIn\bin\Release\net48\TopSolid.Automation.Mcp.Server.AddIn.exe
dotnet run --project .\TopSolid.Automation.Tests -c Release -- --server .\TopSolid.Automation.Mcp.Server.AddIn\bin\Release\net48\TopSolid.Automation.Mcp.Server.AddIn.exe --ui-smoke
# Optional actual Ollama status question; no CAD mutation:
dotnet run --project .\TopSolid.Automation.Tests -c Release -- --server .\TopSolid.Automation.Mcp.Server.AddIn\bin\Release\net48\TopSolid.Automation.Mcp.Server.AddIn.exe --live-ui-ollama qwen3.5:9b-q4_K_M
```

`scripts/LiveWorkflow/native_workflow.py` defaults to read-only preflight. Its mutation mode requires `--approve-plan` with the exact SHA-256 of the reviewed plan. Do not enable it without explicit human approval. The optional `--approved-native-plan` Studio test similarly restricts approval to the exact fixture circle and refuses unrelated synchronized documents.

## Remaining limits

- Native geometry, shape readback and real rollback behavior need the approved fixture run. Through drilling, native 2D drawing sketches, arbitrary existing assemblies and populated CAM/CAE/drafting documents need representative fixtures.
- Synchronized document metadata is rechecked before dispatch; inspection is not an atomic snapshot of the entire CAD model.
- No arbitrary fillet/pocket/chamfer/Boolean creation, general dimensional constraint editing, CAM strategy creation, postprocessor run, simulation run, NC transmission, Wire host or standalone PDM Explorer adapter is exposed.
- Real cloud authentication/quota/model behavior is untested; no paid cloud credentials were used. Both provider formats passed scripted HTTP contract tests.
- Live vendor crashes and connection loss during modification were not deliberately induced. A vendor modification cannot safely be hard-killed; uncertain results require inspection.
- Sending 128 schemas adds context and latency. Accuracy, language adherence, context limits and tool support depend on the model. The loop is bounded to 16 rounds and 24 calls.

See [manual mapping](docs/MANUALS_AND_WORKFLOWS.md) for source editions and the difference between manual commands, verified APIs and implemented tools. The workspace has no Git metadata; no commit or PR was created.
