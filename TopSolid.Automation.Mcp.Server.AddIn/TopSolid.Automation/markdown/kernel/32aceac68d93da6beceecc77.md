Source: https://help.topsolid.com/7.20/en/TopSolid'Automation/api/kernel/TopSolid.Kernel.Automating.IShapes.IsFaceReversed.html

Source SHA-256: `d0c8f25fe953a75e2081bd64756a6fa44c6145210b45bd7be771611403f145fa`

# Method IsFaceReversed

#### IsFaceReversed(ElementItemId)

Tells whether a face is reversed.

##### Declaration

```
[OperationContract]
[AvailableSinceVersion(7, 7, 0, 0)]
bool IsFaceReversed(ElementItemId inFaceId)
```

##### Parameters

|  Type |  Name |  Description |  
| --- | --- | --- | 
   
|  ElementItemId |  inFaceId |  Identifier of the face to analyze.  |  

##### Returns

|  Type |  Description |  
| --- | --- | 
   
|  bool |  Whether the orientation of the face is the opposite to the parametric orientation of its attached surface.  |  

##### Remarks

This method is available since v7.7.
