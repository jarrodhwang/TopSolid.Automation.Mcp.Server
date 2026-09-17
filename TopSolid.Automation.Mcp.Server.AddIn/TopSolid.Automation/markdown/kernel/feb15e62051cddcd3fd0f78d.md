Source: https://help.topsolid.com/7.20/en/TopSolid'Automation/api/kernel/TopSolid.Kernel.Automating.IElements.GetParent.html

Source SHA-256: `60e96883c2d34a678c566a832ff4e8143fbeaf2026ef20254216b1d68ed83cd8`

# Method GetParent

#### GetParent(ElementId)

Gets the parent operation of an element.

##### Declaration

```
[OperationContract]
[AvailableSinceVersion(7, 6, 0, 0)]
ElementId GetParent(ElementId inElementId)
```

##### Parameters

|  Type |  Name |  Description |  
| --- | --- | --- | 
   
|  ElementId |  inElementId |  Identifier of the element to analyze.  |  

##### Returns

|  Type |  Description |  
| --- | --- | 
   
|  ElementId |  Identifier of the parent operation, or Empty if the element does not have a parent.  |  

##### Remarks

This method is available since v7.6.
