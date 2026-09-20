"""Opt-in disposable native color/Undo check using Studio's configured MCP binary.

Default: read-only connection check. --run-fixture creates a uniquely named PDM
project and part, leaves them for inspection, and restores the original active
document. Never saves colors, runs CAM methods, generates NC, or deletes user data.
"""
import argparse, copy, json, os, subprocess, sys, uuid
from pathlib import Path

sys.dont_write_bytecode = True
ROOT = Path(__file__).resolve().parents[2]
sys.path.insert(0, str(ROOT / 'scripts/LiveWorkflow'))
from native_workflow import Mcp

OUT = ROOT / 'artifacts/cam-color-live'
CONTROL = Path(__file__).with_name('Fixture-Control.ps1')
PALETTE = {'Id':'studio-cam-colors','Version':1,'Name':'Studio CAM Colors v1','Roles':[
    {'Key':k,'Label':n,'Hex':v} for k,n,v in [
        ('facing','Facing region','#00BFFF'),('roughing','Roughing region','#FF8000'),
        ('pocket','Pocket region','#0066FF'),('hole','Hole / bore','#FFFF00'),
        ('contour','Contour / boundary','#00CC66'),('finish','Finish surface','#AA55FF'),
        ('keep-out','Keep-out / protected','#FF0000'),('reference','Reference geometry','#FF00FF')]]}

def main():
    parser=argparse.ArgumentParser();parser.add_argument('--run-fixture',action='store_true');parser.add_argument('--two-d',action='store_true');args=parser.parse_args()
    settings=json.loads((Path(os.environ['LOCALAPPDATA'])/'TopSolid.Automation.AI.Studio/settings.json').read_text(encoding='utf-8-sig'))
    server=Path(settings['mcpServerPath']); mcp=Mcp(server)
    state={'server':str(server),'steps':[]};receipt=OUT/('fixture-'+uuid.uuid4().hex[:8]+'.json')
    OUT.mkdir(parents=True,exist_ok=True)
    def persist():receipt.write_text(json.dumps(state,indent=2),encoding='utf8')
    def write(name,arguments):
        prepared=mcp.rpc('topsolid/prepare',{'name':name,'arguments':arguments})
        assert prepared['toolName']==name and prepared['arguments']==arguments
        step={'tool':name,'arguments':copy.deepcopy(arguments),'state':'submitted'};state['steps'].append(step);persist()
        result=mcp.tool(name,arguments,prepared['confirmationToken'])
        step.update(state='completed',result=result);persist();return result
    def pages():
        rows=[];offset=0
        while True:
            page=mcp.tool('topsolid_inspect_cam_color_geometry',{'documentId':state['documentId'],'offset':offset,'limit':20})
            rows.extend(page['items'])
            if not page['hasMore']:
                assert len(rows)==page['total'];return rows
            offset=page['nextOffset']
    def plan(pairs):
        return {'documentId':state['documentId'],'palette':PALETTE,'targets':[
            {'target':r['target'],'fingerprint':r['fingerprint'],'originalColor':r['color'],'roleKey':role,'group':'Fixture '+role} for r,role in pairs]}
    def control(action):
        result=subprocess.run([str(Path(os.environ['WINDIR'])/'System32/WindowsPowerShell/v1.0/powershell.exe'),'-NoProfile','-ExecutionPolicy','Bypass','-File',str(CONTROL),'-Receipt',str(receipt),'-Action',action],capture_output=True,text=True)
        if result.returncode:raise RuntimeError(result.stdout+result.stderr)
        print(result.stdout.strip(),flush=True)
    try:
        state['host']=mcp.tool('topsolid_get_status');assert state['host']['connected']
        state['originalDocumentId']=mcp.tool('topsolid_get_active_document').get('document',{}).get('documentId')
        if not args.run_fixture:print('Read-only preflight passed:',server);return
        state['fixtureName']='Studio CAM Colors disposable '+uuid.uuid4().hex[:8]
        project=write('topsolid_create_project',{'name':state['fixtureName']})
        state['projectId']=project['pdmObjectId']
        doc=write('topsolid_create_document',{'ownerId':state['projectId'],'name':state['fixtureName'],'extension':'.Top2D' if args.two_d else '.TopPrt','useDefaultTemplate':False})
        state['documentId']=doc['documentId'];persist()
        opened=write('topsolid_open_document',{'documentId':state['documentId']});state['documentId']=opened.get('documentId',state['documentId'])
        if args.two_d:
            rectangle=write('topsolid_create_rectangle2d',{'documentId':state['documentId'],'placement':'2d','width':40,'height':20,'units':'mm'})
            state['documentId']=rectangle['documentId'];persist()
            points=write('topsolid_create_points2d',{'documentId':state['documentId'],'points':[{'point':{'x':50,'y':10},'name':'Color fixture point'}],'units':'mm'})
            state['documentId']=points['documentId'];persist();before=pages()
            sketch=next(r for r in before if r['kind']=='sketch2d' and r['colorSupported'])
            point=next(r for r in before if r['kind']=='point2d' and r['name']=='Color fixture point' and r['colorSupported'])
            arguments=plan([(sketch,'contour'),(point,'reference')])
        else:
            cylinder=write('topsolid_create_cylinder',{'documentId':state['documentId'],'diameter':40,'height':20,'units':'mm','name':'Color fixture cylinder'})
            state['documentId']=cylinder['documentId'];persist()
            rows=pages();faces=[r for r in rows if r['kind']=='face'];assert len(faces)==3
            baseline=write('topsolid_apply_cam_color_plan',plan([(faces[0],'keep-out')]))
            state['documentId']=baseline['documentId'];persist();before=pages()
            shape=next(r for r in before if r['kind']=='shape' and r['colorSupported'])
            sketch=next(r for r in before if r['kind']=='sketch2d' and r['colorSupported'])
            faces=[r for r in before if r['kind']=='face']
            arguments=plan([(shape,'roughing'),(sketch,'reference'),(faces[1],'facing')])
        state['before']=before;persist()
        result=write('topsolid_apply_cam_color_plan',arguments);state['documentId']=result['documentId'];persist()
        assert result['saved'] is False and result['readBackVerified'] is True and len(result['items'])==len(arguments['targets'])
        if not args.two_d:
            after=pages();protected=next(r for r in after if r['key']==faces[0]['key'])
            assert protected['color']==faces[0]['color'], 'Existing face override changed'
            state['exactRgbAndOverridePreservation']=True
        else:state['native2dSketchAndPointRgbVerified']=True
        try:mcp.rpc('topsolid/prepare',{'name':'topsolid_apply_cam_color_plan','arguments':arguments})
        except RuntimeError:state['stalePlanRejected']=True
        else:raise AssertionError('Stale color plan accepted')
        persist();control('undo')
        undone=pages()
        assert {r['key']:r['color'] for r in undone}=={r['key']:r['color'] for r in before},'Undo did not restore all colors'
        state['nativeUndoRestoredWholeBatch']=True;state['success']=True;persist()
        print('PASS native exact RGB, stale rejection, saved=false and single native Undo ('+('2D sketch and point' if args.two_d else 'mixed entity/face colors and existing overrides')+').',flush=True)
        print('Receipt:',receipt,flush=True)
    finally:
        if state.get('documentId'):
            try:control('restore')
            except Exception as error:state['restoreError']=str(error);persist();print('Restore failed:',error)
        mcp.close()

if __name__=='__main__':main()
