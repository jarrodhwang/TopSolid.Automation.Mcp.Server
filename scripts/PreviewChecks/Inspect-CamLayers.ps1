param([string]$OutputDirectory="$PSScriptRoot/../../artifacts/cam-layer-repair")
$ErrorActionPreference='Stop'
$sdk='C:\Program Files\TOPSOLID\TopSolid 7.20\bin'
foreach($name in @('TopSolid.Kernel.SX','TopSolid.Kernel.Automating','TopSolid.Cad.Design.Automating','TopSolid.Cam.NC.Kernel.Automating')){
    [void][Reflection.Assembly]::LoadFrom((Join-Path $sdk ($name+'.dll')))
}
$output=[IO.Path]::GetFullPath($OutputDirectory);[void][IO.Directory]::CreateDirectory($output)
try {
    [void][TopSolid.Kernel.Automating.TopSolidHost]::Connect($false)
    [void][TopSolid.Cam.NC.Kernel.Automating.TopSolidCamHost]::Connect()
    $doc=[TopSolid.Kernel.Automating.TopSolidHost]::Documents.EditedDocument
    $dirty=[TopSolid.Kernel.Automating.TopSolidHost]::Documents.IsDirty($doc)
    $parts=[TopSolid.Cam.NC.Kernel.Automating.TopSolidCamHost]::Documents.GetParts($doc)
    $queue=[Collections.Generic.Queue[TopSolid.Kernel.Automating.ElementId]]::new()
    foreach($id in $parts){$queue.Enqueue($id)}
    $seen=[Collections.Generic.HashSet[int]]::new();$rows=@()
    while($queue.Count -gt 0 -and $seen.Count -lt 400){
        $id=$queue.Dequeue();if(-not $seen.Add($id.Id)){continue}
        $children=@([TopSolid.Kernel.Automating.TopSolidHost]::Elements.GetConstituents($id))
        if([TopSolid.Kernel.Automating.TopSolidHost]::Operations.IsOperation($id)){$children+=@([TopSolid.Kernel.Automating.TopSolidHost]::Operations.GetChildren($id))}
        $rows+=@{id=$id.Id;type=[TopSolid.Kernel.Automating.TopSolidHost]::Elements.GetTypeFullName($id);name=[TopSolid.Kernel.Automating.TopSolidHost]::Elements.GetName($id);visible=[TopSolid.Kernel.Automating.TopSolidHost]::Elements.IsVisible($id);children=@($children|ForEach-Object{$_.Id})}
        foreach($child in $children){$queue.Enqueue($child)}
    }
    $elements=@([TopSolid.Kernel.Automating.TopSolidHost]::Elements.GetElements($doc)|ForEach-Object{
        $type=[TopSolid.Kernel.Automating.TopSolidHost]::Elements.GetTypeFullName($_)
        if($type -match 'Stock|Faceted|Machined|PartInclusion'){@{id=$_.Id;type=$type;name=[TopSolid.Kernel.Automating.TopSolidHost]::Elements.GetName($_);visible=[TopSolid.Kernel.Automating.TopSolidHost]::Elements.IsVisible($_)}}
    })
    $report=@{documentId=$doc.PdmDocumentId;parts=@($parts|ForEach-Object{$_.Id});tree=$rows;otherLayers=$elements;dirtyUnchanged=($dirty -eq [TopSolid.Kernel.Automating.TopSolidHost]::Documents.IsDirty($doc))}
    $report|ConvertTo-Json -Depth 8|Set-Content (Join-Path $output 'native-layer-inventory.json') -Encoding UTF8
    $report|ConvertTo-Json -Depth 8
}finally{
    [TopSolid.Cam.NC.Kernel.Automating.TopSolidCamHost]::Disconnect()
    [TopSolid.Kernel.Automating.TopSolidHost]::Disconnect()
}
