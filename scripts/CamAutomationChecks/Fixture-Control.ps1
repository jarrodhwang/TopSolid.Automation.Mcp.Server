param([Parameter(Mandatory)][string]$Receipt,[Parameter(Mandatory)][ValidateSet('copy','undo','machining','restore')][string]$Action)
$ErrorActionPreference='Stop'
$state=Get-Content -LiteralPath $Receipt -Raw | ConvertFrom-Json
$sdk='C:\Program Files\TOPSOLID\TopSolid 7.20\bin'
[void][Reflection.Assembly]::LoadFrom((Join-Path $sdk 'TopSolid.Kernel.Automating.dll'))
try {
    [void][TopSolid.Kernel.Automating.TopSolidHost]::Connect($false)
    if (-not $state.fixtureName.StartsWith('Studio CAM Automation disposable ')) { throw 'Invalid fixture identity.' }
    if ($Action -eq 'copy') {
        $owner=[TopSolid.Kernel.Automating.PdmObjectId]::new($state.projectId)
        if ([TopSolid.Kernel.Automating.TopSolidHost]::Pdm.GetName($owner) -ne $state.fixtureName) { throw 'Fixture owner mismatch.' }
        $sources=[Collections.Generic.List[TopSolid.Kernel.Automating.PdmObjectId]]::new()
        $sources.Add([TopSolid.Kernel.Automating.PdmObjectId]::new($state.sourcePdmId))
        # A distinct copy; do not save the source document or copy its part number.
        $copies=[TopSolid.Kernel.Automating.TopSolidHost]::Pdm.CopySeveralWithOptions($sources,$owner,$false,$true)
        if ($copies.Count -ne 1 -or $copies[0].Id -eq $state.sourcePdmId) { throw 'Expected one distinct fixture copy.' }
        [TopSolid.Kernel.Automating.TopSolidHost]::Pdm.SetName($copies[0],$state.fixtureName)
        $doc=[TopSolid.Kernel.Automating.TopSolidHost]::Documents.GetDocument($copies[0])
        @{pdmObjectId=$copies[0].Id;documentId=$doc.PdmDocumentId} | ConvertTo-Json -Compress
    } elseif ($Action -in @('undo','machining')) {
        $doc=[TopSolid.Kernel.Automating.DocumentId]::new($state.documentId)
        if ([TopSolid.Kernel.Automating.TopSolidHost]::Documents.GetName($doc) -ne $state.fixtureName -or
            [TopSolid.Kernel.Automating.TopSolidHost]::Documents.EditedDocument.PdmDocumentId -ne $state.documentId) { throw 'Undo target is not the exact disposable fixture.' }
        if ($Action -eq 'undo') {
            if (-not [TopSolid.Kernel.Automating.TopSolidHost]::Application.InvokeCommand('TopSolid.Kernel.WX.Commands.UndoCommand')) { throw 'Undo rejected.' }
            'Native Undo invoked on disposable fixture.'
        } else {
            $stages=@([TopSolid.Kernel.Automating.TopSolidHost]::Operations.GetStages($doc) | Where-Object { [TopSolid.Kernel.Automating.TopSolidHost]::Elements.GetTypeFullName($_) -eq 'TopSolid.Cam.NC.Kernel.DB.Stages.MachiningStageOperation' })
            if ($stages.Count -ne 1) { throw 'Expected one machining stage in the fixture.' }
            if (-not [TopSolid.Kernel.Automating.TopSolidHost]::Application.StartModification('Disposable CAM fixture stage setup',$false)) { throw 'Native modification is busy.' }
            try {
                [TopSolid.Kernel.Automating.TopSolidHost]::Documents.EnsureIsDirty([ref]$doc)
                $stage=[TopSolid.Kernel.Automating.ElementId]::new($doc,$stages[0].Id)
                [TopSolid.Kernel.Automating.TopSolidHost]::Operations.SetWorkingStage($stage)
                [TopSolid.Kernel.Automating.TopSolidHost]::Operations.ResetInsertionOperation($doc)
                [TopSolid.Kernel.Automating.TopSolidHost]::Application.EndModification($true,$true)
                'Disposable fixture machining stage selected.'
            } catch {
                [TopSolid.Kernel.Automating.TopSolidHost]::Application.EndModification($false,$false)
                throw
            }
        }
    } elseif ($state.originalDocumentId) {
        [TopSolid.Kernel.Automating.TopSolidHost]::Documents.EditedDocument=[TopSolid.Kernel.Automating.DocumentId]::new($state.originalDocumentId)
        'Original active document restored.'
    }
} finally { [TopSolid.Kernel.Automating.TopSolidHost]::Disconnect() }
