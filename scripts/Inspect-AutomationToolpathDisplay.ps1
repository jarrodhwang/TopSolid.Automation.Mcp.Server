param([int]$OperationId = 1221, [string]$OutputDirectory = "$PSScriptRoot\..\artifacts\toolpath-repair")
$ErrorActionPreference = 'Stop'
$sdk = 'C:\Program Files\TOPSOLID\TopSolid 7.20\bin'
foreach ($name in @('TopSolid.Kernel.SX','TopSolid.Kernel.Automating','TopSolid.Cam.NC.Kernel.Automating')) {
    [void][Reflection.Assembly]::LoadFrom((Join-Path $sdk ($name + '.dll')))
}
[void][IO.Directory]::CreateDirectory([IO.Path]::GetFullPath($OutputDirectory))
try {
    [void][TopSolid.Kernel.Automating.TopSolidHost]::Connect($false)
    [void][TopSolid.Cam.NC.Kernel.Automating.TopSolidCamHost]::Connect()
    $doc = [TopSolid.Kernel.Automating.TopSolidHost]::Documents.EditedDocument
    $op = [TopSolid.Kernel.Automating.ElementId]::new($doc,$OperationId)
    $report = [ordered]@{document=$doc.PdmDocumentId; operation=$OperationId; dirty=[TopSolid.Kernel.Automating.TopSolidHost]::Documents.IsDirty($doc); elements=@()}
    $queue = [Collections.Generic.Queue[TopSolid.Kernel.Automating.ElementId]]::new()
    $seen = [Collections.Generic.HashSet[int]]::new()
    $queue.Enqueue($op)
    while($queue.Count -gt 0 -and $seen.Count -lt 200) {
        $element = $queue.Dequeue()
        if(-not $seen.Add($element.Id)){continue}
        $type = [TopSolid.Kernel.Automating.TopSolidHost]::Elements.GetTypeFullName($element)
        $report.elements += [ordered]@{id=$element.Id;type=$type;name=[TopSolid.Kernel.Automating.TopSolidHost]::Elements.GetName($element);visible=[TopSolid.Kernel.Automating.TopSolidHost]::Elements.IsVisible($element)}
        foreach($child in [TopSolid.Kernel.Automating.TopSolidHost]::Elements.GetConstituents($element)){$queue.Enqueue($child)}
        if ([TopSolid.Kernel.Automating.TopSolidHost]::Operations.IsOperation($element)) { foreach($child in [TopSolid.Kernel.Automating.TopSolidHost]::Operations.GetChildren($element)){$queue.Enqueue($child)} }
    }
    $view = [TopSolid.Kernel.Automating.TopSolidHost]::Visualization3D.GetActiveView($doc)
    $image = [IO.Path]::GetFullPath((Join-Path $OutputDirectory 'automation-current-view.png'))
    [TopSolid.Kernel.Automating.TopSolidHost]::Visualization3D.SaveScreenShotBitmap($doc,$view,$image)
    $report.screenshot = $image
    $report | ConvertTo-Json -Depth 8 | Set-Content (Join-Path $OutputDirectory 'automation-display.json') -Encoding UTF8
    $report | ConvertTo-Json -Depth 8
} finally {
    [TopSolid.Cam.NC.Kernel.Automating.TopSolidCamHost]::Disconnect()
    [TopSolid.Kernel.Automating.TopSolidHost]::Disconnect()
}
