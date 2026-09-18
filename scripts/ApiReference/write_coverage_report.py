"""Verify every declared binding source and document actual registered tools by category."""
import json, os
from pathlib import Path
from urllib.parse import unquote
from cache_reference import ROOT, fetch

manifest=json.loads((ROOT/'docs/api/mcp-tools.json').read_text(encoding='utf-8-sig'))
index=json.loads((ROOT/'docs/api/reference-index.json').read_text(encoding='utf-8'))
local_index=json.loads((ROOT/'TopSolid.Automation.Mcp.Server.AddIn/TopSolid.Automation/reference-index.json').read_text(encoding='utf-8'))
paths={entry['href'] for entry in index['entries']}
official_base=index['source'].rstrip('/')+'/'
encoded_official_base=official_base.replace("'", '%27')
local_paths={}
for entry in local_index['entries']:
    for field in ('article', 'markdown'):
        relative=entry.get(field)
        if relative:
            local_paths['TopSolid.Automation/'+relative.replace('\\', '/').lstrip('/')] = entry['href']

def declared_path(reference):
    normalized=reference.replace('\\', '/')
    for base in (official_base, encoded_official_base):
        if normalized.lower().startswith(base.lower()):
            return unquote(normalized[len(base):]).lstrip('/')
    if normalized in local_paths:
        return local_paths[normalized]
    embedded='TopSolid.Automation/embedded/'
    if normalized.lower().startswith(embedded.lower()):
        return unquote(normalized[len(embedded):]).lstrip('/')
    raise ValueError('Unrecognized API reference: '+reference)

groups={}
sources={}
for tool in manifest:
    groups.setdefault(tool['_meta']['topsolid/category'],[]).append(tool)
    for reference in tool['_meta']['topsolid/api']:
        path=declared_path(reference)
        if path not in paths: raise ValueError('Not in official reference: '+path)
        sources[path]=fetch(path)

lines=['# API coverage and implemented MCP tools','',
       f"Indexed **{len(index['entries']):,} symbols** across **{len(paths):,} official reference pages**. Exposed **{len(manifest)} tools** in **{len(groups)} categories**: {sum(t['annotations']['readOnlyHint'] for t in manifest)} inspection/reference tools and {sum(not t['annotations']['readOnlyHint'] for t in manifest)} confirmed action tools.",'',
       'The reference cache covers the published 7.20 xref index. Runtime tool declarations use bundle-relative local files; the hyperlinks below point to the publisher source for provenance. This does not mean that every API has a wrapper or has been exercised in TopSolid. Bindings use explicit compiled calls to public Automation interfaces. There is no generic API invocation tool.','',
       '## Evidence levels','',
       '- **Reference:** the complete xref and linked API pages were cached; selected contracts were reviewed in detail. Each tool lists its source pages below.',
       '- **Assemblies:** seven installed Automation assemblies, version 7.20.400.107, were reflected including exported types, fields, constructors, properties and methods. Production references the six TopSolid application Automation modules plus the SX dependency. Standalone PDM Explorer is indexed separately.',
       '- **Tests:** schema/confirmation/transactions/geometry conversions use offline tests. Runtime evidence and the limits of that evidence are in [VALIDATION.md](../../VALIDATION.md).',
       '- **Changes:** PDM creation, document actions, native sketch geometry/profiles, extrusion/revolution/loft/drilling, typed parameters and smart definitions, assembly inclusion and selected CAM updates are implemented. Every change requires approval. See VALIDATION.md for the exact operations actually exercised.','',
       '## CAD/CAM module boundaries','',
       'The public CAM SDK provides shared documents, machines, operations, parameters, tools, programs, NC files, postprocessor and simulation services. The requested technology folders (`Cam2D`, `Cam3D`, `Cam4D`, `Cam3Plus2D`, `Cam5D`, `MillTurnCam`, `RobotCam`) remain explicit extension locations. No separate vendor interface or strategy-creation capability is invented for those names.',
       '', 'Wire reference pages are included, but no installed Wire Automation assembly was found. The Wire adapter is not implemented. PDM project/library listings obtain creation dates from verified backing-document creation-date parameters through the kernel API. Missing or ambiguous dates are explicit; no PDM Explorer service or modification-date substitution is used.', '',
       '## Registered tools','', '| Category | Inspection/reference | Confirmed changes |','|---|---:|---:|']
for category,tools in sorted(groups.items()):
    writes=sum(not t['annotations']['readOnlyHint'] for t in tools)
    lines.append(f'| {category} | {len(tools)-writes} | {writes} |')
for category,tools in sorted(groups.items()):
    lines += ['', '### '+category, '']
    for tool in tools:
        mode='confirmation required' if not tool['annotations']['readOnlyHint'] else 'inspection/reference'
        lines += [f"- `{tool['name']}` ({mode}): {tool['description']}"]
        if tool['_meta']['topsolid/api']:
            links=', '.join('['+declared_path(reference).rsplit('/',1)[1].replace('.html','')+']('+official_base+declared_path(reference)+')' for reference in tool['_meta']['topsolid/api'])
            lines += ['  - API: '+links]
    folder=ROOT/'TopSolid.Automation.Mcp.Server.AddIn/Tools'/category
    folder.mkdir(parents=True,exist_ok=True)
    coverage_link=os.path.relpath(ROOT/'docs/api/COVERAGE.md',folder).replace('\\','/')
    (folder/'README.md').write_text('# '+category+'\n\nRegistered tools:\n\n'+'\n'.join('- `'+t['name']+'`'+(' — requires explicit confirmation.' if not t['annotations']['readOnlyHint'] else ' — inspection/reference.') for t in tools)+'\n\nSee [API coverage]('+coverage_link+') and the root validation report for source contracts and runtime limits.\n',encoding='utf-8')
for name in ['Cam2D','Cam3D','Cam4D','Cam3Plus2D','Cam5D','MillTurnCam','RobotCam']:
    folder=ROOT/'TopSolid.Automation.Mcp.Server.AddIn/Tools/Cam'/name
    (folder/'README.md').write_text('# '+name+'\n\nTechnology extension area. Existing CAM data is inspected through the sibling Operation, Machine, PartSetup, CuttingConditions, Tooling, NC, Postprocessor and Simulation tools. The public SDK uses shared CAM interfaces; this folder does not imply a separate vendor interface.\n\nNo strategy creation or machine execution tool is registered here. Adding one requires a documented operation contract, typed parameters and explicit confirmation/transaction handling appropriate to its actual effect.\n',encoding='utf-8')
(ROOT/'docs/api/COVERAGE.md').write_text('\n'.join(lines)+'\n',encoding='utf-8')
report={'tools':len(manifest),'categories':len(groups),'declaredBindingPages':len(sources),'allDeclaredSourcePagesFound':True,'referenceSymbols':len(index['entries']),'referencePages':len(paths)}
(ROOT/'docs/api/binding-verification.json').write_text(json.dumps(report,indent=2),encoding='utf-8')
print(json.dumps(report))
