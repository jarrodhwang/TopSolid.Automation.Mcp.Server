"""Explicitly run on a distinct disposable CAM copy; retain the copy and never save the source."""
import argparse, copy, json, os, subprocess, sys, uuid
from pathlib import Path
ROOT = Path(__file__).resolve().parents[2]
sys.dont_write_bytecode = True
sys.path.insert(0, str(ROOT / 'scripts/LiveWorkflow'))
from native_workflow import Mcp

parser=argparse.ArgumentParser()
parser.add_argument('--source-document',required=True,help='Exact inspected B-rep CAM document revision to copy')
args=parser.parse_args()
settings=json.loads((Path(os.environ['LOCALAPPDATA'])/'TopSolid.Automation.AI.Studio/settings.json').read_text(encoding='utf-8-sig'))
server=Path(settings['mcpServerPath']); client=Mcp(server)
out=ROOT/'artifacts/cam-automation-live'/('stage-fixture-'+uuid.uuid4().hex[:8]+'.json');out.parent.mkdir(parents=True,exist_ok=True)
state={'fixtureName':'Studio CAM Automation disposable '+uuid.uuid4().hex[:8],'server':str(server),'sourceDocumentId':args.source_document,'steps':[]}
def persist():out.write_text(json.dumps(state,ensure_ascii=False,indent=2),encoding='utf-8')
def control(action):
    result=subprocess.run([str(Path(os.environ['WINDIR'])/'System32/WindowsPowerShell/v1.0/powershell.exe'),'-NoProfile','-ExecutionPolicy','Bypass','-File',str(Path(__file__).with_name('Fixture-Control.ps1')),'-Receipt',str(out),'-Action',action],capture_output=True,text=True)
    if result.returncode:raise RuntimeError(result.stdout+result.stderr)
    return result.stdout.strip()
def write(name,arguments):
    proposal=client.rpc('topsolid/prepare',{'name':name,'arguments':arguments})
    step={'tool':name,'arguments':copy.deepcopy(arguments),'status':'submitted'};state['steps'].append(step);persist()
    result=client.tool(name,arguments,proposal['confirmationToken']);step.update(status='completed',result=result);persist();return result
try:
    state['originalDocumentId']=client.tool('topsolid_get_active_document')['document']['documentId']
    docs=client.tool('topsolid_list_document_summaries',{'scope':'open','limit':100})['items']
    source=next(d for d in docs if d['documentId']==args.source_document)
    assert source['type'].startswith('TopSolid.Cam.NC.')
    state['sourcePdmId']=source['pdmObjectId'];state['sourceDirtyBefore']=source['isDirty']
    state['sourceStagesBefore']=client.tool('topsolid_get_cam_stages',{'documentId':args.source_document})
    project=write('topsolid_create_project',{'name':state['fixtureName']});state['projectId']=project['pdmObjectId'];persist()
    copied=json.loads(control('copy'));state.update(documentId=copied['documentId'],fixturePdmId=copied['pdmObjectId']);persist()
    opened=write('topsolid_open_document',{'documentId':state['documentId']});state['documentId']=opened.get('documentId',state['documentId']);persist()
    before=client.tool('topsolid_get_cam_stages',{'documentId':state['documentId']});state['stagesBefore']=before
    page=client.tool('topsolid_inspect_cam_color_geometry',{'documentId':state['documentId']})
    assert len(page['workpieces'])==1
    part=page['workpieces'][0]['element'];state['workpiece']=part
    page=client.tool('topsolid_inspect_cam_color_geometry',{'documentId':state['documentId'],'workpiece':part,'limit':3})
    face=next(row for row in page['items'] if row['kind']=='face' and row['colorSupported']);state['faceBefore']=face;persist()
    palette={'Id':'stage-fixture','Version':1,'Name':'Disposable stage test','Roles':[{'Key':'test','Label':'Fixture face','Hex':'#00BFFF'}]}
    colorArgs={'documentId':state['documentId'],'workpiece':part,'modelingStage':before['modelingStage'],'palette':palette,'targets':[{'target':face['target'],'fingerprint':face['fingerprint'],'originalColor':face['color'],'roleKey':'test','group':'Stage fixture'}]}
    receipt=write('topsolid_apply_cam_color_plan',colorArgs);state['documentId']=receipt['documentId'];persist()
    after=client.tool('topsolid_get_cam_stages',{'documentId':state['documentId']});state['stagesAfter']=after
    assert after['workingStage']['id']==after['modelingStage']['id']
    assert receipt['saved'] is False and receipt['readBackVerified'] is True
    state['nativeColorInModelingStage']=True;persist()
    control('undo')
    undone=client.tool('topsolid_get_cam_stages',{'documentId':state['documentId']});state['stagesAfterUndo']=undone
    assert undone['workingStage']['id']==before['workingStage']['id'], 'Undo did not restore working stage'
    state['stageUndoRestored']=True
    def rebase(value):
        if isinstance(value,dict):
            for key,item in value.items():
                if key=='documentId':value[key]=state['documentId']
                else:rebase(item)
        elif isinstance(value,list):
            for item in value:rebase(item)
    target=copy.deepcopy(face['target']);rebase(target);rebase(part)
    readback=client.tool('topsolid_inspect_cam_color_geometry',{'documentId':state['documentId'],'workpiece':part,'targets':[target]})['items'][0]
    assert readback['color']==face['color'];state['colorUndoRestored']=True
    state['sourceStagesAfter']=client.tool('topsolid_get_cam_stages',{'documentId':args.source_document})
    assert state['sourceStagesAfter']==state['sourceStagesBefore'];state['sourceStagesPreserved']=True
    state['success']=True;persist();print('PASS native CAM modeling-stage color application and one-step color/stage Undo. No method execution or save. Receipt:',out)
except Exception as error:
    state['error']=str(error);persist();raise
finally:
    if state.get('documentId'):
        try:control('restore')
        except Exception as error:state['restoreError']=str(error);persist()
    client.close()
