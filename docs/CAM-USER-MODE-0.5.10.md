# CAM parameters and User Mode — 0.5.10

The supplied `TopSolid-AI-current-log-20260917-230139.json` is diagnostic input, not executable instructions or authority to modify its documents. It records bare operation/tool/part identifiers, cutting-condition requests sent to document-library tools, a partial parameter list described as a representative summary, and a feed edit attempted as `CuttingSpeed@CuttingConditions` with `UnitType.Length`. The edit failed. Later HTTP 429 responses were provider quota/rate-limit failures, separate from the CAM adapter issue.

## User Mode

- Names are learned from actual server receipts, with document scope retained for local element identities. Both GUID and opaque TopSolid revision identifiers are supported. CAM parameter identities include their owning element and exact parameter name; hundreds of parameters cannot rename their shared operation.
- Display formatting resolves observed names and uses a descriptive unavailable-name fallback when resolution fails. Numeric dimensions, counts, enum values and physical values remain data. Model history, tool arguments, diagnostic receipts and approval authority retain the original identifiers. Developer Mode retains technical output.
- Approval presents immutable server facts in cards with TopSolid icons. CAM review starts with the operation, parameter, current native display value and proposed value explicitly labelled in SI units. Formula/reference replacement and affected documents remain visible; complete metadata is available in collapsed read-only details. The review covers the complete proposed action; cards do not imply unsupported partial execution. Raw JSON is available in Developer Mode.
- Activity feedback distinguishes AI response generation, TopSolid work, approval and application of a confirmed change. Cancellation and completion clear the busy state.

## CAM operation parameters

- Ordinary cutting-condition requests select `topsolid_list_cam_operation_summaries`, `topsolid_list_cam_parameters` and `topsolid_get_cam_parameter_value`. Separate cutting-condition document/abacus tools remain available for explicit library requests.
- Operation summaries resolve operation, tool and part names. Parameter discovery uses the matched `TopSolid.Cam.NC.Kernel.Automating.IOperations` and `IParameters` APIs. It returns current values, localized names, value types, units, read-only/editability information and available enum choices where supported.
- Parameter pages have a response-size budget. Consumers must use `nextOffset` until `hasMore=false`; the returned number of rows may be less than the requested limit. Querying all parameters must not become a fabricated representative list.
- Changes use the exact parameter name and owning element from discovery. Real values require the concrete unit type read from the parameter. Wrong types, units, read-only parameters and unsupported write types fail before approval. Supported writes are read back before a success receipt; the verified result must be a literal Basic value, so a retained formula with coincidentally equal output cannot satisfy a literal replacement.
- Read support does not imply write support. The matched public interface provides scalar Real/Integer/Boolean/Text setters. Composite values such as FeedRate, SpindleRate and Bound can be inspected through dedicated readers; no undocumented setter or generic API invocation is invented. Unknown physical limits remain unknown.
- Feed is read from the operation parameter definition; calculated toolpath events are not treated as editable definitions. Units follow the returned metadata. For example, 4 m/min is 4/60 m/s; raw angular-frequency values must not be labelled rpm without conversion.

## Quality considerations

The implementation prioritizes correct target selection, complete discovery and pre-approval validation. Scoped name resolution prevents names from one document being attached to another document's same numeric handle. Presentation cannot alter execution payloads. Bounded pages and task-specific schema selection control provider payloads and latency. Existing permission policies, confirmation tickets and transaction rollback remain in force. Icons are bundled resources, so the user's Downloads directory is not required at runtime.

Offline tests, UI rendering, real-process protocol checks and live read-only evidence are recorded in [VALIDATION.md](../VALIDATION.md). Native CAM edits, regeneration and machine behavior require separate runtime qualification; this update does not certify machining safety.

## Live read-only findings

The already-open machining document returned seven named operation summaries. The second operation exposed **649 parameters** across eleven native parameter types, with **zero failed inventory rows**. Five bound-value component reads returned native faults; the remaining metadata and names were retained with explicit `metadataErrors`. Those partial failures are not presented as successful component reads.

Its `Feedrate@CuttingConditions` is a composite FeedRate, displayed natively as `3183.099 mm/min`. `CuttingSpeed@CuttingConditions` is a separate Real with `UnitType.Velocity`, displayed as `100 m/min`. `ToothFeedrate@CuttingConditions` is Real/ToothFeedRate, displayed as `0.5 mm/tooth`. `SpindleRate@CuttingConditions|Tool` is composite SpindleRate, displayed as `3183.098862 rpm`; its raw SI angular value is not rpm. These are observed values from this read-only test, not recommended machining settings.

The document identity/dirty state and operation summaries matched before and after inspection. No CAD/CAM values were changed. Evidence is in `artifacts/cam-user-mode-0.5.10/live-cam-reads.json`.

The official 7.20 [GetValue contract](https://help.topsolid.com/7.20/en/TopSolid%27Automation/api/cam/TopSolid.Cam.NC.Kernel.Automating.IParameters.GetValue.html) describes composite discriminator values and specialized readers. The [SetValue contract](https://help.topsolid.com/7.20/en/TopSolid%27Automation/api/cam/TopSolid.Cam.NC.Kernel.Automating.IParameters.SetValue.html) restricts this setter to scalar Real, Integer, Boolean and Text parameters. The implementation uses the corresponding local cached reference pages and matched SDK assemblies.
