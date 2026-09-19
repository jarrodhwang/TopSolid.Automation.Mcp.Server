param(
    [Parameter(Mandatory)][string]$SdkDirectory,
    [string]$OutputDirectory = 'artifacts/occt-preview/runtime'
)
$ErrorActionPreference = 'Stop'
$repo = Split-Path -Parent $PSScriptRoot
$sdk = (Resolve-Path -LiteralPath $SdkDirectory).Path
$occt = Join-Path $sdk 'opencascade-7.9.3-vc14-64'
$vswhere = Join-Path ${env:ProgramFiles(x86)} 'Microsoft Visual Studio/Installer/vswhere.exe'
$vs = & $vswhere -latest -products '*' -requires Microsoft.VisualStudio.Component.VC.Tools.x86.x64 -property installationPath
if (!$vs) { throw 'A Visual Studio C++ desktop toolchain is required.' }
$cmake = Join-Path $vs 'Common7/IDE/CommonExtensions/Microsoft/CMake/CMake/bin/cmake.exe'
$msvc = Get-ChildItem -LiteralPath (Join-Path $vs 'VC/Tools/MSVC') -Directory | Sort-Object Name -Descending | Select-Object -First 1
$dumpbin = Join-Path $msvc.FullName 'bin/Hostx64/x64/dumpbin.exe'
$version = (& $vswhere -latest -products '*' -requires Microsoft.VisualStudio.Component.VC.Tools.x86.x64 -property installationVersion).Split('.')[0]
$generator = if ($version -eq '18') { 'Visual Studio 18 2026' } else { 'Visual Studio 17 2022' }
$build = Join-Path $repo 'artifacts/occt-worker-build'
$out = [IO.Path]::GetFullPath($OutputDirectory, $repo)
& $cmake -S (Join-Path $repo 'native/OcctPreview') -B $build -G $generator -A x64 "-DOpenCASCADE_DIR=$occt/cmake"
if ($LASTEXITCODE) { throw 'OCCT preview configure failed.' }
& $cmake --build $build --config Release --parallel
if ($LASTEXITCODE) { throw 'OCCT preview build failed.' }
New-Item -ItemType Directory -Force -Path $out | Out-Null
$worker = Join-Path $build 'Release/TopSolid.OcctPreview.exe'
Copy-Item -LiteralPath $worker -Destination $out
$bins = @{}
$sdkDlls = Get-ChildItem -LiteralPath (Join-Path $occt 'win64/vc14/bin') -Filter '*.dll'
$thirdPartyDlls = Get-ChildItem -LiteralPath (Join-Path $sdk '3rdparty-vc14-64') -Recurse -Filter '*.dll' |
    Where-Object { $_.FullName -notmatch '(?i)[\\/]debug[\\/]|_debug\.dll$' }
foreach ($dll in @($sdkDlls) + @($thirdPartyDlls)) { if (!$bins.ContainsKey($dll.Name)) { $bins[$dll.Name] = $dll.FullName } }
$seen = @{}; $queue = [Collections.Generic.Queue[string]]::new(); $queue.Enqueue($worker)
while ($queue.Count) {
    $binary = $queue.Dequeue()
    foreach ($line in (& $dumpbin /dependents $binary)) {
        if ($line -match '^\s+([\w.-]+\.dll)\s*$') {
            $name = $Matches[1]
            if ($bins.ContainsKey($name) -and !$seen.ContainsKey($name)) {
                $seen[$name] = $true; Copy-Item -LiteralPath $bins[$name] -Destination $out; $queue.Enqueue($bins[$name])
            }
        }
    }
}
$licenses = Join-Path $out 'licenses'; New-Item -ItemType Directory -Force -Path $licenses | Out-Null
Copy-Item -LiteralPath (Join-Path $occt 'LICENSE_LGPL_21.txt'),(Join-Path $occt 'OCCT_LGPL_EXCEPTION.txt') -Destination $licenses
# Keep upstream license notices for dependencies actually copied; no unrelated Qt/tool runtime is shipped.
foreach ($dependency in @('tbb-2021.13.0-x64','jemalloc-vc14-64','freetype-2.13.3-x64','freeimage-3.18.0-x64')) {
    $path = Join-Path $sdk "3rdparty-vc14-64/$dependency"
    if (Test-Path -LiteralPath $path) {
        Get-ChildItem -LiteralPath $path -File | Where-Object { $_.Name -match '(?i)license|copying' } |
            ForEach-Object { Copy-Item -LiteralPath $_.FullName -Destination (Join-Path $licenses "$dependency-$($_.Name)") }
    }
}
@{
    occtVersion = '7.9.3'
    source = 'https://github.com/Open-Cascade-SAS/OCCT/releases/tag/V7_9_3'
    sdkArchiveSha256 = 'AFBEF3457FBC4A2BDCA0608E0FE284392F51E4C2C42CCBFB9DF7168D8E4EB9B3'
    binaries = @(Get-ChildItem -LiteralPath $out -File | Where-Object { $_.Extension -in @('.dll','.exe') } | Get-FileHash | Select-Object @{n='file';e={Split-Path -Leaf $_.Path}},Hash)
} | ConvertTo-Json -Depth 5 | Set-Content -LiteralPath (Join-Path $out 'runtime-manifest.json') -Encoding utf8
Write-Output "OCCT preview runtime: $out"
