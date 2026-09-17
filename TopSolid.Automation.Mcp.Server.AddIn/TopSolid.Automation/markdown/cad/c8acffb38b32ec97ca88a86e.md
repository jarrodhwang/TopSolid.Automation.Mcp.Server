Source: https://help.topsolid.com/7.20/en/TopSolid'Automation/api/cad/TopSolid.Cad.Design.Automating.IAssemblies.GetFunctionsInheritingOperation.html

Source SHA-256: `4560d1b055dd36c6d7159a28609edf34313e783781cf7bc822d31d42ae81ebf8`

# Method GetFunctionsInheritingOperation

#### GetFunctionsInheritingOperation(ElementId, out ElementId, out List<ElementId>, out List<ElementId>)

Gets a functions inheriting operation.

##### Declaration

```
[OperationContract]
[AvailableSinceVersion(7, 8, 0, 0)]
void GetFunctionsInheritingOperation(ElementId inOperationId, out ElementId outOccurrenceId, out List<ElementId> outFunctionIds, out List<ElementId> outChildIds)
```

##### Parameters

|  Type |  Name |  Description |  
| --- | --- | --- | 
   
|  ElementId |  inOperationId |  Identifier of the functions inheriting operation to analyze.  |  
 
|  ElementId |  outOccurrenceId |  Identifier of the occurrence of the part or assembly providing the functions to inherit.  |  
 
|  List<ElementId> |  outFunctionIds |  Identifiers of the specified function entities to inherit from in the occurrence definition document.  |  
 
|  List<ElementId> |  outChildIds |  Identifiers of the children provided functions.  |  

##### Remarks

This method is available since v7.8.
