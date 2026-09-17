Source: https://help.topsolid.com/7.20/en/TopSolid'Automation/api/kernel/TopSolid.Kernel.Automating.ISketches3D.GetSegmentVertices.html

Source SHA-256: `fb2d5f94aa4f376d6c5930df5050fd1c5a54521742124e1bf428ee0a59d6058f`

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
