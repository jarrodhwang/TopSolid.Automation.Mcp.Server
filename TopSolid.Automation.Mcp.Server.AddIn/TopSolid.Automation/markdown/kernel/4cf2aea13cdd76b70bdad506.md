Source: https://help.topsolid.com/7.20/en/TopSolid'Automation/api/kernel/TopSolid.Kernel.Automating.ISketches2D.GetSegmentCurveType.html

Source SHA-256: `d9a0aa6252979e3907441b4b771c9bc2920514226279975a9afdb2b1b7a8810d`

# Method GetSegmentCurveType

#### GetSegmentCurveType(ElementItemId)

Gets the type of the curve attached to a segment.

##### Declaration

```
[OperationContract]
[AvailableSinceVersion(7, 7, 0, 0)]
CurveType GetSegmentCurveType(ElementItemId inSegmentId)
```

##### Parameters

|  Type |  Name |  Description |  
| --- | --- | --- | 
   
|  ElementItemId |  inSegmentId |  Identifier of the segment to analyze.  |  

##### Returns

|  Type |  Description |  
| --- | --- | 
   
|  CurveType |  Type of the curve attached to the segment.  |  

##### Remarks

This method is available since v7.7.
