param(
    [string]$DocumentId,
    [ValidateRange(1, 128)][int]$MaximumRows = 96,
    [string]$OutputFile = "$PSScriptRoot\..\artifacts\list-preview-20260918\itoolpath-contract-probe.json"
)
$ErrorActionPreference = 'Stop'
$sdkDirectory = 'C:\Program Files\TOPSOLID\TopSolid 7.20\bin'
foreach ($name in @('TopSolid.Kernel.SX', 'TopSolid.Kernel.Automating', 'TopSolid.Cam.NC.Kernel.Automating')) {
    [void][Reflection.Assembly]::LoadFrom((Join-Path $sdkDirectory ($name + '.dll')))
}
$interface = [TopSolid.Cam.NC.Kernel.Automating.IToolPath]
$report = [ordered]@{
    timestampUtc = [DateTime]::UtcNow.ToString('O')
    interface = $interface.FullName
    assembly = $interface.Assembly.Location
    assemblyVersion = $interface.Assembly.GetName().Version.ToString()
    methods = @($interface.GetMethods() | ForEach-Object { $_.ToString() })
    maximumRowsPerOperation = $MaximumRows
    operations = @()
}
try {
    [void][TopSolid.Kernel.Automating.TopSolidHost]::Connect($false)
    [void][TopSolid.Cam.NC.Kernel.Automating.TopSolidCamHost]::Connect()
    $loaded = [TopSolid.Kernel.Automating.TopSolidHost]::Documents.GetDocuments()
    $doc = $loaded | Where-Object {
        if ($DocumentId) { $_.PdmDocumentId -eq $DocumentId }
        else { [TopSolid.Kernel.Automating.TopSolidHost]::Documents.GetTypeFullName($_) -like '*.MillTurnDocument' }
    } | Select-Object -First 1
    if ($null -eq $doc) { throw 'No matching loaded CAM document. This probe never opens documents.' }
    $report.hostVersion = [TopSolid.Kernel.Automating.TopSolidHost]::Application.Version
    $report.clientVersion = [TopSolid.Kernel.Automating.TopSolidHost]::ClientVersion
    $report.documentId = $doc.PdmDocumentId
    $report.documentName = [TopSolid.Kernel.Automating.TopSolidHost]::Documents.GetName($doc)
    $beforeDirty = [TopSolid.Kernel.Automating.TopSolidHost]::Documents.IsDirty($doc)
    $report.dirtyBefore = $beforeDirty
    foreach ($operation in ([TopSolid.Cam.NC.Kernel.Automating.TopSolidCamHost]::Operations.GetOperations($doc) | Select-Object -First 12)) {
        $extended = [TopSolid.Cam.NC.Kernel.Automating.ElementExId]::new($operation)
        $result = [ordered]@{
            id = $operation.Id
            name = [TopSolid.Kernel.Automating.TopSolidHost]::Elements.GetName($operation)
            upToDate = [TopSolid.Cam.NC.Kernel.Automating.TopSolidCamHost]::Operations.IsUpToDate($extended)
            columns = @()
            rowsRead = 0
            pointRows = 0
            emptyPointRows = 0
            numericXYZRows = 0
            samples = @()
        }
        $columns = [TopSolid.Cam.NC.Kernel.Automating.TopSolidCamHost]::ToolPath.StartToolPath($operation)
        $result.available = $null -ne $columns
        if ($null -ne $columns) {
            try {
                $result.columns = @($columns)
                for ($rowIndex = 0; $rowIndex -lt $MaximumRows; $rowIndex++) {
                    $row = [TopSolid.Cam.NC.Kernel.Automating.TopSolidCamHost]::ToolPath.NextToolPathItem($operation)
                    if ($null -eq $row) { break }
                    $result.rowsRead++
                    if ($row.ContainsKey('X') -and $row.ContainsKey('Y') -and $row.ContainsKey('Z') -and
                        $row['X'] -is [double] -and $row['Y'] -is [double] -and $row['Z'] -is [double]) { $result.numericXYZRows++ }
                    if ($row.ContainsKey('GOTO_XYZ_3D')) {
                        $result.pointRows++
                        $value = $row['GOTO_XYZ_3D']
                        if ($null -eq $value -or ($value -is [string] -and $value.Length -eq 0)) { $result.emptyPointRows++ }
                        if ($result.samples.Count -lt 3) {
                            $entries = @($row.GetEnumerator() | ForEach-Object {
                                [ordered]@{ key = $_.Key; type = $(if ($null -eq $_.Value) { 'null' } else { $_.Value.GetType().FullName }); value = $_.Value }
                            })
                            $result.samples += [ordered]@{ row = $rowIndex; entries = $entries }
                        }
                    }
                }
            }
            finally { [TopSolid.Cam.NC.Kernel.Automating.TopSolidCamHost]::ToolPath.EndToolPath($operation) }
        }
        $report.operations += $result
    }
    $report.dirtyAfter = [TopSolid.Kernel.Automating.TopSolidHost]::Documents.IsDirty($doc)
    $report.dirtyUnchanged = $beforeDirty -eq $report.dirtyAfter
    $report.note = 'Direct read-only Automation calls. No MCP JSON conversion, recalculation, simulation or native document changes.'
}
finally {
    [TopSolid.Cam.NC.Kernel.Automating.TopSolidCamHost]::Disconnect()
    [TopSolid.Kernel.Automating.TopSolidHost]::Disconnect()
}
$resolvedOutput = [IO.Path]::GetFullPath($OutputFile)
[void][IO.Directory]::CreateDirectory([IO.Path]::GetDirectoryName($resolvedOutput))
[IO.File]::WriteAllText($resolvedOutput, ($report | ConvertTo-Json -Depth 16), [Text.UTF8Encoding]::new($false))
[pscustomobject]@{ interface=$report.interface; hostVersion=$report.hostVersion; assemblyVersion=$report.assemblyVersion; dirtyUnchanged=$report.dirtyUnchanged; report=$resolvedOutput } | ConvertTo-Json
$report.operations | ForEach-Object { [pscustomobject]@{id=$_.id; available=$_.available; upToDate=$_.upToDate; rows=$_.rowsRead; pointRows=$_.pointRows; emptyPoints=$_.emptyPointRows; numericXYZRows=$_.numericXYZRows} } | ConvertTo-Json
