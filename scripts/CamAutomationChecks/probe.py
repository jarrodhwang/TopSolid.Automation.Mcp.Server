"""Read-only CAM stage/method/preview probe against Studio's configured MCP executable."""
import json, os, sys
from pathlib import Path
ROOT = Path(__file__).resolve().parents[2]
sys.dont_write_bytecode = True
sys.path.insert(0, str(ROOT / 'scripts/LiveWorkflow'))
from native_workflow import Mcp

settings = json.loads((Path(os.environ['LOCALAPPDATA']) / 'TopSolid.Automation.AI.Studio/settings.json').read_text(encoding='utf-8-sig'))
server = Path(settings['mcpServerPath'])
report = {'executable': str(server), 'nativeMutationPerformed': False, 'documents': []}
out = ROOT / 'artifacts/cam-automation-live/probe.json'
out.parent.mkdir(parents=True, exist_ok=True)
client = Mcp(server)
try:
    report['status'] = client.tool('topsolid_get_status')
    report['active'] = client.tool('topsolid_get_active_document')
    tools = client.rpc('tools/list', {})['tools']
    report['tools'] = [t['name'] for t in tools if t['name'] in ('topsolid_get_cam_stages', 'topsolid_inspect_cam_method', 'topsolid_execute_cam_method')]
    page = client.tool('topsolid_list_document_summaries', {'scope': 'open', 'limit': 100})
    rows = list(page['items'])
    active = report['active'].get('document', {})
    if active and not any(row['documentId'] == active['documentId'] for row in rows):
        rows.append(dict(active, type=active.get('typeFullName', '')))
    for row in rows:
        if not row.get('type', '').startswith('TopSolid.Cam.NC.'):
            continue
        item = {'documentId': row['documentId'], 'name': row.get('name'), 'type': row.get('type')}
        report['documents'].append(item)
        try:
            if 'Method' in row['type']:
                item['method'] = client.tool('topsolid_inspect_cam_method', {'pdmObjectId': row['pdmObjectId']})
                continue
            item['stages'] = client.tool('topsolid_get_cam_stages', {'documentId': row['documentId']})
            inspection = client.tool('topsolid_inspect_cam_color_geometry', {'documentId': row['documentId']})
            item['workpieces'] = inspection.get('workpieces', [])
            if len(item['workpieces']) == 1:
                part = item['workpieces'][0]['element']
                inspection = client.tool('topsolid_inspect_cam_color_geometry', {'documentId': row['documentId'], 'workpiece': part, 'limit': 5})
                item['geometry'] = [{'kind': r['kind'], 'colorSupported': r['colorSupported'], 'geometryFingerprint': r.get('geometryFingerprint')} for r in inspection['items']]
                faces = [r['target']['face'] for r in inspection['items'] if 'face' in r['target']]
                if faces:
                    item['preview'] = client.rpc('topsolid/graphicPreview', {'documentId': row['documentId'], 'workpiece': part, 'camFaceGeometry': True, 'faces': faces[:1], 'chunked': True})
                    if item['preview'].get('transferId'):
                        client.rpc('topsolid/graphicPreview', {'action': 'release', 'transferId': item['preview']['transferId']})
        except Exception as error:
            item['error'] = str(error)
    report['openPageComplete'] = not page['hasMore']
finally:
    out.write_text(json.dumps(report, ensure_ascii=False, indent=2), encoding='utf-8')
    client.close()
print(json.dumps(report, ensure_ascii=True, indent=2))
