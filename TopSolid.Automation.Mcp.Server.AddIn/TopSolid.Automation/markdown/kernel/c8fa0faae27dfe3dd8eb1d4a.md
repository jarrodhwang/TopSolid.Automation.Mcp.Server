Source: https://help.topsolid.com/7.20/en/TopSolid'Automation/api/kernel/TopSolid.Kernel.Automating.IEntities.SetCurrentSet.html

Source SHA-256: `1c78ffe9f4900cadea68bd4c0adecf04775919b2d211e1576384e8779d0fe5b9`

# Method SetCurrentSet

#### SetCurrentSet(DocumentId, ElementId)

Sets the current set definition entity in a document.

##### Declaration

```
[OperationContract]
[AvailableSinceVersion(7, 14, 300, 140)]
void SetCurrentSet(DocumentId inDocumentId, ElementId inElementId)
```

##### Parameters

|  Type |  Name |  Description |  
| --- | --- | --- | 
   
|  DocumentId |  inDocumentId |  Identifier of the document to set the current set.  |  
 
|  ElementId |  inElementId |  Identifiers of the set definition entity.  |  

##### Remarks

StartModification(string, bool) and EnsureIsDirty(ref DocumentId) must be called before calling this method.
 
This method is available since v7.14.300.140.
