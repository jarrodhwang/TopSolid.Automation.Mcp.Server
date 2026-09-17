Source: https://help.topsolid.com/7.20/en/TopSolid'Automation/api/kernel/TopSolid.Kernel.Automating.IParameters.PublishInteger.html

Source SHA-256: `e5592bb918147e6f122f598e508eae4de7c711ec0c671716f830c09451452fe9`

# Method PublishInteger

#### PublishInteger(DocumentId, string, SmartInteger)

Creates an integer publishing entity in a document.

##### Declaration

```
[OperationContract]
[AvailableSinceVersion(7, 8, 0, 0)]
ElementId PublishInteger(DocumentId inDocumentId, string inDescription, SmartInteger inDefinition)
```

##### Parameters

|  Type |  Name |  Description |  
| --- | --- | --- | 
   
|  DocumentId |  inDocumentId |  Identifier of the document to modify.  |  
 
|  string |  inDescription |  Publishing description.  |  
 
|  SmartInteger |  inDefinition |  Publishing definition.  |  

##### Returns

|  Type |  Description |  
| --- | --- | 
   
|  ElementId |  Identifier of the created publishing entity.  |  

##### Remarks

StartModification(string, bool) and EnsureIsDirty(ref DocumentId) must be called before calling this method.
 
This method is available since v7.8.
