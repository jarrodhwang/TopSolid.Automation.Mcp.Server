param(
    [Parameter(Mandatory = $true)][string] $HostName,
    [ValidateRange(1, 65535)][int] $Port = 443,
    [string] $ServerPath = (Join-Path $PSScriptRoot '../TopSolid.Automation.Mcp.Server.AddIn/bin/Release/net48/TopSolid.Automation.Mcp.Server.AddIn.exe')
)
$ErrorActionPreference = 'Stop'
$resolvedServer = (Resolve-Path -LiteralPath $ServerPath).Path
if ([Uri]::CheckHostName($HostName) -eq [UriHostNameType]::Unknown -or $HostName.Contains(':')) { throw 'Enter a DNS host name or IPv4 address.' }
$secret = Read-Host 'Gateway access token (at least 32 characters)' -AsSecureString
$pointer = [Runtime.InteropServices.Marshal]::SecureStringToBSTR($secret)
try {
    $env:TOPSOLID_GATEWAY_TOKEN = [Runtime.InteropServices.Marshal]::PtrToStringBSTR($pointer)
    & $resolvedServer --https-gateway "https://${HostName}:${Port}/topsolid/"
    if ($LASTEXITCODE -ne 0) { throw "Gateway stopped with exit code $LASTEXITCODE. Check the HTTPS certificate binding and URL reservation." }
}
finally {
    Remove-Item Env:TOPSOLID_GATEWAY_TOKEN -ErrorAction SilentlyContinue
    [Runtime.InteropServices.Marshal]::ZeroFreeBSTR($pointer)
    $secret.Dispose()
}
