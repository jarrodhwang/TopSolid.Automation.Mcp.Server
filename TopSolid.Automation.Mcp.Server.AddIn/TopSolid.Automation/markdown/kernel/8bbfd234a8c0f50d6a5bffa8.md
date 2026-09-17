Source: https://help.topsolid.com/7.20/en/TopSolid'Automation/api/kernel/TopSolid.Kernel.Automating.IFamilies.IsFamily.html

Source SHA-256: `c3b250cf63d81b40bc15b22417be2630cd86dad937c1e2084a5eafbc404729ca`

# Method IsFamily

#### IsFamily(DocumentId)

Tells whether a document is a family document.

##### Declaration

```
[OperationContract]
[AvailableSinceVersion(7, 7, 0, 0)]
bool IsFamily(DocumentId inDocumentId)
```

##### Parameters

|  Type |  Name |  Description |  
| --- | --- | --- | 
   
|  DocumentId |  inDocumentId |  Identifier of the document to analyze.  |  

##### Returns

|  Type |  Description |  
| --- | --- | 
   
|  bool |  The document is a family document.  |  

##### Remarks

This method is available since v7.7.
