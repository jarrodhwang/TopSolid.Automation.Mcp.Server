"""Stage/color/Undo smoke on an already-created disposable CAM copy, not a production workpiece."""
import argparse, copy, json, os, subprocess, sys, uuid
from pathlib import Path
ROOT = Path(__file__).resolve().parents[2]
sys.dont_write_bytecode = True
sys.path.insert(0, str(ROOT / 'scripts/LiveWorkflow'))
from native_workflow import Mcp

parser = argparse.ArgumentParser()
parser.add_argument('--fixture-receipt', required=True)
args = parser.parse_args()
source = json.loads(Path(args.fixture_receipt).read_text(encoding='utf-8-sig'))
assert source['fixtureName'].startswith('Studio CAM Automation disposable ')
settings = json.loads((Path(os.environ['LOCALAPPDATA'])/'TopSolid.Automation.AI.Studio/settings.json').read_text(encoding='utf-8-sig'))
out = ROOT/'artifacts/cam-automation-live'/('owned-shape-'+uuid.uuid4().hex[:8]+'.json')
state = {key: source[key] for key in ['fixtureName','documentId','originalDocumentId']}
state.update(server=settings['mcpServerPath'], steps=[])
def persist(): out.write_text(json.dumps(state,ensure_ascii=False,indent=2),encoding='utf-8')
def control(action):
    persist()
    result = subprocess.run(['powershell.exe','-NoProfile','-ExecutionPolicy','Bypass','-File',str(Path(__file__).with_name('Fixture-Control.ps1')),'-Receipt',str(out),'-Action',action],capture_output=True,text=True)
    if result.returncode: raise RuntimeError(result.stdout+result.stderr)
    return result.stdout.strip()
client = Mcp(Path(state['server']))
def write(name, arguments):
    proposal = client.rpc('topsolid/prepare', {'name':name,'arguments':arguments})
    assert all(d['documentId']==state['documentId'] for d in proposal['target'].get('affectedDocuments',[])), 'Unexpected synchronized document'
    step = {'tool':name,'arguments':copy.deepcopy(arguments),'proposal':{k:v for k,v in proposal.items() if k!='confirmationToken'},'state':'submitted'}
    state['steps'].append(step); persist()
    result = client.tool(name,arguments,proposal['confirmationToken']); step.update(state='completed',result=result)
    state['documentId']=result.get('documentId',state['documentId']);persist();return result
try:
    docs=client.tool('topsolid_list_document_summaries',{'scope':'open','limit':100})['items']
    assert any(d['documentId']==state['documentId'] and d['name']==state['fixtureName'] for d in docs)
    write('topsolid_open_document', {'documentId':state['documentId']})
    state['before']=client.tool('topsolid_get_cam_stages',{'documentId':state['documentId']})
    shape=write('topsolid_create_cylinder',{'documentId':state['documentId'],'diameter':8,'height':12,'origin':{'x':300,'y':300,'z':0},'units':'mm','name':'Studio CAM stage fixture '+uuid.uuid4().hex[:6]})['shape']
    state['afterModeling']=client.tool('topsolid_get_cam_stages',{'documentId':state['documentId']})
    assert state['afterModeling']['workingStage']==state['afterModeling']['modelingStage']
    state['modelingStageVerified']=True;persist()
    face=client.tool('topsolid_list_shape_faces',{'element':shape,'limit':1})['items'][0]
    control('machining')
    before=client.tool('topsolid_get_cam_stages',{'documentId':state['documentId']});state['beforeColor']=before
    colorArgs={'documentId':state['documentId'],'faces':[face],'color':{'r':0,'g':191,'b':255}}
    result=write('topsolid_color_shape_faces',colorArgs)
    after=client.tool('topsolid_get_cam_stages',{'documentId':state['documentId']});state['afterColor']=after
    assert after['workingStage']==after['modelingStage'] and result['readBackVerified'] and result['saved'] is False
    state['nativeFaceColorVerified']=True;persist()
    control('undo')
    undone=client.tool('topsolid_get_cam_stages',{'documentId':state['documentId']});state['afterUndo']=undone
    assert undone['workingStage']==before['workingStage'];state['stageUndoVerified']=True
    # Preview is read-only. Its original-color receipt proves Undo restored the prior face RGB.
    check=client.rpc('topsolid/prepare',{'name':'topsolid_color_shape_faces','arguments':colorArgs})
    original=state['steps'][-1]['proposal']['target']['faces'][0]['color']
    assert check['target']['faces'][0]['color']==original;state['colorUndoVerified']=True
    state['success']=True;persist();print('PASS native CAM modeling, face color and one-step color/stage Undo:',out,flush=True)
except Exception as error:
    state['error']=str(error);persist();raise
finally:
    try:control('restore')
    except Exception as error:state['restoreError']=str(error);persist()
    client.close()
