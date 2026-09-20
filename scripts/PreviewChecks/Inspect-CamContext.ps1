$ErrorActionPreference = 'Stop'
$sdk = 'C:\Program Files\TOPSOLID\TopSolid 7.20\bin'
foreach ($name in @('TopSolid.Kernel.SX','TopSolid.Kernel.Automating','TopSolid.Cad.Design.Automating','TopSolid.Cam.NC.Kernel.Automating')) {
    [void][Reflection.Assembly]::LoadFrom((Join-Path $sdk ($name + '.dll')))
}
$output = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '../../artifacts/cam-context'))
[void][IO.Directory]::CreateDirectory($output)
[void][TopSolid.Kernel.Automating.TopSolidHost]::Connect($false)
[void][TopSolid.Cad.Design.Automating.TopSolidDesignHost]::Connect()
[void][TopSolid.Cam.NC.Kernel.Automating.TopSolidCamHost]::Connect()
try {
    $doc = [TopSolid.Kernel.Automating.TopSolidHost]::Documents.EditedDocument
    $dirty = [TopSolid.Kernel.Automating.TopSolidHost]::Documents.IsDirty($doc)
    $machine = [TopSolid.Cam.NC.Kernel.Automating.TopSolidCamHost]::Documents.GetMachine($doc)
    $report = [ordered]@{ document=$doc.PdmDocumentId; machineId=$machine.Id; machineName=[TopSolid.Kernel.Automating.TopSolidHost]::Elements.GetName($machine) }
    try { $report.representations = @([TopSolid.Cad.Design.Automating.TopSolidDesignHost]::Representations.GetRepresentations($doc) | ForEach-Object { [pscustomobject]@{id=$_.Id; name=[TopSolid.Kernel.Automating.TopSolidHost]::Elements.GetName($_)} }) } catch { $report.representationError=$_.Exception.Message }
    for ($i=0; $i -lt [TopSolid.Kernel.Automating.TopSolidHost]::Application.ExporterCount; $i++) {
        [string]$name=''; [Collections.Generic.List[string]]$extensions=$null
        [TopSolid.Kernel.Automating.TopSolidHost]::Application.GetExporterFileType($i,[ref]$name,[ref]$extensions)
        if ($extensions -notcontains '.glb') { continue }
        foreach ($representation in @('') + @($report.representations | ForEach-Object {$_.id.ToString()})) {
            $visualizations=$true
            $options=[TopSolid.Kernel.Automating.TopSolidHost]::Application.GetExporterOptions($i)
            for ($j=0; $j -lt $options.Count; $j++) {
                $option=$options[$j]
                switch ($option.Key) {
                    'IS_COMPRESSED' {$option.Value='False'}
                    'EXPORTS_CAMERAS' {$option.Value='False'}
                    'EXPORTS_LIGHTS' {$option.Value='False'}
                    'EXPORTS_VISUALIZATIONS' {$option.Value=$visualizations.ToString()}
                    'EXPORTS_TEXTURES' {$option.Value='0'}
                    'REFERENCE_FRAME' {$option.Value=''}
                    'REPRESENTATION_ID' {$option.Value=$representation}
                    'SIMULATION_ID' {$option.Value=''}
                }
                $options[$j]=$option
            }
            $file=Join-Path $output ('context-rep-' + $representation + '.glb')
            [TopSolid.Kernel.Automating.TopSolidHost]::Documents.ExportWithOptions($i,$options,$doc,$file)
            $reader=[IO.BinaryReader]::new([IO.File]::OpenRead($file))
            try { $reader.BaseStream.Position=12; $length=$reader.ReadInt32(); [void]$reader.ReadInt32(); $json=[Text.Encoding]::UTF8.GetString($reader.ReadBytes($length)) | ConvertFrom-Json }
            finally {$reader.Dispose()}
            $report['export'+$representation]=[ordered]@{ bytes=(Get-Item -LiteralPath $file).Length; nodes=@($json.nodes | Select-Object name,mesh,children); meshes=$json.meshes.Count }
        }
        break
    }
    $report.dirtyUnchanged=($dirty -eq [TopSolid.Kernel.Automating.TopSolidHost]::Documents.IsDirty($doc))
    [TopSolid.Kernel.Automating.TopSolidHost]::Documents.zExportToTopglTF($doc,$output,'full-context',$true,$false,$false,$true,$true,$true,$true,0,[TopSolid.Kernel.Automating.Color]::Empty)
    $report.dirtyUnchanged=($dirty -eq [TopSolid.Kernel.Automating.TopSolidHost]::Documents.IsDirty($doc))
    $report.nativeContextFiles=@(Get-ChildItem -LiteralPath $output -Filter '*full-context*' | Select-Object Name,Length)
    $report.activeUnchanged=($doc -eq [TopSolid.Kernel.Automating.TopSolidHost]::Documents.EditedDocument)
    $report | ConvertTo-Json -Depth 10 | Set-Content -LiteralPath (Join-Path $output 'probe.json') -Encoding UTF8
    $report | ConvertTo-Json -Depth 10
} finally {
    [TopSolid.Cam.NC.Kernel.Automating.TopSolidCamHost]::Disconnect()
    [TopSolid.Cad.Design.Automating.TopSolidDesignHost]::Disconnect()
    [TopSolid.Kernel.Automating.TopSolidHost]::Disconnect()
}
