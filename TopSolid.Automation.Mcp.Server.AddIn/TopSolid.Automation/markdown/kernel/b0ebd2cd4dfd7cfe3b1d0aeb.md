Source: https://help.topsolid.com/7.20/en/TopSolid'Automation/api/kernel/TopSolid.Kernel.Automating.IGeometries2D.SetFrameGeometry.html

Source SHA-256: `c5a586153bbb97c19a3f94137a565c2acfac6a1144c8f5a0134274c639f91fea`

# Method SetFrameGeometry

#### SetFrameGeometry(ElementId, Frame2D)

Sets the geometry of a frame entity.

##### Declaration

```
[OperationContract]
[AvailableSinceVersion(7, 6, 0, 0)]
void SetFrameGeometry(ElementId inElementId, Frame2D inGeometry)
```

##### Parameters

|  Type |  Name |  Description |  
| --- | --- | --- | 
   
|  ElementId |  inElementId |  Identifier of the frame entity to modify.  |  
 
|  Frame2D |  inGeometry |  New frame geometry.  |  

##### Remarks

StartModification(string, bool) and EnsureIsDirty(ref DocumentId) must be called before calling this method.
 
This method is available since v7.6.
