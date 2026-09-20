# CAM preview layers — validation on 2026-09-20

The default CAM preview shows the target part and the current machined/faceted stock at 30% opacity in yellow. The right toolbar switches between remaining stock, original stock, and part-only; the two stock layers are mutually exclusive. Stock changes retain the camera and selected operation. The machine button adds the machine and environment already included in the CAM document and fits their bounds.

## Geometry and state

- Organized CAM exports use native Z-up metres. Ordinary GLB exports use Y-up metres. Both now reach the viewer in Z-up millimetres.
- Only the exporter-owned CAM categories are rendered, avoiding the repeated meshes in representation and inclusion trees. The design representation identifies target meshes; unidentified role assignments do not recolor all geometry as stock.
- Original stock is read from the included part-setting definition. Automation source entity IDs identify the stock shapes, and the native part-setting occurrence transform places them in CAM space. The exporter’s entity-ID suffix is used only to match a verified Automation identity, never a guessed stock name.
- No machine document is opened. Export and stock discovery do not modify visibility, camera, selection, geometry, calculation, or saved state. Original-stock extraction failure retains the normal CAM preview and disables the unavailable stock button.
- Stock edges are omitted to avoid obscuring target edges. Facets/scallops already present in TopSolid’s machined part remain unchanged. No depth displacement or replacement geometry is applied.
- Decoded immutable geometry is shared between layer combinations. Toggle and operation-switch tests verify no additional model export. Export/decode sizes and hierarchy depths remain bounded; the existing isolated Draco decoder has a deadline.

The implementation uses TopSolid Automation only. Organized export and stock-ID matching depend on the installed exporter’s format; unsupported arrangements must report unavailable stock rather than manufacture a blank from a bounding box.

## Live evidence

Test document: `Usinage rouet avec interpales`, revision `d6_0_50`, TopSolid 7.20.400.107.

| View | Triangles |
| --- | ---: |
| Target part | 43,157 |
| Target + current stock | 210,647 |
| Target + original stock | 46,229 |
| Machine + environment + target + current stock | 742,053 |

The target and original-stock envelope align at approximately Z=50.0477–115.0924 mm. The final combined transfer is 6,198,776 bytes. One measured decode took approximately 1.98 seconds; this is a sample, not a performance guarantee.

Five geometry combinations, including isolated current stock, rendered on the NVIDIA RTX PRO 3000 through Direct3D 11. WPF UI tests also verified stock toggles, machine toggles, retained operation overlay, camera/zoom retention, and operation switching without another export. The isolated-stock/native-view comparison confirmed that the visible scallop pattern belongs to the actual machined stock.

The Debug Studio path reported in the supplied log was rebuilt with its matching bundled MCP server. Live tests against that server verified unchanged edited document, dirty state, loaded document identities, active view, and native camera. Debug/Release test builds completed with zero warnings/errors; the Studio suite passed 51/51 groups and the server suite passed 15,809 checks.

Evidence: `artifacts/cam-context/live-context-rpc.json`, `artifacts/cam-context/live-layers.glb`, `artifacts/cam-layer-repair/gpu-layers.json`, `artifacts/cam-layer-repair/live-native-state.json`, and the `gpu-*.png` QA captures. QA images are test evidence, not toolpath data.

## Remaining toolpath limitation

The final live check of operation 1221 returned `upToDate=true`, scanned 263 IToolPath rows, and encountered 256 empty point values with zero drawable segments. The model/machine preview works, but this installed Automation response still cannot supply the current job’s actual 3D toolpath. The viewer explicitly reports missing coordinates. No ADS or screenshot fallback is used.

Reproduction commands (run from the repository root with the built tests):

```powershell
TopSolid.Automation.Tests.exe --live-cam-context <matching-server.exe>
TopSolid.Automation.Tests.exe --cam-context artifacts/cam-context/live-layers.glb
TopSolid.Automation.Tests.exe --cam-context-ui artifacts/cam-context/live-layers.glb
TopSolid.Automation.Tests.exe --cam-layers-gpu artifacts/cam-context/live-layers.glb
TopSolid.Automation.Tests.exe --live-toolpath-geometry <matching-server.exe> <documentId> 1221
```

The live fixture requires the already-open example CAM document. It never calculates or saves it. Native-color and layer assertions are separate from the synthetic-coordinate tests; successful geometry/UI checks do not imply successful retrieval of the live toolpath.
