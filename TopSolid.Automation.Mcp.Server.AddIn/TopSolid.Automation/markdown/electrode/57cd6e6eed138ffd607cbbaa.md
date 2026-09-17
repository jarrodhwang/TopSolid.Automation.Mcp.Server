Source: https://help.topsolid.com/7.20/en/TopSolid'Automation/api/electrode/TopSolid.Cad.Electrode.Automating.IElectrodes.IsElectrodeTheoreticalPosition.html

Source SHA-256: `e86acbc505ba5f5eab52879380a5ac194f46836a68d3ec25a73b9af756981622`

# Method IsElectrodeTheoreticalPosition

#### IsElectrodeTheoreticalPosition(ElementId)

Tells whether a element is a theoretical position of an electrode.

##### Declaration

```
[OperationContract]
[AvailableSinceVersion(7, 12, 0, 0)]
bool IsElectrodeTheoreticalPosition(ElementId inElementId)
```

##### Parameters

|  Type |  Name |  Description |  
| --- | --- | --- | 
   
|  ElementId |  inElementId |  Identifier of the electrode to analyze.  |  

##### Returns

|  Type |  Description |  
| --- | --- | 
   
|  bool |  The element is a theoretical position of an electrode.  |  

##### Remarks

This method is available since v7.12.
