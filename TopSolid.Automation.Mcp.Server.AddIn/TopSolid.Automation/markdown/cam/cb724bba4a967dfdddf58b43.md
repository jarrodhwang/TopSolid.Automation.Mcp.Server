Source: https://help.topsolid.com/7.20/en/TopSolid'Automation/api/cam/TopSolid.Cam.NC.Kernel.Automating.IOperations.SetCuttingConditionsAbacus.html

Source SHA-256: `0551851767d6503a1e6b5d1a7b5258bf1a5e93348c0c92e39dab91f19239387e`

# Method SetCuttingConditionsAbacus

#### SetCuttingConditionsAbacus(ElementId, ElementId)

Sets the cutting condition abacus for an operation.

##### Declaration

```
[OperationContract]
[AvailableSinceVersion(7, 14, 300, 144)]
void SetCuttingConditionsAbacus(ElementId inElementId, ElementId inAbacusId)
```

##### Parameters

|  Type |  Name |  Description |  
| --- | --- | --- | 
   
|  ElementId |  inElementId |  Identifier of the operation to modify.  |  
 
|  ElementId |  inAbacusId |  Identifier of the abacus to use.  |
