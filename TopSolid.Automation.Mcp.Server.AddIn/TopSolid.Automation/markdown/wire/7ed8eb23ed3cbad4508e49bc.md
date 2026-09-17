Source: https://help.topsolid.com/7.20/en/TopSolid'Automation/api/wire/TopSolid.Cam.NC.Wire.Automating.IWireTechnologies.GetName.html

Source SHA-256: `c9bcd81ac6f9df11a6c88e5be0f8fae387b0254cf4592996567587972c503d05`

# Method GetName

#### GetName(ElementId)

Get technology name.

##### Declaration

```
[OperationContract]
[AvailableSinceVersion(7, 11, 300, 160)]
SmartText GetName(ElementId inTechnologyId)
```

##### Parameters

|  Type |  Name |  Description |  
| --- | --- | --- | 
   
|  ElementId |  inTechnologyId |  Identifier of the technology to modify.  |  

##### Returns

|  Type |  Description |  
| --- | --- | 
   
|  SmartText |  Technology name.  |
