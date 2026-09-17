Source: https://help.topsolid.com/7.20/en/TopSolid'Automation/api/cae/TopSolid.Cae.Kernel.Automating.IResults.GetMaterialName.html

Source SHA-256: `e6a4211ac0fc0b2f23b078f40b0fa8e0d8289ad00fc5a787c999f8b66e54ec56`

# Method GetMaterialName

#### GetMaterialName(DocumentId)

Gets the material name from a result document.

##### Declaration

```
[OperationContract]
[AvailableSinceVersion(7, 19, 15, 0)]
string GetMaterialName(DocumentId inDocumentId)
```

##### Parameters

|  Type |  Name |  Description |  
| --- | --- | --- | 
   
|  DocumentId |  inDocumentId |  Identifier of the document to analyze.  |  

##### Returns

|  Type |  Description |  
| --- | --- | 
   
|  string |  The name of the material.  |  

##### Remarks

This method is available since v7.19.

##### Examples
 
```
DocumentId docId = TopSolidHost.Documents.EditedDocument;
string materialName = TopSolidCaeHost.Results.GetMaterialName(docId);
```
