Source: https://help.topsolid.com/7.20/en/TopSolid'Automation/api/kernel/TopSolid.Kernel.Automating.IParameters.SetRealParameterConstraintsMaximumLimit.html

Source SHA-256: `d03059fbd31b9a60e1e69c986ea1ebcbf6efee5ac7abd580a4fbed0f72ac02c9`

# Method SetRealParameterConstraintsMaximumLimit

#### SetRealParameterConstraintsMaximumLimit(ElementId, RealProperty)

Sets the real parameter constraint maximum limit.

##### Declaration

```
[OperationContract]
[AvailableSinceVersion(7, 17, 0, 0)]
void SetRealParameterConstraintsMaximumLimit(ElementId inElementId, RealProperty inProperty)
```

##### Parameters

|  Type |  Name |  Description |  
| --- | --- | --- | 
   
|  ElementId |  inElementId |  Identifier of the parameter.  |  
 
|  RealProperty |  inProperty |  The new value.  |  

##### Remarks

StartModification(string, bool) and EnsureIsDirty(ref DocumentId) must be called before calling this method.
 
This method is available since v7.17.000.000.
