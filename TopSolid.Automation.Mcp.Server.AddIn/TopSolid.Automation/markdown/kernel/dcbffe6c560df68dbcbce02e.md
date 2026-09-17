Source: https://help.topsolid.com/7.20/en/TopSolid'Automation/api/kernel/TopSolid.Kernel.Automating.IParameters.SetColorValue.html

Source SHA-256: `9639626414c072a5a686c1b6f6bc184e8bdc0a79ee288a9e3c119e8fe2c9e2fb`

# Method SetColorValue

#### SetColorValue(ElementId, Color)

Sets the value of a Color parameter.

##### Declaration

```
[OperationContract]
[AvailableSinceVersion(7, 15, 400, 100)]
void SetColorValue(ElementId inElementId, Color inValue)
```

##### Parameters

|  Type |  Name |  Description |  
| --- | --- | --- | 
   
|  ElementId |  inElementId |  Identifier of the parameter to modify.  |  
 
|  Color |  inValue |  New value.  |  

##### Remarks

StartModification(string, bool) and EnsureIsDirty(ref DocumentId) must be called before calling this method.
 
This method is available since v7.15.400.100.
