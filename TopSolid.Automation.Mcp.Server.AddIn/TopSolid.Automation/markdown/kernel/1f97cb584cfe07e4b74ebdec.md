Source: https://help.topsolid.com/7.20/en/TopSolid'Automation/api/kernel/TopSolid.Kernel.Automating.IPdm.CheckIn.html

Source SHA-256: `8f6379e0421abbf0f827e3cefef75e5d3091da9ebaed1c5f58bd6ba4d6d2ff70`

# Method CheckIn

#### CheckIn(PdmObjectId, bool)

Checks-in an object.

##### Declaration

```
[OperationContract]
[AvailableSinceVersion(7, 6, 0, 0)]
void CheckIn(PdmObjectId inObjectId, bool inRecurses)
```

##### Parameters

|  Type |  Name |  Description |  
| --- | --- | --- | 
   
|  PdmObjectId |  inObjectId |  PDM object identifier.  |  
 
|  bool |  inRecurses |  Tells if to recurse on objects contained.  |  

##### Remarks

This method must not be called between StartModification(string, bool) and EndModification(bool, bool) methods calls.
 
This method is available since v7.6.
