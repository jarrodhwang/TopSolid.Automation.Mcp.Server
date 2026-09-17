Source: https://help.topsolid.com/7.20/en/TopSolid'Automation/api/kernel/TopSolid.Kernel.Automating.IParameters.GetEnumerationDefinition.html

Source SHA-256: `7fd4d9d2455c67ff696e1edbc7ec9bf9d51a5a4ad00ae80a218496b4f2e828fc`

# Method GetEnumerationDefinition

#### GetEnumerationDefinition(ElementId)

Gets the GUID of the class defining the enumeration of an enumeration parameter.

##### Declaration

```
[OperationContract]
[AvailableSinceVersion(7, 6, 0, 0)]
Guid GetEnumerationDefinition(ElementId inElementId)
```

##### Parameters

|  Type |  Name |  Description |  
| --- | --- | --- | 
   
|  ElementId |  inElementId |  Identifier of the parameter to analyze.  |  

##### Returns

|  Type |  Description |  
| --- | --- | 
   
|  Guid |  Enumeration GUID.  |  

##### Remarks

This method is available since v7.6.
