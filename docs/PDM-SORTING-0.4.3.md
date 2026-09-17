# Creation-date ordering — 0.4.3

**Historical note, corrected in 0.5.0:** the claimed PDM Explorer prerequisite below was too broad. Live 7.20 checks showed that projects/libraries resolve to backing metadata documents whose creation-date parameters are readable through the kernel API. The current implementation uses that verified path. See [0.5.0 findings and evidence](TOPSOLID-OBJECT-MODEL-0.5.0.md#correction-projects-and-libraries-have-backing-documents-here).

The reported `order by old one first` reply did not sort anything and unnecessarily repeated 116 names. This patch makes that outcome explicit and concise.

- PDM list tools accept `orderBy: oldestFirst` or `newestFirst`. They gather verified creation dates and sort the entire result before applying pagination; duplicate names remain separate records.
- Missing date service, missing values or ambiguous date strings return `sortApplied: false` with a short explanation and no unsorted substitute list. ISO/year-first date formats are accepted; ambiguous locale-dependent strings are not guessed.
- Exact sort follow-ups after a PDM list use those MCP tools directly without another model inference. Other wording follows the normal model/tool loop with explicit sorting guidance. A sort follow-up does not silently apply to older, unrelated conversations.
- Successful output includes creation dates. A failed sort displays only the reason. Native access stays in MCP; no changes or app auto-starts are performed.

Live verification: the current session returned `creationDateServiceUnavailable`, total 50 projects. The unavailable service remains an external prerequisite; this patch does not claim to enable it. Kernel document creation-date parameters and modification dates cannot substitute for project/library creation dates. The successful-date path was validated with synthetic records, not a running PDM Explorer service.

Validation: 26 application test groups and 300 offline server checks passed; Release build had zero warnings/errors. Tests cover global ordering before pagination, duplicate names, oldest/newest direction, ambiguous dates, concise failures and zero-inference sort follow-ups. Full GUI interaction was not retested.
