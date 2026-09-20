# Machine element visibility

In the CAM operation preview, click the arrow beside **Show machine** to open **Machine elements**. Expand the native machine groups and check the components to display. A parent checkbox controls its whole subtree; a partial check indicates mixed visibility. Apply updates the preview and Cancel discards the dialog's draft.

The tree is derived from the organized CAM export's `CamMachine` hierarchy. Mesh aliases are matched by their exact exported mesh/name pair to the actual machine display instances; ambiguous aliases fall back to individual rendered components. Names are never guessed from model output. Fixtures, workpiece, stock, and toolpath remain separate from machine visibility. The choices belong to the loaded preview snapshot, survive the master machine toggle and operation switches using that snapshot, and reset when the document geometry is reloaded.

Decoded component geometry is immutable and reused. Checkbox changes require no further export, native command, document modification, or save. Apply preserves the camera when the machine is already displayed.

Run `dotnet run --project scripts/OperationDetailChecks/OperationDetailChecks.csproj -c Release -- --machine artifacts/cam-context/full-context.glb` to validate against the captured native DMU65 export. The fixture checks the nine native groups, partial parent state, draft isolation, geometry counts, actual dropdown/Apply routing, master-toggle retention, and one-export behavior. Light/dark dialog captures are written to `artifacts/operation-detail-checks`. This fixture uses saved native data; it is not a live document modification test.
