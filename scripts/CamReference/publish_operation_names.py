"""Publish reviewed label mappings; discovery candidates never become labels automatically."""
import json
from pathlib import Path
from collect_operation_catalog import ROOT, OUT

# Exact native suffix | exact help-path suffix | project Korean translation | category
# The UI command namespace/family and installed type were reviewed together.
# Korean strings are project translations, not a claim of official Korean terminology.
REVIEWED = """
MillTurn.DB.EndMilling.EndMillingOperation|/UI/endmilling/Commands/EndMillingOperationCommand.htm|엔드 밀링|milling
MillTurn.DB.EndMilling.NibblingOperation|/UI/endmilling/Commands/nibblingoperationcommand.htm|윤곽 황삭|milling
MillTurn.DB.SideMilling.SideMillingOperation|/UI/sidemilling/commands/SideMillingOperationCommand.htm|사이드 밀링|milling
MillTurn.DB.SlotMilling.SlotMillingOperation|/UI/slotmilling/Commands/slotmillingOperationCommand.htm|슬롯 밀링|milling
MillTurn.DB.TSlotMilling.TSlotMillingOperation|/UI/tslotmilling/commands/tslotmillingoperationcommand.htm|T 슬롯 밀링|milling
MillTurn.DB.ChamferMilling.ChamferMillingOperation|/UI/chamfermilling/Commands/chamfermillingOperationCommand.htm|모따기|milling
MillTurn.DB.CornerRounding.CornerRoundingOperation|/UI/cornerrounding/commands/cornerroundingOperationCommand.htm|모서리 라운딩|milling
MillTurn.DB.MouseFacing.Operations.MouseFacingOperation|/UI/mousefacing/commands/mousefacingoperationcommand.htm|마우스 지정 평면 가공|milling
MillTurn.DB.Broaching.Operation.BroachingOperation|/UI/broaching/commands/broachingoperationcommand.htm|브로칭|milling
MillTurn.DB.ThreadMilling.ThreadMillingOperation|/UI/threadmilling/commands/threadmillingoperationcommand.htm|외부 나사 밀링|milling
MillTurn.DB.BreakingEdges.BreakingEdgesOperation|/MillTurn/UI/breakingedges/Commands/breakingedgesOperationCommand.htm|모서리 제거|milling
MillTurn.DB.Engraving.EngravingOperation|/UI/engraving/commands/engravingoperationcommand.htm|각인|milling
MillTurn.DB.CuttingHoldingTabs.CuttingHoldingTabsOperation|/UI/CuttingHoldingTabs/Commands/CuttingHoldingTabsOperationCommand.htm|고정 탭 절단|milling
MillTurn.DB.Plunge.PlungePocketOperation|/UI/plunge/commands/plungepocketoperationcommand.htm|플런지 포켓 가공|milling
MillTurn.DB.Plunge.PlungeSideOperation|/UI/plunge/commands/plungesideoperationcommand.htm|플런지 윤곽 가공|milling
MillTurn.DB.Plunge.PlungeStampPocketOperation|/UI/plunge/commands/plungestamppocketoperationcommand.htm|펀칭 포켓 가공|milling
MillTurn.DB.PointToPoint.Operations.PointToPointOperation|/UI/pointtopoint/commands/PointToPointOperationCommand.htm|구멍 가공|hole-family
MillTurn.DB.PointToPoint.Operations.HoleOperation|/UI/pointtopoint/commands/PointToPointOperationCommand.htm|구멍 가공|hole-family
MillTurn.Form.DB.Roughing.RoughingOperation|/Form/UI/Roughing/commands/roughingoperationcommand.htm|형상 황삭|form
MillTurn.Form.DB.Roughing.Planar.PlanarRoughingOperation|/Form/UI/Roughing/commands/planarroughingoperationcommand.htm|평면 영역 가공|form
MillTurn.Form.DB.Roughing.UnderCutting.UnderCuttingRoughingOperation|/Form/UI/Roughing/commands/undercuttingroughingoperationcommand.htm|언더컷 황삭|form
MillTurn.Form.DB.Finishing.FinishingOperation|/Form/UI/finishing/commands/finishingoperationcommand.htm|3D 정삭|form
MillTurn.Form.DB.Finishing.UnderCuttingHncOperation|/Form/UI/finishing/commands/undercuttinghncoperationcommand.htm|언더컷 등고선 가공|form
MillTurn.Form.DB.GreatFinish.GreatFinishOperation|/Form/UI/Greatfinish/Commands/greatfinishoperationcommand.htm|초정삭|form
MillTurn.Form.DB.Sweeping.Operation.SweepingOperation|/Form/UI/sweeping/commands/sweepingoperationcommand.htm|스위핑|form
MillTurn.Form.DB.Matleft.MatleftOperation|/Form/UI/matleft/commands/matleftoperationcommand.htm|3D 잔삭|form
MillTurn.Form.DB.Contouring.ContouringOperation|/Form/UI/Contouring/commands/contouringoperationcommand.htm|3D 윤곽 가공|form
MillTurn.Form.DB.Pipe.SweptOperation|/Form/UI/pipe/commands/sweptoperationcommand.htm|스웹트 가공|form
MillTurn.Form.DB.VerticalRoughing.VerticalRoughingOperation|/Form/UI/verticalroughing/Commands/verticalroughingoperationcommand.htm|플런지 황삭|form
MillTurn.Form.DB.Roughing.FourAxis.FourAxisRoughingOperation|/Form/UI/Roughing/commands/fouraxisroughingoperationcommand.htm|4축 반경 방향 황삭|multi-axis
MillTurn.Form.DB.Finishing.AutomaticRadialOperation|/Form/UI/finishing/commands/automaticradialoperationcommand.htm|4축 반경 방향 정삭|multi-axis
MillTurn.Form.DB.ScrewMilling.ScrewMillingOperation|/Form/UI/screwmilling/commands/screwmillingoperationcommand.htm|스크루 밀링|multi-axis
MillTurn.Form.DB.Finishing.FiveAxisHncOperation|/Form/UI/finishing/commands/fiveaxishncoperationcommand.htm|5축 등고선 가공|multi-axis
MillTurn.FiveAxis.DB.Contour.ContourOperation|/fiveaxis/ui/contour/commands/contouroperationcommand.htm|5축 윤곽 가공|multi-axis
MillTurn.FiveAxis.DB.Swarf.SwarfOperation|/fiveaxis/ui/swarf/commands/swarfoperationcommand.htm|스와프 가공|multi-axis
MillTurn.FiveAxis.DB.Auto5x.Auto5xOperation|/fiveaxis/ui/auto5x/commands/auto5xoperationcommand.htm|자동 5축 가공|multi-axis
MillTurn.FiveAxis.DB.MultiBlade.MultiBladeRoughingOperation|/fiveaxis/ui/multiblade/commands/multibladeroughingoperationcommand.htm|블레이드 황삭|multi-axis
MillTurn.FiveAxis.DB.MultiBlade.MultiBladeHubFinishingOperation|/fiveaxis/ui/multiblade/commands/multibladehubfinishingoperationcommand.htm|허브 정삭|multi-axis
MillTurn.FiveAxis.DB.MultiBlade.MultiBladeBladeFinishingOperation|/fiveaxis/ui/multiblade/commands/multibladebladefinishingoperationcommand.htm|블레이드 정삭|multi-axis
MillTurn.FiveAxis.DB.MultiBlade.MultiBladeFilletFinishingOperation|/fiveaxis/ui/multiblade/commands/multibladefilletfinishingoperationcommand.htm|블레이드 필렛 가공|multi-axis
MillTurn.FiveAxis.DB.Deburring.DeburringOperation|/fiveaxis/ui/deburring/commands/deburringoperationcommand.htm|5축 디버링|multi-axis
MillTurn.FiveAxis.DB.MultiAxisRoughing.MultiAxisRoughingOperation|/fiveaxis/ui/multiaxisroughing/commands/multiaxisroughingoperationcommand.htm|다축 포켓 가공|multi-axis
MillTurn.FiveAxis.DB.PortMachining.PortMachiningOperation|/fiveaxis/ui/portmachining/commands/portmachiningoperationcommand.htm|포트 가공|multi-axis
MillTurn.FiveAxis.DB.Roughing3Plus2.Roughing3Plus2Operation|/fiveaxis/ui/roughing3plus2/commands/roughing3plus2operationcommand.htm|3+2축 황삭|multi-axis
MillTurn.FiveAxis.DB.RotaryMachining.RotaryMachiningOperation|/fiveaxis/ui/rotarymachining/commands/rotarymachiningoperationcommand.htm|고급 4축 반경 방향 가공|multi-axis
MillTurn.FiveAxis.DB.GeodesicMachining.GeodesicMachiningOperation|/fiveaxis/ui/geodesicmachining/commands/geodesicmachiningoperationcommand.htm|5축 정삭|multi-axis
MillTurn.Turn.DB.Roughing.RoughingOperation|/turn/ui/roughing/commands/roughingoperationcommand.htm|선삭 황삭|turning
MillTurn.Turn.DB.Finishing.FinishingOperation|/turn/ui/finishing/commands/finishingoperationcommand.htm|선삭 정삭|turning
MillTurn.Turn.DB.Groove.GrooveOperation|/turn/ui/groove/commands/grooveoperationcommand.htm|홈 가공·절단|turning
MillTurn.Turn.DB.Threading.ThreadingOperation|/turn/ui/threading/commands/threadingoperationcommand.htm|선삭 나사 가공|turning
MillTurn.Turn.DB.BreakingEdges.BreakingEdgesOperation|/turn/ui/breakingedges/commands/breakingedgesoperationcommand.htm|선삭 모서리 제거|turning
MillTurn.Turn.DB.AngleGrooving.AngleGroovingOperation|/turn/ui/anglegrooving/commands/anglegroovingoperationcommand.htm|코너 릴리프|turning
MillTurn.Turn.DB.PolygonalTurning.PolygonalTurningOperation|/turn/ui/polygonalturning/commands/polygonalturningoperationcommand.htm|다각형 선삭|turning
MillTurn.Turn.DB.Movement.MovementOperation|/turn/ui/movement/commands/movementoperationcommand.htm|선삭 이동|auxiliary
MillTurn.Grinding.DB.CylindricalContouring.CylindricalContouringOperation|/grinding/ui/cylindricalcontouring/commands/cylindricalcontouringoperationcommand.htm|원통 윤곽 연삭|grinding
MillTurn.Grinding.DB.CylindricalFacing.CylindricalFacingOperation|/grinding/ui/cylindricalfacing/commands/cylindricalfacingoperationcommand.htm|원통 단면 연삭|grinding
MillTurn.Grinding.DB.Contour.ContourOperation|/grinding/ui/contour/commands/contouroperationcommand.htm|윤곽 연삭|grinding
MillTurn.Grinding.DB.Facing.FacingOperation|/grinding/ui/facing/commands/facingoperationcommand.htm|평면 연삭|grinding
MillTurn.DB.VirtualJog.Operation.VirtualJogOperation|/UI/virtualjog/commands/virtualjogoperationcommand.htm|가상 조그|auxiliary
MillTurn.DB.ControlPoints.Operation.ControlPointsOperation|/UI/controlpoints/commands/controlpointsoperationcommand.htm|제어점|auxiliary
Kernel.DB.Annex.Operation.EnvironmentOperation|/annex/dialogs/environment/geometrypane.htm|환경 활성|auxiliary
Kernel.DB.Annex.Operation.StockModificationOperation|/annex/dialogs/stockmodification/geometrypane.htm|소재 변경|auxiliary
Kernel.DB.Annex.Operation.MachineElementsActivationOperation|/annex/commands/machineelementsactivationcommand.htm|기계 요소 활성|auxiliary
Kernel.DB.Operations.Repetitions.RepetitionNcOperation|/repetitions/repetitioncommand.htm|가공 반복|auxiliary
Kernel.DB.PartSetting.GrindingReserve.GrindingReserveOperation|/partsetting/grindingreserve/grindingreservecommand.htm|연삭 여유|auxiliary
Kernel.DB.Operations.SketchToolPath.SketchToolPathOperation|/sketchtoolpath/sketchtoolpathcommand.htm|3D 공구 경로 스케치|geometry
MillTurn.DB.Operations.IsoparametricCurvesOnParallelOperation|/UI/commands/isoparametriccurvesonparallelcommand.htm|평행면 등매개변수 곡선|geometry
MillTurn.DB.Operations.SketchResidualProfilesOperation|/UI/commands/sketchresidualprofilescommand.htm|3D 잔삭 스케치|geometry
MillTurn.Form.DB.SketchAngularSeparationOperation|/Form/UI/commands/sketchangularseparationcommand.htm|각도 분리 프로파일|geometry
MillTurn.Form.DB.SketchPlanarFacesOperation|/Form/UI/commands/sketchplanarfacescommand.htm|평면 곡선 스케치|geometry
MillTurn.Form.DB.SketchContactAreaToolOperation|/Form/UI/commands/sketchcontactareatoolcommand.htm|공구 접촉 영역 스케치|geometry
MillTurn.Form.DB.SketchToolShapeRestAreaOperation|/CommonDialogBox/sketchtoolshaperestareacommand.htm|공구 형상 기준 잔여 소재 스케치|geometry
"""


def write_json(path, value):
    path.parent.mkdir(parents=True, exist_ok=True)
    path.write_text(json.dumps(value, ensure_ascii=False, indent=2) + '\n', encoding='utf-8')


def main():
    discovery = json.loads((OUT / 'discovery.json').read_text(encoding='utf-8'))
    assemblies = json.loads((OUT / 'installed-types.json').read_text(encoding='utf-8'))
    installed = {t['nativeType']: {**t, 'assembly': a['assembly'], 'version': a['version'], 'assemblySha256': a['sha256']}
                 for a in assemblies for t in a['types']}
    api = {t['nativeType']: t for t in discovery['operationTypes']}
    icons = {t['NativeType']: t for t in json.loads((ROOT / 'TopSolid.Automation.AI.Studio/Assets/TopSolid/provenance.json').read_text(encoding='utf-8-sig')) if 'NativeType' in t}
    mapped = {}
    for line in REVIEWED.strip().splitlines():
        suffix, help_suffix, ko, category = line.split('|')
        native = 'TopSolid.Cam.NC.' + suffix
        if native not in installed or installed[native]['isAbstract']:
            raise ValueError('Reviewed mapping needs an installed concrete type: ' + native)
        topics = [t for t in discovery['helpTopics'] if ('/' + t['path'].lower()).endswith(help_suffix.lower())]
        if len(topics) != 1 or not topics[0].get('pageVerified'):
            raise ValueError('Reviewed mapping needs one verified help page: ' + help_suffix)
        topic = topics[0]
        en = topic['pageTitle']
        notes = 'UI command family and exact installed namespace reviewed together; not a documented API-to-UI contract.'
        if category == 'hole-family':
            en = 'Hole machining'
            notes += ' The official Drilling page covers drilling, reaming, tapping and countersinking; the class alone does not identify the cycle.'
        if suffix.endswith('.EnvironmentOperation'):
            en = 'Environment activation'
            notes += ' Environment activation is the user-requested label; official help calls the command Environment Management.'
        if suffix.endswith('.SweepingOperation'):
            en = 'Sweeping'
            notes += ' Short display name requested by the user; official topic title is 3D Sweeping.'
        mapped[native] = {'nativeType': native, 'englishName': en, 'koreanName': ko, 'category': category,
            'officialHelpTitle': topic['pageTitle'], 'officialTocTitle': topic['title'], 'helpUrl': topic['url'],
            'helpTextSha256': topic['htmlTextSha256'], 'apiUrl': api.get(native, {}).get('apiUrl'),
            'typeEvidence': installed[native], 'translationStatus': 'project-translation', 'mappingStatus': 'reviewed-help-and-installed-type', 'notes': notes}

    inventory = []
    for native in sorted(set(installed) | set(api) | set(icons)):
        row = {'nativeType': native, 'status': 'mapped' if native in mapped else 'unresolved-display-name',
               'installed': installed.get(native), 'iconKey': icons.get(native, {}).get('Key'),
               'iconSourceVersion': '7.19' if native in icons else None,
               'api': {k: v for k, v in api.get(native, {}).items() if k in ('apiTitle', 'apiDescription', 'apiUrl', 'apiAssembly', 'pageVerified', 'htmlTextSha256', 'abstract')},
               'display': mapped.get(native)}
        if native not in mapped:
            row['reason'] = 'No reviewed official user-facing name; do not infer a machining strategy from the CLR suffix.'
            if native not in installed:
                row['reason'] += ' Not found as an exact Operation-suffixed class in the installed 7.20 CAM DLL scan.'
            elif installed[native]['isAbstract']:
                row['reason'] += ' Abstract/base class, not a distinct selectable machining strategy.'
        inventory.append(row)

    metadata = {'schemaVersion': 1, 'sourceVersion': 'TopSolid Help 7.20', 'collectedUtc': discovery['collectedUtc'],
                'koreanNamesAreOfficial': False, 'entries': list(mapped.values())}
    write_json(ROOT / 'TopSolid.Automation.AI.Studio/Assets/Reference/cam-operation-names.json', metadata)
    report = {'collectedUtc': discovery['collectedUtc'], 'scope': 'Union of existing 7.19 icon keys, documented 7.20 ADS CAM DB Operation classes and installed Operation-suffixed CAM DB classes; not a complete list of machining strategies.',
              'helpTopicsVerified': sum(t.get('pageVerified', False) for t in discovery['helpTopics']),
              'apiTypesVerified': sum(t.get('pageVerified', False) for t in api.values()),
              'installedTypes': len(installed), 'mappedTypes': len(mapped), 'inventory': inventory,
              'helpTopics': [{k: v for k, v in t.items() if k != 'links'} for t in discovery['helpTopics']]}
    write_json(ROOT / 'docs/cam/operation-type-audit.json', report)
    lines = ['# TopSolid 7.20 오퍼레이션 표시명 대조표', '',
        f"공식 사용자 도움말 {report['helpTopicsVerified']}개, ADS 클래스 문서 {report['apiTypesVerified']}개, 설치 DLL의 Operation 접미사 CAM DB 클래스 {len(installed)}개를 대조했다. 확인된 표시명 매핑은 {len(mapped)}개다.", '',
        '영문은 공식 도움말의 제목을 기준으로 하며, 한국어는 프로젝트 번역이다. 환경 활성과 스위핑은 사용자 지정 간결한 표시명을 유지한다. 내부 타입과 도움말 명령의 연결은 모듈·명령 패밀리를 검토한 매핑이며 공식 API 계약으로 주장하지 않는다.', '',
        '## 검증된 표시명', '', '| 분류 | 내부 타입 | 공식 도움말 제목 | 한국어 표시명 | 근거 |', '|---|---|---|---|---|']
    for row in mapped.values():
        lines.append(f"| {row['category']} | `{row['nativeType'].removeprefix('TopSolid.Cam.NC.')}` | {row['officialHelpTitle']} | {row['koreanName']} | [Help]({row['helpUrl']})" + (f" · [ADS]({row['apiUrl']})" if row['apiUrl'] else '') + ' |')
    lines += ['', '## 아직 표시명을 확정하지 않은 타입', '',
        '아래 항목은 추상 기반 클래스·관리 작업·이전 버전 아이콘·미검증 클래스가 섞여 있다. 가공 종류 개수로 합산하지 않으며, UI는 내부 클래스명을 노출하지 않고 중립적인 표시를 사용한다.', '',
        '| 내부 타입 | 설치 7.20 확인 | ADS 문서 | 비고 |', '|---|---|---|---|']
    for row in inventory:
        if row['status'] == 'mapped':
            continue
        note = '추상/기반 클래스' if (row['installed'] or {}).get('isAbstract') else '사용자용 명칭 추가 검증 필요'
        source = '[ADS](' + row['api']['apiUrl'] + ')' if row['api'].get('apiUrl') else '없음'
        lines.append(f"| `{row['nativeType'].removeprefix('TopSolid.Cam.NC.')}` | {'있음' if row['installed'] else '없음'} | {source} | {note} |")
    lines += ['', '## 주의할 차이', '',
        '- NibblingOperation: 니블링으로 직역하지 않고 Contour Roughing → 윤곽 황삭.',
        '- GeodesicMachiningOperation: 사용자 명령은 5X Finishing → 5축 정삭.',
        '- MultiAxisRoughingOperation: 사용자 명령은 Multi-axis pocketing → 다축 포켓 가공.',
        '- AngleGroovingOperation: 사용자 명령은 Corner relief → 코너 릴리프.',
        '- VerticalRoughingOperation: 도움말은 Plunge Roughing → 플런지 황삭.',
        '- PointToPointOperation/HoleOperation: 같은 구멍 가공 패밀리에 탭·리밍 등 여러 사이클이 있으므로 클래스만으로 세부 사이클을 판정하지 않는다.',
        '- ADS 사이트 패치 버전과 설치 DLL 패치 버전이 다르다. 각 행에 버전과 근거를 보존한다.',
        '- 이 검증은 이름·타입·표시에 관한 것이며 가공 생성, 계산, NC 출력이나 기계 안전성 검증은 수행하지 않았다.', '']
    (ROOT / 'docs/cam/operation-type-names.ko.md').write_text('\n'.join(lines), encoding='utf-8')
    print(json.dumps({'mapped': len(mapped), 'inventory': len(inventory), 'installedTypes': len(installed),
        'unresolved': len(inventory) - len(mapped), 'oldIconTypesAbsent': sum(k not in installed for k in icons)}, ensure_ascii=False))


if __name__ == '__main__':
    main()
