# Review layout and CAM identity fixes — 0.5.13

The supplied September 18 log contains the correct CAM names in `operationName`. The selection dialog only read generic name fields and therefore displayed “이름 없는 작업”. It now prioritizes the native operation description, including its number, without inventing a number from page position. Selection still returns the original immutable operation identity and source arguments.

The active document confirmed these native labels:

- `[2: 볼 동시가공 커브 스위핑 (축 방향)]`
- `[3: 동시가공 스위핑 (축 방향)]`
- `[4: 직선가공]`, `[5: 직선가공]`, `[6: 커브따라가공]`, `[7: 커브따라가공]`

## Approval review

One scrolling page replaces the single-tab frame and recursively nested cards. Requested dimensions appear first. Profiles and other object lists have small headings; coordinates occupy one row with explicit X/Y/Z components. Known geometry kinds are localized without translating user-assigned names. Full prepared facts remain available under one disclosure, built only when expanded.

Operation parameter approval retains the operation, parameter, current value and new value, including explicit SI units for proposed real values. Formula/reference replacement and affected-document scope remain visible. Generic arguments use a properties-sheet icon; ordinary parameters use the native CAD parameter symbol. The 3D preview and immutable confirmation payload are unchanged. Cancel remains the default keyboard action.

## Native icons

`IOperations.GetOperations` returns `TaskOperation` wrappers for this CAM document. Their kernel friendly name is merely “테스크”. The server now uses `IOperations.GetNCOperation`, then `IElements.GetTypeFullName`, to obtain the actual machining class. It exposes that class as display metadata without changing the selected task's identity.

The two sweeping operations resolve to `TopSolid.Cam.NC.MillTurn.Form.DB.Sweeping.Operation.SweepingOperation`. Straight and curve-following tasks resolve to `TopSolid.Cam.NC.MillTurn.DB.SideMilling.SideMillingOperation`. These map directly to the matching original DB-class icons. The registry contains 132 exact class mappings covering NC operations, milling, form machining, five-axis machining, turning, grinding and wire operations.

`scripts/Import-ReviewIcons.ps1` imports unmodified icons from the supplied 7.19 collection and records their paths, exact type names and SHA-256 hashes in `Assets/TopSolid/provenance.json`. An exact full class name is required; operation names and translated captions never guess the machining type. Unknown types use the generic NC icon. A native metadata fault does not discard an otherwise readable operation; transport failures still propagate.

Parameter categories use separate tool, cutting-condition, geometry, strategy, comment, multi-axis and properties icons. Category matching handles paths such as `CuttingConditions|Tool`, `Global|Comments`, and `First|Strategy`; unknown categories retain a neutral parameter icon. Category names are data and are not assumed to be a complete one-to-one description of every TopSolid pane.

Icon images are cached and frozen. All packaged PNGs together occupy approximately 484 KB. Exact class mapping avoids per-item UI API calls and language-dependent heuristics. The supported host remains the installed 7.20.400.107 Automation SDK; icon artwork comes from the user's 7.19 collection. Native type metadata is hidden in User Mode and preserved in developer receipts.

## Verification

- Release and Debug solution builds: zero warnings/errors.
- Application suite: 39/39 groups; offline server suite: 15,249 checks.
- Protocol suite against the configured Debug MCP executable: 2,094 checks with native queries disabled. A separate live CAM run verifies native reads.
- Read-only live CAM run: all seven operation choices retained their native labels and exact icon mappings; 649 parameters and eight cutting-condition samples read successfully, zero failed rows. Document and operation state were unchanged; no native write calls.
- WPF fixtures: light/dark flat sketch review, Korean geometry labels, compact window controls, lazy full details, exact operation card images, CAM approval header/category images, immutable selection/approval and loaded icon resources. Offscreen captures use software rendering; they are not GPU throughput measurements.
- Live operation summary timing: 0.18 s on the first measured read and 0.02 s on the repeated read, seven operations. These are local observations, not performance guarantees.

Evidence is under `artifacts/cam-review-0.5.13` and `artifacts/ui-redesign`. The complete local bundle is `artifacts/TopSolid-AI-0.5.13`. The supplied log's separate cylinder-creation exception is outside these review/name/icon fixes.

## Chat requests to test

Use User Mode and the approval permission mode. For the first two examples, activate the CAM document. For modeling examples, activate a part document and cancel the final approval if only inspecting the UI.

1. `현재 CAM 문서의 가공 작업을 선택 대화 상자로 보여줘. 작업을 선택하면 이름과 번호만 알려주고, 변경하지 마.`
2. `2번 작업의 절삭 조건 파라미터를 선택 대화 상자로 보여줘. 현재 값과 단위를 알려주고 변경하지 마.`
3. `활성 부품에 지름 40 mm 원과 60 × 30 mm 사각형을 한 스케치로 만들 변경 내용을 검토하게 해줘. 승인 전에는 실행하지 마.`
4. `활성 부품의 원점에서 Z 방향으로 지름 40 mm, 높이 80 mm 실린더를 만들고 싶어. 먼저 변경 검토와 3D 미리보기를 보여줘.`
