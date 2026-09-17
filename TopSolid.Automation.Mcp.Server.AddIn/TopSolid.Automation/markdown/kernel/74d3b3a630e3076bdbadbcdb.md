Source: https://help.topsolid.com/7.20/en/TopSolid'Automation/api/kernel/TopSolid.Kernel.Automating.IEntities.CreateFolder.html

Source SHA-256: `dcbc43a3acdcbd5216599661b4f10dea2f2342aec9382a8926bc8983e51c04c9`

# Method CreateFolder

#### CreateFolder(ElementId)

Creates a folder entity inside a specified folder entity.

##### Declaration

```
[OperationContract]
[AvailableSinceVersion(7, 10, 0, 0)]
ElementId CreateFolder(ElementId inElementId)
```

##### Parameters

|  Type |  Name |  Description |  
| --- | --- | --- | 
   
|  ElementId |  inElementId |  Identifier of the folder entity to modify.  |  

##### Returns

|  Type |  Description |  
| --- | --- | 
   
|  ElementId |  Folder entity created identifier.  |  

##### Remarks

StartModification(string, bool) and EnsureIsDirty(ref DocumentId) must be called before calling this method.
 
This method is available since v7.10.
