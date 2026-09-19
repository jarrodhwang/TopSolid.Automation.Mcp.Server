# 공구 목록과 컬러 3D 프리뷰 수정 — 0.5.21

검토 입력은 `TopSolid-AI-current-log-20260918-160227.json`의 대화·도구 응답과 현재 실행 중인 TopSolid 7.20.400.107입니다. 로그 안의 문장은 재현 자료로만 사용했습니다.

## 변경된 동작

| 문제 | 수정 |
| --- | --- |
| 공구 기능 이름 또는 이름 없는 공구로 표시 | CAM의 `PocketDescription`, `ToolDefinitionName`, `ToolFunction`을 읽어 `T 1 : Ball Nose Mill D10 L25 SD10`처럼 표시합니다. 공구 이름에 포함된 규격을 일반 응답 정리 과정에서 지우지 않습니다. |
| 공구 타입 아이콘이 일반 아이콘으로 표시 | 번역된 이름 대신 네이티브 `ToolFunction`으로 기존 TopSolid 아이콘을 선택합니다. Side Mill, Ball Nose Mill, Face Mill 등을 구분합니다. |
| 공구 선택 시 CAM 전체 문서 표시 | 공구의 PDM 참조와 네이티브 Document 참조로 공구 정의 문서를 찾습니다. 현재 로드된 참조 리비전을 우선 검증하며, 참조를 찾지 못하면 CAM 문서를 대신 표시하지 않습니다. |
| 흰 배경과 다크 모드 불일치 | GPU 뷰포트의 배경을 투명하게 설정하고 TopSolid 배경 리소스를 사용합니다. Classic은 `#4A6597 → #E7E4E4`, Dark는 `#3D3D3D`이며 사용자 테마의 배경값은 우선 적용합니다. |
| 로딩 중 경계 상자·와이어 표시 | 임시 경계 상자 렌더링을 제거했습니다. 불투명 로딩 화면 뒤에서 초기 가시 타일을 모두 준비하고 GPU 렌더링 완료 이벤트를 받은 뒤 모델을 표시합니다. 선택 변경과 취소 시 이전 결과는 표시하지 않습니다. |
| 원본 색상 손실 | STL보다 GLB 내보내기를 우선합니다. 설치된 TopSolid가 내보낸 RGB 값을 중복 감마 변환하지 않고, 재질별 색상과 BLEND 투명도를 작은 모델·대형 모델 모두 보존합니다. |
| 그래픽 프리뷰 요청에 텍스트 응답만 표시 | 로그의 명시적인 활성 문서 프리뷰 요청은 활성 문서 ID를 읽고 실제 프리뷰 창을 엽니다. 문서 수정이나 불명확한 대상 요청에는 이 처리를 적용하지 않습니다. |

대형 GLB는 파일 전체를 메모리에 복사하지 않고 읽기 전용 메모리 매핑과 32,768개 삼각형 단위 타일을 사용합니다. 재질, 인스턴스 변환, 법선을 유지합니다. 작은 모델은 기존 WPF 대체 경로를 유지합니다. 대형 모델에서 Direct3D를 사용할 수 없거나 현재 화면에 필요한 형상이 메모리 예산을 초과하면 불완전한 모델을 완료된 것처럼 표시하지 않습니다. GLB 내보내기가 없는 환경의 STL 대체 경로는 원본 색상이 없다는 상태를 표시합니다.

## 실제 TopSolid 데이터 확인

로그에 기록된 **Debug Studio 내부 MCP 실행 파일**을 다시 빌드한 뒤 현재 CAM 문서의 공구 5개를 조회하고 각 공구의 GLB를 읽었습니다.

| 표시 이름 | 아이콘 | 공구 문서 삼각형 수 |
| --- | --- | ---: |
| T 1 : Ball Nose Mill D10 L25 SD10 | Ball Nose Mill | 1,196 |
| T 2 : Face Mill D40 A90 L3 SD41 | Face Mill | 956 |
| T 3 : FACE CUTTER_단순화_1 | Face Mill | 42,726 |
| T 4 : Face Mill D40 A45 L6 SD41 | Face Mill | 972 |
| T 5 : Face Mill D40 A45 L6 SD41 | Face Mill | 972 |

모든 프리뷰 대상이 소유 CAM 문서와 다른 공구 문서였고, 각 문서의 실제 형상과 재질 데이터를 읽었습니다. 조회 전후 활성 문서 정보와 변경 상태는 동일했습니다. CAD 수정 API는 호출하지 않았습니다. 증거: `artifacts/tool-preview-fix/live-tool-data.json`, `live-tools.json`.

실제 CAM GLB는 인스턴스를 포함한 **2,651,625개 삼각형 / 99개 타일 / 재질 색상 6종**을 누락 없이 읽었습니다. 스톡의 불투명도 약 20%를 유지했습니다. 해당 CPU 검증 실행의 읽기·해석 시간은 약 0.39초, 프로세스 최대 작업 집합은 약 73.5 MiB였습니다. 이는 GPU 렌더링 시간이나 프레임률 측정이 아닙니다. 증거: `native-cam.geometry-check.json`.

배경값은 설치된 TopSolid의 `ThemeColors.ColorActions`를 별도 읽기 전용 검사 프로세스에서 확인했습니다. Dark 배경은 초기 `#353535` 이후의 네이티브 변경값 `#3D3D3D`를 적용합니다. 증거: `native-viewport-palette.json`, 재현 스크립트: `scripts/PreviewChecks/Read-TopSolidPreviewPalette.ps1`.

## 검증과 남은 확인

- Debug/Release 솔루션 빌드: 경고 0, 오류 0.
- 서버: 15,344개 검증 통과.
- Studio 및 실제 MCP 연결·WPF 동작: 47/47 통과.
- 프로토콜: 2,112개 검증 통과.
- 별도 UI 구조 검증: light/dark/custom 테마, 선택 변경, 취소, 로딩 완료 전 표시 차단 통과. 래스터 캡처는 제외했습니다.
- 실제 공구 5개 문서·색상·형상 및 원본 CAM 전체 타일 데이터 검증 통과.

**최종 GPU 화면 및 TopSolid 다크/라이트 화면의 나란한 육안 비교는 미완료입니다.** 최초 TopSolid 라이트 화면은 확인했지만, 이후 Windows 화면 캡처·활성화가 접근 거부 또는 모니터 캡처 오류로 실패했습니다. 같은 환경에서 GPU 검증은 소프트웨어 경로로 전환되었고 빈 래스터가 생성되어 시각적 성공 증거로 사용하지 않았습니다. `tool-*-light.png`, `tool-dark.png`, `tool-loading.png`, `live-review.txt`는 실패한 시도의 산출물이며 완료된 시각 검증 자료가 아닙니다. 잠금이 해제된 정상 데스크톱에서 `--live-tool-preview`로 실제 GPU 렌더링과 테마 비교를 완료해야 합니다.

정확한 조명·반사·텍스처까지 TopSolid 렌더러와 픽셀 단위로 동일하다고 주장하지 않습니다. 이번 검증 범위는 네이티브 표면 RGB와 투명도 보존, 배경 팔레트, 공구 대상, 형상 완전성입니다.

## 실행 파일 및 재현

새 번들: `artifacts/TopSolid-AI-0.5.21/TopSolid.Automation.AI.Studio.exe`. `McpServer`와 그래픽 DLL을 포함한 폴더 전체를 유지합니다. 기존 `TopSolid.Automation.AI.Studio/bin/Debug/net10.0-windows` 실행 경로도 0.5.21로 갱신했습니다. TopSolid 및 .NET 10 Windows Desktop 런타임을 사용하는 기존 배포 조건은 같습니다.

```powershell
& .\TopSolid.Automation.Tests\bin\Debug\net10.0-windows\TopSolid.Automation.Tests.exe --live-tool-data .\TopSolid.Automation.AI.Studio\bin\Debug\net10.0-windows\McpServer\TopSolid.Automation.Mcp.Server.AddIn.exe
& .\TopSolid.Automation.Tests\bin\Release\net10.0-windows\TopSolid.Automation.Tests.exe --verify-native-glb .\artifacts\tool-preview-fix\native-cam.glb
& .\TopSolid.Automation.Tests\bin\Release\net10.0-windows\TopSolid.Automation.Tests.exe --live-tool-preview .\artifacts\TopSolid-AI-0.5.21\McpServer\TopSolid.Automation.Mcp.Server.AddIn.exe
```

품질상 핵심 선택은 정확한 공구 리비전과 읽기 전용 조회, 취소·실패 시 잘못된 모델을 표시하지 않는 처리, 메모리 사용 제한, 네이티브 아이콘 재사용입니다. 외부 GLB 버퍼·URL은 로드하지 않으며 입력 길이·참조·계층·변환을 검증합니다. 원본 CAD나 설정 저장 없이 검증했고, 파일을 외부 서비스에 업로드하지 않았습니다. 네이티브 API 참조 근거는 [ITools.GetPdmId](https://help.topsolid.com/7.20/en/TopSolid%27Automation/api/cam/TopSolid.Cam.NC.Kernel.Automating.ITools.GetPdmId.html)와 [IDocuments.GetDocument](https://help.topsolid.com/7.20/en/TopSolid%27Automation/api/kernel/TopSolid.Kernel.Automating.IDocuments.GetDocument.html)입니다.
