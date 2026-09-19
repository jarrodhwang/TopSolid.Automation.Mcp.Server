# 목록 다이얼로그와 대형 모델 프리뷰 — 0.5.20

목록 요청은 실제 MCP 조회 결과를 아이콘 카드 다이얼로그에 표시합니다. 대형 모델은 Direct3D 11과 파일 기반 타일 로딩으로 표시합니다. 오퍼레이션 선택과 툴패스 오버레이 연결은 구현됐지만, 현재 설치된 TopSolid 7.20.400.107에서 실제 툴패스 좌표를 받는 단계는 미완료입니다.

## 동작

- `가공 오퍼레이션 목록`, `공구 리스트`, `문서 목록`, `파라미터 목록`, `엔터티 목록`, `프로젝트 목록`, `라이브러리 목록`은 별도 다이얼로그 요청 없이 목록 창을 엽니다. 단순 요청은 모델 호출 없이 조회합니다. 특정 이름·조건이 포함된 요청과 파트 등 문맥에 따라 범위가 달라지는 요청은 모델이 조회 범위를 결정하고, 조회 결과의 표시 형식은 Studio가 결정합니다.
- 이름·위치 검색, 종류별 아이콘, CAM 공구 번호·종류·규격, 전체/현재 조회 수, 다음 페이지 읽기를 제공합니다. 검색은 현재 읽은 항목을 대상으로 합니다. 실패하거나 범위가 맞지 않는 결과를 정상 목록으로 취급하지 않습니다. 목록 창을 닫는 동작은 작업 취소로 기록하지 않습니다.
- 오퍼레이션 목록은 문서 모델을 먼저 표시합니다. 항목을 선택하면 그 오퍼레이션의 정확한 `DocumentId`/`ElementId`로 경로를 요청합니다. 연속 선택은 이전 경로를 즉시 지우고, 늦게 도착한 결과를 무시합니다. 같은 문서에서는 모델 타일·카메라를 유지합니다.

## 대형 모델 처리와 품질 절충

Direct3D 11 전용 정점/인덱스/선 버퍼와 다중 스레드 준비를 사용합니다. 큰 binary STL은 전체 파일을 메모리 배열로 만들지 않고 512 KiB 전송 조각과 32,768개 삼각형 단위 타일로 읽습니다. 파일 오프셋과 전체 크기는 64비트입니다. 화면과 RAM/VRAM 예산에 맞는 타일을 유지하며, 상주하지 않은 보이는 타일은 경계 상자로 표시합니다. CPU 정점/법선 계산에는 `System.Numerics`를 사용합니다. 모델 크기와 화면 프레임률은 동일한 지표가 아닙니다.

TopSolid 내보내기는 별도 MCP 프로세스의 읽기 API로 수행하며 문서, 공차 기본값, 선택 상태를 수정하지 않습니다. 기본 STL 프리뷰 공차는 0.05 mm / 5°입니다. 큰 STL에는 전역 모서리/인접성 사전을 만들지 않으므로 음영 표시를 우선합니다. STL은 원본의 면 색상 정보를 보존하지 않으며 중립 회색으로 표시합니다. 작은 GLB는 원래 재질 색상을 사용합니다. 큰 GLB에는 아직 타일 importer가 없어 128 MiB 제한이 남습니다. 현재 TopSolid 경로는 STL을 우선 사용합니다.

GPU 생성/기기 오류 시 작은 모델은 기존 WPF 표시로 대체합니다. 파일 기반 대형 모델은 GPU가 필요하다는 상태를 표시합니다. API에는 호출자 파일 경로 대신 임시 전송 식별자를 사용하며, 조각의 식별자·오프셋·길이를 확인합니다. 임시 파일은 성공·실패·취소 후 정리합니다. 취소 중인 네이티브 호출은 완료를 기다려 응답을 소진하므로 다른 선택의 응답으로 잘못 사용되지 않습니다.

같은 저장소의 별도 프리뷰 작업이 추가한 파일 기반 타일 처리와 선택적 OCCT importer를 보존해 통합했습니다. OCCT 네이티브 런타임 설치/빌드는 이번 검증에 포함하지 않았습니다.

## 실제 툴패스 표시가 남은 이유

현재 CAM 문서의 오퍼레이션 `10990`은 계산 완료 상태이지만, `IToolPath.NextToolPathItem`의 `GOTO_XYZ_3D` 값은 직접 Automation 호출에서도 `System.String`의 빈 값입니다. 설치된 `TopSolid.Cam.NC.Kernel.Automating.Host.dll`의 `ToolPathHost.ToAutomating(CLData)`를 읽기 전용으로 조사한 결과, 정수/실수/문자열 이외 CLData 형식은 빈 문자열을 반환합니다. 서버의 JSON 변환 이전에 좌표 정보가 소실됩니다.

추가 재검증: 공식 [`TopSolid.Cam.NC.Kernel.Automating.IToolPath`](https://help.topsolid.com/7.20/en/TopSolid%27Automation/api/cam/TopSolid.Cam.NC.Kernel.Automating.IToolPath.html)와 설치된 DLL의 인터페이스는 모두 `StartToolPath(ElementId)`, `NextToolPathItem(ElementId)`, `EndToolPath(ElementId)` 세 메서드입니다. 현재 `Automation/ToolpathPreview.cs`는 이 순서를 사용합니다. 인터페이스는 경로의 열/행 데이터를 제공하며 화면에 그리는 메서드는 제공하지 않습니다. 반환 좌표를 Studio 렌더러가 그려야 합니다.

`scripts/Inspect-IToolPathContract.ps1`로 같은 문서의 7개 오퍼레이션에서 최대 96행씩, 총 599행을 직접 조회했습니다. `GOTO_XYZ_3D`가 있는 363행 모두 빈 문자열이며, 숫자 `X`/`Y`/`Z` 세 열이 함께 반환된 행은 0개입니다. 관련 열은 `GOTO_XYZ_3D`, `3D_CENTER_XYZ`, 가공기 축/프레임 열이고 문서 예시의 독립 X/Y/Z 열은 없습니다. 호스트와 직접 참조한 Automation DLL은 모두 7.20.400.107이며, 모든 검사 대상은 up-to-date 상태입니다. 문서 dirty 상태는 유지됐습니다. 재현 결과는 `artifacts/list-preview-20260918/itoolpath-contract-probe.json`에 저장했습니다. 이는 검사한 행과 현재 설치 버전의 결과이며 다른 버전 전체로 일반화하지 않습니다.

따라서 선택 UI, 경로 버퍼, 좌표 단위 검사, 취소 처리는 구현돼도 **현재 파일의 실제 툴패스가 다이얼로그에 표시됐다고 보고하지 않습니다.** 유효한 좌표를 받지 못하면 `TopSolid에서 툴패스 좌표를 반환하지 않았습니다`를 표시합니다. 지원하지 않는 원호와 미해결 가공 좌표계는 직선으로 추정하지 않습니다. 일부 경로만 받은 경우 부분 표시임을 알립니다. 완성에는 좌표와 문서 좌표계 변환을 제공하는 TopSolid 지원 API 또는 별도로 검증된 프로세스 내부 ADS 연동이 필요합니다. 이번 작업은 TopSolid DLL 수정이나 프로세스 주입을 하지 않았습니다.

## 검증

2026-09-18, Windows x64, TopSolid 7.20.400.107에서 확인했습니다.

| 항목 | 결과 |
| --- | --- |
| Release 솔루션 빌드 | 오류 0, 경고 0 |
| Studio 회귀 테스트 | 44/44 통과; 번들 서버 연결·실제 Send UI 포함 최종 통합 검사 46/46 통과 |
| MCP 서버 검사 | 15,321 통과; 해당 검사에서는 CAD 연결/변경 없음 |
| WPF UI 검사 | 아이콘·검색·반응형 배치·연속 선택·경로 실패·카메라 유지 통과 |
| 타일 검사 | 64비트 크기·범위·메모리 예산·취소·손상 데이터 검사 통과 |
| 실제 CAM STL | 138,881,384 bytes / 2,777,626 triangles / 85 tiles |
| 내보내기 + 파일 전송 | 약 5.61초 |
| 전체 파일 타일 인덱스 | 약 32 ms |
| 실제 GPU | NVIDIA RTX PRO 3000 Blackwell Generation Laptop GPU / Direct3D 11 |
| 실제 모델 GPU 표시 | 85개 타일 모두 상주; 타일 로딩과 초기 프레임 대기 합계 약 1.31초 |
| GPU 테스트 프로세스 최고 작업 집합 | 약 695 MiB; TopSolid 프로세스 및 VRAM 사용량은 별도 |
| 원본 문서 상태 | 조회 전후 이름·식별자·dirty 상태 동일 |
| 실제 툴패스 | 256개 이동 지점의 좌표가 누락됨; `coordinatesUnavailable` 확인 |

위 측정은 현재 실제 모델과 검증용 형상에 한합니다. 15GB 파일, 수억 개 면, 프레임률, 모든 GPU·원격 세션의 성능은 검증하지 않았습니다.

근거 파일: `artifacts/list-preview-20260918/live-paged-preview.json`, `direct3d-device.json`, `direct3d-native-model.json`, `direct3d-native-model.png`. `artifacts/ui-redesign/graphic-operation-list-toolpath-ko.png`의 경로는 UI 검증용 합성 좌표입니다.

## 실행

새 실행 파일은 `artifacts/TopSolid-AI-0.5.20/TopSolid.Automation.AI.Studio.exe`입니다. 같은 폴더의 `McpServer`와 그래픽 의존 DLL을 함께 유지합니다. 기존 Debug Studio는 실행 중이라 교체하지 않았습니다. 사용 중인 Studio를 정상 종료하고 이 실행 파일로 시작해야 변경이 적용됩니다. TopSolid 자체를 종료할 필요는 없습니다. 다른 Studio 빌드의 `McpServer`를 가리키는 저장 설정은 기존 `ResolveServerPath` 동작에 따라 새 번들의 서버로 해석됩니다.
