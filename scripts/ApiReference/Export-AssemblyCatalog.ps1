param(
    [string]$AssemblyDirectory = 'C:\Program Files\TOPSOLID\TopSolid 7.20\bin',
    [string]$OutputPath = (Join-Path $PSScriptRoot '../../docs/api/assemblies-7.20.json')
)
$ErrorActionPreference = 'Stop'
$catalog = @()
$names = @('TopSolid.Kernel.Automating','TopSolid.Cad.Design.Automating','TopSolid.Cad.Drafting.Automating','TopSolid.Cam.NC.Kernel.Automating','TopSolid.Cad.Electrode.Automating','TopSolid.Pdm.Explorer.Automating','TopSolid.Cae.Kernel.Automating')
foreach ($name in $names) {
    $file = Join-Path $AssemblyDirectory ($name + '.dll')
    if (!(Test-Path -LiteralPath $file)) { continue }
    $assembly = [Reflection.Assembly]::LoadFrom($file)
    $types = @()
    foreach ($type in ($assembly.GetExportedTypes() | Sort-Object FullName)) {
        $methods = @($type.GetMethods([Reflection.BindingFlags]'Public,Static,Instance,DeclaredOnly') | Where-Object { !$_.IsSpecialName } | ForEach-Object {
            $method = $_
            [ordered]@{name=$method.Name; returnType=$method.ReturnType.FullName; signature=$method.ToString(); parameters=@($method.GetParameters() | ForEach-Object { [ordered]@{name=$_.Name; type=$_.ParameterType.FullName; isOut=$_.IsOut; isByRef=$_.ParameterType.IsByRef} })}
        })
        $properties = @($type.GetProperties([Reflection.BindingFlags]'Public,Static,Instance,DeclaredOnly') | ForEach-Object { [ordered]@{name=$_.Name; type=$_.PropertyType.FullName; canRead=$_.CanRead; canWrite=$_.CanWrite} })
        $fields = @($type.GetFields([Reflection.BindingFlags]'Public,Static,Instance,DeclaredOnly') | ForEach-Object { [ordered]@{name=$_.Name;type=$_.FieldType.FullName;isStatic=$_.IsStatic} })
        $types += [ordered]@{name=$type.FullName; kind=$(if($type.IsInterface){'interface'}elseif($type.IsEnum){'enum'}elseif($type.IsValueType){'struct'}else{'class'}); constructors=@($type.GetConstructors() | ForEach-Object {$_.ToString()}); methods=$methods; properties=$properties; fields=$fields; enumValues=$(if($type.IsEnum){@([Enum]::GetNames($type))}else{@()})}
    }
    $catalog += [ordered]@{name=$name;version=$assembly.GetName().Version.ToString();sha256=(Get-FileHash -LiteralPath $file -Algorithm SHA256).Hash;types=$types}
}
$catalog | ConvertTo-Json -Depth 16 | Set-Content -LiteralPath $OutputPath -Encoding UTF8
Write-Output ('Exported {0} assemblies, {1} types to {2}' -f $catalog.Count,(@($catalog | ForEach-Object {$_.types})).Count,$OutputPath)
