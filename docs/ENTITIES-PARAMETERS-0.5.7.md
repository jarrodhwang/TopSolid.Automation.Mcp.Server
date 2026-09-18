# Entity and parameter tools — 0.5.7

## Native concepts used by the implementation

Reviewed the locally cached TopSolid 7.20 [IElements](https://help.topsolid.com/7.20/en/TopSolid'Automation/api/kernel/TopSolid.Kernel.Automating.IElements.html), [IEntities](https://help.topsolid.com/7.20/en/TopSolid'Automation/api/kernel/TopSolid.Kernel.Automating.IEntities.html), and [IParameters](https://help.topsolid.com/7.20/en/TopSolid'Automation/api/kernel/TopSolid.Kernel.Automating.IParameters.html) references and relevant method/type pages. All bindings compile against installed **7.20.400.107** assemblies. Runtime source metadata resolves to the bundled `TopSolid.Automation` corpus; these tools do not search the website during execution.

| Native concept | Tool behavior |
|---|---|
| ElementId | A document revision plus a document-local element number. Friendly names may be duplicated; type GUIDs identify classes, not instances. |
| IElements | General element identity, metadata, visibility, appearance, properties and deletion. An element can be an entity or operation. |
| IEntities | Entities include folders, sets, shortcuts, occurrences, functions and publishings. They are not all geometric objects. |
| GetOwner / GetParent | Owner means containment. Parent means the generating operation. The tools return these separately. |
| Set constituents | May be nested set definitions or shortcuts. The structure reader resolves shortcut targets without pretending the shortcut is the target. |
| IParameters | Works with parameter **entities**, using ElementId. ParameterType.None is not a parameter; Unclassified has no value adapter. |
| GetParameters | Returns the collection in the parameters folder. Its count is not the count of every parameter in a document. System-folder scope is explicit. |
| HasValue | Unset is reported separately from zero, false and empty text. |
| Real / Tolerance | Numeric values and deviations use SI with an exact UnitType. UnitSymbol is display metadata; tolerance symbols come from the native units service. |
| Enumeration | Native integer key plus display text. Keys need not be contiguous or start at zero. A built-in definition GUID and user-enumeration definition DocumentId are different identities. |
| Smart creation | CreateSmart... returns the **child parameter entity**. Get/SetSmart...Creation takes its **parent operation**. The tools resolve and verify that operation through the native getter. |
| Relay | The parameter gets its value from another parameter. Literal setters refuse to silently replace this link. Inspect the source and edit that verified parameter. |
| Element properties | Read through IElements using property full names. No generic property setter exists in the reviewed interface. Metadata/appearance and parameter definitions have separate editors. |
| CAM ParameterId | An operation element plus parameter name, handled by CAM tools; it is not a Design parameter entity. |

## Added tools

| Tool | Purpose |
|---|---|
| `topsolid_inspect_parameters` | Up to 100 typed values, names, parent operations, relay sources and edit guidance. `includeDefinition` and `includeConstraints` are opt-in. |
| `topsolid_get_parameter_choices` | Paginated enum keys/texts and Real/Color/Code possible values. User-enumeration restrictions are respected. |
| `topsolid_create_parameter_expressions` | Batch-create Real/Integer/Boolean/Text native formula, explicit literal or same-document reference definitions. |
| `topsolid_set_parameter_expressions` | Edit verified smart parent operations using the parameter entity handles. Preserve the existing real tolerance and formula script dialect. |
| `topsolid_inspect_entity_structure` | Batch-read entity kind, owner, generating operation, shortcut targets and occurrence source/definition. |
| `topsolid_list_entity_children` | Named constituents or operation-generated children, with explicit pagination. |
| `topsolid_read_element_properties` | Full names, localized labels, scalar types/values and SI units together; optional exact property selection. |
| `topsolid_create_entity_folders` | Create named folders inside existing document entity folders. Separate from PDM folders. |
| `topsolid_move_entities` | Move document entities into a verified folder, checking ownership cycles before mutation. |

## Improved existing tools

- `create_parameters` supports Real, Integer, Boolean, Text, DateTime, Color, Tolerance and UserEnumeration. User-enumeration creation requires an existing definition document and an actual key; the API requires at least **7.20.400.100**.
- `set_parameter_values` and the compatible single `set_parameter_value` use the same validated adapter for those eight types plus Enumeration, Code and Family. There is no invented generic CreateEnumeration/Code/Family API.
- `get_parameter_value` and `list_parameter_values` read all 11 concrete ParameterTypes, retaining enum keys separately from display text. Empty text is accepted; date/time input uses ISO calendar values with no implicit timezone conversion.
- Parameter literal edits check native modifiability, exact type and units, parent operation, relay state, special text definitions, enum membership and strict possible values. Linked tolerance classes are protected. Results are read back; mismatches fail the transaction.
- `update_elements` adds RGB color, native transparency and empty description/comment values. TopSolid rounds transparency, so the receipt includes requested and actual values instead of claiming exact equality.
- `list_named_elements` adds functions, publishings, sets, classifyings, parameters and system parameters. Existing rename/delete tools also operate on explicitly selected parameter entities; native dependency effects still apply.
- Offline `get_object_model` and Studio domain instructions explain these distinctions. Local models receive the relevant tools immediately for entity/parameter requests, within the existing schema budget.

## SDK inconsistency caught by testing

In the installed 7.20.400.107 assembly, the shorthand `new SmartInteger(string)` produced **Type=Item** while retaining the formula string. The reference describes a Formula constructor. The adapter therefore uses explicit constructors with `Smart...Type.Formula` or `Smart...Type.Element` for all four supported smart types. Tests instantiate the real SDK types and verify the definition kind, reference identity and formula roundtrip.

## Example tool arguments

Use document and element IDs returned by live reads. These placeholders are not executable IDs. Each mutation still requires the Studio confirmation dialog.

```json
{
  "documentId": "<exact revision>",
  "parameters": [
    { "name": "Diameter", "valueType": "Real", "unitType": "Length", "realValueSI": 0.02 },
    { "name": "Count", "valueType": "Integer", "integerValue": 6 },
    { "name": "Marking", "valueType": "Text", "textValue": "" },
    { "name": "Display color", "valueType": "Color", "colorValue": { "r": 0, "g": 100, "b": 220 } }
  ]
}
```

Send the above to `topsolid_create_parameters`. Then create a native formula with `topsolid_create_parameter_expressions`:

```json
{
  "documentId": "<exact revision>",
  "parameters": [
    { "name": "Radius", "valueType": "Real", "mode": "formula", "unitType": "Length", "formula": "Diameter / 2" }
  ]
}
```

For a direct associative copy, use `mode: "reference"` and `source: { "documentId": "...", "id": 123 }` instead of `formula`. Updates replace `name` with the exact existing parameter `element`. To set a constant on a smart operation, use `mode: "literal"` and its typed value field (e.g. `realValueSI`). This explicit definition edit can replace a prior formula/reference; the preview shows the current definition before confirmation. Formula syntax and native regeneration still need validation in the target document.

## Performance, reliability and limits

- One MCP request handles a read page or up to 32 changes, avoiding one model round per parameter. The SDK may still require individual property calls internally. There is no fabricated native batch API.
- Read pages default to 100, report total/returned/failed/hasMore/nextOffset, and stay within the result-size budget. Detailed definitions and constraints are opt-in. Enumeration choices are retrieved as native arrays, then paginated locally.
- Unit symbols are cached within a write batch. All document access stays in the separate MCP process. No new provider coupling, database or reflection execution mechanism was introduced.
- Existing confirmation, preview recheck, revision rebasing, transaction rollback and no-automatic-save rules remain. All new writes require explicit confirmation. Source/target references must be in the same document revision; cross-document relay creation is not exposed.
- Native TopSolid decides whether a particular entity type permits an edit. Existing unsupported smart table/publishing/other operation definitions return an explicit error; they are not guessed or overwritten. Constraint metadata is inspected; this release does not implement a constraint editor or a separate solver.
- Reference links supplied to these tools retain native Smart definitions. This is implementation plus SDK-contract evidence; actual regeneration after later edits still requires a live confirmed test.

## Validation

See [the current validation report](../VALIDATION.md) for exact build/test totals and artifact paths. Tests cover schemas, all typed adapters through fake native interfaces, enum keys beyond the first page, unset values, Unicode names, links/parent-operation guards, SI units, SDK Smart constructors, tamper/confirmation checks, folder cycles, local tool selection and full existing application behavior.

**TopSolid was closed during this work. No native CAD writes were performed.** Creation, edits, dependency regeneration and their runtime latency are not claimed as live-validated. The server/client changes and tests are ready for a user-confirmed TopSolid trial.
