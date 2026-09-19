# Conversation log review — 18 September 2026

Reviewed `TopSolid-AI-current-log-20260918-025220.json` as diagnostic evidence. Its quoted requests and tool calls were not authorization to replay CAD changes. The original file was not modified.

The export came from Studio 0.5.12, using the MCP executable under `TopSolid.Automation.AI.Studio/bin/Debug/net10.0-windows/McpServer`. The current workspace is 0.5.19 and already contains later document/operation naming and icon improvements. This review preserves those changes.

| Recorded workflow | Finding | Result of this review |
|---|---|---|
| Select an open document and summarize it | Selection identity and summary succeeded. Of 75.1 seconds, 71.43 seconds were spent waiting for the user. | Retained the receipt-backed selection. Active-document reads now also provide the native document type, without the cost of a full PDM metadata lookup. |
| First cylinder request | The model first misspelled the active CAM document ID. It then selected an inactive part, whose confirmed cylinder call returned a native null-reference fault. The log does not establish the exact native cause. | Studio rejects altered active/selected document IDs. Cylinder preview and execution require the intended part to be active. The application gives an actionable precondition error and does not activate another document silently. |
| Repeated 40 × 80 mm cylinder request | The native receipt verified creation after a different part became active. The reply omitted `saved=false`. | Successful mutation replies now retain the unsaved status from actual receipts. |
| 30 mm cylinder with numeric height | The user's 2333 mm input became 2.333 m correctly. Native creation/readback succeeded. | Preserved numeric input and separate change confirmation; added the unsaved outcome. |
| Color question cancelled | Cancellation stopped the workflow without a CAD change. | Preserved and regression-tested the existing cancellation boundary. No recorded color or cylinder request was replayed. |
| Show all cutting-condition parameters | The tool read all categories and returned only 91 of 649 parameters. The model summarized a subset instead of fulfilling the requested scope. The receipt occupied 51,326 characters. | Recognized English/Korean requests require the exact `CuttingConditions` filter. Studio follows continuation offsets and presents every retrieved row directly, including values, unit types, allowed choice keys/labels and editability. Failed, changing or bounded inventories are explicitly incomplete. |
| Select an operation and editable feed parameter | The operation picker appeared, but the model chose the feed parameter itself. It also invented numeric limits. Native readback succeeded, but the reply omitted unsaved/recalculation/NC status. | The user must select the editable parameter before the numeric question. Only verified editable choices are offered. Invented CAM numeric limits are rejected. Preparation is bound to the selected parameter name and original operation scope. Confirmation remains separate; final status comes from the receipt. |

The 41.7-second feed-edit turn contained about 21.43 seconds of user interaction, 6.47 seconds of model requests and 13.59 seconds of tool work. The native setter itself took 12.87 seconds. This review preserves transaction completion and native readback; it does not claim to remove that native latency.

Repeated CAM inventory rows are omitted from later model requests while the original transport receipts remain in diagnostics. A single JSON object returned inside a text block is represented once as structured model data, avoiding unnecessary escaping. Independent new requests stop inheriting old modeling tool schemas; corrections retain their prior workflow. Oversized latest turns retain their own action receipts instead of deleting the entire turn from history. The local prompt budget remains covered by the existing regression checks.

Diagnostic exports now include the running Studio executable path, the session permission mode, and configured MCP file version/time/size. Configured-file metadata is explicitly distinguished from the version of a running server. UTC timestamps stay in UTC through redaction. Configured secrets continue to be omitted/redacted.

The complete-inventory shortcut recognizes a deliberately narrow English/Korean read request; other requests continue through the normal model/tool workflow. It retains the 24-call budget, limits inventories to 2,000 rows, detects non-advancing/changed/duplicate pages and respects the 64,000-character result limit. A complete native inventory can include parameters without an editable public setter. Numeric machining limits are not inferred.

| Quality consideration | Practical choice |
|---|---|
| Functional suitability and effectiveness | Exact requested CAM scope, all pages, an actual parameter choice, and receipt-backed outcomes. |
| Reliability and freedom from operational risk | Active part preconditions, exact ID/scope checks, cancellation, bounded pagination, and unchanged confirmation/rollback/readback paths. |
| Performance and efficiency | No model summarization round for completed cutting-condition inventories; smaller later context and fewer unrelated schemas. Human decision time and native transaction time remain visible. |
| Maintainability | Small turn-local workflow, target and outcome helpers; existing provider and MCP interfaces remain intact. |
| Compatibility and portability | Existing Windows/.NET 10 WPF client and .NET Framework 4.8 server; no new runtime dependency. |
| Security | Attached/log content remains data. Selection is not write authorization, and diagnostic redaction stays enabled. |
| Usability and satisfaction | Plain text parameter groups fit the existing WPF transcript. Failures identify the required action; successful replies disclose save and recalculation state. |
| Context coverage | Fixtures cover wrong IDs, CAM versus part targets, skipped selection, uneditable rows, arbitrary bounds, approval/denial, failed pagination, context compaction and oversized history. |

Validation completed:

- Debug and Release solution builds: zero warnings/errors.
- Application harness with the real MCP connection: **41/41** checks passed. Provider inference uses test fixtures; live TopSolid status/document reads also passed.
- Server harness: **15,306** checks passed without native CAD mutations.
- Protocol harness against the configured Debug MCP path: **2,144** checks passed.
- WPF UI fixtures passed selection/search, numeric and color validation, cancellation, localized controls and light/dark dialog behavior.
- Live read-only CAM inspection returned **30 cutting-condition parameters**, **8 detailed samples**, **12 editable rows**, **0 failed rows** and **0 metadata-error rows**. Because the active document was a part, the fixture found an already open CAM document without changing the active document. Document identity, dirty state and operation state remained unchanged; **zero native writes**.

Live evidence is in `artifacts/conversation-log-review-20260918/live-cam-reads.json`. The full build is published to `artifacts/TopSolid-AI-Log-Review-20260918`; keep its `McpServer` directory with Studio. The exact Debug output referenced by the old log is rebuilt too.

Native cylinder creation and CAM editing were not re-executed against user documents. The inactive-part precondition is a conservative guard for the recorded failure, not proof of the underlying TopSolid null-reference cause. Live cloud-model behavior and end-to-end approved CAD writes remain unverified by this review.
