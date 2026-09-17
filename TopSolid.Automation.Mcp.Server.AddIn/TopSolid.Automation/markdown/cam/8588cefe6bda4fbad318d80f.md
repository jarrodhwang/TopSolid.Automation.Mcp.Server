Source: https://help.topsolid.com/7.20/en/TopSolid'Automation/api/cam/TopSolid.Cam.NC.Kernel.Automating.IMachineTools.GetPockets.html

Source SHA-256: `768dff8d55db6b1bc53c40a914257c8b3e86273d3ac81af57076b29e536a2a98`

# Method GetPockets

#### GetPockets(ElementId)

Get pockets of machine element (spindle, turret, magazine).

##### Declaration

```
[OperationContract]
[AvailableSinceVersion(7, 10, 0, 0)]
List<ElementId> GetPockets(ElementId inMachineElementId)
```

##### Parameters

|  Type |  Name |  Description |  
| --- | --- | --- | 
   
|  ElementId |  inMachineElementId |  Machine element identifier.  |  

##### Returns

|  Type |  Description |  
| --- | --- | 
   
|  List<ElementId> |  List of pockets.  |
