Source: https://help.topsolid.com/7.20/en/TopSolid'Automation/api/kernel/TopSolid.Kernel.Automating.ISketches2D.CreateArcSegment.html

Source SHA-256: `f355851e2b6d97866e3b1a6213fcd63b4a26cef079f30270137cf03de870b577`

# Method CreateArcSegment

#### CreateArcSegment(ElementItemId, ElementItemId, Point2D, bool)

Creates a new circular arc segment into the ModifiedSketch.

##### Declaration

```
[OperationContract]
[AvailableSinceVersion(7, 7, 201, 80)]
ElementItemId CreateArcSegment(ElementItemId inStartVertexId, ElementItemId inEndVertexId, Point2D inCenter, bool inTurnsClockwise)
```

##### Parameters

|  Type |  Name |  Description |  
| --- | --- | --- | 
   
|  ElementItemId |  inStartVertexId |  Identifier of the start vertex.  |  
 
|  ElementItemId |  inEndVertexId |  Identifier of the end vertex.  |  
 
|  Point2D |  inCenter |  Circle center.  |  
 
|  bool |  inTurnsClockwise |  Turns clockwise around the center when going form start to end if true, anti-clockwise otherwise.  |  

##### Returns

|  Type |  Description |  
| --- | --- | 
   
|  ElementItemId |  Identifier of the created segment.  |  

##### Remarks

StartModification(ElementId) must be called before calling this method.
 
This method is available since v7.7.201.80.
