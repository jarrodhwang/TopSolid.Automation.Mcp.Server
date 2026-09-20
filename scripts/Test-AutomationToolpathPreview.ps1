param(
    [Parameter(Mandatory=$true)][string]$DocumentId,
    [int]$OperationId = 1221,
    [string]$Server = "$PSScriptRoot\..\TopSolid.Automation.Mcp.Server.AddIn\bin\Release\net48\TopSolid.Automation.Mcp.Server.AddIn.exe",
    [string]$OutputDirectory = "$PSScriptRoot\..\artifacts\toolpath-repair"
)
$ErrorActionPreference = 'Stop'
$directory = [IO.Path]::GetFullPath($OutputDirectory)
[void][IO.Directory]::CreateDirectory($directory)
$start = [Diagnostics.ProcessStartInfo]::new([IO.Path]::GetFullPath($Server))
$start.UseShellExecute = $false; $start.CreateNoWindow = $true
$start.RedirectStandardInput = $true; $start.RedirectStandardOutput = $true; $start.RedirectStandardError = $true
$start.StandardOutputEncoding = [Text.Encoding]::UTF8
$process = [Diagnostics.Process]::Start($start)
$stderr = $process.StandardError.ReadToEndAsync()
function Rpc($method,$arguments) {
    $process.StandardInput.WriteLine((@{jsonrpc='2.0';id=1;method=$method;params=$arguments}|ConvertTo-Json -Depth 15 -Compress))
    $process.StandardInput.Flush()
    $read = $process.StandardOutput.ReadLineAsync()
    if(-not $read.Wait(60000)){throw 'IToolPath read timed out.'}
    $message=$read.Result|ConvertFrom-Json
    if($message.error){throw ($message.error|ConvertTo-Json -Compress)}
    return $message.result
}
try {
    $null=Rpc 'initialize' @{protocolVersion='2025-03-26';capabilities=@{};clientInfo=@{name='automation-toolpath-validation';version='1'}}
    $process.StandardInput.WriteLine('{"jsonrpc":"2.0","method":"notifications/initialized"}')
    $request=@{documentId=$DocumentId;id=$OperationId}
    $watch=[Diagnostics.Stopwatch]::StartNew()
    $result=Rpc 'topsolid/toolpathPreview' $request
    $elapsed=$watch.Elapsed.TotalSeconds
    if($result.status -notin @('ready','coordinatesUnavailable') -or $result.format -ne 'segments-f32' -or $result.source -ne 'IToolPath'){throw ($result|ConvertTo-Json -Depth 8)}
    if($result.operation.documentId -ne $DocumentId -or $result.operation.id -ne $OperationId){throw 'Operation identity changed.'}
    $bytes = [Convert]::FromBase64String($result.data)
    if($bytes.Length -ne $result.segments * 24){throw 'Invalid geometry buffer size.'}
    if($result.status -eq 'ready' -and $result.segments -le 0){throw 'Ready result has no geometry.'}
    $result|Add-Member rawCoordinatesRetrieved ($result.status -eq 'ready')
    $result.PSObject.Properties.Remove('data'); $result|Add-Member elapsedSeconds $elapsed
    $result|ConvertTo-Json -Depth 8|Set-Content (Join-Path $directory 'itoolpath-operation-geometry.json') -Encoding UTF8
    $result.PSObject.Properties.Remove('columns')
    $result|ConvertTo-Json -Depth 8
} finally {
    $process.StandardInput.Close()
    if(-not $process.WaitForExit(3000)){Write-Warning 'Server is still completing its native call; it was not killed.'}else{$process.Dispose()}
}
