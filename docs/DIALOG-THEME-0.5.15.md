# Consistent TopSolid dialogs — 0.5.15

The main window, Developer dashboard, connection status, approval reviews, typed questions and error details now share the same TopSolid title bar, toolbar gradient, command buttons, typography, separators and theme resources. Title-bar controls have English/Korean accessible names. Native WPF window chrome retains dragging, resizing and system-window commands; windows do not use a transparent composition layer.

## Visible behavior

- Approval and question actions use the original TopSolid OK/Cancel icons. Questions retain type-specific icons and searchable cards, with compact corners and the same hover/selection colors as other controls. CAM operation icons and immutable approval facts are preserved.
- Headers and bottom action rows span the entire dialog, including graphical reviews. At narrow widths, graphical questions show Selection / 3D preview tabs so the viewport cannot consume the selection list. Resizing and switching tabs preserve the selected object.
- Checkboxes, disclosure controls, tabs, scrollbars, tooltips and menu surfaces use the shared palette. Open windows update when light, dark or a saved TopSolid custom palette changes. Color swatches retain their actual RGB color while hovered or focused.
- Handled errors remain in chat and redacted logs. A toolbar error icon opens a themed review on demand; failures do not open automatic modal loops. Developer details are available only in Developer Mode. Successful operations and a new chat clear the previous error indicator.
- Clearing a selection now clears both its visible highlight and the retained answer. The same item can be selected again immediately.
- File open/save and image-file selection continue to use Windows shell dialogs, preserving shell navigation, accessibility and network-location support. Their appearance follows Windows. No replacement file explorer is introduced.

## Test in Studio

Select light and dark appearance in Settings, then try:

1. `사용할 프로젝트를 이름과 아이콘이 있는 선택 창으로 보여줘.` — search, select, clear and select the same project again.
2. `열려 있는 부품을 선택 창으로 보여줘.` — resize the dialog to see the side-by-side and tabbed 3D layouts; switch tabs and verify the selected part remains selected.
3. `현재 부품에 40 × 30 mm 사각형을 만들기 전에 변경 승인 창을 보여줘.` — inspect the flat facts, footer and original confirmation icons; cancel to leave the model unchanged.
4. `색상 선택 창으로 부품에 사용할 색상을 물어봐.` — hover color swatches and enter RGB/HEX values; the swatch itself should not become a theme highlight color.
5. `절삭 조건을 수정할 가공을 먼저 선택 창으로 보여줘.` — check native CAM names, operation numbers and matching operation-type icons before any change approval.

Open Developer Mode to compare its toolbar, gauges, tabs and GPU page with the main window. If a handled error occurs, use the toolbar error icon to inspect it; closing error details has no retry or CAD side effect.

## Verification and practical trade-offs

The separate WPF fixture checks live theme changes on the same open window, custom toolbar/button colors, Korean title/command localization, accessible close actions, title-drag region separation, long error scrolling, explicit error review, recovery cleanup and cancellation through the caption. Existing suites cover immutable approval data, input validation, exact selection identities, graphical previews and minimum window sizes. Renders include the shared caption and are stored in `artifacts/ui-redesign`.

Rendering uses the existing WPF/Direct3D path with dynamic shared brushes. There is no additional web renderer, blur layer or per-frame UI timer. Lists retain virtualization and geometry budgets are unchanged. The offscreen captures use software rendering for repeatability; they do not measure GPU throughput or prove manual mixed-DPI window dragging. No CAD writes were needed for this change.

Complete framework-dependent bundle: `artifacts/TopSolid-AI-0.5.15`. Keep the full directory, including `McpServer`. Test logs: `artifacts/dialog-theme-0.5.15`. Detailed results are in [VALIDATION.md](../VALIDATION.md).
