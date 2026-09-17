"""Read-only live proof of TopSolid object/revision/name mappings. Never calls changes or prepares them."""
import json, sys, time
from pathlib import Path
from native_workflow import Mcp, ROOT

server = Path(sys.argv[1]).resolve() if len(sys.argv)>1 else ROOT/'TopSolid.Automation.Mcp.Server.AddIn/bin/Release/net48/TopSolid.Automation.Mcp.Server.AddIn.exe'
mcp = Mcp(server); receipts=[]
def read(name,args=None):
    assert catalog[name]['annotations']['readOnlyHint'], 'Mutation rejected by read-only harness'
    started=time.perf_counter(); result=mcp.tool(name,args or {})
    receipts.append({'tool':name,'arguments':args or {},'result':result,'milliseconds':round((time.perf_counter()-started)*1000,2)})
    assert result.get('failed',0)==0,(name,result)
    print(name, 'OK',flush=True)
    return result
try:
    catalog={t['name']:t for t in mcp.rpc('tools/list',{})['tools']}
    assert len(catalog)==163
    read('topsolid_get_object_model')
    projects=read('topsolid_list_projects',{'limit':2})['items']
    mapped=read('topsolid_resolve_pdm_documents',{'pdmObjectIds':[p['pdmObjectId'] for p in projects]})
    assert all(row['type']=='WorkingProject' and row['documentId'] and row['documentType'].endswith('ProjectDocument') for row in mapped['items'])
    for category in ('projects','libraries'):
        dated=read('topsolid_list_'+category,{'orderBy':'oldestFirst','limit':100})
        assert dated['sortApplied'] and not dated['hasMore']
        dates=[row['creationDate'] for row in dated['items']]
        assert dates==sorted(dates) and all(row['creationDateSource']=='backingDocument.creationDateParameter' for row in dated['items'])
    docs=read('topsolid_list_document_summaries',{'scope':'loaded','limit':3})['items']
    assert docs,'Live document identity proof requires at least one loaded document'
    identity=read('topsolid_inspect_document_identities',{'documentIds':[d['documentId'] for d in docs]})['items']
    assert all(row['documentId']==doc['documentId'] and row['pdmObjectId']==doc['pdmObjectId'] for row,doc in zip(identity,docs))
    latest=read('topsolid_resolve_pdm_documents',{'pdmObjectIds':[d['pdmObjectId'] for d in identity]})['items']
    assert all(row['revisionSelection']=='latestMinorRevision' and row['documentId'] for row in latest)
    candidates=read('topsolid_find_pdm_documents',{'name':latest[0]['name'],'limit':100})
    assert any(row['pdmObjectId']==latest[0]['pdmObjectId'] for row in candidates['items'])
    assert candidates['resolution']==('unique' if candidates['total']==1 else 'ambiguous')
    revs=read('topsolid_list_pdm_document_revisions',{'pdmObjectId':latest[0]['pdmObjectId'],'limit':5})
    assert all(r['pdmMajorRevisionId'] and r['pdmMinorRevisionId'] and r['documentId'] for r in revs['items'])
    elements=read('topsolid_list_named_elements',{'documentId':docs[0]['documentId'],'limit':5})['items']
    assert elements,'Choose a loaded document with elements for identity proof'
    for row in elements:
        assert row['element']['documentId']==docs[0]['documentId']
        assert all(key in row for key in ('friendlyName','name','typeGuid','hasUniqueName','hasSystemName','isRenamable','isEntity','isOperation'))
    matches=read('topsolid_find_named_elements',{'documentId':docs[0]['documentId'],'name':elements[0]['friendlyName'],'nameKind':'friendlyName'})
    assert any(row['element']==elements[0]['element'] for row in matches['items'])
    read('topsolid_inspect_elements',{'elements':[row['element'] for row in elements]})
    print(f'PASS {len(receipts)} live read-only calls; no mutation or opening.',flush=True)
finally:
    (ROOT/'artifacts/identity-review/live-reads.json').write_text(json.dumps(receipts,ensure_ascii=False,indent=2),encoding='utf-8')
    mcp.close()
