Source: https://help.topsolid.com/7.20/en/TopSolid'Automation/api/kernel/TopSolid.Kernel.Automating.IOperations.GetChild.html

Source SHA-256: `e2e27ecd5af83e99847a2adc72bfde86352398d7c8a7462834fa7a9ae20261f7`

# Method GetChild

#### GetChild(ElementId)

Gets the child element of an operation.

##### Declaration

```
[OperationContract]
[AvailableSinceVersion(7, 6, 0, 0)]
ElementId GetChild(ElementId inElementId)
```

##### Parameters

|  Type |  Name |  Description |  
| --- | --- | --- | 
   
|  ElementId |  inElementId |  Identifier of the operation to analyze.  |  

##### Returns

|  Type |  Description |  
| --- | --- | 
   
|  ElementId |  Identifier of the child element found, or Empty if none.  |  

##### Remarks

The operation must not have several children, otherwise an exception is thrown.
 
This method is available since v7.6.
