$ErrorActionPreference = 'Stop'
$folder = 'C:\Program Files\TOPSOLID\TopSolid 7.20\bin'
foreach ($name in @('TopSolid.Kernel.SX','TopSolid.Kernel.Automating','TopSolid.Cam.NC.Kernel.Automating')) {
    [void][Reflection.Assembly]::LoadFrom((Join-Path $folder ($name + '.dll')))
}
try {
    [void][TopSolid.Kernel.Automating.TopSolidHost]::Connect($false)
    [void][TopSolid.Cam.NC.Kernel.Automating.TopSolidCamHost]::Connect()
    $doc = [TopSolid.Kernel.Automating.TopSolidHost]::Documents.GetDocuments() | Where-Object {
        [TopSolid.Kernel.Automating.TopSolidHost]::Documents.GetTypeFullName($_) -like '*.MillTurnDocument'
    } | Select-Object -First 1
    for($e=0; $e -lt [TopSolid.Kernel.Automating.TopSolidHost]::Application.ExporterCount; $e++) {
        $name=''; $ext=[System.Collections.Generic.List[string]]::new()
        [TopSolid.Kernel.Automating.TopSolidHost]::Application.GetExporterFileType($e,[ref]$name,[ref]$ext)
        if([TopSolid.Kernel.Automating.TopSolidHost]::Documents.CanExport($e,$doc)) { [pscustomobject]@{exporter=$e;name=$name;extensions=$ext} | ConvertTo-Json -Compress }
    }
    $ops = [TopSolid.Cam.NC.Kernel.Automating.TopSolidCamHost]::Operations.GetOperations($doc)
    $op = $ops[1]
    $columns = [TopSolid.Cam.NC.Kernel.Automating.TopSolidCamHost]::ToolPath.StartToolPath($op)
    try {
        for ($i=0; $i -lt 3; $i++) {
            $row = [TopSolid.Cam.NC.Kernel.Automating.TopSolidCamHost]::ToolPath.NextToolPathItem($op)
            if ($null -eq $row) { break }
            if ($row.ContainsKey('GOTO_XYZ_3D')) {
                $value = $row['GOTO_XYZ_3D']
                if ($null -ne $value) { [pscustomobject]@{ row=$i; type=$value.GetType().FullName; value=$value } | ConvertTo-Json -Depth 6 -Compress }
            }
        }
    } finally { [TopSolid.Cam.NC.Kernel.Automating.TopSolidCamHost]::ToolPath.EndToolPath($op) }
} finally {
    [TopSolid.Cam.NC.Kernel.Automating.TopSolidCamHost]::Disconnect()
    [TopSolid.Kernel.Automating.TopSolidHost]::Disconnect()
}
