param([Parameter(Mandatory=$true)][string]$Receipt,[ValidateSet('undo','restore')][string]$Action)
$ErrorActionPreference='Stop'
$state=Get-Content -LiteralPath $Receipt -Raw|ConvertFrom-Json
$sdk='C:\Program Files\TOPSOLID\TopSolid 7.20\bin'
[void][Reflection.Assembly]::LoadFrom((Join-Path $sdk 'TopSolid.Kernel.Automating.dll'))
try {
    [void][TopSolid.Kernel.Automating.TopSolidHost]::Connect($false)
    $doc=[TopSolid.Kernel.Automating.DocumentId]::new($state.documentId)
    if(-not $state.fixtureName.StartsWith('Studio CAM Colors disposable ') -or
       [TopSolid.Kernel.Automating.TopSolidHost]::Documents.GetName($doc) -ne $state.fixtureName) { throw 'Fixture identity mismatch.' }
    if($Action -eq 'undo') {
        if([TopSolid.Kernel.Automating.TopSolidHost]::Documents.EditedDocument.PdmDocumentId -ne $state.documentId) { throw 'Fixture is no longer the edited document. Undo refused.' }
        # Exact command name verified from the installed native menu resources.
        if(-not [TopSolid.Kernel.Automating.TopSolidHost]::Application.InvokeCommand('TopSolid.Kernel.WX.Commands.UndoCommand')) { throw 'Native Undo was not accepted.' }
        'Native Undo invoked on exact disposable fixture.'
    } elseif($state.originalDocumentId) {
        [TopSolid.Kernel.Automating.TopSolidHost]::Documents.EditedDocument=[TopSolid.Kernel.Automating.DocumentId]::new($state.originalDocumentId)
        'Original active document restored; disposable fixture retained for inspection.'
    }
} finally { [TopSolid.Kernel.Automating.TopSolidHost]::Disconnect() }
