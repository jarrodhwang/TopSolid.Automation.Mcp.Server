# Native CAM context preview

`TopSolid.DracoPreview.exe` is a small, isolated mesh decoder for the organized
TopSolid CAM GLB export. It does not load TopSolid, change documents, or contact
the network. The Studio launches one batch per export, kills it on cancellation
or after 45 seconds, and removes its exact temporary files.

Build from the repository root with the installed x64 Draco SDK:

```powershell
./scripts/Build-DracoPreview.ps1 -DracoRoot 'artifacts/occt-sdk/7.9.3/3rdparty-vc14-64/draco-1.4.1-vc14-64'
dotnet build TopSolid.Automation.AI.Studio -c Release
```

The script needs the Visual Studio C++ toolchain and CMake. Studio's build and
publish copy `artifacts/draco-preview/runtime` to `DracoPreview`, including
`LICENSE.draco`. Draco is statically linked; no Python or network installation is
needed on the user's workstation. A build without the worker retains ordinary
document previews.

The installed TopSolid 7.20.400.107 exposes `IDocuments.zExportToTopglTF`. Ordinary
GLB export omits the machine. The organized export includes representation and
tool duplicates, so the importer selects only the exporter-owned
`EntityFilterTypes` roots `Machine`, `Environment`, and `MachinedParts`. Native
transforms, surface colors and opacity are retained. Meshes are decoded once;
machine visibility changes reuse the cached work-only or work-plus-machine scene.

This export's organization is version-dependent. Missing categories, malformed
data, decoding limits or an unavailable worker fall back to ordinary geometry.
The initial scene excludes the machine. This is display geometry, not machine
motion or collision verification. The separate operation-preview path remains
responsible for toolpath display.

Validation commands (the last one reads the currently open CAM document):

```powershell
./TopSolid.Automation.Tests/bin/Release/net10.0-windows/TopSolid.Automation.Tests.exe
./TopSolid.Automation.Tests/bin/Release/net10.0-windows/TopSolid.Automation.Tests.exe --cam-context-ui artifacts/cam-context/full-context.glb
./TopSolid.Automation.Tests/bin/Release/net10.0-windows/TopSolid.Automation.Tests.exe --live-cam-context (Resolve-Path ./TopSolid.Automation.Mcp.Server.AddIn/bin/Release/net48/TopSolid.Automation.Mcp.Server.AddIn.exe).Path
```
