# Automation-only toolpath display

The selected operation now falls back to TopSolid's real rendered toolpath when the Automation table cannot supply coordinates. The current impeller operation was verified live: red cutting paths and native approach, retract and linking colors are visible. No ADS API, registration certificate, native TopSolid add-in, or host binary modification is required.

## Cause and implementation

On installed TopSolid 7.20.400.107, operation 1221 wraps machining operation 1219 and is up to date. The Automation `IToolPath` scan returns empty strings for `GOTO_XYZ_3D` and arc-center point fields. The absence of coordinates does not mean the calculated toolpath is absent. GLB, VRML and X3D exports tested here did not export those paths as line geometry.

The server first keeps the existing coordinate reader for compatible hosts. For this host, it finds the selected operation's generated path entities through Automation `Elements.GetConstituents` and `Operations.GetChildren`, then uses `Visualization3D.SaveScreenShotBitmap`. It found three path entities for this operation. The native renderer supplies the actual five-axis result and movement colors. The coordinate renderer's feed color is also changed to red.

The private preview protocol adds `native-view-png`. Studio labels it **Toolpath · native rendered view**. Orbit, camera selection, pan and zoom request an updated native image after a 350 ms debounce. A separate local distance scale is hidden because native viewport dimensions can differ from the Studio canvas. This is a rendered-image fallback, not recovered XYZ segments or independently rotatable local toolpath geometry.

## Restoration and bounds

- Temporary visibility changes occur inside an Automation modification that is always cancelled with `EndModification(false, false)`.
- The original camera is restored and redrawn. The server verifies document identity, dirty state, active document, visibility and camera values before returning `stateRestored: true`.
- There is no CAM calculation, NC generation, save or check-in. An active TopSolid command returns busy.
- Requests contain an explicit document and operation identity. Operation switches and camera changes reject stale replies.
- Once a capture is dispatched, cancellation abandons its display but allows rollback to complete. Disconnect waits for cleanup. A broken transport never forcibly terminates a server inside the temporary modification.
- Camera values, entity traversal and image dimensions/bytes are bounded. PNG files use an application-created temporary directory and are removed after capture; callers cannot supply an output path.

## Compatibility and trade-offs

Native capture requires a local TopSolid connection and the screenshot API introduced in 7.20.326. Remote/gateway sessions and older hosts retain the coordinate-reader result. Native camera and visibility can briefly change in TopSolid while capturing, then are restored. Rendering inherits the user's native color settings; no global TopSolid colors are changed. The current job's cutting color was already red, as requested.

This avoids inaccurate reconstruction of missing five-axis coordinates. The cost is a short native capture after navigation, instead of continuous local rendering of a toolpath. Validation covered the already-dirty current CAM job; clean-document/PDM transition cases and remote capture have not been certified.

## Validation

- Release build succeeded with zero warnings/errors in an isolated output directory.
- Server checks: 15,547 passed.
- Studio regression groups: 51/51 passed, including cancellation and disconnect during a native capture.
- Targeted WPF toolpath tests passed: operation identity, coordinate/image modes, camera debounce, delayed old replies, preserved part geometry and unchanged saved settings.
- Live WPF test used the actual operation, displayed its native capture, switched to the top camera and zoomed, confirmed the image changed, and verified restoration after both captures.
- Direct server capture initially took 0.32 seconds on this machine; this is one sample, not a performance guarantee.
- The broader `--ui-shell` run reached a separate settings/composer model-selector assertion failure. A standalone broad preview fixture also lacked the owner state expected by its approval test. These are not reported as passing; the operation-specific fixture is independently runnable.

Reproduce the targeted tests with `TopSolid.Automation.Tests.exe --toolpath-preview-ui`. For a live document use `--live-native-toolpath <server.exe> <documentId> <operationId>`. `scripts/Test-AutomationToolpathPreview.ps1` also verifies the RPC result using an explicit document ID; its default camera is the measured impeller fixture camera.

Outputs are in `artifacts/toolpath-repair`: `automation-operation-path.png`, `studio-native-toolpath.png`, `studio-native-toolpath-top.png`, and their JSON validation reports. The updated runnable Studio bundle is `artifacts/TopSolid-AI-toolpath-20260920`, including its matching `McpServer` folder. Launch that bundle after closing the older Studio when convenient; existing chats are not interrupted by this repair.
