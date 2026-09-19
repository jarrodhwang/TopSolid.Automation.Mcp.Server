param([string]$OutputDirectory = "$PSScriptRoot\..\..\artifacts\tool-preview-fix", [switch]$Export)
$ErrorActionPreference = 'Stop'
$sdk = 'C:\Program Files\TOPSOLID\TopSolid 7.20\bin'
foreach ($name in @('TopSolid.Kernel.SX', 'TopSolid.Kernel.Automating', 'TopSolid.Cam.NC.Kernel.Automating')) {
    [void][Reflection.Assembly]::LoadFrom((Join-Path $sdk ($name + '.dll')))
}
[void][IO.Directory]::CreateDirectory($OutputDirectory)
[void][TopSolid.Kernel.Automating.TopSolidHost]::Connect($false)
[void][TopSolid.Cam.NC.Kernel.Automating.TopSolidCamHost]::Connect()
try {
    $doc = [TopSolid.Kernel.Automating.TopSolidHost]::Documents.EditedDocument
    $dirty = [TopSolid.Kernel.Automating.TopSolidHost]::Documents.IsDirty($doc)
    $rows = @()
    $toolDocument = $null
    foreach ($tool in [TopSolid.Cam.NC.Kernel.Automating.TopSolidCamHost]::Documents.GetTools($doc, $false)) {
        $pdm = [TopSolid.Cam.NC.Kernel.Automating.TopSolidCamHost]::Tools.GetPdmId($tool)
        $targetDoc = [TopSolid.Kernel.Automating.TopSolidHost]::Documents.GetDocument($pdm)
        if ($null -eq $toolDocument) { $toolDocument = $targetDoc }
        $parameters = @([TopSolid.Cam.NC.Kernel.Automating.TopSolidCamHost]::Tools.GetParameters($tool) | Where-Object { $_.Name -match '\.(ToolDefinitionName|PocketDescription|ToolFunction|Document)$' } | ForEach-Object {
            $nativeValue = [TopSolid.Cam.NC.Kernel.Automating.TopSolidCamHost]::Parameters.GetValue($_)
            [pscustomobject]@{ Name=$_.Name; Type=[TopSolid.Cam.NC.Kernel.Automating.TopSolidCamHost]::Parameters.GetType($_).ToString(); Value=[TopSolid.Cam.NC.Kernel.Automating.TopSolidCamHost]::Parameters.ToInvariantStringValue($_); NativeValueType=if($null -ne $nativeValue){$nativeValue.GetType().FullName} }
        })
        $rows += [pscustomobject]@{ Tool=$tool.Id; Pdm=$pdm.ToString(); Document=$targetDoc.PdmDocumentId; Loaded=[TopSolid.Kernel.Automating.TopSolidHost]::Documents.GetDocuments().Contains($targetDoc); Name=[TopSolid.Kernel.Automating.TopSolidHost]::Documents.GetName($targetDoc); Parameters=$parameters }
    }
    $exporters = @()
    for ($i=0; $i -lt [TopSolid.Kernel.Automating.TopSolidHost]::Application.ExporterCount; $i++) {
        [string]$exportName=''; [System.Collections.Generic.List[string]]$extensions=$null
        [TopSolid.Kernel.Automating.TopSolidHost]::Application.GetExporterFileType($i, [ref]$exportName, [ref]$extensions)
        if ($extensions -notcontains '.glb') { continue }
        $options=[TopSolid.Kernel.Automating.TopSolidHost]::Application.GetExporterOptions($i)
        $exporters += [pscustomobject]@{ Index=$i; Name=$exportName; CanCam=[TopSolid.Kernel.Automating.TopSolidHost]::Documents.CanExport($i,$doc); Options=@($options) }
        if ($Export) {
            for ($j=0; $j -lt $options.Count; $j++) {
                $option=$options[$j]
                switch ($option.Key) {
                    'IS_COMPRESSED' { $option.Value='False' }
                    'EXPORTS_CAMERAS' { $option.Value='False' }
                    'EXPORTS_LIGHTS' { $option.Value='False' }
                    'EXPORTS_VISUALIZATIONS' { $option.Value='False' }
                    'IS_Y_UP' { $option.Value='True' }
                    'EXPORTS_TEXTURES' { $option.Value='0' }
                    'REFERENCE_FRAME' { $option.Value='' }
                    'REPRESENTATION_ID' { $option.Value='' }
                    'SIMULATION_ID' { $option.Value='' }
                }
                $options[$j]=$option
            }
            [TopSolid.Kernel.Automating.TopSolidHost]::Documents.ExportWithOptions($i,$options,$toolDocument,(Join-Path $OutputDirectory 'native-tool.glb'))
            [TopSolid.Kernel.Automating.TopSolidHost]::Documents.ExportWithOptions($i,$options,$doc,(Join-Path $OutputDirectory 'native-cam.glb'))
        }
    }
    $report=[ordered]@{ tools=$rows; exporters=$exporters; dirtyUnchanged=($dirty -eq [TopSolid.Kernel.Automating.TopSolidHost]::Documents.IsDirty($doc)); activeUnchanged=($doc -eq [TopSolid.Kernel.Automating.TopSolidHost]::Documents.EditedDocument); parameterMethods=@([TopSolid.Cam.NC.Kernel.Automating.IParameters].GetMethods() | Where-Object Name -match 'Document|Pdm' | ForEach-Object ToString) }
    $report | ConvertTo-Json -Depth 8 | Set-Content -LiteralPath (Join-Path $OutputDirectory 'native-tool-preview-probe.json') -Encoding UTF8
    $rows | Select-Object Tool,Name,Document,Loaded | Format-Table -AutoSize
    [pscustomobject]$report | Select-Object dirtyUnchanged,activeUnchanged,parameterMethods | ConvertTo-Json -Depth 3
} finally {
    [TopSolid.Cam.NC.Kernel.Automating.TopSolidCamHost]::Disconnect()
    [TopSolid.Kernel.Automating.TopSolidHost]::Disconnect()
}
