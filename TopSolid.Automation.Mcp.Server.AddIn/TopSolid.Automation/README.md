# Local TopSolid.Automation reference

6898 documented symbols across 3335 unique official API pages.
Includes the publisher's namespaces, classes, interfaces, structures, enumerations, fields, properties, methods and overloads. Descriptions, syntax, parameters, return values, remarks and examples are retained where the publisher supplies them. Undocumented information is not invented.

- [Symbol index](INDEX.md): every xref symbol links to its article. Overloads and enum fields can share an article.
- `reference-index.json`: machine-readable symbol-to-file mapping.
- `articles/`: individual JSON articles, loaded on demand by MCP.
- `markdown/`: human-readable articles.
- `html/`: exact cached original pages for full markup fidelity; SHA-256 recorded in `manifest.json`.
- `manifest.json`: provenance and completeness against the official xref map, not a claim of undocumented internal API coverage.

Source: https://help.topsolid.com/7.20/en/TopSolid'Automation/
At runtime, MCP tool metadata and API-reference results use bundle-relative `TopSolid.Automation/...` paths. The official URL above is provenance only; the server reads the bundled articles locally and does not open or download the website during chat. Regenerate with `scripts/ApiReference/cache_reference.py`, then `export_local_reference.py`. Build/publish copies this folder beside the server. The embedded snapshot remains a fallback when files are absent. Documentation does not authorize or implement new executable tools.

The pre-existing DLLs in this source folder are legacy files; the build continues to reference the matched installed 7.20 SDK, not these DLLs.
