param([string]$AssemblyDirectory = (Join-Path $PSScriptRoot '../TopSolid.Automation'))
$ErrorActionPreference = 'Stop'
$taskAssemblyDirectory = (Resolve-Path -LiteralPath $AssemblyDirectory).Path
Get-ChildItem -LiteralPath $taskAssemblyDirectory -Filter '*.dll' | ForEach-Object {
    [Reflection.AssemblyName]::GetAssemblyName($_.FullName).FullName
}
$taskKernel = [Reflection.Assembly]::LoadFrom((Join-Path $taskAssemblyDirectory 'TopSolid.Kernel.Automating.dll'))
$taskMemberNames = @{
    TopSolidHost = @('Connect','IsConnected','Application','Documents','ClientVersion','Disconnect')
    IApplication = @('Version')
    IDocuments = @('EditedDocument','Exists','GetName','GetTypeFullName','GetTypeGuid','IsDirty')
    DocumentId = @('.ctor','IsEmpty','PdmDocumentId')
}
foreach ($taskTypeName in @('TopSolidHost','IApplication','IDocuments','DocumentId')) {
    $taskType = $taskKernel.GetType('TopSolid.Kernel.Automating.' + $taskTypeName, $true)
    Write-Output $taskType.FullName
    $taskType.GetMembers([Reflection.BindingFlags]'Public,Static,Instance,DeclaredOnly') | Where-Object {
        $taskMemberNames[$taskTypeName] -contains $_.Name
    } | ForEach-Object { $_.ToString() }
}
