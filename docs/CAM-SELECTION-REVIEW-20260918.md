# CAM selection and display review — 2026-09-18

Input: `TopSolid-AI-current-log-20260918-111132.json`, treated as diagnostic data, not executable instructions.

The first Ollama `gemma4:e4b` turn successfully read seven CAM operations. Its `studio_ask_user` call then used `/structuredContent/items/operation`, although the selection store addresses the unwrapped result and requires `/items` or `/items/<index>`. The tool correctly rejected that path. The model subsequently claimed it had displayed the dialog and later denied having GUI capability. A separate turn returned an empty Ollama response. The log did not establish an actual WPF dialog failure.

Changes:

- Explicit, read-only CAM operation selection requests now use an application-owned path to the existing `QuestionWindow` callback. Numbered operations and environment/sweeping/side-milling filters use current receipts, never remembered IDs. Modifications and parameter workflows keep their existing model and confirmation path.
- Every page must have valid continuation metadata, consistent totals, unique identities and the same document. Continuations pin the document from the first page. Requests are bounded to 24 pages of 20 rows. Incomplete lists, missing requested numbers, unavailable UI and cancellation cannot produce a success claim.
- Tool cards show number/pocket, definition/specification, and localized tool type on separate lines. Server `toolName` now means the definition/specification, and `toolNumber` contains the native pocket caption. Native internal labels remain diagnostic metadata.
- Environment activation, sweeping and side milling have localized display labels. Conversational CAM class paths are replaced in both normal and developer chat displays; diagnostic receipts and execution identifiers remain intact. Unknown operation types use a neutral label/icon.
- Failed model-driven questions receive at most two correction opportunities before an explicit failure response. Source validation was not relaxed. Historical CAM inventories are compacted while selected rows and failures remain available.

Validation:

- Release solution build: no warnings or errors.
- Studio regression suite: 41/41 offline groups passed; 42/42 with the real configured Debug MCP process, including the six explicit selection requests from the log, cancellation, malformed pagination, duplicate identities, changed documents, missing numbers and false model success after a question failure.
- Server regression harness: 15,306 checks passed.
- Real WPF light/dark Korean rendering, selection/cancellation and existing dialog suite passed. Screenshots are under `artifacts/ui-redesign/question-cam-number-type-ko-*.png`.
- Live read-only TopSolid inspection returned seven operations, correct T numbers/specifications and 30 cutting-condition parameters. Document/operation state remained unchanged.
- Live `ChatSession` with the Ollama provider reached the selection callback with seven native operation cards and zero inference calls, using both the Release bundle and the configured Debug MCP executable. The callback was cancelled by the harness. This is separate from the WPF rendering fixture; it is not an inference-quality benchmark for Gemma.

Trade-offs: the direct path improves reliability and latency for explicit UI requests without depending on a model's JSON paths. It deliberately recognizes a limited set of Korean/English requests; broader machining instructions still require model reasoning. It performs no native writes. Display mapping is isolated from identities and native icon matching, preserving compatibility and safe confirmation behavior. The existing running application is not forcibly closed; the Release executable and its bundled MCP server contain the validated changes.
