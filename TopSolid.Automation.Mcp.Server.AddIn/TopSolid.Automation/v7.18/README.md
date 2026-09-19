# TopSolid 7.18 Automation profile

These DLLs were copied from `C:\Program Files\TOPSOLID\TopSolid 7.18\bin` and are included as a versioned deployment/reference profile:

- `TopSolid.Kernel.Automating.dll`
- `TopSolid.Kernel.SX.dll`
- `TopSolid.Cad.Design.Automating.dll`
- `TopSolid.Cad.Drafting.Automating.dll`
- `TopSolid.Cam.NC.Kernel.Automating.dll`
- `TopSolid.Pdm.Explorer.Automating.dll`

The 7.18 installation did not contain `TopSolid.Cad.Electrode.Automating.dll` or `TopSolid.Cae.Kernel.Automating.dll`. Tools using those module APIs therefore advertise a 7.20 minimum and are rejected before execution on 7.18. The project remains compiled against the matched 7.20 SDK so the complete tool catalog can be built; this profile is copied to the published output under `TopSolid.Automation/v7.18`.
