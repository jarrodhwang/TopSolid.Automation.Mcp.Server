Source: https://help.topsolid.com/7.20/en/TopSolid'Automation/api/kernel/TopSolid.Kernel.Automating.ISketches2D.GetSegmentRange.html

Source SHA-256: `8c2999b429bce84e6cad403d544aa0e9706b9998e13dab54773da77b294b78f1`

# Method GetSegmentRange

#### GetSegmentRange(ElementItemId, out double, out double)

Gets the parametric range of a segment.

##### Declaration

```
[OperationContract]
[AvailableSinceVersion(7, 7, 0, 0)]
void GetSegmentRange(ElementItemId inSegmentId, out double outTMin, out double outTMax)
```

##### Parameters

|  Type |  Name |  Description |  
| --- | --- | --- | 
   
|  ElementItemId |  inSegmentId |  Identifier of the segment to analyze.  |  
 
|  double |  outTMin |  Minimum parametric value.  |  
 
|  double |  outTMax |  Maximum parametric value.  |  

##### Remarks

This method is available since v7.7.
