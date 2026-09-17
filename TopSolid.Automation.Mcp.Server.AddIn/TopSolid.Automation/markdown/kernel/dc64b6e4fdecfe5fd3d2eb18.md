Source: https://help.topsolid.com/7.20/en/TopSolid'Automation/api/kernel/TopSolid.Kernel.Automating.ISketches2D.GetSegmentPoint.html

Source SHA-256: `2709ac73d8c6878338dbd568bbaa78a4be68b1c6fe8944af12e4e481f6e39bf8`

# Method GetSegmentPoint

#### GetSegmentPoint(ElementItemId, double)

Gets a point on a segment at a specified parametric value.

##### Declaration

```
[OperationContract]
[AvailableSinceVersion(7, 7, 0, 0)]
Point2D GetSegmentPoint(ElementItemId inSegmentId, double inT)
```

##### Parameters

|  Type |  Name |  Description |  
| --- | --- | --- | 
   
|  ElementItemId |  inSegmentId |  Identifier of the segment to analyze.  |  
 
|  double |  inT |  Parameter within segment parametric range.  |  

##### Returns

|  Type |  Description |  
| --- | --- | 
   
|  Point2D |  Point on the segment at the specified parametric value.  |  

##### Remarks

This method is available since v7.7.
