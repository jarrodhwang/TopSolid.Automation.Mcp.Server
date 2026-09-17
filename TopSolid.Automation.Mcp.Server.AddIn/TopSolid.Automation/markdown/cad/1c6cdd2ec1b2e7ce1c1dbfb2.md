Source: https://help.topsolid.com/7.20/en/TopSolid'Automation/api/cad/TopSolid.Cad.Design.Automating.IBoms.GetColumnTitle.html

Source SHA-256: `4ede8fbb312eb5190d49329f2c8a0a295ba2418cf45d9405e67cf97893f9e1d2`

# Method GetColumnTitle

#### GetColumnTitle(DocumentId, int)

Gets the title of a column of a BOM.

##### Declaration

```
[OperationContract]
[AvailableSinceVersion(7, 12, 0, 0)]
string GetColumnTitle(DocumentId inDocumentId, int inColumnIx)
```

##### Parameters

|  Type |  Name |  Description |  
| --- | --- | --- | 
   
|  DocumentId |  inDocumentId |  Identifier of the BOM document to analyze.  |  
 
|  int |  inColumnIx |  Index of the column to analyze, within [0,GetColumnCount(DocumentId)-1].  |  

##### Returns

|  Type |  Description |  
| --- | --- | 
   
|  string |  Title of the column.  |  

##### Remarks

This method is available since v7.12.
