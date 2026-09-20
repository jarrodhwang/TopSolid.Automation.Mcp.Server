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
    if(-not $read.Wait(60000)){throw 'Preview response timed out; leave TopSolid open so the temporary transaction can finish.'}
    $message=$read.Result|ConvertFrom-Json
    if($message.error){throw ($message.error|ConvertTo-Json -Compress)}
    return $message.result
}
try {
    $null=Rpc 'initialize' @{protocolVersion='2025-03-26';capabilities=@{};clientInfo=@{name='automation-toolpath-validation';version='1'}}
    $process.StandardInput.WriteLine('{"jsonrpc":"2.0","method":"notifications/initialized"}')
    # Camera is in SI. This view surrounds the current impeller fixture measured in the export probe.
    $request=@{documentId=$DocumentId;id=$OperationId;view=@{eye=@(-.35,-.35,.24);look=@(1,1,-.55);up=@(0,0,1);angle=0;radius=.14;machine=$false}}
    $watch=[Diagnostics.Stopwatch]::StartNew()
    $result=Rpc 'topsolid/toolpathPreview' $request
    $elapsed=$watch.Elapsed.TotalSeconds
    if($result.status -ne 'ready' -or $result.format -ne 'native-view-png' -or $result.stateRestored -ne $true){throw ($result|ConvertTo-Json -Depth 8)}
    [IO.File]::WriteAllBytes((Join-Path $directory 'automation-operation-path.png'),[Convert]::FromBase64String($result.data))
    $result.PSObject.Properties.Remove('data'); $result|Add-Member elapsedSeconds $elapsed
    $result|ConvertTo-Json -Depth 8|Set-Content (Join-Path $directory 'automation-operation-path.json') -Encoding UTF8
    $result|ConvertTo-Json -Depth 8
} finally {
    $process.StandardInput.Close()
    if(-not $process.WaitForExit(3000)){Write-Warning 'Server is still completing its native call; it was not killed.'}else{$process.Dispose()}
}
