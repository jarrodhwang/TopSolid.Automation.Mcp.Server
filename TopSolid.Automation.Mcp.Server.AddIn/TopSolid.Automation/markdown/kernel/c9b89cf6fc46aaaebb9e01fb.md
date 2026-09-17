Source: https://help.topsolid.com/7.20/en/TopSolid'Automation/api/kernel/TopSolid.Kernel.Automating.IDocuments.IsDirty.html

Source SHA-256: `858a296d31d2396ba2b21fd6319575afbf372bde7c4aee9041be2aeb48d14788`

# Method IsDirty

#### IsDirty(DocumentId)

Tells whether a document is dirty.

##### Declaration

```
[OperationContract]
[AvailableSinceVersion(7, 6, 0, 0)]
bool IsDirty(DocumentId inDocumentId)
```

##### Parameters

|  Type |  Name |  Description |  
| --- | --- | --- | 
   
|  DocumentId |  inDocumentId |  Identifier of the document to analyze.  |  

##### Returns

|  Type |  Description |  
| --- | --- | 
   
|  bool |  The document is dirty.  |  

##### Remarks

A document is said dirty when it has been modified since it was last saved.
 
This method is available since v7.6.
