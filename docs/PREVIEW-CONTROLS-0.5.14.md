# Preview controls, appearance and precision — 0.5.14

Approval and question previews now use the requested TopSolid mouse gestures. The gesture is selected when the button is pressed and remains stable until that button is released. Another button's release does not interrupt it; lost capture, cleared selection and disposal stop navigation.

| Action | Input |
| --- | --- |
| Rotate | Ctrl + right-button drag, or wheel-button drag |
| Move the view | Right-button drag |
| Zoom | Roll the wheel |
| Fit | Fit button, F or left double-click |
| Standard views | Isometric, top, front, right; Home resets to isometric |

Left-button dragging does not rotate or pan. Arrow keys rotate and +/− zoom. English/Korean hints and accessibility help describe the same gestures. The old orbit/pan mode buttons were removed so they cannot override the mouse contract.

Proposed shapes default to neutral silver-gray (#C0C0C0). An explicitly requested RGB color remains visible. Black, depth-tested ribbons replace thick world-space edge tubes. Feature edges and curved silhouettes remain one device-independent pixel wide as the camera zooms; surfaces still occlude hidden edges. Native WPF multisample antialiasing is retained. Edges are derived from mesh adjacency (20° creases, boundaries and view-dependent silhouettes), not exact B-rep topology.

## Precision and document safety

- The preview target is **0.05 mm chord tolerance and 5° angular tolerance**. Local cylinders choose a subdivision count that meets both limits, including large radii; they do not silently relax the tolerance when a model exceeds the budget.
- Installed TopSolid 7.20.400.107 glTF options do not expose tessellation tolerance. The STL exporter exposes `LINEAR_TOLERANCE`, `ANGULAR_TOLERANCE`, `WRITE_MODE`, `USER_UNIT_SET`, `USER_UNIT` and `AGGREGATES_SHAPES`. The preview therefore prefers binary STL with 0.00005 m, π/36 radians, millimetres and the document's default reference frame. Live inspection confirmed `WRITE_MODE=0` is binary; `1` is ASCII.
- Only the per-call export option list is modified. No document/operation modeling or visualization tolerance, exporter default, active document, CAD shape or approval payload is changed. Export remains outside a modification transaction.
- STL carries geometry without native face materials, so this precise document snapshot is neutral gray. Proposed explicit colors are preserved. Hosts without the required STL options can use the existing GLB path, preserving its colors and showing that it uses document display tolerance. That fallback does not claim 0.05 mm / 5° precision.
- Native STL is bounded to **250,000 triangles / 750,000 vertices / 12,500,084 bytes**. This accommodates the supplied style of curved cutting tools: the loaded face cutter required 225,484 triangles at the requested precision. GLB retains its 2 MiB, 300,000 expanded vertex, 512 primitive and 2,048 node limits. At most 10,000 edge ribbons are rendered. Larger models leave review controls usable and direct the user to TopSolid.
- The private RPC's bounded line limit includes base64 expansion and a small envelope allowance. Geometry remains outside AI messages and chat history. Parsers reject truncated data, invalid counts/coordinates, unexpected units/axes and external glTF content.

## GPU and performance

The production app explicitly selects WPF `RenderMode.Default`, allowing its **Direct3D hardware renderer** on the Windows-selected NVIDIA, AMD/Radeon or Intel adapter. It does not force software rendering, install a vendor compute dependency, change system GPU preferences or claim a particular GPU is active. Windows/driver policy can select the adapter or require a software fallback. Capability tier and rendering preference are written to diagnostics; tier 2 alone is not proof of an active NVIDIA/Radeon device or a frame-rate measurement.

One viewport renders frozen surface batches. STL decoding, smoothing, adjacency and surface construction run off the UI thread. Camera events coalesce edge updates at render priority; only the bounded edge batch is rebuilt, with no CAD request, surface rebuild, hit testing or idle animation loop. Panning reuses the edge batch. This follows [Microsoft's WPF 3D guidance](https://learn.microsoft.com/en-us/dotnet/desktop/wpf/graphics-multimedia/maximize-wpf-3d-performance) and [render-mode precedence](https://learn.microsoft.com/en-us/dotnet/api/system.windows.media.renderoptions.processrendermode?view=windowsdesktop-10.0).

Measured read-only run on the loaded **FACE CUTTER_단순화_1**: 11,274,284 bytes, 225,484 triangles, 63 × 63 × 108 mm; export/transport 1.26 s, decode/build 0.73 s. Across 24 camera directions, CPU edge preparation median was 11.65 ms and p95 23.58 ms. Probe process peak working set was 562 MiB including transport, parsing, geometry and repeated edge allocations. These are local observations, not GPU frame-rate guarantees. The finite geometry budget deliberately limits memory and interaction cost; precision is never traded away to fit that budget.

## Verification and test prompts

Release/Debug builds, application/server/protocol checks and WPF light/dark/Korean review fixtures are recorded in [VALIDATION.md](../VALIDATION.md). Controls tests cover both rotation gestures, right-button pan, ignored left dragging, extra button release, capture cancellation, zoom and fit. Geometry tests cover tolerance at several radii, explicit/default colors, stable edge width, binary STL dimensions, malformed data and a maximum-size base64 response through the real stdio transport fixture.

Native read-only probes confirmed the loaded cube and curved tool retain document identity, dirty state and shape inventory. The cube retained its existing 0.2 mm / 15° document visualization setting while its export used 0.05 mm / 5°. The same unchanged settings were confirmed for the curved tool. No CAD writes were executed. WPF images are offscreen software renders; no actual NVIDIA/Radeon adapter-use or GPU-throughput benchmark is claimed.

Evidence: `artifacts/graphic-preview-0.5.14`, `artifacts/ui-redesign/graphic-*.png`. Complete bundle: `artifacts/TopSolid-AI-0.5.14`.

Try these chat requests, then inspect the preview before approving:

1. `현재 부품에 지름 80 mm, 높이 160 mm 원기둥을 만들기 전에 미리 보여줘.` — neutral cylinder, round silhouette, thin black outlines, both rotation gestures and right-drag pan.
2. `현재 부품에 파란색 40 × 30 × 20 mm 직육면체를 만들기 전에 미리 보여줘.` — explicitly requested color and sharp edges.
3. `열려 있는 부품 중에서 선택할 수 있게 보여줘.` — selection preview, native 0.05 mm / 5° export, switching/clearing selection and loading feedback.

Scroll in and out, check that outlines stay thin, press F to fit, and cancel to finish without creating geometry.
