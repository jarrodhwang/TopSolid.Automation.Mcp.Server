# Question dialogs — 0.5.11

Studio exposes `studio_ask_user` locally to the AI when its question UI is available. Necessary clarification uses this tool instead of a text-only question. The MCP server's 187-tool catalog is unchanged. The selected permission mode still applies to any later change; a dialog answer is data, never approval.

## Interaction

- Searchable, keyboard-accessible icon cards for projects, libraries, documents, parameters, operations, machines, CAM operation parameters, elements, edges, points, curves, surfaces and shapes. Single and multiple selection retain exact identities while filtering; no target is preselected.
- Friendly names and returned location, parent, revision, value or geometry context are shown. Native identifiers stay in the returned data. Unnamed items have a type and list ordinal, not an invented native name. The model must obtain distinguishing context for ambiguous geometry. This is a picker for objects returned by read tools; it does not add a new native TopSolid viewport picking API.
- Text (up to 4,000 characters), integer (signed 32-bit), and decimal inputs validate before Continue. Numeric answers include the explicitly requested input unit; conversion to an action's units remains the assistant's responsibility. Only verified numeric constraints may be requested.
- Color supports a swatch palette, preview, hexadecimal and synchronized RGB bytes. Images use the existing bounded PNG/JPEG attachment loader, a local preview, and an explicit Continue before transmission to the configured AI provider. Images remain untrusted reference data.
- Theme-aware layouts and English/Korean interface labels. AI-generated question text follows the user's response language. Activity changes to “Waiting for your answer” while the dialog is open.
- Cancel/window-close returns a cancelled receipt, stops pending tools and does not re-prompt. Turn cancellation closes the dialog. Already completed actions remain in the conversation.

## Data contract

```json
{
  "question": "Which project should be used?",
  "kind": "select",
  "itemKind": "project",
  "sources": [{ "toolCallId": "actual-read-call", "path": "/items" }]
}
```

Source paths are JSON pointers into a successful, unwrapped tool result from the current turn. Set `toolCallId` to the receipt's client-owned `studioSourceId`; this is explicitly included in content because Ollama omits tool call IDs on the wire. Paths can target an array or one row, including a CAM parameter's native `allowedValues`. Each source may override `itemKind` for a combined project/library picker. Data is cloned on capture, and a question cannot invent object values or substitute labels. Error receipts and unknown source paths are rejected. Only general, non-object choices can use the `choices` text array.

Answers contain `status`, `kind`, and either a typed `value`, image metadata, or `selected` entries containing the original row, source tool and scope arguments. The assistant reads that answer on its next round. A question and a CAD write in the same tool batch cannot execute the write. Existing prepare/confirmation and target revalidation remain mandatory.

Up to 500 loaded candidates, 24 sources, eight questions per turn and a 60,000-character answer ceiling bound UI and model costs. Search is explicitly labelled as searching loaded items. Models must fetch remaining pages or narrow the list before asking; the dialog does not silently claim an incomplete list is complete. Image bytes use provider image parts rather than receipt text or diagnostic logs.

Direct part-creation and project check-in shortcuts also use the picker when several real projects match the requested name. Complete, unambiguous requests continue without unnecessary questions.

## Validation

`UserQuestionTests` exercises exact and immutable identities, source rejection, duplicate names, unit and culture handling, scalar/color validation, model/tool answer continuation, Ollama HTTP source references, image payload boundaries, same-batch write blocking, cancellation, answer recovery after provider failure and direct creation ambiguity. `QuestionUiTests` exercises native WPF selection/search, multiple selection, empty results, visible selection summaries and clearing, typed input validation, RGB/hex synchronization, image preview, modal submission, cancellation of MainWindow's actual callback, and light/dark English/Korean renders. The real-process fixture checks an actual document read against the card name and selected native identity without changing CAD data.

No live AI-provider inference or native CAD writes are needed for these fixtures. Model compliance with question-tool instructions and live geometry identification still depend on the selected model and available TopSolid read tools.
