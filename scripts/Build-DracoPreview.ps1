param([string]$DracoRoot='artifacts/occt-sdk/7.9.3/3rdparty-vc14-64/draco-1.4.1-vc14-64')
$ErrorActionPreference='Stop'
$repo=Split-Path -Parent $PSScriptRoot
$sdkPath=if([IO.Path]::IsPathRooted($DracoRoot)){$DracoRoot}else{Join-Path $repo $DracoRoot}
$draco=(Resolve-Path -LiteralPath $sdkPath).Path
$vswhere=Join-Path ${env:ProgramFiles(x86)} 'Microsoft Visual Studio/Installer/vswhere.exe'
$vs=& $vswhere -latest -products '*' -requires Microsoft.VisualStudio.Component.VC.Tools.x86.x64 -property installationPath
$cmake=Join-Path $vs 'Common7/IDE/CommonExtensions/Microsoft/CMake/CMake/bin/cmake.exe'
$major=(& $vswhere -latest -products '*' -requires Microsoft.VisualStudio.Component.VC.Tools.x86.x64 -property installationVersion).Split('.')[0]
$generator=if($major -eq '18'){'Visual Studio 18 2026'}else{'Visual Studio 17 2022'}
$build=Join-Path $repo 'artifacts/draco-preview-build'
& $cmake -S (Join-Path $repo 'native/DracoPreview') -B $build -G $generator -A x64 "-DDRACO_ROOT=$draco"
if($LASTEXITCODE){throw 'Draco configure failed'}
& $cmake --build $build --config Release
if($LASTEXITCODE){throw 'Draco build failed'}
$out=Join-Path $repo 'artifacts/draco-preview/runtime'
[void][IO.Directory]::CreateDirectory($out)
Copy-Item -LiteralPath (Join-Path $build 'Release/TopSolid.DracoPreview.exe') -Destination $out
Copy-Item -LiteralPath (Join-Path $repo 'native/DracoPreview/LICENSE.draco') -Destination $out
Get-ChildItem -LiteralPath (Join-Path $draco 'bin') -Filter '*.dll' -ErrorAction SilentlyContinue | Copy-Item -Destination $out
