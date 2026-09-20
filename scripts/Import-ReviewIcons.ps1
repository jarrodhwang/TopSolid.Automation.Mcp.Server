param([string]$IconRoot = 'C:\Users\jarrod\Downloads\TopSolid Download\TopSolid.Icons.v7.19\Icons')
$ErrorActionPreference = 'Stop'
$destination = Join-Path $PSScriptRoot '../TopSolid.Automation.AI.Studio/Assets/TopSolid'
$manifestPath = Join-Path $destination 'provenance.json'
$existing = @(Get-Content -LiteralPath $manifestPath -Raw | ConvertFrom-Json)
$entries = [Collections.Generic.List[object]]::new()
$replacements = [ordered]@{
    'approve' = 'Cad\Kernel\WX\Dialogs\Tools.OK.png'
    'cancel' = 'Cad\Kernel\WX\Dialogs\Tools.Cancel.png'
    'error' = 'Cad\Kernel\WX\Resources.Error.png'
    'warning' = 'Cad\Kernel\WX\Resources.Warning.png'
    'question' = 'Cad\Kernel\WX\Resources.Question.png'
    'parameter' = 'Cad\Kernel\DB\Parameters\RealParameterEntity.png'
    'arguments' = 'Cad\Kernel\WG\Commands\ShowPropertiesCommand.png'
    'operation' = 'Cam\NC\Kernel\DB\Operations\NCOperation.png'
    'cam-tool' = 'Cam\NC\MillTurn\UI\Resources\ToolEntity.png'
    'cam-favorites' = 'Quote\UI\Resources.Favorite.png'
    'cam-cutting-conditions' = 'Cam\NC\Kernel\UI\Resources\CuttingConditionsPane.png'
    'cam-geometry' = 'Cam\NC\MillTurn\UI\Resources\GeometryPanel.png'
    'cam-strategy' = 'Cam\NC\MillTurn\UI\Resources\Parameters.png'
    'cam-comment' = 'Cam\NC\MillTurn\UI\Resources\CommentsPane.png'
    'cam-multi-axis' = 'Cam\NC\Kernel\UI\Resources\MillTurnMultiAxisPane.png'
    'cam-properties' = 'Cam\NC\MillTurn\UI\Resources\Properties.png'
}
foreach ($entry in $existing) {
    if (!$replacements.Contains($entry.Key) -and !$entry.NativeType) { $entries.Add($entry) }
}
function Import-Icon($key, $source, $nativeType) {
    $file = Join-Path $IconRoot $source
    Copy-Item -LiteralPath $file -Destination (Join-Path $destination ($key + '.png'))
    $entry = [ordered]@{ Key=$key; Source=$source; Sha256=(Get-FileHash -LiteralPath $file -Algorithm SHA256).Hash }
    if ($nativeType) { $entry.NativeType=$nativeType }
    $entries.Add([pscustomobject]$entry)
}
foreach ($key in $replacements.Keys) { Import-Icon $key $replacements[$key] $null }
# Match the exact native DB class to its original icon. No text/translation guesses,
# and no stripping DB namespace segments that distinguish 2D, form, five-axis or turning.
foreach ($file in Get-ChildItem -LiteralPath (Join-Path $IconRoot 'Cam/NC') -Filter '*Operation.png' -File -Recurse | Sort-Object FullName) {
    $relative = [IO.Path]::GetRelativePath($IconRoot, $file.FullName)
    if ($relative -notmatch '\\DB(?:[.\\])') { continue }
    $type = 'TopSolid.' + $relative.Substring(0, $relative.Length-4).Replace('\','.')
    $key = 'cam-op-' + (Get-FileHash -LiteralPath $file.FullName -Algorithm SHA256).Hash.Substring(0,16).ToLowerInvariant()
    Import-Icon $key $relative $type
}
$entries | ConvertTo-Json -Depth 5 | Set-Content -LiteralPath $manifestPath -Encoding utf8
Write-Output ('Imported {0} native operation mappings and seven CAM category icons.' -f @($entries | Where-Object NativeType).Count)
