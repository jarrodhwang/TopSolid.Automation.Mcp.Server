Source: https://help.topsolid.com/7.20/en/TopSolid'Automation/api/cam/TopSolid.Cam.NC.Kernel.Automating.IWorldCoordinateSystems.GetWcsSolutionFrameGeometry.html

Source SHA-256: `a6effa5178bf573f0c7fc5ec2a4828b7a03aa9589ce5b75ef89c3d4e7e36cbbd`

# Method GetWcsSolutionFrameGeometry

#### GetWcsSolutionFrameGeometry(ElementId)

Gets the WCS solution frame.

##### Declaration

```
[OperationContract]
[AvailableSinceVersion(7, 16, 400, 80)]
Frame3D GetWcsSolutionFrameGeometry(ElementId inSolutionId)
```

##### Parameters

|  Type |  Name |  Description |  
| --- | --- | --- | 
   
|  ElementId |  inSolutionId |  Identifier of the solution to analyze.  |  

##### Returns

|  Type |  Description |  
| --- | --- | 
   
|  Frame3D |  The Wcs solution frame geometry.  |
