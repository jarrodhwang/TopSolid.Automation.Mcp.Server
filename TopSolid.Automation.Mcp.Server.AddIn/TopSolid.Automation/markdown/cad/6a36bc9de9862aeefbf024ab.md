Source: https://help.topsolid.com/7.20/en/TopSolid'Automation/api/cad/TopSolid.Cad.Design.Automating.IAssemblies.InheritOccurrenceFunctions.html

Source SHA-256: `9220ff4c2ddf7c859070c3899cca28feb43a5b7c8514b07738861a4c418384ce`

# Method InheritOccurrenceFunctions

#### InheritOccurrenceFunctions(ElementId, List<ElementId>)

Provides functions by inheriting from the ones provided in a part or assembly occurrence.

##### Declaration

```
[OperationContract]
[AvailableSinceVersion(7, 8, 0, 0)]
ElementId InheritOccurrenceFunctions(ElementId inOccurrenceId, List<ElementId> inFunctionIds)
```

##### Parameters

|  Type |  Name |  Description |  
| --- | --- | --- | 
   
|  ElementId |  inOccurrenceId |  Identifier of the occurrence of the part or assembly providing the functions to inherit.  |  
 
|  List<ElementId> |  inFunctionIds |  Identifiers of the specified function entities to inherit from in the occurrence definition document.  |  

##### Returns

|  Type |  Description |  
| --- | --- | 
   
|  ElementId |  Identifier of the created functions inheriting operation.  |  

##### Remarks

StartModification(string, bool) and EnsureIsDirty(ref DocumentId) must be called before calling this method.
 
This method is available since v7.8.
