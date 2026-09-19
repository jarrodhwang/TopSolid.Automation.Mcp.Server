# Workstation preview: implementation and verification

Updated 2026-09-18. Preview remains read only: it does not replace TopSolid's modeling/CAM kernel, change document tolerances, calculate machining operations, or authorize an edit.

## Implemented

- Direct3D 11 through open-source HelixToolkit: GPU buffers, frustum culling, parallel preparation, MSAA and existing TopSolid camera gestures.
- Disk-backed binary STL snapshots, 64-bit offsets and 32,768-triangle tiles. Native `fileBacked` exports preserve the requested 0.05 mm / 5 degree tessellation without rejecting a large source or repeatedly coarsening it to the old small-preview limit.
- Private MCP transfer uses 512 KiB chunks, validating transfer identity, offset and decoded length before writing to a unique temporary file. Geometry never goes to the AI. Document identity is checked before display.
- Indexing retains tile bounds, not all vertices. Visible tiles load asynchronously. Visible tiles outside the residency budget retain explicit bounds boxes; the footer distinguishes detailed tiles from proxies. Camera changes update residency.
- Worker count scales with logical processors. RAM and actual DXGI adapter memory determine residency budgets. `System.Numerics` uses available SIMD implementations on Intel/AMD without forcing AVX2/AVX512 globally.
- The local-file button accepts binary STL, and STEP/IGES/OCCT BREP when the bundled OCCT 7.9.3 worker is present. The worker imports and meshes separately from Studio; cancellation terminates it. Windows Unicode paths are supported.
- Invalid files, stale selections and canceled loads clean up owned snapshots. A failed replacement clears the previous geometry rather than showing it for the new selection.
- Navigation retains middle drag / Ctrl + right drag orbit, right drag pan, wheel zoom, F fit and Home isometric.

## Native color references

The user's original Desktop captures were located and copied unchanged:

| Reference | SHA-256 | Use |
|---|---|---|
| `reference/TopSolidColor1.png` | `9E711B95CABAD196489D73977BE3217BF99E26D7BFFEA96286BC702C7F57F737` | Feed, reduced feed, lead-in/out, stock, finish, link-in |
| `reference/TopSolidColor2.png` | `EAE261FAD3145C10F8781CF1393D7721DC44A32272C7F7ACA66C24B25F021020` | Link-out, path point, rapid, maximum feed |
| `reference/TopSolidColor4.png` | `982FC6778590989F1EEA94569D7C3172E57B78B17C685E656FC27C3C3B2D791B` | Sketch constraint colors |

`TopSolidPreviewPalette` records exact RGB values. Typed `motionRoles` color individual path segments in both Direct3D and WPF. Missing motion roles use neutral gray; no rapid/cutting classification is guessed. The sketch semantic palette is ready, but native sketch-state extraction is not implemented by this change. Existing TopSolid background/theme settings remain authoritative.

## Evidence

| Check | Result | Scope |
|---|---|---|
| Synthetic binary STL | 15,000,000,084 bytes; 300,000,000 triangles; 9,156 tiles | Entire file generated/indexed; first/last tile decoded; temporary file deleted |
| Index time | 1,625.38 ms | Warm file cache after generation, 23 workers; not a cold NVMe measurement |
| Peak working set | 103.84 MiB | Index/test process; excludes Windows file cache and GPU residency |
| Two tile decodes | 40.34 ms | Winding normals, local origins and indices; not frame rate |
| Native TopSolid snapshot | 138,881,384 bytes; 2,777,626 triangles; 85/85 resident tiles | Actual loaded CAM document exported/rendered with the published candidate server; document and dirty state unchanged in the separate read-only integration check |
| Native tiles and first frames | 996.79 ms; 698.98 MiB peak process working set | Includes test fixture waits; not sustained FPS or native CAM load time |
| Native display adapter | NVIDIA RTX PRO 3000 Blackwell Generation Laptop GPU, approximately 12 GB dedicated VRAM | Actual DXGI adapter, not WPF capability-tier inference |
| OCCT STEP -> Studio pane | 6 faces / 12 triangles; 40 x 30 x 20 mm; Korean filename | Real OCCT import and actual GPU draw; navigation and invalid replacement checked |

Machine: Intel Core Ultra 9 285HX, 24 logical processors, approximately 64 GB RAM.
Reports/captures: `artifacts/workstation-preview/evidence/`; native integration evidence: `artifacts/list-preview-20260918/`.
The Studio offline suite passed 44/44 checks and the server suite passed 15,321 checks. The separate `--workstation-preview` check covers OCCT, Unicode, dimensions, actual Studio GPU integration, camera controls, palette and failed replacement. The UI shell checks also passed; their generic WPF screenshots were skipped, while the dedicated GPU framebuffer captures were inspected.

The runnable candidate is `artifacts/TopSolid-AI-Workstation-Preview/TopSolid.Automation.AI.Studio.exe`. Keep the complete folder together: it includes the matching MCP server and OCCT runtime. This candidate was published successfully; the installed/Desktop application was not replaced.

These results do **not** establish 15 GB native CAM rendering, hundreds of millions of CAD surfaces, sustained interactive FPS at that scale, or CATIA/Parasolid performance parity. Triangles, CAD faces and complete CAM file bytes are different quantities. Native documents also include history, operations, toolpaths, stock and other data absent from STL.

## Current limits and trade-offs

- Large STL works out of core, but initial transfer/indexing finish before the first geometry display. There is no persistent tile cache/BVH, assembly instancing, geometric multi-resolution LOD or occlusion culling yet. Bounds proxies are not detailed surfaces.
- Large GLB still uses the existing in-memory importer with a 128 MiB limit. The native exporter prefers STL. Removing the remaining GLB limit needs a tiled importer with shared-instance geometry, not simply larger arrays.
- STL has no reliable standard color/topology metadata. Large native and OCCT snapshots use neutral gray; small GLB retains materials. Paged geometry uses winding normals; CAD edge extraction and normal continuity across tile boundaries remain future improvements.
- Binary STL has a 32-bit triangle-count field (4,294,967,295 maximum triangles). This format limit is separate from the removed small application source limit.
- Residency estimates reserve space for duplicate managed/GPU buffers; they are not upfront allocations. Driver/OS memory pressure can still reduce capacity. A failed GPU reports an error for streamed scenes instead of building an enormous WPF fallback.
- TopSolid export is synchronous inside its API. Canceling display drains that call then releases the transfer; it never kills TopSolid or interrupts an approved operation.
- OCCT STEP/IGES/BREP import holds BREP in the worker's RAM. Parallel meshing does not make the kernel/import stage out of core.
- Some TopSolid CAM reads currently return empty coordinate strings. The path reader reports this; it does not invent points, arc/frame semantics or machining classes.

## Practical quality review

| Quality | Result / trade-off |
|---|---|
| Functional suitability | Native display/local neutral preview work. Large GLB and native sketch-state extraction remain incomplete. |
| Reliability | Cancelable tiles/worker, generation checks, explicit proxies, invalid-file recovery and owned-file cleanup. Native export drains on cancel. |
| Performance efficiency | Parallel indexing, SIMD and GPU buffers; disk backing avoids whole-source copies. Warm-cache/index timings are separated from drawing. |
| Maintainability | Streaming/index/residency code is separate from WPF, MCP and the native worker. CLI checks compile the production streaming sources. |
| Compatibility | Windows x64/WPF/D3D11; no CUDA/vendor dependency. Only the listed NVIDIA adapter was measured. OCCT reads neutral files, not native TopSolid CAM files. |
| Security | No model-supplied paths or executable geometry. The picker launches a fixed bundled worker. Preview cannot grant approval authority. |
| Usability | Existing gestures and reference palette; local-file provenance, proxies and errors are explicit. |
| Portability | Managed streaming core; Windows x64 renderer/distribution. Separate native worker builds against a pinned upstream SDK. |

Quality in use: operators inspect geometry without modifying documents; paging reduces process memory pressure while retaining context. Proxies/errors prevent missing geometry from appearing complete. Customer-scale CAD/CAM workloads, different GPUs/drivers, remote desktop and memory-pressure recovery still need representative interactive qualification.

## Reproduce

```powershell
dotnet run --project scripts/PreviewChecks/PreviewChecks.csproj -c Release -- --benchmark 300000000
./scripts/Build-OcctPreview.ps1 -SdkDirectory artifacts/occt-sdk/7.9.3
dotnet build TopSolid.Automation.Tests/TopSolid.Automation.Tests.csproj -c Release
./TopSolid.Automation.Tests/bin/Release/net10.0-windows/TopSolid.Automation.Tests.exe --workstation-preview
```

The synthetic benchmark needs 15 GB temporary space and deletes its fixture. The OCCT build expects the official combined 7.9.3 SDK extracted under the supplied directory; normal/offline builds do not fetch dependencies. Studio copies the optional `artifacts/occt-preview/runtime` folder to `OcctPreview` in its output. The manifest records upstream source/version and binary hashes. Retain upstream license/source obligations when redistributing this runtime to customers.

## Primary references

- [OCCT visualization](https://occt3d.com/dev/doc/overview/html/occt_user_guides__visualization.html): presentation is separate from geometry/topology.
- [OCCT meshing](https://occt3d.com/dev/doc/overview/html/occt_user_guides__mesh.html): BREP tessellation and parameters.
- [OCCT 7.9.3](https://github.com/Open-Cascade-SAS/OCCT/releases/tag/V7_9_3): pinned Windows SDK release.
