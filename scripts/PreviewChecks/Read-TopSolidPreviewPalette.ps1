param([string]$OutputFile = "$PSScriptRoot\..\..\artifacts\tool-preview-fix\native-viewport-palette.json")
$ErrorActionPreference = 'Stop'
$sdk = 'C:\Program Files\TOPSOLID\TopSolid 7.20\bin'
$rows = @()
# Read color definitions in this inspection process. Never set ThemeManager or
# load this vendor UI assembly into Studio, and never change the CAD session.
foreach($name in @('TopSolid.Kernel.SX','TopSolid.Kernel.UI','TopSolid.Kernel.WX')) {
    $assembly = [Reflection.Assembly]::LoadFrom((Join-Path $sdk ($name + '.dll')))
    $typeName = if($name -eq 'TopSolid.Kernel.WX'){'TopSolid.Kernel.WX.Drawing.ThemeColors'}else{$name+'.ThemeColors'}
    $type = $assembly.GetType($typeName)
    if($null -eq $type) { continue }
    $instance = [Activator]::CreateInstance($type,$true)
    $actions = $type.GetProperty('ColorActions').GetValue($instance,$null)
    foreach($action in $actions) {
        if($action.FullName -notmatch '(?i)documentbackground') { continue }
        $row = [pscustomobject]@{Name=$action.FullName; Light=$action.LightColor.Argb.ToArgb().ToString('X8'); Dark=$action.DarkColor.Argb.ToArgb().ToString('X8'); Source=$assembly.Location; Version=$assembly.GetName().Version.ToString(); Action=$action.ActionType.ToString(); Since=$action.Version.ToString()}
        $rows += $row
        $row | Format-List
    }
}
$rows | ConvertTo-Json -Depth 3 | Set-Content -LiteralPath $OutputFile -Encoding UTF8
