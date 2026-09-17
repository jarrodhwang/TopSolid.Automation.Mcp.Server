"""Controlled, opt-in native integration test. Default mode only performs preflight reads.

Requires explicit approval of fixture-plan.json via --approve-plan <SHA256>.
Never shipped with the application. Never obtains approval from model output.
"""
import argparse, copy, hashlib, json, math, subprocess, threading
from pathlib import Path

ROOT = Path(__file__).resolve().parents[2]
PLAN_PATH = Path(__file__).with_name('fixture-plan.json')
PLAN = json.loads(PLAN_PATH.read_text(encoding='utf-8'))
PLAN_HASH = hashlib.sha256(PLAN_PATH.read_bytes()).hexdigest()
OUT = ROOT / 'artifacts/native-workflow'

class Mcp:
    def __init__(self, server):
        self.process = subprocess.Popen([str(server)], stdin=subprocess.PIPE, stdout=subprocess.PIPE, stderr=subprocess.PIPE, text=True, encoding='utf-8')
        self.n = 0
        self.errors = []
        self.drain = threading.Thread(target=lambda: self.errors.extend(self.process.stderr), daemon=True)
        self.drain.start()
        self.rpc('initialize', {'protocolVersion':'2025-03-26','capabilities':{},'clientInfo':{'name':'explicitly-approved-native-fixture','version':'0.3.0'}})
        self.process.stdin.write('{"jsonrpc":"2.0","method":"notifications/initialized"}\n'); self.process.stdin.flush()
    def rpc(self, method, params):
        self.n += 1
        self.process.stdin.write(json.dumps({'jsonrpc':'2.0','id':self.n,'method':method,'params':params})+'\n'); self.process.stdin.flush()
        reply = json.loads(self.process.stdout.readline())
        if reply.get('id') != self.n: raise RuntimeError('MCP response ID mismatch')
        if 'error' in reply: raise RuntimeError(json.dumps(reply['error']))
        return reply['result']
    def tool(self, name, args=None, token=None):
        params = {'name':name,'arguments':args or {}}
        if token: params['_meta'] = {'confirmationToken':token}
        result = self.rpc('tools/call', params)
        if result.get('isError'): raise RuntimeError(json.dumps(result))
        return json.loads(result['content'][0]['text'])
    def close(self):
        self.process.stdin.close()
        self.process.wait(timeout=20)  # Never kill an in-flight modification.

def preflight(mcp):
    status = mcp.tool('topsolid_get_status')
    if not status.get('connected'): raise RuntimeError('TopSolid is not connected: '+json.dumps(status))
    templates = mcp.tool('topsolid_get_template_projects')
    queue = [x for k,x in templates.items() if 'Document' in k and x]
    # Empty user template stores are normal. Existing working-document metadata also
    # verifies extensions without opening or modifying any of those documents.
    queue.extend(item['pdmObjectId'] for item in mcp.tool('topsolid_list_projects', {'limit':100})['items'])
    candidates = {}; seen = set()
    while queue and len(seen) < 100 and not {'.TopPrt','.TopAsm'}.issubset(candidates):
        parent = queue.pop(0)
        if parent in seen: continue
        seen.add(parent)
        children = mcp.tool('topsolid_list_pdm_children', {'pdmObjectId':parent,'limit':100})
        queue.extend(child['pdmObjectId'] for child in children['items'] if child['kind'] == 'folder')
        document_ids = [child['pdmObjectId'] for child in children['items'] if child['kind'] == 'document']
        if document_ids:
            offset = 0
            while True:
                details = mcp.tool('topsolid_inspect_pdm_objects', {'pdmObjectIds':document_ids,'offset':offset})
                for info in details['items']:
                    if not info.get('isError') and info.get('extension'): candidates.setdefault(info['extension'], []).append(info)
                if not details['hasMore']: break
                offset = details['nextOffset']
    report = {'host':status,'templates':candidates,'planSha256':PLAN_HASH,'plan':PLAN}
    OUT.mkdir(parents=True,exist_ok=True)
    (OUT/'preflight.json').write_text(json.dumps(report,ensure_ascii=False,indent=2),encoding='utf-8')
    print('Plan SHA256:',PLAN_HASH,flush=True)
    print('Verified live document extensions:', ', '.join(candidates),flush=True)
    return candidates

def run(mcp, templates):
    docs = {}; handles = {}; receipts = []; project = None
    def persist():
        (OUT/'receipts.json').write_text(json.dumps({'planSha256':PLAN_HASH,'projectId':project,'documents':docs,'handles':handles,'receipts':receipts},ensure_ascii=False,indent=2),encoding='utf-8')
    def rebase(value, old, new):
        if isinstance(value,dict):
            for key,v in value.items():
                if key == 'documentId' and v == old: value[key] = new
                else: rebase(v,old,new)
        elif isinstance(value,list):
            for v in value: rebase(v,old,new)
    def write(name,args,label=None):
        nonlocal project
        if name == 'topsolid_create_project':
            assert project is None and args == {'name':PLAN['projectName']}
        elif name == 'topsolid_create_document':
            assert project and args['ownerId'] == project and args['name'] in PLAN['documents']
        else:
            assert project and label in docs and args['documentId'] == docs[label]
        proposal = mcp.rpc('topsolid/prepare', {'name':name,'arguments':args})
        assert proposal['toolName'] == name and proposal['arguments'] == args
        assert all(d['documentId'] in docs.values() for d in proposal['target'].get('affectedDocuments', [])), 'Fixture approval never includes existing synchronized documents'
        visible = {k:v for k,v in proposal.items() if k != 'confirmationToken'}
        receipt = {'tool':name,'arguments':copy.deepcopy(args),'proposal':visible,'state':'submitted'}
        receipts.append(receipt); persist()
        try: result = mcp.tool(name,args,proposal['confirmationToken'])
        except Exception as ex: receipt.update(state='failed-or-uncertain',error=str(ex)); persist(); raise
        receipt.update(state='completed',result=copy.deepcopy(result))
        if label and result.get('documentId'):
            old = docs.get(label); docs[label] = result['documentId']
            if old: rebase(handles,old,docs[label])
        persist(); print('PASS',name,flush=True)
        return result
    def document(label,extension):
        if extension not in templates: raise RuntimeError('No live template verified for '+extension)
        result = write('topsolid_create_document',{'ownerId':project,'name':label,'extension':extension,'useDefaultTemplate':False})
        docs[label] = result['documentId'];persist()
        write('topsolid_open_document',{'documentId':docs[label]},label)
    def contour(label,key,points,placement='xy',**extra):
        args = {'documentId':docs[label],'placement':placement,'units':'mm','name':key,'createSection':True,
                'contour':{'start':{'x':points[0][0],'y':points[0][1]},'closed':True,'segments':[{'kind':'line','end':{'x':p[0],'y':p[1]}} for p in points[1:]]},**extra}
        result = write('topsolid_create_contour2d',args,label); handles[key]=result;persist();return result
    project = write('topsolid_create_project',{'name':PLAN['projectName']})['pdmObjectId'];persist()
    plate,tube,loft,assembly = PLAN['documents']
    document(plate,'.TopPrt')
    c = contour(plate,'Base Contour',[(0,0),(80,0),(80,50),(0,50),(0,0)])
    result = write('topsolid_extrude_sketch',{'documentId':docs[plate],'section':c['section'],'length':20,'direction':{'x':0,'y':0,'z':1},'units':'mm','name':'Validation Block'},plate)
    assert abs(result['volumeCubicMetres']-.08*.05*.02) < 1e-10
    handles['block']=result;persist()
    write('topsolid_save_document',{'documentId':docs[plate]},plate)
    renamed = write('topsolid_rename_element',{'documentId':docs[plate],'element':handles['Base Contour']['sketch'],'name':'Validated Base Contour'},plate)
    assert renamed['name'] == 'Validated Base Contour'
    write('topsolid_create_contour2d',{'documentId':docs[plate],'placement':'xy','units':'mm','x':120,'name':'Arc Validation',
          'contour':{'start':{'x':10,'y':0},'closed':False,'segments':[{'kind':'arc','end':{'x':0,'y':10},'center':{'x':0,'y':0},'clockwise':False}]}},plate)
    document(tube,'.TopPrt')
    c = contour(tube,'Revolution Contour',[(10,0),(20,0),(20,30),(10,30),(10,0)],'xz')
    result = write('topsolid_revolve_sketch',{'documentId':docs[tube],'section':c['section'],'axisOrigin':{'x':0,'y':0,'z':0},'axisDirection':{'x':0,'y':0,'z':1},'angleDegrees':360,'units':'mm'},tube)
    assert abs(result['volumeCubicMetres']-math.pi*(.02**2-.01**2)*.03) < 1e-10
    document(loft,'.TopPrt')
    profiles=[]
    for radius,z in [(10,0),(20,30)]:
        result=write('topsolid_create_circle2d',{'documentId':docs[loft],'placement':'xy','radius':radius,'z':z,'units':'mm'},loft)
        profiles.append(result['profile'])
    result=write('topsolid_loft_sketch',{'documentId':docs[loft],'profiles':profiles},loft)
    assert abs(result['volumeCubicMetres']-math.pi*.03*(.01**2+.01*.02+.02**2)/3) < 1e-9
    document(assembly,'.TopAsm')
    for source,x in [(plate,0),(tube,120)]:
        write('topsolid_include_assembly_document',{'documentId':docs[assembly],'sourceDocumentId':docs[source],'translation':{'x':x,'y':0,'z':0},'units':'mm','fixed':True},assembly)
    assert mcp.tool('topsolid_list_assembly_parts',{'documentId':docs[assembly]})['total'] == 2
    for label in docs: write('topsolid_save_document',{'documentId':docs[label]},label)
    persist(); print('COMPLETE: native integration workflow; results retained for review.',flush=True)

if __name__ == '__main__':
    parser=argparse.ArgumentParser();parser.add_argument('--approve-plan');parser.add_argument('--server',type=Path,default=ROOT/'TopSolid.Automation.Mcp.Server.AddIn/bin/Release/net48/TopSolid.Automation.Mcp.Server.AddIn.exe');options=parser.parse_args()
    if options.approve_plan and options.approve_plan != PLAN_HASH: raise SystemExit('Approval does not match the exact current fixture plan.')
    client=Mcp(options.server)
    try:
        templates=preflight(client)
        if options.approve_plan: run(client,templates)
        else: print('Read-only preflight complete. No TopSolid document changes were requested.',flush=True)
    finally: client.close()
