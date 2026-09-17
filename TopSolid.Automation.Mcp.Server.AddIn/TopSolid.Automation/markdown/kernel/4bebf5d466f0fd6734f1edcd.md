Source: https://help.topsolid.com/7.20/en/TopSolid'Automation/api/kernel/TopSolid.Kernel.Automating.IGeometries3D.GetAbsoluteZAxis.html

Source SHA-256: `ef024a1e70b129032aee99d0a5612ba2a7bcc29b8325aae636daaf4e3de9ed05`

# Method GetAbsoluteZAxis

#### GetAbsoluteZAxis(DocumentId)

Gets the absolute Z axis entity of a document.

##### Declaration

```
[OperationContract]
[AvailableSinceVersion(7, 6, 0, 0)]
ElementId GetAbsoluteZAxis(DocumentId inDocumentId)
```

##### Parameters

|  Type |  Name |  Description |  
| --- | --- | --- | 
   
|  DocumentId |  inDocumentId |  Identifier of the document to analyze.  |  

##### Returns

|  Type |  Description |  
| --- | --- | 
   
|  ElementId |  Identifier of the absolute Z axis entity found.  |  

##### Remarks

This method is available since v7.6.
