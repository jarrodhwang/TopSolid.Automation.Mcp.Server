Source: https://help.topsolid.com/7.20/en/TopSolid'Automation/api/kernel/TopSolid.Kernel.Automating.IElements.Exists.html

Source SHA-256: `1896b254ca30e087e6eba010d6fba480e46a49a9859321419589df528474a670`

# Method Exists

#### Exists(ElementId)

Tells whether an element still exists.

##### Declaration

```
[OperationContract]
[AvailableSinceVersion(7, 6, 0, 0)]
bool Exists(ElementId inElementId)
```

##### Parameters

|  Type |  Name |  Description |  
| --- | --- | --- | 
   
|  ElementId |  inElementId |  Identifier of the element to analyze.  |  

##### Returns

|  Type |  Description |  
| --- | --- | 
   
|  bool |  The element still exists.  |  

##### Remarks

This method is available since v7.6.
