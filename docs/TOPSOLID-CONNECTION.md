# TopSolid 연결 설정

`설정 > TopSolid 연결`에서 대상을 지정하고 `연결 테스트` 후 `설정 저장`을 누릅니다. 테스트는 별도 MCP 프로세스에서 버전, PID, 라이선스만 읽습니다. 저장하여 연결 대상을 바꾸면 기존 대화의 문서/요소 ID와 승인 상태를 새 대상으로 전달하지 않도록 새 대화를 시작합니다. 시작 시 연결/라이선스 확인에 실패한 화면에서도 `TopSolid 연결`로 설정하고 재시도할 수 있습니다.

기존 메시지와 진단 로그는 화면에 유지합니다. 연결을 다시 맺을 때에도 AI의 활성 문서 정보를 초기화하여 이전 프로세스의 ID로 작업하지 않도록 합니다.

## 로컬 컴퓨터

- **자동:** 선택한 버전과 일치하는 실행 인스턴스가 정확히 하나일 때 연결합니다. 버전을 비우면 전체 실행 인스턴스가 하나여야 합니다.
- **실행 중인 인스턴스 선택:** 새로 고침 후 버전과 PID로 대상을 고릅니다. PID뿐 아니라 프로세스 시작 시각도 저장합니다. 재시작된 인스턴스는 새로 선택해야 합니다.
- **파이프 이름 지정:** TopSolid를 `TopSolid.exe -pipeName cad-720-a`처럼 실행한 경우 해당 이름을 입력합니다. 같은 버전의 여러 인스턴스에도 각각 다른 이름을 사용합니다.

여러 TopSolid가 모두 기본 파이프를 사용하면 API가 그중 하나에만 연결될 수 있습니다. PID는 연결 후 확인 값이며 PID만으로 새 엔드포인트가 생기지는 않습니다. 서로 다른 파이프 이름으로 시작하거나, 각 인스턴스에 별도의 Automation TCP 포트를 설정해야 합니다. Studio는 TopSolid를 자동 실행하거나 기존 인스턴스를 재시작하지 않습니다. 선택한 PID와 응답한 PID가 다르면 작업 전에 연결을 끊습니다.

목록에는 7.18/7.19도 표시하며, MCP 공통 도구의 지원 하한은 **7.18**입니다. 도구마다 필요한 최소 버전은 `tools/list`의 `_meta.topsolid/minimumVersion`에 포함됩니다. 연결된 버전보다 높은 도구를 호출하면 작업을 실행하지 않고 `지원되지 않는 버전입니다. 이 도구는 TopSolid X 이상이 필요합니다` 형식의 오류를 반환하며 Studio 화면에도 같은 안내를 표시합니다.

7.18 설치에는 Cae/Electrode Automating DLL이 없으므로 해당 모듈 도구는 **7.20 이상**이 필요합니다. 네이티브 모델링 미리보기/생성 경로는 현재 SDK 검증 기준인 **7.20.326 이상**을 요구합니다. 그 외 공통/Kernel/Design/Drafting/CAM 경로는 7.18 하한으로 노출하되 실제 모듈 라이선스와 문서 상태는 별도로 확인합니다.

## 원격 컴퓨터: Automation TCP

원격 TopSolid에서 `도구 > 옵션 > 일반 > Automation > 원격 접근 관리`를 켜고 지정한 포트를 사용합니다. Studio에는 호스트/IP와 포트를 입력합니다. 버전은 선택 사항입니다. 여러 버전/인스턴스는 **인스턴스별 고유 포트**로 선택합니다. 포트를 비우면 저장하지 않습니다.

이 경로는 공식 `TopSolidHost.DefineConnection(host, port, null, 0)`을 사용합니다. 이벤트 콜백은 사용하지 않으므로 클라이언트 수신 포트를 열 필요가 없습니다. 원격 접근 허용, Windows 계정/네트워크 인증, 방화벽은 실제 환경에 맞게 구성해야 합니다. TCP를 HTTPS로 취급하지 않습니다.

## 원격 컴퓨터: HTTPS 게이트웨이

번들 MCP 서버의 **전용 HTTPS/WebSocket 터널**을 사용합니다. 일반 HTTP MCP 서버 URL이나 TopSolid의 TCP 포트와 호환되지 않습니다. 원격 게이트웨이는 TopSolid가 실행된 Windows 사용자 세션에서 실행합니다. Studio는 게이트웨이의 `인스턴스 새로 고침`으로 원격 PC의 버전/PID를 조회하고, 로컬과 동일하게 한 대상을 선택합니다.

원격 PC 준비:

1. 빌드/배포한 `McpServer` 폴더 전체와 .NET Framework 4.8을 준비합니다. 실행 파일만 복사하지 마세요.
2. 사용할 DNS 이름 또는 IP가 SAN에 포함된 서버 인증서를 설치합니다. Studio PC에서 발급 CA를 신뢰해야 합니다. 인증서 검증을 끄는 옵션은 없습니다.
3. 관리자가 Windows HTTP.sys에 해당 HTTPS 포트의 인증서를 연결하고 실행 사용자에게 URL 권한을 부여합니다. 예시의 호스트, 사용자, 포트와 인증서 지문은 실제 값으로 바꿉니다. 기존 포트 바인딩을 먼저 확인하세요.

   ```powershell
   netsh http show sslcert
   netsh http add sslcert ipport=0.0.0.0:9443 certhash=CERTIFICATE_THUMBPRINT appid='{2C18BA29-A9DD-484E-BE49-11790289A146}' certstorename=MY
   netsh http add urlacl url=https://cad-host:9443/topsolid/ user=DOMAIN\Operator
   ```

4. 필요한 클라이언트에만 방화벽의 HTTPS 포트 접근을 허용합니다. 설치 스크립트는 인증서, 신뢰 저장소, URL ACL, 방화벽을 자동 변경하지 않습니다.
5. 안전하게 생성한 32자 이상 토큰을 준비하고 다음 스크립트를 실행합니다. 토큰은 숨김 입력으로 받으며 명령행 인수나 로그에 출력하지 않습니다. 토큰 소유자는 해당 사용자 세션의 TopSolid를 조작할 권한을 가지므로 승인된 사용자에게만 제공합니다.

   ```powershell
   .\scripts\Start-TopSolidHttpsGateway.ps1 -HostName cad-host -Port 9443 -ServerPath 'C:\TopSolid-AI\McpServer\TopSolid.Automation.Mcp.Server.AddIn.exe'
   ```

6. Studio에서 HTTPS 게이트웨이 방식, 같은 호스트/포트/토큰을 입력하고 목록을 새로 고칩니다. 빈 HTTPS 포트는 443입니다. 토큰은 현재 Windows 사용자와 엔드포인트에 묶어 DPAPI로 암호화하여 저장합니다. 주소/포트를 편집하면 토큰과 이전 원격 PID 선택이 지워집니다.

연결마다 별도의 STA MCP 프로세스와 승인 티켓을 사용하며 동시 세션은 8개로 제한됩니다. 네트워크가 끊겨도 이미 접수한 CAD 변경을 강제 종료하거나 자동 재시도하지 않습니다. 처리 결과가 불명확하면 TopSolid에서 결과를 확인해야 합니다. 게이트웨이는 대기 중인 작업이 끝날 때까지 해당 프로세스의 슬롯을 유지합니다.

## 검증 범위

설정/토큰 저장과 대상 검증 테스트, 실제 로컬 7.20 연결 및 모호한 대상·오래된 PID·다른 버전 거부를 검증합니다. 원격 PC의 방화벽, 인증서 배포, Windows 인증 및 실제 원격 CAD 작업은 해당 배포 환경에서 별도 확인해야 합니다.

재현 명령:

```powershell
dotnet build TopSolid.Automation.Mcp.Server.slnx -c Release
.\TopSolid.Automation.Tests\bin\Release\net10.0-windows\TopSolid.Automation.Tests.exe --topsolid-connection-settings
.\TopSolid.Automation.Tests\bin\Release\net10.0-windows\TopSolid.Automation.Tests.exe --topsolid-connection-ui
.\TopSolid.Automation.Mcp.Server.Tests\bin\Release\net48\TopSolid.Automation.Mcp.Server.Tests.exe --connections
.\TopSolid.Automation.Mcp.Server.Tests\bin\Release\net48\TopSolid.Automation.Mcp.Server.Tests.exe --version-support
.\TopSolid.Automation.Mcp.Server.Tests\bin\Release\net48\TopSolid.Automation.Mcp.Server.Tests.exe --gateway-transport
.\TopSolid.Automation.Tests\bin\Release\net10.0-windows\TopSolid.Automation.Tests.exe --topsolid-connection-live "$PWD\TopSolid.Automation.Mcp.Server.AddIn\bin\Release\net48\TopSolid.Automation.Mcp.Server.AddIn.exe"
```

게이트웨이 전송 테스트는 테스트 WebSocket과 실제 별도 MCP 프로세스로 초기화, 알림, 도구 목록 전달과 정상 종료를 확인합니다. 인증서/실제 HTTPS 네트워크 접속 시험을 대신하지 않습니다. UI 캡처는 `artifacts/topsolid-connection-review/`에 생성됩니다.

## 품질 검토

| 관점 | 적용 및 제한 |
| --- | --- |
| 기능 적합성 | 로컬 단일/다중 인스턴스와 버전 선택, 원격 TCP 포트별 선택, HTTPS 원격 목록 조회. PID만으로 기본 파이프 충돌을 해결할 수는 없음. |
| 신뢰성 | PID와 시작 시각 확인, 버전 불일치 거부, 잘못된 저장 설정의 기본 로컬 연결 방지, 시작 전 설정 복구. 변경 도중 통신 실패 시 작업 결과를 확인하고 재시도. |
| 성능 | 목록은 사용자 요청 시 조회, 제한 시간과 메시지 크기 제한, HTTPS 동시 세션 최대 8개. 끝나지 않는 네이티브 작업은 강제 종료 대신 슬롯 유지. |
| 유지보수 | 공통 연결 DTO, 재사용 설정 편집기, 서버의 STA 연결 소유권 유지. 게이트웨이와 설정/대상 검증 테스트 분리. |
| 호환성 | 기존 설정은 로컬 자동 연결로 이전. 공통 도구는 7.18부터 지원하고 도구별 최소 버전을 호출 전에 검사. 7.18에 없는 Cae/Electrode는 7.20 이상, 모델링 경로는 7.20.326 이상. TCP 및 HTTPS 게이트웨이는 서로 다른 방식으로 표시. |
| 보안 | 토큰 DPAPI 보호와 엔드포인트 결속, 정상 인증서 검증, 인증 전 프로세스 실행 금지, 브라우저 Origin 차단, 고정 로컬 실행 파일. |
| 사용성 | 버전/PID 표시, 테스트/취소, 한국어/영어 및 밝은/어두운 테마, 이전 대화 화면 유지. |
| 이식성 | Windows와 .NET Framework 4.8 및 HTTP.sys 전제. 별도 원격 게이트웨이 설치와 인증서·방화벽 설정 필요. |

실제 사용 품질은 대상 선택과 연결 테스트의 성공 여부(효과성), 목록 재사용과 기본 포트 처리(효율성), 명확한 오류·복구 화면(만족도), 잘못된 대상에서의 작업 방지와 승인 유지(위험 감소), 단일/다중 버전·원격·재시작·오류 상황(사용 맥락 범위)으로 점검합니다. 실제 원격 운영 환경 검증은 남아 있습니다.

공식 API 근거: [원격 TCP 연결](https://help.topsolid.com/7.20/en/TopSolid%27Automation/articles/Remote.html), [PipeName](https://help.topsolid.com/7.20/en/TopSolid%27Automation/api/kernel/TopSolid.Kernel.Automating.TopSolidHost.PipeName.html), [Connect](https://help.topsolid.com/7.20/en/TopSolid%27Automation/api/kernel/TopSolid.Kernel.Automating.TopSolidHost.Connect.html).
