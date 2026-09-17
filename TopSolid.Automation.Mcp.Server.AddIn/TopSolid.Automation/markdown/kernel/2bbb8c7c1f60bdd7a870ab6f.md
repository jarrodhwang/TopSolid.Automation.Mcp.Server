Source: https://help.topsolid.com/7.20/en/TopSolid'Automation/api/kernel/TopSolid.Kernel.Automating.IGeometries3D.GetProfiles.html

Source SHA-256: `32ebd6e68d0f2538d77161539d32b99f9a49b9008fc35d8c60b5ea5bbe899247`

# Method GetProfiles

#### GetProfiles(DocumentId)

Gets the profile entities that are in the profile folder of a document.

##### Declaration

```
[OperationContract]
[AvailableSinceVersion(7, 20, 400, 180)]
List<ElementId> GetProfiles(DocumentId inDocumentId)
```

##### Parameters

|  Type |  Name |  Description |  
| --- | --- | --- | 
   
|  DocumentId |  inDocumentId |  Identifier of the document to analyze.  |  

##### Returns

|  Type |  Description |  
| --- | --- | 
   
|  List<ElementId> |  Profile entities found.  |
