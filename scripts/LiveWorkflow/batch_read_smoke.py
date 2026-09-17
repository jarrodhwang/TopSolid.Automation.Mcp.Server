"""Read-only integration exercise for the 0.4 batch tools. Never prepares/executes changes."""
import json, sys
from pathlib import Path
from native_workflow import Mcp, ROOT

server = Path(sys.argv[1]).resolve() if len(sys.argv) > 1 else ROOT/'TopSolid.Automation.Mcp.Server.AddIn/bin/Release/net48/TopSolid.Automation.Mcp.Server.AddIn.exe'
out = ROOT/'artifacts/batch-tools'
out.mkdir(parents=True, exist_ok=True)
mcp = Mcp(server)
receipts = []
def read(name, args=None):
    assert catalog[name]['annotations']['readOnlyHint'], 'Read-only runner rejected a change tool'
    result = mcp.tool(name, args)
    if 'items' in result:
        assert result['returned'] == len(result['items']) and result['failed'] == 0, (name, result)
        assert result['hasMore'] == (result['offset'] + result['returned'] < result['total'])
    receipts.append({'tool': name, 'arguments': args or {}, 'result': result})
    print(name, 'returned='+str(result.get('returned', '-')), 'total='+str(result.get('total', '-')), flush=True)
    return result
try:
    catalog = {tool['name']: tool for tool in mcp.rpc('tools/list', {})['tools']}
    assert len(catalog) == 163
    assert mcp.tool('topsolid_get_status')['connected']
    documents = read('topsolid_list_document_summaries', {'scope':'loaded', 'limit':100})['items']
    projects = mcp.tool('topsolid_list_projects', {'limit':5})['items']
    if projects:
        pdm = read('topsolid_inspect_pdm_objects', {'pdmObjectIds':[p['pdmObjectId'] for p in projects]})
        assert [row['name'] for row in pdm['items']] == [p['name'] for p in projects]
        mixed = mcp.tool('topsolid_inspect_pdm_objects', {'pdmObjectIds':[projects[0]['pdmObjectId'], 'invalid-pdm-id']})
        assert mixed['returned'] == 2 and mixed['failed'] == 1 and mixed['items'][0]['name'] == projects[0]['name'] and mixed['items'][1]['isError']
        receipts.append({'tool':'topsolid_inspect_pdm_objects', 'test':'one invalid handle preserves successful rows', 'result':mixed})
    inspected = set()
    for document in documents:
        dtype = document['type']; doc = document['documentId']
        # One representative of each actual loaded native type, without opening anything.
        if dtype in inspected: continue
        inspected.add(dtype)
        elements = read('topsolid_list_named_elements', {'documentId':doc, 'limit':7})['items']
        if elements:
            batch = read('topsolid_inspect_elements', {'elements':[v['element'] for v in elements], 'limit':7})
            assert [v['displayName'] for v in batch['items']] == [v['displayName'] for v in elements]
        read('topsolid_list_parameter_values', {'documentId':doc, 'limit':7})
        read('topsolid_list_document_property_values', {'documentId':doc, 'limit':7})
        read('topsolid_list_shape_summaries', {'documentId':doc, 'limit':7})
        for d in (2, 3):
            sketches = read('topsolid_list_named_elements', {'documentId':doc, 'kind':f'sketches{d}d', 'limit':1})['items']
            if sketches:
                for kind in ('vertices','segments','profiles'):
                    read(f'topsolid_read_sketch{d}d_geometry', {'sketch':sketches[0]['element'], 'kind':kind, 'limit':7})
        if 'Assembly' in dtype:
            read('topsolid_list_assembly_occurrences', {'documentId':doc, 'limit':7})
        if 'Cam.' in dtype or 'CAM' in dtype:
            read('topsolid_list_cam_operation_summaries', {'documentId':doc, 'limit':7})
        if len(inspected) >= 6: break
    print('PASS:',len(receipts),'read-only batch calls;',len(inspected),'loaded document types. No modeling or cloud inference.',flush=True)
finally:
    (out/'live-read-receipts.json').write_text(json.dumps(receipts, ensure_ascii=False, indent=2), encoding='utf-8')
    mcp.close()
