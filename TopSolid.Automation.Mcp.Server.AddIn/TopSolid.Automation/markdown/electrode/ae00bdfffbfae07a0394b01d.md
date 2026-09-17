Source: https://help.topsolid.com/7.20/en/TopSolid'Automation/api/electrode/TopSolid.Cad.Electrode.Automating.IElectrodes.GetElectrodeYPosition.html

Source SHA-256: `e65eed314ad25e28eab4754a2ccda94038e93df1eaaa94cc98c8d5d607e9bda9`

# Method GetElectrodeYPosition

#### GetElectrodeYPosition(ElementId, int)

Get the Y position of an electrode at a given position index.

##### Declaration

```
[OperationContract]
[AvailableSinceVersion(7, 12, 0, 0)]
double GetElectrodeYPosition(ElementId inElementId, int inPositionIndex)
```

##### Parameters

|  Type |  Name |  Description |  
| --- | --- | --- | 
   
|  ElementId |  inElementId |  Identifier of the electrode to analyze.  |  
 
|  int |  inPositionIndex |  Position index.  |  

##### Returns

|  Type |  Description |  
| --- | --- | 
   
|  double |  Y position.  |  

##### Remarks

This method is available since v7.12.
