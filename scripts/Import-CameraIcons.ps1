param([string]$TopSolidBin = 'C:\Program Files\TOPSOLID\TopSolid 7.20\bin',
      [string]$IconRoot = 'C:\Users\jarrod\Downloads\TopSolid Download\TopSolid.Icons.v7.19\Icons')
$ErrorActionPreference = 'Stop'
$destination = Join-Path $PSScriptRoot '../TopSolid.Automation.AI.Studio/Assets/TopSolid'
$manifestPath = Join-Path $destination 'provenance.json'
$keys = @('top','bottom','front','back','left','right','iso','perspective')
$assembly = [Reflection.Assembly]::LoadFrom((Join-Path $TopSolidBin 'TopSolid.Kernel.GR.dll'))
$entries = @(Get-Content -Raw -LiteralPath $manifestPath | ConvertFrom-Json | Where-Object {$_.Key -notlike 'camera-*' -and $_.Key -ne 'operation-group-tool' -and $_.Key -ne 'view-machine'})
foreach ($key in $keys) {
    $native = if ($key -eq 'iso') {'Isometric'} else {$key.Substring(0,1).ToUpperInvariant()+$key.Substring(1)}
    $resource = 'TopSolid.Kernel.GR.Cameras.Camera.'+$native+'Camera.ico'
    $stream = $assembly.GetManifestResourceStream($resource)
    if ($null -eq $stream) {throw "Missing native camera icon: $resource"}
    $file = Join-Path $destination ('camera-'+$key+'.ico')
    $output = [IO.File]::Create($file)
    try {$stream.CopyTo($output)} finally {$output.Dispose();$stream.Dispose()}
    $entries += [pscustomobject]@{Key='camera-'+$key;Source='TopSolid.Kernel.GR.dll!'+$resource;Sha256=(Get-FileHash -LiteralPath $file).Hash}
}
foreach ($entry in @(@('operation-group-tool','Cam\NC\Kernel\UI.OperationManager\Resources\TreeViewRepresentationSorted.png'), @('view-machine','Cam\NC\Kernel\UI.Machines\Commands\NCMachineDisplayCommand.png'))) {
    $source=Join-Path $IconRoot $entry[1]; $file=Join-Path $destination ($entry[0]+'.png')
    Copy-Item -LiteralPath $source -Destination $file
    $entries += [pscustomobject]@{Key=$entry[0];Source=$entry[1];Sha256=(Get-FileHash -LiteralPath $file).Hash}
}
$entries | ConvertTo-Json -Depth 5 | Set-Content -LiteralPath $manifestPath -Encoding UTF8
