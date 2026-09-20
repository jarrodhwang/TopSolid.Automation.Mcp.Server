[CmdletBinding(SupportsShouldProcess)]
param(
    [Parameter(Mandatory)][string]$AddInDirectory,
    [Parameter(Mandatory)][string]$RegistrationCertificate,
    [string]$PackageDirectory = $PSScriptRoot
)
$ErrorActionPreference = 'Stop'
$targetDirectory = [IO.Path]::GetFullPath($AddInDirectory)
$packageRoot = [IO.Path]::GetFullPath($PackageDirectory)
$certificatePath = (Resolve-Path -LiteralPath $RegistrationCertificate).ProviderPath
if (-not (Test-Path -LiteralPath $targetDirectory -PathType Container)) {
    throw 'Select an existing directory configured for TopSolid external add-ins.'
}
if ((Get-Item -LiteralPath $certificatePath).Length -eq 0 -or (Get-Item -LiteralPath $certificatePath).Length -gt 1048576) {
    throw 'Provide the issued TopSolid add-in registration certificate.'
}
$files = @('TopSolid.Automation.CamPreview.AddIn.dll', 'Newtonsoft.Json.dll')
foreach ($name in $files) {
    $sourcePath = Join-Path $packageRoot $name
    if (-not (Test-Path -LiteralPath $sourcePath -PathType Leaf)) { throw "Missing packaged file: $name" }
    $destinationPath = Join-Path $targetDirectory $name
    if ($name -eq 'Newtonsoft.Json.dll' -and (Test-Path -LiteralPath $destinationPath) -and
        (Get-FileHash -LiteralPath $sourcePath).Hash -ne (Get-FileHash -LiteralPath $destinationPath).Hash) {
        throw 'A different Newtonsoft.Json.dll already exists. Use a dedicated configured add-in directory.'
    }
}
if ($PSCmdlet.ShouldProcess($targetDirectory, 'Install registered Studio CAM preview add-in')) {
    foreach ($name in $files) { Copy-Item -LiteralPath (Join-Path $packageRoot $name) -Destination (Join-Path $targetDirectory $name) }
    Copy-Item -LiteralPath $certificatePath -Destination (Join-Path $targetDirectory 'StudioCamPreview.registration.xml')
    Write-Output 'Files installed. Restart TopSolid and verify the registered add-in load and face preview before use.'
}
