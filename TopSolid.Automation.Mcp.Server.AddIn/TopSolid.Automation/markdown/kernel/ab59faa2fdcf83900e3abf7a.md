Source: https://help.topsolid.com/7.20/en/TopSolid'Automation/api/kernel/TopSolid.Kernel.Automating.ISketches2D.GetSegmentVertices.html

Source SHA-256: `7bfd78a213da6f2bd549e70fdc1ddd40ec6afeb9365cc2a0e3547926ed4b1a47`

# Method GetSegmentVertices

#### GetSegmentVertices(ElementItemId, out ElementItemId, out ElementItemId)

Gets the vertices of a segment.

##### Declaration

```
[OperationContract]
[AvailableSinceVersion(7, 7, 0, 0)]
void GetSegmentVertices(ElementItemId inSegmentId, out ElementItemId outStartVertexId, out ElementItemId outEndVertexId)
```

##### Parameters

|  Type |  Name |  Description |  
| --- | --- | --- | 
   
|  ElementItemId |  inSegmentId |  Identifier of the segment to analyze.  |  
 
|  ElementItemId |  outStartVertexId |  Identifier of the start vertex, or Empty if none.  |  
 
|  ElementItemId |  outEndVertexId |  Identifier of the end vertex, or Empty if none.  |  

##### Remarks

This method is available since v7.7.
