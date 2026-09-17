Source: https://help.topsolid.com/7.20/en/TopSolid'Automation/api/cam/TopSolid.Cam.NC.Kernel.Automating.INCFiles.html

Source SHA-256: `7ae00bafba13a3b5ee19539b5c03f176f131ab1058eff4832a7ad6ed09f45061`

# Interface INCFiles

Gives access to NC files, containing NC codes.

###### Namespace: TopSolid.Cam.NC.Kernel.Automating

###### Assembly: TopSolid.Cam.NC.Kernel.Automating.dll

##### Syntax

```
[ServiceContract]
[AvailableSinceVersion(7, 9, 0, 0)]
public interface INCFiles
```

### Methods 

|  Name |  Description |  
| --- | --- | 
   
|   ExportNCCodes(ElementId, string)  |  Save the NC codes of the given file.  |  
 
|   GetNCCodes(ElementId)  |  Get the NC Codes of the given NC file entity.  |  
 
|   GetNCCodesPartial(ElementId, int, int)  |  Get some NC Code lines of the given NC file entity.  |  
 
|   GetNCCodesSize(ElementId)  |  Get the NC Codes lines number of the given NC file entity.  |  
 
|   GetNCFiles(DocumentId)  |  Get the programs contained in the given Iso file.  |  
 
|   GetNCFilesDocuments(DocumentId, string)  |  Get from the Pdm the Iso files created for the given cam file.  |  
 
|   GetOtherNCFileBufferPartial(PdmMinorRevisionId, int, int, ref int)  |  Get buffer of NC Code file.  |  
 
|   GetOtherNCFileCodes(PdmMinorRevisionId)  |  Get all NC Code lines of the given other NC file document.  |  
 
|   GetOtherNCFileCodesPartial(PdmMinorRevisionId, int, int)  |  Get some NC Code lines of the given of the given other NC file document.  |  
 
|   IsNCFile(ElementId)  |  Tells whether an element is a NC file.  |  
 
|   IsNCFilesDocument(DocumentId)  |  Tells whether a document is a NC files document.  |  
 
|   UpdateNCCodes(ElementId, string)  |  Updates the NC codes of the given NC file entity in the document.  |  
 
|   UpdateNCCodesPartial(ElementId, bool, List<string>)  |  Updates the NC codes partialy of the given NC file entity in the document.  |  
 
|   UpdateOtherNCFileBufferPartial(PdmMinorRevisionId, bool, char[], int)  |  Update other NC file using buffer.  |  
 
|   UpdateOtherNCFileCodesPartial(PdmMinorRevisionId, bool, List<string>)  |  Update other NC file using list of strings.  |
