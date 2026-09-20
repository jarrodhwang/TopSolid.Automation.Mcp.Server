# Toolpath geometry through IToolPath

The preview uses `TopSolidCamHost.ToolPath` (`IToolPath`) directly: `StartToolPath(operation)`, repeated `NextToolPathItem(operation)`, and `EndToolPath(operation)` in `finally`. No ADS certificate or in-process add-in is required for these Automation calls.

The screenshot fallback has been removed. The private preview protocol advertises only `segments-f32` (version 3). The client rejects image payloads, and local orbit, pan, zoom, camera selection and machine visibility do not request new toolpath data. The model and machine use exported geometry in the local 3D renderer.

## Coordinate handling

- Explicit `Point3D` values or finite numeric X/Y/Z columns become line segments, with API metres converted to the preview's millimetres.
- Incomplete coordinate rows break continuity. Unsupported arcs and unresolved named coordinate frames are omitted and reported as partial; they are never joined with invented straight cuts.
- Reads are bounded to 200,000 rows, 100,000 segments and 12 seconds checked between API calls. The native scan is released on completion, early exit or failure. A blocking individual host call cannot be interrupted by that deadline.
- After 256 missing points with no segments, the scan returns `coordinatesUnavailable`. No calculation, simulation, NC generation, document save, native camera or visibility change occurs.
- Operation switches reject stale replies and retain the loaded model and camera. The server reports actual columns and rows scanned for diagnostics.

## Installed-host evidence

Direct calls to the installed `TopSolid.Cam.NC.Kernel.Automating.dll` version **7.20.400.107** were tested against all eight operations in the open CAM document on 2026-09-20. These calls bypass MCP serialization entirely.

All eight scans were available and their operations reported up to date. The probe read 23 rows from operation 1839 and the first 96 rows from each of the seven machining operations. All 480 sampled `GOTO_XYZ_3D` entries were empty strings; no separate numeric X/Y/Z rows were returned. Document dirty state was unchanged.

This proves that these sampled rows do not expose drawable coordinates through the installed interface. It does not mean the native calculated paths are absent or that every TopSolid version behaves identically. Inspection of the installed host converter also found that it converts scalar numbers and strings but returns an empty string for other CL data types, including point data. Machine-axis scalar values are not treated as document-space tool-tip coordinates.

Consequently, the current job's actual toolpath cannot yet be shown through these returned coordinates. The preview displays the interactive model with an explicit missing-coordinate status. A compatible API response is needed to validate actual toolpath drawing on this job; the renderer's coordinate tests use clearly identified fixtures.

The official interface contract is [IToolPath](https://help.topsolid.com/7.20/en/TopSolid%27Automation/api/cam/TopSolid.Cam.NC.Kernel.Automating.IToolPath.html).

## Reproduction and validation

Run `scripts/Inspect-IToolPathContract.ps1` with Windows PowerShell and an explicit loaded document ID. Evidence is saved in `artifacts/toolpath-repair/itoolpath-live-current.json`.

`scripts/Test-AutomationToolpathPreview.ps1` validates the geometry-only RPC and records whether actual coordinates were retrieved. `TopSolid.Automation.Tests.exe --toolpath-preview-ui` tests geometry rendering, image rejection, operation switching, late replies and navigation without backend calls. `--live-toolpath-geometry <server.exe> <documentId> <operationId>` tests the live model/machine scene and records the actual API outcome separately from UI success.

Debug and Release builds succeeded. The final Release regression run passed 15,809 server checks and 51/51 Studio groups. Targeted WPF geometry checks passed. The build reported six CS8620 nullability warnings in CAM scene construction (three repeated for the WPF temporary project); these are not reported as a warning-free build.

The live WPF check loaded the model and machine, changed the camera and machine visibility without additional requests, and verified unchanged document information. It correctly reported missing coordinates. Its report is `artifacts/toolpath-repair/studio-itoolpath-geometry.json`; the PNGs beside it show the local geometry renderer, not images used as toolpath data.

The runnable bundle is `artifacts/TopSolid-AI-IToolPath-20260920/TopSolid.Automation.AI.Studio.exe`, including its matching `McpServer`. A final request to that packaged server scanned 263 rows from operation 1221, reported 256 missing points and zero segments, and returned in approximately 1.29 seconds. This timing is a single sample. See `itoolpath-operation-geometry.json`. Existing Studio and TopSolid sessions were left running.

These results verify the geometry-only implementation and missing-data behavior. They do not claim successful retrieval or drawing of this job's actual toolpath.
