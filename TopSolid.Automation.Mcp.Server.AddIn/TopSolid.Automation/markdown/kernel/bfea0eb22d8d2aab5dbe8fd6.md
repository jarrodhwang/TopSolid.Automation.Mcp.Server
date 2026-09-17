Source: https://help.topsolid.com/7.20/en/TopSolid'Automation/api/kernel/TopSolid.Kernel.Automating.IShapes.GetEdgePoint.html

Source SHA-256: `def42ff4b26825b6c3fb2df9484f2fa8bac1d3d1b390d832ecbf7a7c4c194300`

# Method GetEdgePoint

#### GetEdgePoint(ElementItemId, double)

Gets a point on the curve attached to an edge at a specified parametric value.

##### Declaration

```
[OperationContract]
[AvailableSinceVersion(7, 7, 0, 0)]
Point3D GetEdgePoint(ElementItemId inEdgeId, double inT)
```

##### Parameters

|  Type |  Name |  Description |  
| --- | --- | --- | 
   
|  ElementItemId |  inEdgeId |  Identifier of the edge to analyze.  |  
 
|  double |  inT |  Parameter within edge curve parametric range.  |  

##### Returns

|  Type |  Description |  
| --- | --- | 
   
|  Point3D |  Point on the edge curve at the specified parametric value.  |  

##### Remarks

This method is available since v7.7.
