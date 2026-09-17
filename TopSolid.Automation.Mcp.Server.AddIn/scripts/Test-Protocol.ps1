param(
    [string]$ServerPath = (Join-Path $PSScriptRoot '../bin/Debug/net48/TopSolid.Automation.Mcp.Server.AddIn.exe'),
    [switch]$SkipTopSolidQueries
)

$ErrorActionPreference = 'Stop'
$resolvedServerPath = (Resolve-Path -LiteralPath $ServerPath).Path
$serverDirectory = Split-Path -Parent $resolvedServerPath
$taskStart = [System.Diagnostics.ProcessStartInfo]::new($resolvedServerPath)
$taskStart.UseShellExecute = $false
$taskStart.CreateNoWindow = $true
$taskStart.RedirectStandardInput = $true
$taskStart.RedirectStandardOutput = $true
$taskStart.RedirectStandardError = $true
$taskStart.StandardOutputEncoding = [Text.UTF8Encoding]::new($false)
$taskStart.StandardErrorEncoding = [Text.UTF8Encoding]::new($false)
$taskProcess = [System.Diagnostics.Process]::new()
$taskProcess.StartInfo = $taskStart
$null = $taskProcess.Start()
$taskDiagnostics = $taskProcess.StandardError.ReadToEndAsync()
$script:taskChecks = 0

function Assert-Check([bool]$Condition, [string]$Message) {
    if (!$Condition) { throw $Message }
    $script:taskChecks++
}
function Send-Json($Message) {
    $taskProcess.StandardInput.WriteLine((ConvertTo-Json -InputObject $Message -Depth 20 -Compress))
    $taskProcess.StandardInput.Flush()
}
function Read-Reply {
    $taskRead = $taskProcess.StandardOutput.ReadLineAsync()
    if (!$taskRead.Wait(20000)) { throw 'MCP reply exceeded 20 seconds.' }
    $taskLine = $taskRead.Result
    if ($null -eq $taskLine) { throw 'MCP server closed stdout unexpectedly.' }
    return ($taskLine | ConvertFrom-Json)
}
function Request($Id, [string]$Method, $Parameters = @{}) {
    Send-Json @{ jsonrpc = '2.0'; id = $Id; method = $Method; params = $Parameters }
    $taskReply = Read-Reply
    Assert-Check ($taskReply.id -eq $Id) "Reply ID mismatch for $Method."
    return $taskReply
}

try {
    $reply = Request 1 'tools/list'
    Assert-Check ($reply.error.code -eq -32002) 'Tools must require initialization.'
    $reply = Request 2 'initialize' @{ protocolVersion = '2025-03-26'; capabilities = @{}; clientInfo = @{ name = 'protocol-smoke'; version = '1.0' } }
    Assert-Check ($reply.result.protocolVersion -eq '2025-03-26') 'Wrong protocol version.'
    Send-Json @{ jsonrpc = '2.0'; method = 'notifications/initialized' }
    $reply = Request 3 'tools/list'
    $toolNames = @($reply.result.tools | ForEach-Object { $_.name })
    Assert-Check ($toolNames.Count -eq 171) 'Expected all 171 reviewed tools.'
    foreach ($name in @('topsolid_get_status', 'topsolid_get_active_document', 'topsolid_get_document_info')) {
        Assert-Check ($toolNames -contains $name) "Missing tool $name."
    }
    Assert-Check (@($reply.result.tools | Where-Object { !$_.annotations.readOnlyHint }).Count -eq 50) 'All 50 confirmed action tools should be registered.'
    $toolManifest = $reply.result.tools
    $apiReferences = @($toolManifest | ForEach-Object { @($_._meta.'topsolid/api') } | Where-Object { -not [string]::IsNullOrWhiteSpace([string]$_) })
    Assert-Check (@($apiReferences | Where-Object { [string]$_ -like 'https://help.topsolid.com/*' }).Count -eq 0) 'Runtime API metadata must not expose website URLs.'
    Assert-Check (@($apiReferences | Where-Object { -not ([string]$_).StartsWith('TopSolid.Automation/', [StringComparison]::Ordinal) }).Count -eq 0) 'Runtime API metadata must use bundled local references.'
    foreach ($apiReference in $apiReferences) {
        if (-not ([string]$apiReference).Contains('/embedded/')) {
            $localReferencePath = Join-Path $serverDirectory ([string]$apiReference).Replace('/', [System.IO.Path]::DirectorySeparatorChar)
            Assert-Check (Test-Path -LiteralPath $localReferencePath) "Runtime API metadata points to a missing local file: $apiReference"
        }
    }
    $manifestPath = Join-Path $PSScriptRoot '../../docs/api/mcp-tools.json'
    $toolManifest | ConvertTo-Json -Depth 30 | Set-Content -LiteralPath $manifestPath -Encoding UTF8
    $reply = Request 100 'tools/call' @{ name = 'topsolid_create_rectangle2d'; arguments = @{ documentId = 'unapproved'; placement = '2d'; width = 20; height = 10 } }
    Assert-Check ($reply.error.code -eq -32010) 'Writes must be rejected without confirmation before accessing TopSolid.'
    $reply = Request 101 'tools/call' @{ name = 'topsolid_create_circle2d'; arguments = @{ documentId = 'unapproved'; placement = '2d'; radius = -1 } }
    Assert-Check ($reply.error.code -eq -32602) 'Invalid geometry must fail schema validation.'
    $reply = Request 102 'tools/call' @{ name = 'topsolid_search_api_reference'; arguments = @{ query = 'CreateSketchIn2D'; limit = 5 } }
    Assert-Check (!$reply.result.isError -and $reply.result.content[0].text.Contains('CreateSketchIn2D')) 'Offline reference search must work.'
    $reply = Request 103 'tools/call' @{ name = 'topsolid_get_api_reference'; arguments = @{ symbol = 'TopSolid.Kernel.Automating.ISketches2D.CreateSketchIn2D'; length = 2000 } }
    Assert-Check (!$reply.result.isError -and $reply.result.content[0].text.Contains('EnsureIsDirty')) 'Full cached contract must include modification requirements.'
    $reply = Request 4 'tools/call' @{ name = 'invented_tool'; arguments = @{} }
    Assert-Check ($reply.error.code -eq -32602) 'Unknown tool must fail.'
    $reply = Request 5 'tools/call' @{ name = 'topsolid_get_status'; arguments = @{ launch = $true } }
    Assert-Check ($reply.error.code -eq -32602) 'Unexpected arguments must fail before Automation access.'
    $reply = Request 6 'tools/call' @{ name = 'topsolid_get_document_info'; arguments = @{ documentId = 123 } }
    Assert-Check ($reply.error.code -eq -32602) 'Document ID type must be checked.'
    $reply = Request 7 'tools/call' @{ name = 'topsolid_get_document_info'; arguments = @{ documentId = ' ' } }
    Assert-Check ($reply.error.code -eq -32602) 'Blank document ID must fail.'
    $reply = Request 8 'invented/method'
    Assert-Check ($reply.error.code -eq -32601) 'Unknown method must fail.'
    $taskProcess.StandardInput.WriteLine('{broken-json')
    $reply = Read-Reply
    Assert-Check ($reply.error.code -eq -32700) 'Malformed JSON must return parse error.'
    Send-Json @(@{ jsonrpc = '2.0'; id = 'batch-a'; method = 'ping' }, @{ jsonrpc = '2.0'; method = 'notifications/cancelled'; params = @{ requestId = 999 } }, @{ jsonrpc = '2.0'; id = 'batch-b'; method = 'ping' })
    $reply = @(Read-Reply)
    Assert-Check ($reply.Count -eq 2 -and $reply[0].id -eq 'batch-a' -and $reply[1].id -eq 'batch-b') 'Batch must reply only to requests.'
    $reply = Request 9 'ping'
    Assert-Check ($null -ne $reply.result) 'Ping must succeed after malformed JSON.'

    if (!$SkipTopSolidQueries) {
        $reply = Request 10 'tools/call' @{ name = 'topsolid_get_status'; arguments = @{} }
        Assert-Check ($reply.result.isError -eq $false) 'Availability check should return a truthful status.'
        $status = $reply.result.content[0].text | ConvertFrom-Json
        Assert-Check ($status.connected -is [bool]) 'Status requires a Boolean connected value.'
        Write-Output ('Live Automation status: ' + ($status | ConvertTo-Json -Compress))
        $reply = Request 11 'tools/call' @{ name = 'topsolid_get_active_document'; arguments = @{} }
        Assert-Check ($null -ne $reply.result.content) 'Active document must return a tool result even when unavailable.'
        if (!$status.connected) { Assert-Check ($reply.result.isError -eq $true) 'Unavailable document query must report a tool execution error.' }
        Write-Output ('Live document result: ' + $reply.result.content[0].text)
        $reply = Request 12 'tools/call' @{ name = 'topsolid_get_document_info'; arguments = @{} }
        Assert-Check ($null -ne $reply.result.content) 'Document information must return a tool result.'
        Write-Output ('Live document info result: ' + $reply.result.content[0].text)
        if ($status.connected) {
            $liveReads = @()
            $readId = 200
            foreach ($tool in @('topsolid_list_documents', 'topsolid_list_loaded_documents', 'topsolid_list_projects', 'topsolid_list_libraries', 'topsolid_list_active_licenses')) {
                $reply = Request $readId 'tools/call' @{ name = $tool; arguments = @{ limit = 5 } }
                $readId++
                Assert-Check ($reply.result.isError -eq $false) "Live read failed: $tool : $($reply.result.content[0].text)"
                $data = $reply.result.content[0].text | ConvertFrom-Json
                Assert-Check ($data.total -ge 0 -and @($data.items).Count -le 5) "Invalid page result from $tool."
                $liveReads += [pscustomobject]@{ tool = $tool; success = $true; total = $data.total; returned = @($data.items).Count }
                Write-Output ("Live {0}: total={1}, returned={2}" -f $tool, $data.total, @($data.items).Count)
                if ($tool -eq 'topsolid_list_loaded_documents' -and @($data.items).Count -gt 0) {
                    foreach ($detailTool in @('topsolid_get_document_info', 'topsolid_list_elements')) {
                        $reply = Request $readId 'tools/call' @{ name = $detailTool; arguments = @{ documentId = $data.items[0] } }
                        $readId++
                        Assert-Check ($reply.result.isError -eq $false) "Loaded-document read failed: $detailTool."
                        $liveReads += [pscustomobject]@{ tool = $detailTool; success = $true; target = 'existing loaded document' }
                    }
                }
                if ($tool -in @('topsolid_list_projects', 'topsolid_list_libraries')) {
                    $namesReply = Request $readId 'tools/call' @{ name = $tool; arguments = @{} }
                    $readId++
                    Assert-Check ($namesReply.result.isError -eq $false) "Default friendly-name query failed: $tool."
                    $names = $namesReply.result.content[0].text | ConvertFrom-Json
                    Assert-Check ($names.limit -eq 100 -and @($names.items).Count -eq [Math]::Min(100, $names.total)) "Default name page incomplete: $tool."
                    Assert-Check ($names.hasMore -eq ($names.total -gt 100)) "Default name page continuation incorrect: $tool."
                    Assert-Check (@($names.items | Where-Object { $_.name -isnot [string] -or [string]::IsNullOrEmpty($_.pdmObjectId) }).Count -eq 0) "Friendly names or IDs missing: $tool."
                }
                if ($tool -eq 'topsolid_list_projects' -and @($data.items).Count -gt 0) {
                    $reply = Request $readId 'tools/call' @{ name = 'topsolid_get_pdm_object_info'; arguments = @{ pdmObjectId = $data.items[0].pdmObjectId } }
                    $readId++
                    Assert-Check ($reply.result.isError -eq $false) 'Live PDM object detail query failed.'
                    $liveReads += [pscustomobject]@{ tool = 'topsolid_get_pdm_object_info'; success = $true }
                }
            }
            $liveReads | ConvertTo-Json -Depth 5 | Set-Content -LiteralPath (Join-Path $PSScriptRoot '../../artifacts/live-read-verification.json') -Encoding UTF8
        }
    }
    $taskProcess.StandardInput.Close()
    Assert-Check ($taskProcess.WaitForExit(5000)) 'Server must exit when stdin closes.'
    Assert-Check ($taskProcess.ExitCode -eq 0) 'Server must exit successfully.'
    Assert-Check ([string]::IsNullOrEmpty($taskProcess.StandardOutput.ReadToEnd())) 'Unexpected extra protocol output.'
    Write-Output "PASS: $script:taskChecks protocol checks."
    Write-Output ('Diagnostics: ' + $taskDiagnostics.Result.Trim())
}
finally {
    if (!$taskProcess.HasExited) { $taskProcess.Kill(); $taskProcess.WaitForExit() }
    $taskProcess.Dispose()
}
