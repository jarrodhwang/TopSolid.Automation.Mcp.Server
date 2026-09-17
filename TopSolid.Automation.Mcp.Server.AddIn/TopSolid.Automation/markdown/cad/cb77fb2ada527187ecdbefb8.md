Source: https://help.topsolid.com/7.20/en/TopSolid'Automation/api/cad/TopSolid.Cad.Design.Automating.IParts.IsPart.html

Source SHA-256: `a61a2f8cd12f4b6bdc0255a487e74f610054f18f0a5e27da93b1fef285cb8109`

# Method IsPart

#### IsPart(DocumentId)

Tells whether a document is a part document.

##### Declaration

```
[OperationContract]
[AvailableSinceVersion(7, 6, 0, 0)]
bool IsPart(DocumentId inDocumentId)
```

##### Parameters

|  Type |  Name |  Description |  
| --- | --- | --- | 
   
|  DocumentId |  inDocumentId |  Identifier of the document to analyze.  |  

##### Returns

|  Type |  Description |  
| --- | --- | 
   
|  bool |  The document is a part document.  |  

##### Remarks

This method is available since v7.6.
