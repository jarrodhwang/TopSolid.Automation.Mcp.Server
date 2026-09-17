Source: https://help.topsolid.com/7.20/en/TopSolid'Automation/api/cam/TopSolid.Cam.NC.Kernel.Automating.IWorldCoordinateSystems.GetWcsSolution.html

Source SHA-256: `6050a78b3cd0a6aaa3e2f758cc51127d0085f1506039f2975b76b0de366cc490`

# Method GetWcsSolution

#### GetWcsSolution(ElementId, int)

Gets the WCS solution.

##### Declaration

```
[OperationContract]
[AvailableSinceVersion(7, 16, 400, 80)]
ElementId GetWcsSolution(ElementId inElementId, int inIndex)
```

##### Parameters

|  Type |  Name |  Description |  
| --- | --- | --- | 
   
|  ElementId |  inElementId |  Identifier of the WCS to analyze.  |  
 
|  int |  inIndex |  Index of the solution.  |  

##### Returns

|  Type |  Description |  
| --- | --- | 
   
|  ElementId |  The solution.  |
