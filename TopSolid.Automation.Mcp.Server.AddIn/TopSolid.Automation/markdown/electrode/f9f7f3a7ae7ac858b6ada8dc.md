Source: https://help.topsolid.com/7.20/en/TopSolid'Automation/api/electrode/TopSolid.Cad.Electrode.Automating.IElectrodes.GetElectrodeMandrels.html

Source SHA-256: `7f8c592ef84e91caf691d20e800251f8bf61c70256ee7292f77d1200856ced1f`

# Method GetElectrodeMandrels

#### GetElectrodeMandrels(ElementId)

Gets mandrels of an electrode.

##### Declaration

```
[OperationContract]
[AvailableSinceVersion(7, 12, 0, 0)]
List<ElementId> GetElectrodeMandrels(ElementId inElementId)
```

##### Parameters

|  Type |  Name |  Description |  
| --- | --- | --- | 
   
|  ElementId |  inElementId |  Identifier of the electrode to analyze.  |  

##### Returns

|  Type |  Description |  
| --- | --- | 
   
|  List<ElementId> |  Mandrel entities found.  |  

##### Remarks

This method is available since v7.12.
