param([string]$Server = "$PSScriptRoot\..\TopSolid.Automation.Mcp.Server.AddIn\bin\Release\net48\TopSolid.Automation.Mcp.Server.AddIn.exe")
$ErrorActionPreference = 'Stop'
$start = [Diagnostics.ProcessStartInfo]::new([IO.Path]::GetFullPath($Server))
$start.UseShellExecute = $false
$start.CreateNoWindow = $true
$start.RedirectStandardInput = $true
$start.RedirectStandardOutput = $true
$start.RedirectStandardError = $true
$start.StandardOutputEncoding = [Text.Encoding]::UTF8
$process = [Diagnostics.Process]::Start($start)
$errors = $process.StandardError.ReadToEndAsync()
function Rpc($method, $parameters) {
    $process.StandardInput.WriteLine((@{jsonrpc='2.0';id=1;method=$method;params=$parameters} | ConvertTo-Json -Depth 30 -Compress))
    $process.StandardInput.Flush()
    $read = $process.StandardOutput.ReadLineAsync()
    if (-not $read.Wait(120000)) { throw 'Read timed out' }
    $response = $read.Result | ConvertFrom-Json
    if ($response.error) { throw ($response.error | ConvertTo-Json -Compress) }
    $response.result
}
function ReadTool($name, $arguments) {
    if ($name -notin @('topsolid_list_document_summaries','topsolid_list_cam_operation_summaries','topsolid_read_cam_toolpath')) { throw 'Not a permitted inspection' }
    $result = Rpc 'tools/call' @{name=$name;arguments=$arguments}
    if ($result.isError) { throw ($result | ConvertTo-Json -Depth 8) }
    if ($result.structuredContent) { return $result.structuredContent }
    $result.content[0].text | ConvertFrom-Json
}
try {
    $null = Rpc 'initialize' @{protocolVersion='2025-03-26';capabilities=@{};clientInfo=@{name='read-only-toolpath-inspection';version='1'}}
    $process.StandardInput.WriteLine('{"jsonrpc":"2.0","method":"notifications/initialized"}')
    $process.StandardInput.Flush()
    $docs = ReadTool 'topsolid_list_document_summaries' @{scope='open';limit=100}
    $doc = $docs.items | Where-Object type -Like 'TopSolid.Cam.*' | Select-Object -First 1
    if (-not $doc) { throw 'No loaded CAM document' }
    $ops = ReadTool 'topsolid_list_cam_operation_summaries' @{documentId=$doc.documentId;limit=20}
    $op = $ops.items | Where-Object hasTool | Select-Object -First 1
    $path = ReadTool 'topsolid_read_cam_toolpath' @{element=$op.operation.element;offset=5;limit=50}
    $path.rows | Select-Object -Skip 25 -First 3 | ConvertTo-Json -Depth 8
} finally {
    $process.StandardInput.Close()
    if (-not $process.WaitForExit(3000)) { $process.Kill() }
    $process.Dispose()
}
