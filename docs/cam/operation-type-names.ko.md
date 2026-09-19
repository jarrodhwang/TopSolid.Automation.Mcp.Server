# TopSolid 7.20 오퍼레이션 표시명 대조표

공식 사용자 도움말 134개, ADS 클래스 문서 116개, 설치 DLL의 Operation 접미사 CAM DB 클래스 167개를 대조했다. 확인된 표시명 매핑은 72개다.

영문은 공식 도움말의 제목을 기준으로 하며, 한국어는 프로젝트 번역이다. 환경 활성과 스위핑은 사용자 지정 간결한 표시명을 유지한다. 내부 타입과 도움말 명령의 연결은 모듈·명령 패밀리를 검토한 매핑이며 공식 API 계약으로 주장하지 않는다.

## 검증된 표시명

| 분류 | 내부 타입 | 공식 도움말 제목 | 한국어 표시명 | 근거 |
|---|---|---|---|---|
| milling | `MillTurn.DB.EndMilling.EndMillingOperation` | End Milling | 엔드 밀링 | [Help](https://help.topsolid.com/7.20/en/TopSolid%27Cam/TopSolid/Cam/NC/MillTurn/UI/endmilling/Commands/EndMillingOperationCommand.htm) · [ADS](https://ads.topsolid.com/_doc/TopSolidHelp720/html/dc52f9fb-6dd0-bcf0-9754-a4bb9c2cbf59.htm) |
| milling | `MillTurn.DB.EndMilling.NibblingOperation` | Contour Roughing | 윤곽 황삭 | [Help](https://help.topsolid.com/7.20/en/TopSolid%27Cam/TopSolid/Cam/NC/MillTurn/UI/endmilling/Commands/nibblingoperationcommand.htm) · [ADS](https://ads.topsolid.com/_doc/TopSolidHelp720/html/d85c62a0-38b7-f7ac-eb9d-8fc556fc590f.htm) |
| milling | `MillTurn.DB.SideMilling.SideMillingOperation` | Side Milling | 사이드 밀링 | [Help](https://help.topsolid.com/7.20/en/TopSolid%27Cam/TopSolid/Cam/NC/MillTurn/UI/sidemilling/commands/SideMillingOperationCommand.htm) · [ADS](https://ads.topsolid.com/_doc/TopSolidHelp720/html/e90f3849-12f4-6293-1586-4a887c73c15b.htm) |
| milling | `MillTurn.DB.SlotMilling.SlotMillingOperation` | Slot Milling | 슬롯 밀링 | [Help](https://help.topsolid.com/7.20/en/TopSolid%27Cam/TopSolid/Cam/NC/MillTurn/UI/slotmilling/Commands/slotmillingOperationCommand.htm) · [ADS](https://ads.topsolid.com/_doc/TopSolidHelp720/html/9a20bf55-e9c7-a1a3-a6e1-998aea55d110.htm) |
| milling | `MillTurn.DB.TSlotMilling.TSlotMillingOperation` | TSlot milling | T 슬롯 밀링 | [Help](https://help.topsolid.com/7.20/en/TopSolid%27Cam/TopSolid/Cam/NC/MillTurn/UI/tslotmilling/commands/tslotmillingoperationcommand.htm) · [ADS](https://ads.topsolid.com/_doc/TopSolidHelp720/html/361ecdd2-40c6-0ecc-ee9a-d4fe450a94d0.htm) |
| milling | `MillTurn.DB.ChamferMilling.ChamferMillingOperation` | Chamfering | 모따기 | [Help](https://help.topsolid.com/7.20/en/TopSolid%27Cam/TopSolid/Cam/NC/MillTurn/UI/chamfermilling/Commands/chamfermillingOperationCommand.htm) · [ADS](https://ads.topsolid.com/_doc/TopSolidHelp720/html/8274d77e-0df4-99e3-6973-fe9708a8d885.htm) |
| milling | `MillTurn.DB.CornerRounding.CornerRoundingOperation` | Corner Rounding | 모서리 라운딩 | [Help](https://help.topsolid.com/7.20/en/TopSolid%27Cam/TopSolid/Cam/NC/MillTurn/UI/cornerrounding/commands/cornerroundingOperationCommand.htm) · [ADS](https://ads.topsolid.com/_doc/TopSolidHelp720/html/93f96e56-3814-86ec-79b0-44d0765a68e7.htm) |
| milling | `MillTurn.DB.MouseFacing.Operations.MouseFacingOperation` | Mouse Facing | 마우스 지정 평면 가공 | [Help](https://help.topsolid.com/7.20/en/TopSolid%27Cam/TopSolid/Cam/NC/MillTurn/UI/mousefacing/commands/mousefacingoperationcommand.htm) · [ADS](https://ads.topsolid.com/_doc/TopSolidHelp720/html/f2b0d647-77db-2a31-82e2-8b69af1d3c52.htm) |
| milling | `MillTurn.DB.Broaching.Operation.BroachingOperation` | Broaching | 브로칭 | [Help](https://help.topsolid.com/7.20/en/TopSolid%27Cam/TopSolid/Cam/NC/MillTurn/UI/broaching/commands/broachingoperationcommand.htm) |
| milling | `MillTurn.DB.ThreadMilling.ThreadMillingOperation` | External thread milling | 외부 나사 밀링 | [Help](https://help.topsolid.com/7.20/en/TopSolid%27Cam/TopSolid/Cam/NC/MillTurn/UI/threadmilling/commands/threadmillingoperationcommand.htm) · [ADS](https://ads.topsolid.com/_doc/TopSolidHelp720/html/7da1dc99-39cc-07df-a319-4f9b2a8388e5.htm) |
| milling | `MillTurn.DB.BreakingEdges.BreakingEdgesOperation` | Breaking Edges | 모서리 제거 | [Help](https://help.topsolid.com/7.20/en/TopSolid%27Cam/TopSolid/Cam/NC/MillTurn/UI/breakingedges/Commands/breakingedgesOperationCommand.htm) · [ADS](https://ads.topsolid.com/_doc/TopSolidHelp720/html/d4935c47-e970-3576-ae52-a2dc64a0f7a7.htm) |
| milling | `MillTurn.DB.Engraving.EngravingOperation` | Engraving | 각인 | [Help](https://help.topsolid.com/7.20/en/TopSolid%27Cam/TopSolid/Cam/NC/MillTurn/UI/engraving/commands/engravingoperationcommand.htm) · [ADS](https://ads.topsolid.com/_doc/TopSolidHelp720/html/9b96c44e-8f25-e1f2-0f96-500e578ea286.htm) |
| milling | `MillTurn.DB.CuttingHoldingTabs.CuttingHoldingTabsOperation` | Cutting holding tabs | 고정 탭 절단 | [Help](https://help.topsolid.com/7.20/en/TopSolid%27Cam/TopSolid/Cam/NC/MillTurn/UI/CuttingHoldingTabs/Commands/CuttingHoldingTabsOperationCommand.htm) |
| milling | `MillTurn.DB.Plunge.PlungePocketOperation` | Pocketing by plunges | 플런지 포켓 가공 | [Help](https://help.topsolid.com/7.20/en/TopSolid%27Cam/TopSolid/Cam/NC/MillTurn/UI/plunge/commands/plungepocketoperationcommand.htm) |
| milling | `MillTurn.DB.Plunge.PlungeSideOperation` | Contouring by plunges | 플런지 윤곽 가공 | [Help](https://help.topsolid.com/7.20/en/TopSolid%27Cam/TopSolid/Cam/NC/MillTurn/UI/plunge/commands/plungesideoperationcommand.htm) |
| milling | `MillTurn.DB.Plunge.PlungeStampPocketOperation` | Punching pocketing | 펀칭 포켓 가공 | [Help](https://help.topsolid.com/7.20/en/TopSolid%27Cam/TopSolid/Cam/NC/MillTurn/UI/plunge/commands/plungestamppocketoperationcommand.htm) |
| hole-family | `MillTurn.DB.PointToPoint.Operations.PointToPointOperation` | Drilling | 구멍 가공 | [Help](https://help.topsolid.com/7.20/en/TopSolid%27Cam/TopSolid/Cam/NC/MillTurn/UI/pointtopoint/commands/PointToPointOperationCommand.htm) · [ADS](https://ads.topsolid.com/_doc/TopSolidHelp720/html/560032e2-0d76-7afa-60fe-b181fa06a946.htm) |
| hole-family | `MillTurn.DB.PointToPoint.Operations.HoleOperation` | Drilling | 구멍 가공 | [Help](https://help.topsolid.com/7.20/en/TopSolid%27Cam/TopSolid/Cam/NC/MillTurn/UI/pointtopoint/commands/PointToPointOperationCommand.htm) · [ADS](https://ads.topsolid.com/_doc/TopSolidHelp720/html/eecdf3aa-1b55-6e0a-87a6-036a901adfdb.htm) |
| form | `MillTurn.Form.DB.Roughing.RoughingOperation` | Roughing | 형상 황삭 | [Help](https://help.topsolid.com/7.20/en/TopSolid%27Cam/TopSolid/Cam/NC/MillTurn/Form/UI/Roughing/commands/roughingoperationcommand.htm) · [ADS](https://ads.topsolid.com/_doc/TopSolidHelp720/html/4d85d7ce-2914-b1e3-358a-c698ac06bf75.htm) |
| form | `MillTurn.Form.DB.Roughing.Planar.PlanarRoughingOperation` | Planar faces machining | 평면 영역 가공 | [Help](https://help.topsolid.com/7.20/en/TopSolid%27Cam/TopSolid/Cam/NC/MillTurn/Form/UI/Roughing/commands/planarroughingoperationcommand.htm) · [ADS](https://ads.topsolid.com/_doc/TopSolidHelp720/html/7d7fbe38-a192-d6db-b799-6c5ec660d655.htm) |
| form | `MillTurn.Form.DB.Roughing.UnderCutting.UnderCuttingRoughingOperation` | Under cutting roughing | 언더컷 황삭 | [Help](https://help.topsolid.com/7.20/en/TopSolid%27Cam/TopSolid/Cam/NC/MillTurn/Form/UI/Roughing/commands/undercuttingroughingoperationcommand.htm) · [ADS](https://ads.topsolid.com/_doc/TopSolidHelp720/html/d0208516-2476-d53a-841e-6551fcac57a7.htm) |
| form | `MillTurn.Form.DB.Finishing.FinishingOperation` | 3D Finishing | 3D 정삭 | [Help](https://help.topsolid.com/7.20/en/TopSolid%27Cam/TopSolid/Cam/NC/MillTurn/Form/UI/finishing/commands/finishingoperationcommand.htm) · [ADS](https://ads.topsolid.com/_doc/TopSolidHelp720/html/75469430-d74a-8f67-3f86-7e48d7dc080d.htm) |
| form | `MillTurn.Form.DB.Finishing.UnderCuttingHncOperation` | Under cutting Z level | 언더컷 등고선 가공 | [Help](https://help.topsolid.com/7.20/en/TopSolid%27Cam/TopSolid/Cam/NC/MillTurn/Form/UI/finishing/commands/undercuttinghncoperationcommand.htm) · [ADS](https://ads.topsolid.com/_doc/TopSolidHelp720/html/63be5bcc-da26-fca2-a661-4a3325a2673b.htm) |
| form | `MillTurn.Form.DB.GreatFinish.GreatFinishOperation` | Super-finishing | 초정삭 | [Help](https://help.topsolid.com/7.20/en/TopSolid%27Cam/TopSolid/Cam/NC/MillTurn/Form/UI/Greatfinish/Commands/greatfinishoperationcommand.htm) |
| form | `MillTurn.Form.DB.Sweeping.Operation.SweepingOperation` | 3D Sweeping | 스위핑 | [Help](https://help.topsolid.com/7.20/en/TopSolid%27Cam/TopSolid/Cam/NC/MillTurn/Form/UI/sweeping/commands/sweepingoperationcommand.htm) · [ADS](https://ads.topsolid.com/_doc/TopSolidHelp720/html/0b15aea9-03a7-78de-00cf-906480cf76a3.htm) |
| form | `MillTurn.Form.DB.Matleft.MatleftOperation` | 3D Remilling | 3D 잔삭 | [Help](https://help.topsolid.com/7.20/en/TopSolid%27Cam/TopSolid/Cam/NC/MillTurn/Form/UI/matleft/commands/matleftoperationcommand.htm) · [ADS](https://ads.topsolid.com/_doc/TopSolidHelp720/html/6deddc34-bffe-5fc0-0682-df7643463bf5.htm) |
| form | `MillTurn.Form.DB.Contouring.ContouringOperation` | 3D Contouring | 3D 윤곽 가공 | [Help](https://help.topsolid.com/7.20/en/TopSolid%27Cam/TopSolid/Cam/NC/MillTurn/Form/UI/Contouring/commands/contouringoperationcommand.htm) · [ADS](https://ads.topsolid.com/_doc/TopSolidHelp720/html/840e0e6a-d565-57ff-15b8-f563f4583b37.htm) |
| form | `MillTurn.Form.DB.Pipe.SweptOperation` | Swept machining | 스웹트 가공 | [Help](https://help.topsolid.com/7.20/en/TopSolid%27Cam/TopSolid/Cam/NC/MillTurn/Form/UI/pipe/commands/sweptoperationcommand.htm) |
| form | `MillTurn.Form.DB.VerticalRoughing.VerticalRoughingOperation` | Plunge Roughing: Roughing | 플런지 황삭 | [Help](https://help.topsolid.com/7.20/en/TopSolid%27Cam/TopSolid/Cam/NC/MillTurn/Form/UI/verticalroughing/Commands/verticalroughingoperationcommand.htm) · [ADS](https://ads.topsolid.com/_doc/TopSolidHelp720/html/15034eb0-f73c-4218-878c-70c183d67c12.htm) |
| multi-axis | `MillTurn.Form.DB.Roughing.FourAxis.FourAxisRoughingOperation` | 4 axis roughing | 4축 반경 방향 황삭 | [Help](https://help.topsolid.com/7.20/en/TopSolid%27Cam/TopSolid/Cam/NC/MillTurn/Form/UI/Roughing/commands/fouraxisroughingoperationcommand.htm) · [ADS](https://ads.topsolid.com/_doc/TopSolidHelp720/html/fabbe857-9975-d180-72e2-1d184f37f118.htm) |
| multi-axis | `MillTurn.Form.DB.Finishing.AutomaticRadialOperation` | 4X Radial Finishing | 4축 반경 방향 정삭 | [Help](https://help.topsolid.com/7.20/en/TopSolid%27Cam/TopSolid/Cam/NC/MillTurn/Form/UI/finishing/commands/automaticradialoperationcommand.htm) · [ADS](https://ads.topsolid.com/_doc/TopSolidHelp720/html/9b119a8e-2803-4549-8298-77da768541f4.htm) |
| multi-axis | `MillTurn.Form.DB.ScrewMilling.ScrewMillingOperation` | Screw milling | 스크루 밀링 | [Help](https://help.topsolid.com/7.20/en/TopSolid%27Cam/TopSolid/Cam/NC/MillTurn/Form/UI/screwmilling/commands/screwmillingoperationcommand.htm) |
| multi-axis | `MillTurn.Form.DB.Finishing.FiveAxisHncOperation` | Five Axis Constant Z | 5축 등고선 가공 | [Help](https://help.topsolid.com/7.20/en/TopSolid%27Cam/TopSolid/Cam/NC/MillTurn/Form/UI/finishing/commands/fiveaxishncoperationcommand.htm) · [ADS](https://ads.topsolid.com/_doc/TopSolidHelp720/html/9c82d763-a05f-b10c-226c-96cd6c03f3f9.htm) |
| multi-axis | `MillTurn.FiveAxis.DB.Contour.ContourOperation` | 5D Contour | 5축 윤곽 가공 | [Help](https://help.topsolid.com/7.20/en/TopSolid%27Cam/TopSolid/Cam/NC/MillTurn/fiveaxis/ui/contour/commands/contouroperationcommand.htm) · [ADS](https://ads.topsolid.com/_doc/TopSolidHelp720/html/2b936bb4-9913-6d80-5705-d847c001e9b1.htm) |
| multi-axis | `MillTurn.FiveAxis.DB.Swarf.SwarfOperation` | Swarf | 스와프 가공 | [Help](https://help.topsolid.com/7.20/en/TopSolid%27Cam/TopSolid/Cam/NC/MillTurn/fiveaxis/ui/swarf/commands/swarfoperationcommand.htm) · [ADS](https://ads.topsolid.com/_doc/TopSolidHelp720/html/23d6b008-de30-840c-15be-45463dcd6d05.htm) |
| multi-axis | `MillTurn.FiveAxis.DB.Auto5x.Auto5xOperation` | Auto 5-axis machining | 자동 5축 가공 | [Help](https://help.topsolid.com/7.20/en/TopSolid%27Cam/TopSolid/Cam/NC/MillTurn/fiveaxis/ui/auto5x/commands/auto5xoperationcommand.htm) |
| multi-axis | `MillTurn.FiveAxis.DB.MultiBlade.MultiBladeRoughingOperation` | Blade roughing | 블레이드 황삭 | [Help](https://help.topsolid.com/7.20/en/TopSolid%27Cam/TopSolid/Cam/NC/MillTurn/fiveaxis/ui/multiblade/commands/multibladeroughingoperationcommand.htm) |
| multi-axis | `MillTurn.FiveAxis.DB.MultiBlade.MultiBladeHubFinishingOperation` | Hub finishing | 허브 정삭 | [Help](https://help.topsolid.com/7.20/en/TopSolid%27Cam/TopSolid/Cam/NC/MillTurn/fiveaxis/ui/multiblade/commands/multibladehubfinishingoperationcommand.htm) |
| multi-axis | `MillTurn.FiveAxis.DB.MultiBlade.MultiBladeBladeFinishingOperation` | Blade finishing | 블레이드 정삭 | [Help](https://help.topsolid.com/7.20/en/TopSolid%27Cam/TopSolid/Cam/NC/MillTurn/fiveaxis/ui/multiblade/commands/multibladebladefinishingoperationcommand.htm) |
| multi-axis | `MillTurn.FiveAxis.DB.MultiBlade.MultiBladeFilletFinishingOperation` | Radius machining | 블레이드 필렛 가공 | [Help](https://help.topsolid.com/7.20/en/TopSolid%27Cam/TopSolid/Cam/NC/MillTurn/fiveaxis/ui/multiblade/commands/multibladefilletfinishingoperationcommand.htm) |
| multi-axis | `MillTurn.FiveAxis.DB.Deburring.DeburringOperation` | 5X Deburring | 5축 디버링 | [Help](https://help.topsolid.com/7.20/en/TopSolid%27Cam/TopSolid/Cam/NC/MillTurn/fiveaxis/ui/deburring/commands/deburringoperationcommand.htm) |
| multi-axis | `MillTurn.FiveAxis.DB.MultiAxisRoughing.MultiAxisRoughingOperation` | Multi-axis pocketing | 다축 포켓 가공 | [Help](https://help.topsolid.com/7.20/en/TopSolid%27Cam/TopSolid/Cam/NC/MillTurn/fiveaxis/ui/multiaxisroughing/commands/multiaxisroughingoperationcommand.htm) |
| multi-axis | `MillTurn.FiveAxis.DB.PortMachining.PortMachiningOperation` | Port machining | 포트 가공 | [Help](https://help.topsolid.com/7.20/en/TopSolid%27Cam/TopSolid/Cam/NC/MillTurn/fiveaxis/ui/portmachining/commands/portmachiningoperationcommand.htm) |
| multi-axis | `MillTurn.FiveAxis.DB.Roughing3Plus2.Roughing3Plus2Operation` | Roughing 3+2 axis | 3+2축 황삭 | [Help](https://help.topsolid.com/7.20/en/TopSolid%27Cam/TopSolid/Cam/NC/MillTurn/fiveaxis/ui/roughing3plus2/commands/roughing3plus2operationcommand.htm) |
| multi-axis | `MillTurn.FiveAxis.DB.RotaryMachining.RotaryMachiningOperation` | 4X Radial Advanced | 고급 4축 반경 방향 가공 | [Help](https://help.topsolid.com/7.20/en/TopSolid%27Cam/TopSolid/Cam/NC/MillTurn/fiveaxis/ui/rotarymachining/commands/rotarymachiningoperationcommand.htm) |
| multi-axis | `MillTurn.FiveAxis.DB.GeodesicMachining.GeodesicMachiningOperation` | 5X Finishing | 5축 정삭 | [Help](https://help.topsolid.com/7.20/en/TopSolid%27Cam/TopSolid/Cam/NC/MillTurn/fiveaxis/ui/geodesicmachining/commands/geodesicmachiningoperationcommand.htm) |
| turning | `MillTurn.Turn.DB.Roughing.RoughingOperation` | Turning Roughing | 선삭 황삭 | [Help](https://help.topsolid.com/7.20/en/TopSolid%27Cam/TopSolid/Cam/NC/MillTurn/turn/ui/roughing/commands/roughingoperationcommand.htm) · [ADS](https://ads.topsolid.com/_doc/TopSolidHelp720/html/1fc0dd40-066c-b77a-9c22-c8dc36ee55e0.htm) |
| turning | `MillTurn.Turn.DB.Finishing.FinishingOperation` | Turning Finishing | 선삭 정삭 | [Help](https://help.topsolid.com/7.20/en/TopSolid%27Cam/TopSolid/Cam/NC/MillTurn/turn/ui/finishing/commands/finishingoperationcommand.htm) |
| turning | `MillTurn.Turn.DB.Groove.GrooveOperation` | Groove / Parting | 홈 가공·절단 | [Help](https://help.topsolid.com/7.20/en/TopSolid%27Cam/TopSolid/Cam/NC/MillTurn/turn/ui/groove/commands/grooveoperationcommand.htm) |
| turning | `MillTurn.Turn.DB.Threading.ThreadingOperation` | Threading | 선삭 나사 가공 | [Help](https://help.topsolid.com/7.20/en/TopSolid%27Cam/TopSolid/Cam/NC/MillTurn/turn/ui/threading/commands/threadingoperationcommand.htm) |
| turning | `MillTurn.Turn.DB.BreakingEdges.BreakingEdgesOperation` | Breaking Edges | 선삭 모서리 제거 | [Help](https://help.topsolid.com/7.20/en/TopSolid%27Cam/TopSolid/Cam/NC/MillTurn/turn/ui/breakingedges/commands/breakingedgesoperationcommand.htm) · [ADS](https://ads.topsolid.com/_doc/TopSolidHelp720/html/1a9614d1-1a31-df85-0065-7030d67cef4b.htm) |
| turning | `MillTurn.Turn.DB.AngleGrooving.AngleGroovingOperation` | Corner relief | 코너 릴리프 | [Help](https://help.topsolid.com/7.20/en/TopSolid%27Cam/TopSolid/Cam/NC/MillTurn/turn/ui/anglegrooving/commands/anglegroovingoperationcommand.htm) · [ADS](https://ads.topsolid.com/_doc/TopSolidHelp720/html/984be70b-3486-5f2b-f41f-eda56ddca903.htm) |
| turning | `MillTurn.Turn.DB.PolygonalTurning.PolygonalTurningOperation` | Polygonal turning | 다각형 선삭 | [Help](https://help.topsolid.com/7.20/en/TopSolid%27Cam/TopSolid/Cam/NC/MillTurn/turn/ui/polygonalturning/commands/polygonalturningoperationcommand.htm) |
| auxiliary | `MillTurn.Turn.DB.Movement.MovementOperation` | Movements | 선삭 이동 | [Help](https://help.topsolid.com/7.20/en/TopSolid%27Cam/TopSolid/Cam/NC/MillTurn/turn/ui/movement/commands/movementoperationcommand.htm) |
| grinding | `MillTurn.Grinding.DB.CylindricalContouring.CylindricalContouringOperation` | Cylindrical grinding by contouring | 원통 윤곽 연삭 | [Help](https://help.topsolid.com/7.20/en/TopSolid%27Cam/TopSolid/Cam/NC/MillTurn/grinding/ui/cylindricalcontouring/commands/cylindricalcontouringoperationcommand.htm) |
| grinding | `MillTurn.Grinding.DB.CylindricalFacing.CylindricalFacingOperation` | Cylindrical grinding by facing | 원통 단면 연삭 | [Help](https://help.topsolid.com/7.20/en/TopSolid%27Cam/TopSolid/Cam/NC/MillTurn/grinding/ui/cylindricalfacing/commands/cylindricalfacingoperationcommand.htm) |
| grinding | `MillTurn.Grinding.DB.Contour.ContourOperation` | Grinding by contouring | 윤곽 연삭 | [Help](https://help.topsolid.com/7.20/en/TopSolid%27Cam/TopSolid/Cam/NC/MillTurn/grinding/ui/contour/commands/contouroperationcommand.htm) |
| grinding | `MillTurn.Grinding.DB.Facing.FacingOperation` | Grinding by facing | 평면 연삭 | [Help](https://help.topsolid.com/7.20/en/TopSolid%27Cam/TopSolid/Cam/NC/MillTurn/grinding/ui/facing/commands/facingoperationcommand.htm) |
| auxiliary | `MillTurn.DB.VirtualJog.Operation.VirtualJogOperation` | Virtual jog | 가상 조그 | [Help](https://help.topsolid.com/7.20/en/TopSolid%27Cam/TopSolid/Cam/NC/MillTurn/UI/virtualjog/commands/virtualjogoperationcommand.htm) · [ADS](https://ads.topsolid.com/_doc/TopSolidHelp720/html/96e1c123-7aa4-c712-6764-a67242bd8d6d.htm) |
| auxiliary | `MillTurn.DB.ControlPoints.Operation.ControlPointsOperation` | Control points | 제어점 | [Help](https://help.topsolid.com/7.20/en/TopSolid%27Cam/TopSolid/Cam/NC/MillTurn/UI/controlpoints/commands/controlpointsoperationcommand.htm) · [ADS](https://ads.topsolid.com/_doc/TopSolidHelp720/html/4c508c8f-93da-38ce-5250-c8ed1ddac303.htm) |
| auxiliary | `Kernel.DB.Annex.Operation.EnvironmentOperation` | Environment Management | 환경 활성 | [Help](https://help.topsolid.com/7.20/en/TopSolid%27Cam/TopSolid/Cam/NC/Kernel/ui/annex/dialogs/environment/geometrypane.htm) · [ADS](https://ads.topsolid.com/_doc/TopSolidHelp720/html/c29a3f11-01ac-3d6c-261d-a2d5176b0eef.htm) |
| auxiliary | `Kernel.DB.Annex.Operation.StockModificationOperation` | Stock modification | 소재 변경 | [Help](https://help.topsolid.com/7.20/en/TopSolid%27Cam/TopSolid/Cam/NC/Kernel/ui/annex/dialogs/stockmodification/geometrypane.htm) · [ADS](https://ads.topsolid.com/_doc/TopSolidHelp720/html/03283274-3ff5-52d9-bf16-f9f80cdbf279.htm) |
| auxiliary | `Kernel.DB.Annex.Operation.MachineElementsActivationOperation` | Machine elements activation | 기계 요소 활성 | [Help](https://help.topsolid.com/7.20/en/TopSolid%27Cam/TopSolid/Cam/NC/Kernel/ui/annex/commands/machineelementsactivationcommand.htm) · [ADS](https://ads.topsolid.com/_doc/TopSolidHelp720/html/d2543665-b5f8-fd02-5b68-3bc31e520a09.htm) |
| auxiliary | `Kernel.DB.Operations.Repetitions.RepetitionNcOperation` | Repeat | 가공 반복 | [Help](https://help.topsolid.com/7.20/en/TopSolid%27Cam/TopSolid/Cam/NC/Kernel/ui/repetitions/repetitioncommand.htm) · [ADS](https://ads.topsolid.com/_doc/TopSolidHelp720/html/f61c7d1b-152f-42af-4ca4-af15cb06d235.htm) |
| auxiliary | `Kernel.DB.PartSetting.GrindingReserve.GrindingReserveOperation` | Grinding reserve | 연삭 여유 | [Help](https://help.topsolid.com/7.20/en/TopSolid%27Cam/TopSolid/Cam/NC/Kernel/ui/partsetting/grindingreserve/grindingreservecommand.htm) |
| geometry | `Kernel.DB.Operations.SketchToolPath.SketchToolPathOperation` | 3D sketch tool path | 3D 공구 경로 스케치 | [Help](https://help.topsolid.com/7.20/en/TopSolid%27Cam/TopSolid/Cam/NC/Kernel/ui/sketchtoolpath/sketchtoolpathcommand.htm) |
| geometry | `MillTurn.DB.Operations.IsoparametricCurvesOnParallelOperation` | Isoparametric curve on parallel | 평행면 등매개변수 곡선 | [Help](https://help.topsolid.com/7.20/en/TopSolid%27Cam/TopSolid/Cam/NC/MillTurn/UI/commands/isoparametriccurvesonparallelcommand.htm) |
| geometry | `MillTurn.DB.Operations.SketchResidualProfilesOperation` | Sketch residual 3D | 3D 잔삭 스케치 | [Help](https://help.topsolid.com/7.20/en/TopSolid%27Cam/TopSolid/Cam/NC/MillTurn/UI/commands/sketchresidualprofilescommand.htm) |
| geometry | `MillTurn.Form.DB.SketchAngularSeparationOperation` | Angular separation profiles | 각도 분리 프로파일 | [Help](https://help.topsolid.com/7.20/en/TopSolid%27Cam/TopSolid/Cam/NC/MillTurn/Form/UI/commands/sketchangularseparationcommand.htm) |
| geometry | `MillTurn.Form.DB.SketchPlanarFacesOperation` | Sketch planar faces curves | 평면 곡선 스케치 | [Help](https://help.topsolid.com/7.20/en/TopSolid%27Cam/TopSolid/Cam/NC/MillTurn/Form/UI/commands/sketchplanarfacescommand.htm) |
| geometry | `MillTurn.Form.DB.SketchContactAreaToolOperation` | Sketch contact area tool | 공구 접촉 영역 스케치 | [Help](https://help.topsolid.com/7.20/en/TopSolid%27Cam/TopSolid/Cam/NC/MillTurn/Form/UI/commands/sketchcontactareatoolcommand.htm) |
| geometry | `MillTurn.Form.DB.SketchToolShapeRestAreaOperation` | Remaining material sketch according to the shape of the tool | 공구 형상 기준 잔여 소재 스케치 | [Help](https://help.topsolid.com/7.20/en/TopSolid%27Cam/CommonDialogBox/sketchtoolshaperestareacommand.htm) |

## 아직 표시명을 확정하지 않은 타입

아래 항목은 추상 기반 클래스·관리 작업·이전 버전 아이콘·미검증 클래스가 섞여 있다. 가공 종류 개수로 합산하지 않으며, UI는 내부 클래스명을 노출하지 않고 중립적인 표시를 사용한다.

| 내부 타입 | 설치 7.20 확인 | ADS 문서 | 비고 |
|---|---|---|---|
| `Kernel.DB.Analysis.MachiningPropertiesManagementOperation` | 있음 | [ADS](https://ads.topsolid.com/_doc/TopSolidHelp720/html/6a583aef-f037-7b36-19d1-a879be81f6e7.htm) | 사용자용 명칭 추가 검증 필요 |
| `Kernel.DB.Analyzers.AnalyzersOperation` | 있음 | [ADS](https://ads.topsolid.com/_doc/TopSolidHelp720/html/57596390-b716-1fe7-5c10-1a88d93d193a.htm) | 사용자용 명칭 추가 검증 필요 |
| `Kernel.DB.Annex.Operation.ActivePartSetupOccurrenceOperation` | 없음 | 없음 | 사용자용 명칭 추가 검증 필요 |
| `Kernel.DB.Annex.Operation.AnnexOperation` | 있음 | [ADS](https://ads.topsolid.com/_doc/TopSolidHelp720/html/65b838cb-f174-be3b-491d-f1245656b9be.htm) | 사용자용 명칭 추가 검증 필요 |
| `Kernel.DB.Annex.Operation.SurroundOperation` | 있음 | [ADS](https://ads.topsolid.com/_doc/TopSolidHelp720/html/bd47fb09-92fb-a74e-bfd5-9089dbc7b31f.htm) | 추상/기반 클래스 |
| `Kernel.DB.Batch.BatchWorkManagerProcessAdvancedConfigurationOperation` | 있음 | [ADS](https://ads.topsolid.com/_doc/TopSolidHelp720/html/9cc64cf6-13aa-265f-f6cf-6cca4c87e0dc.htm) | 사용자용 명칭 추가 검증 필요 |
| `Kernel.DB.Configurations.Operations.AutomaticProgramsNamesByMachineDefinitionOperation` | 있음 | 없음 | 사용자용 명칭 추가 검증 필요 |
| `Kernel.DB.CuttingConditions.Documents.CuttingConditionsManagementOperation` | 있음 | [ADS](https://ads.topsolid.com/_doc/TopSolidHelp720/html/a7b50dcb-2bc7-2f2d-4ba3-1f03c4a20085.htm) | 사용자용 명칭 추가 검증 필요 |
| `Kernel.DB.CuttingConditions.Operations.SpecificCuttingConditionsManagementOperation` | 있음 | [ADS](https://ads.topsolid.com/_doc/TopSolidHelp720/html/72689838-0782-2909-b4d5-84a8d4bf098b.htm) | 사용자용 명칭 추가 검증 필요 |
| `Kernel.DB.CuttingConditions.Operations.ToolCCEntityParametersManagementOperation` | 있음 | [ADS](https://ads.topsolid.com/_doc/TopSolidHelp720/html/89fc3e3f-17aa-751a-e423-edb4914f8064.htm) | 사용자용 명칭 추가 검증 필요 |
| `Kernel.DB.Entities.Shapes.Modifications.TrimOperation` | 있음 | [ADS](https://ads.topsolid.com/_doc/TopSolidHelp720/html/171bb60c-99bc-56d2-e1e6-b9247aae3687.htm) | 사용자용 명칭 추가 검증 필요 |
| `Kernel.DB.Features.NCInheritedFeatureOperation` | 있음 | [ADS](https://ads.topsolid.com/_doc/TopSolidHelp720/html/7289de83-f97a-581f-bed8-e23f38d740f0.htm) | 사용자용 명칭 추가 검증 필요 |
| `Kernel.DB.Features.Operations.DrillingsManagementOperation` | 있음 | [ADS](https://ads.topsolid.com/_doc/TopSolidHelp720/html/8720b364-a9d2-9934-34ab-e9f653d6e9b6.htm) | 사용자용 명칭 추가 검증 필요 |
| `Kernel.DB.Features.Operations.PocketsManagementOperation` | 있음 | [ADS](https://ads.topsolid.com/_doc/TopSolidHelp720/html/79a36027-fcc8-9a1b-8ce3-9373192f09b2.htm) | 사용자용 명칭 추가 검증 필요 |
| `Kernel.DB.Helpers.BasicObjects.BasicNCOperation` | 있음 | 없음 | 사용자용 명칭 추가 검증 필요 |
| `Kernel.DB.Knowledge.ItemKnowledgeManagementOperation` | 있음 | [ADS](https://ads.topsolid.com/_doc/TopSolidHelp720/html/9b23c37a-4910-2910-11b5-e765785fbe75.htm) | 사용자용 명칭 추가 검증 필요 |
| `Kernel.DB.Knowledge.KnowledgeBaseManagementOperation` | 있음 | 없음 | 사용자용 명칭 추가 검증 필요 |
| `Kernel.DB.Machines.Operations.MachineDerivationOperation` | 있음 | [ADS](https://ads.topsolid.com/_doc/TopSolidHelp720/html/fd1c3be1-d27e-63d7-81db-a8d38e8d0ca9.htm) | 사용자용 명칭 추가 검증 필요 |
| `Kernel.DB.Machines.Operations.NCMachineInclusionManagementOperation` | 있음 | [ADS](https://ads.topsolid.com/_doc/TopSolidHelp720/html/c163ee78-05a5-dbd3-a5f7-6c740fa08271.htm) | 사용자용 명칭 추가 검증 필요 |
| `Kernel.DB.Method.Actions.MethodActionOperation` | 있음 | [ADS](https://ads.topsolid.com/_doc/TopSolidHelp720/html/159d3272-7010-cfb9-0a7e-70c7bdd12551.htm) | 사용자용 명칭 추가 검증 필요 |
| `Kernel.DB.Nesting.StockExtractionAsNestingSupports.StockExtractionAsNestingSupportOperation` | 있음 | [ADS](https://ads.topsolid.com/_doc/TopSolidHelp720/html/7d92318a-aec0-6a84-e10d-4784defdeff9.htm) | 사용자용 명칭 추가 검증 필요 |
| `Kernel.DB.Operations.AutomationPreparationOperation` | 있음 | [ADS](https://ads.topsolid.com/_doc/TopSolidHelp720/html/58873ff6-24ed-73fd-a3cb-bc625fc55f58.htm) | 추상/기반 클래스 |
| `Kernel.DB.Operations.Displays.DisplayOperation` | 있음 | [ADS](https://ads.topsolid.com/_doc/TopSolidHelp720/html/ceccb313-1d90-053a-babc-846b55cdf4fe.htm) | 사용자용 명칭 추가 검증 필요 |
| `Kernel.DB.Operations.Displays.ObsoleteDisplayOperation` | 있음 | [ADS](https://ads.topsolid.com/_doc/TopSolidHelp720/html/d624fe4e-50c1-54c2-e022-f999565b8018.htm) | 추상/기반 클래스 |
| `Kernel.DB.Operations.EntitiesCreation.CamSketchOperation` | 있음 | [ADS](https://ads.topsolid.com/_doc/TopSolidHelp720/html/a52b3686-e1e2-c18e-fe7d-0c5ed08bcdb2.htm) | 추상/기반 클래스 |
| `Kernel.DB.Operations.EntitiesCreation.NCEntitiesCreationOperation` | 있음 | [ADS](https://ads.topsolid.com/_doc/TopSolidHelp720/html/f8dc2254-7f74-ed81-6840-89cecdda33f8.htm) | 사용자용 명칭 추가 검증 필요 |
| `Kernel.DB.Operations.IsoCodeContext.IsoCodeContextFramesManagementOperation` | 있음 | [ADS](https://ads.topsolid.com/_doc/TopSolidHelp720/html/bb2eb9b8-c573-0f21-e4c3-cd395cd4a140.htm) | 사용자용 명칭 추가 검증 필요 |
| `Kernel.DB.Operations.IsoCodeContext.IsoCodeContextsManagementOperation` | 없음 | 없음 | 사용자용 명칭 추가 검증 필요 |
| `Kernel.DB.Operations.IsoCodeContext.ObsoleteIsoCodeContextsManagementOperation` | 있음 | [ADS](https://ads.topsolid.com/_doc/TopSolidHelp720/html/413ac677-51fb-fccc-1710-124b0599b99d.htm) | 사용자용 명칭 추가 검증 필요 |
| `Kernel.DB.Operations.Link.AbstractLinkOperation` | 있음 | [ADS](https://ads.topsolid.com/_doc/TopSolidHelp720/html/21dd6fd1-fe2a-cc57-affe-f390e1001c8b.htm) | 추상/기반 클래스 |
| `Kernel.DB.Operations.Link.LinkInOperation` | 있음 | [ADS](https://ads.topsolid.com/_doc/TopSolidHelp720/html/5da3fb59-3cf7-9f56-4a86-f3fcaa01696f.htm) | 사용자용 명칭 추가 검증 필요 |
| `Kernel.DB.Operations.LinkOut.LinkOutOperation` | 있음 | [ADS](https://ads.topsolid.com/_doc/TopSolidHelp720/html/4cd4087b-4171-c98e-b7fe-a3d268da745f.htm) | 사용자용 명칭 추가 검증 필요 |
| `Kernel.DB.Operations.Method.MethodOperation` | 있음 | [ADS](https://ads.topsolid.com/_doc/TopSolidHelp720/html/169ad866-0f98-aa75-e953-8dc604222698.htm) | 사용자용 명칭 추가 검증 필요 |
| `Kernel.DB.Operations.MultiStockUpdateNCOperation` | 있음 | [ADS](https://ads.topsolid.com/_doc/TopSolidHelp720/html/e510f61f-a246-6f30-8ca3-74add8172ad1.htm) | 사용자용 명칭 추가 검증 필요 |
| `Kernel.DB.Operations.NCFolderOperation` | 있음 | [ADS](https://ads.topsolid.com/_doc/TopSolidHelp720/html/2a3055ca-a339-5554-3168-e676bab10b9a.htm) | 사용자용 명칭 추가 검증 필요 |
| `Kernel.DB.Operations.NCMechanism.MachineComponentInclusionOperation` | 있음 | [ADS](https://ads.topsolid.com/_doc/TopSolidHelp720/html/e05c7676-fb7f-3801-8246-25b1a1125eed.htm) | 사용자용 명칭 추가 검증 필요 |
| `Kernel.DB.Operations.NCMechanism.NCMechanismDerivationOperation` | 있음 | [ADS](https://ads.topsolid.com/_doc/TopSolidHelp720/html/6ad2c5d9-cc36-70a7-29d6-0d484226024b.htm) | 추상/기반 클래스 |
| `Kernel.DB.Operations.NCMechanism.NCMechanismJointManagementOperation` | 있음 | [ADS](https://ads.topsolid.com/_doc/TopSolidHelp720/html/4fb3cef0-e2a2-cc69-dadf-5fa70221251c.htm) | 사용자용 명칭 추가 검증 필요 |
| `Kernel.DB.Operations.NCMechanism.NCMechanismRigidGroupManagementOperation` | 있음 | [ADS](https://ads.topsolid.com/_doc/TopSolidHelp720/html/ba5bd43b-52c1-e68e-8279-f8aedfba0c0a.htm) | 사용자용 명칭 추가 검증 필요 |
| `Kernel.DB.Operations.NCOperation` | 있음 | [ADS](https://ads.topsolid.com/_doc/TopSolidHelp720/html/49a64d1c-27f2-8684-537f-e538fe1676ce.htm) | 추상/기반 클래스 |
| `Kernel.DB.Operations.NCProgram.NCProgramManagementOperation` | 있음 | [ADS](https://ads.topsolid.com/_doc/TopSolidHelp720/html/1221340c-cd49-b2a1-8103-9cc3b3772c14.htm) | 사용자용 명칭 추가 검증 필요 |
| `Kernel.DB.Operations.Optimized.OptimizedNCOperation` | 있음 | [ADS](https://ads.topsolid.com/_doc/TopSolidHelp720/html/3005253d-1ee3-5740-4dbf-13f806885d36.htm) | 사용자용 명칭 추가 검증 필요 |
| `Kernel.DB.Operations.ParametersCompositeOperation` | 있음 | [ADS](https://ads.topsolid.com/_doc/TopSolidHelp720/html/6f99dac7-57a5-d7e4-8e64-e8bfd7c534a5.htm) | 추상/기반 클래스 |
| `Kernel.DB.Operations.Part.PartMovingOperation` | 있음 | [ADS](https://ads.topsolid.com/_doc/TopSolidHelp720/html/4825e09d-ff4c-229b-7a7d-e1a1d0c5a320.htm) | 사용자용 명칭 추가 검증 필요 |
| `Kernel.DB.Operations.Security.SecurityEntitiesManagementOperation` | 있음 | [ADS](https://ads.topsolid.com/_doc/TopSolidHelp720/html/4aa860bd-3308-1ec0-14a8-29eebe4d6021.htm) | 사용자용 명칭 추가 검증 필요 |
| `Kernel.DB.Operations.Setup.SetupOperation` | 있음 | [ADS](https://ads.topsolid.com/_doc/TopSolidHelp720/html/ddda0b2d-00ae-05f6-424f-ce3ae67d44f5.htm) | 추상/기반 클래스 |
| `Kernel.DB.Operations.SmartOperation` | 있음 | [ADS](https://ads.topsolid.com/_doc/TopSolidHelp720/html/46782505-0adb-fd2f-26a3-8b6d6508c8a0.htm) | 사용자용 명칭 추가 검증 필요 |
| `Kernel.DB.Operations.SpecificLead.SpecificLeadSetupOperation` | 있음 | [ADS](https://ads.topsolid.com/_doc/TopSolidHelp720/html/ceb3da7f-046e-6f84-4545-7d09ba105ad5.htm) | 사용자용 명칭 추가 검증 필요 |
| `Kernel.DB.Operations.StaticOperation` | 있음 | [ADS](https://ads.topsolid.com/_doc/TopSolidHelp720/html/ca623c5e-5755-5980-edfe-9c6912fd2235.htm) | 사용자용 명칭 추가 검증 필요 |
| `Kernel.DB.Operations.StockUpdate.MultiStockUpdateOperation` | 있음 | [ADS](https://ads.topsolid.com/_doc/TopSolidHelp720/html/433d2af4-1f1e-e226-187c-83099133a7bf.htm) | 사용자용 명칭 추가 검증 필요 |
| `Kernel.DB.Operations.StockUpdate.StockUpdateOperation` | 있음 | [ADS](https://ads.topsolid.com/_doc/TopSolidHelp720/html/621c96db-384d-df92-73a8-4bab975ceee9.htm) | 사용자용 명칭 추가 검증 필요 |
| `Kernel.DB.Operations.TaskOperation` | 있음 | [ADS](https://ads.topsolid.com/_doc/TopSolidHelp720/html/9fb44f91-8a99-7c33-f284-e0ca2da2f006.htm) | 사용자용 명칭 추가 검증 필요 |
| `Kernel.DB.Operations.Tuning.StrongTuningOperation` | 있음 | [ADS](https://ads.topsolid.com/_doc/TopSolidHelp720/html/4a182dc8-4b08-fd58-bdc6-e28392201d01.htm) | 사용자용 명칭 추가 검증 필요 |
| `Kernel.DB.Operations.Tuning.TuningOperation` | 있음 | [ADS](https://ads.topsolid.com/_doc/TopSolidHelp720/html/a2467fc0-bff3-7943-2deb-391a72b1618e.htm) | 추상/기반 클래스 |
| `Kernel.DB.Operations.Tuning.WeakTuningOperation` | 있음 | [ADS](https://ads.topsolid.com/_doc/TopSolidHelp720/html/ac7355f0-299b-0878-6cb2-5b6377a09f75.htm) | 사용자용 명칭 추가 검증 필요 |
| `Kernel.DB.OperationsManager.NCOpsManagerEntity.NCSortedOperation` | 없음 | [ADS](https://ads.topsolid.com/_doc/TopSolidHelp720/html/0c591ee4-ae23-df70-8e46-0eec87ab77a8.htm) | 사용자용 명칭 추가 검증 필요 |
| `Kernel.DB.OperationsManager.SortTypeQualityOperation` | 있음 | [ADS](https://ads.topsolid.com/_doc/TopSolidHelp720/html/eb638e2a-f8e7-cfda-517d-4e9d0dace15d.htm) | 사용자용 명칭 추가 검증 필요 |
| `Kernel.DB.Origins.ObsoleteOriginsManagementOperation` | 있음 | 없음 | 사용자용 명칭 추가 검증 필요 |
| `Kernel.DB.Origins.OriginsManagementOperation` | 없음 | 없음 | 사용자용 명칭 추가 검증 필요 |
| `Kernel.DB.PartSetting.DerivatedPartSettingManagementOperation` | 있음 | [ADS](https://ads.topsolid.com/_doc/TopSolidHelp720/html/c984df40-aed1-5361-3516-34afbe595f5e.htm) | 사용자용 명칭 추가 검증 필요 |
| `Kernel.DB.PartSetting.Features.PartHoldingTabsFeatureFolderOperation` | 있음 | [ADS](https://ads.topsolid.com/_doc/TopSolidHelp720/html/c46aafd6-b4d1-9c89-4ba2-95a3b931794f.htm) | 사용자용 명칭 추가 검증 필요 |
| `Kernel.DB.PartSetting.Features.PartTieFeatureFolderOperation` | 없음 | 없음 | 사용자용 명칭 추가 검증 필요 |
| `Kernel.DB.PartSetting.ObsoleteNCPartRepositioningOperation` | 있음 | [ADS](https://ads.topsolid.com/_doc/TopSolidHelp720/html/d4f65a6f-7dd2-be7f-0978-320527948015.htm) | 사용자용 명칭 추가 검증 필요 |
| `Kernel.DB.PartSetting.OldPartSettingManagementOperation` | 있음 | [ADS](https://ads.topsolid.com/_doc/TopSolidHelp720/html/576f0df0-900c-2ae8-1470-d22e479a6dac.htm) | 사용자용 명칭 추가 검증 필요 |
| `Kernel.DB.PartSetting.PartSettingDerivationOperation` | 있음 | 없음 | 사용자용 명칭 추가 검증 필요 |
| `Kernel.DB.PartSetting.PartSettingManagementOperation` | 있음 | [ADS](https://ads.topsolid.com/_doc/TopSolidHelp720/html/3156c43d-1b3a-2435-843e-7a2049e5db94.htm) | 사용자용 명칭 추가 검증 필요 |
| `Kernel.DB.PostProcessors.DatabaseItems.Operation` | 있음 | 없음 | 사용자용 명칭 추가 검증 필요 |
| `Kernel.DB.PostProcessors.NCFiles.NCFilesManagementOperation` | 있음 | [ADS](https://ads.topsolid.com/_doc/TopSolidHelp720/html/9c5e7ee0-4d95-eb70-74da-51b1cd92975f.htm) | 사용자용 명칭 추가 검증 필요 |
| `Kernel.DB.Scenarios.ScenariosManagementOperation` | 있음 | [ADS](https://ads.topsolid.com/_doc/TopSolidHelp720/html/8a30e7bb-3ffe-8d1c-7c0e-84ce492ebee9.htm) | 사용자용 명칭 추가 검증 필요 |
| `Kernel.DB.Stages.MachiningFinalStageOperation` | 있음 | [ADS](https://ads.topsolid.com/_doc/TopSolidHelp720/html/c7213013-8bb7-3d84-e2d7-4dc5e97b6b18.htm) | 사용자용 명칭 추가 검증 필요 |
| `Kernel.DB.Stages.MachiningPreparationStageOperation` | 있음 | [ADS](https://ads.topsolid.com/_doc/TopSolidHelp720/html/8aedcdbf-8b06-8957-a7fb-c4dec2abd2a5.htm) | 사용자용 명칭 추가 검증 필요 |
| `Kernel.DB.Stages.MachiningStageOperation` | 있음 | [ADS](https://ads.topsolid.com/_doc/TopSolidHelp720/html/23ff1f26-2bf8-805d-3c74-a9f73782bc5e.htm) | 사용자용 명칭 추가 검증 필요 |
| `Kernel.DB.Stages.RemachiningStageOperation` | 있음 | [ADS](https://ads.topsolid.com/_doc/TopSolidHelp720/html/24bca813-d490-4b9b-dc4a-b71f373dc79e.htm) | 사용자용 명칭 추가 검증 필요 |
| `Kernel.DB.Tools.Operations.ObsoleteNCToolManagementOperation` | 있음 | 없음 | 사용자용 명칭 추가 검증 필요 |
| `Kernel.DB.Tools.Operations.ToolCatalogManagementOperation` | 있음 | [ADS](https://ads.topsolid.com/_doc/TopSolidHelp720/html/6d48ef9c-4ea3-d04e-7250-cd95d863d176.htm) | 사용자용 명칭 추가 검증 필요 |
| `MillTurn.DB.ControlPoints.Resources.ControlPointsOperation` | 없음 | 없음 | 사용자용 명칭 추가 검증 필요 |
| `MillTurn.DB.CuttingStone.CuttingOperation` | 있음 | 없음 | 사용자용 명칭 추가 검증 필요 |
| `MillTurn.DB.LinkMovements.MachineLinkMovementsRulesManagementOperation` | 있음 | [ADS](https://ads.topsolid.com/_doc/TopSolidHelp720/html/a594b6d4-b165-2980-7733-dc60ed3b0378.htm) | 사용자용 명칭 추가 검증 필요 |
| `MillTurn.DB.Milling.Operation.DebugStockProfileExtenderOperation` | 있음 | [ADS](https://ads.topsolid.com/_doc/TopSolidHelp720/html/768cbf0e-a72d-1b9b-7c34-9a83e5080ca2.htm) | 사용자용 명칭 추가 검증 필요 |
| `MillTurn.DB.Milling.Operation.MillingOperation` | 있음 | [ADS](https://ads.topsolid.com/_doc/TopSolidHelp720/html/e6bfba78-a068-915d-b5b1-fc14692916e6.htm) | 추상/기반 클래스 |
| `MillTurn.DB.Milling.Operation.MillingZoneOperation` | 있음 | 없음 | 사용자용 명칭 추가 검증 필요 |
| `MillTurn.DB.Movement.MovementPointOperation` | 있음 | [ADS](https://ads.topsolid.com/_doc/TopSolidHelp720/html/e46486a0-0cf3-217e-ff84-b167427b93b6.htm) | 사용자용 명칭 추가 검증 필요 |
| `MillTurn.DB.Operations.CamMachiningsNestingCharacteristicsOperation` | 있음 | [ADS](https://ads.topsolid.com/_doc/TopSolidHelp720/html/149adaf8-5f54-8c40-81c0-69f749e5f0cf.htm) | 사용자용 명칭 추가 검증 필요 |
| `MillTurn.DB.Operations.CamNestingCharacteristicsManagementOperation` | 있음 | [ADS](https://ads.topsolid.com/_doc/TopSolidHelp720/html/291bb1a2-f2d9-631a-a0e1-9b901e5a4266.htm) | 사용자용 명칭 추가 검증 필요 |
| `MillTurn.DB.Operations.Displays.MillTurnDisplayOperation` | 있음 | [ADS](https://ads.topsolid.com/_doc/TopSolidHelp720/html/70955953-8aa9-0431-1c3a-23da97fa54a4.htm) | 사용자용 명칭 추가 검증 필요 |
| `MillTurn.DB.Operations.Displays.ObsoleteMillTurnDisplayOperation` | 있음 | [ADS](https://ads.topsolid.com/_doc/TopSolidHelp720/html/4dcd5cb3-9708-3379-baad-5dce78519013.htm) | 사용자용 명칭 추가 검증 필요 |
| `MillTurn.DB.Operations.MillTurnDummyOperation` | 있음 | [ADS](https://ads.topsolid.com/_doc/TopSolidHelp720/html/8c49cbf3-70b4-2378-2959-ca31c472daa8.htm) | 사용자용 명칭 추가 검증 필요 |
| `MillTurn.DB.Operations.MillTurnOperation` | 있음 | [ADS](https://ads.topsolid.com/_doc/TopSolidHelp720/html/f37defd3-1cc5-82f0-8300-e9a5208e1a8c.htm) | 추상/기반 클래스 |
| `MillTurn.DB.Operations.SketchResidualOperation` | 없음 | 없음 | 사용자용 명칭 추가 검증 필요 |
| `MillTurn.DB.Processes.NestingProcessAdvancedConfigurationOperation` | 있음 | [ADS](https://ads.topsolid.com/_doc/TopSolidHelp720/html/a29649db-b1a7-6a80-4149-0c6d7fb6f024.htm) | 사용자용 명칭 추가 검증 필요 |
| `MillTurn.FiveAxis.DB.FiveAxisOperation` | 있음 | [ADS](https://ads.topsolid.com/_doc/TopSolidHelp720/html/6d1b752c-7709-a082-b2ce-d8a6dbb7e331.htm) | 추상/기반 클래스 |
| `MillTurn.FiveAxis.DB.ModuleWorks.ModuleWorksOperation` | 있음 | 없음 | 추상/기반 클래스 |
| `MillTurn.FiveAxis.DB.MultiBlade.MultiBladeEdgeFinishingOperation` | 있음 | 없음 | 사용자용 명칭 추가 검증 필요 |
| `MillTurn.FiveAxis.DB.MultiBlade.MultiBladeOperation` | 있음 | 없음 | 추상/기반 클래스 |
| `MillTurn.FiveAxis.DB.Swarf.SwarfSetupOperation` | 있음 | [ADS](https://ads.topsolid.com/_doc/TopSolidHelp720/html/41a5932e-e704-d1a6-2a85-4d70872f7490.htm) | 사용자용 명칭 추가 검증 필요 |
| `MillTurn.FiveAxis.DB.Tubular.TubularOperation` | 없음 | 없음 | 사용자용 명칭 추가 검증 필요 |
| `MillTurn.Form.DB.FormOperation` | 있음 | [ADS](https://ads.topsolid.com/_doc/TopSolidHelp720/html/19ab7160-0c8b-6184-0e7b-4ea6b3c6457f.htm) | 추상/기반 클래스 |
| `MillTurn.Form.DB.Mirgrind5X.Mirgrind5XOperation` | 있음 | 없음 | 사용자용 명칭 추가 검증 필요 |
| `MillTurn.Form.DB.Multipocketing.MultipocketingOperation` | 있음 | 없음 | 사용자용 명칭 추가 검증 필요 |
| `MillTurn.Form.DB.Stone3D.Stone3dOperation` | 있음 | 없음 | 사용자용 명칭 추가 검증 필요 |
| `MillTurn.Form.DB.Stone4X.Stone4xOperation` | 있음 | 없음 | 사용자용 명칭 추가 검증 필요 |
| `MillTurn.Turn.DB.Operations.TurnOperation` | 있음 | [ADS](https://ads.topsolid.com/_doc/TopSolidHelp720/html/50820c39-8a7c-f758-d6b8-1e6ca6f1e736.htm) | 추상/기반 클래스 |
| `PostProcessors.Kernel.DB.Items.PPOperation` | 있음 | [ADS](https://ads.topsolid.com/_doc/TopSolidHelp720/html/54e631fc-76a9-d92f-41b4-3632ce79f611.htm) | 사용자용 명칭 추가 검증 필요 |
| `Wire.DB.Centering.CenteringOperation` | 없음 | 없음 | 사용자용 명칭 추가 검증 필요 |
| `Wire.DB.Features.FeatureManagementOperation` | 없음 | 없음 | 사용자용 명칭 추가 검증 필요 |
| `Wire.DB.Operations.WireSetupOperation` | 없음 | 없음 | 사용자용 명칭 추가 검증 필요 |

## 주의할 차이

- NibblingOperation: 니블링으로 직역하지 않고 Contour Roughing → 윤곽 황삭.
- GeodesicMachiningOperation: 사용자 명령은 5X Finishing → 5축 정삭.
- MultiAxisRoughingOperation: 사용자 명령은 Multi-axis pocketing → 다축 포켓 가공.
- AngleGroovingOperation: 사용자 명령은 Corner relief → 코너 릴리프.
- VerticalRoughingOperation: 도움말은 Plunge Roughing → 플런지 황삭.
- PointToPointOperation/HoleOperation: 같은 구멍 가공 패밀리에 탭·리밍 등 여러 사이클이 있으므로 클래스만으로 세부 사이클을 판정하지 않는다.
- ADS 사이트 패치 버전과 설치 DLL 패치 버전이 다르다. 각 행에 버전과 근거를 보존한다.
- 이 검증은 이름·타입·표시에 관한 것이며 가공 생성, 계산, NC 출력이나 기계 안전성 검증은 수행하지 않았다.
