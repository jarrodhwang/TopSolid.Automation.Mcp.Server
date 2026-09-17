Source: https://help.topsolid.com/7.20/en/TopSolid'Automation/api/kernel/TopSolid.Kernel.Automating.IGeometries3D.SetFrameByPointAndTwoDirectionsCreation.html

Source SHA-256: `008e0c2ca62301411f33ffa6931ea2640818866cf6206deffba3b502e17b3ceb`

# Method SetFrameByPointAndTwoDirectionsCreation

#### SetFrameByPointAndTwoDirectionsCreation(ElementId, SmartPoint3D, SmartDirection3D, SmartDirection3D, bool)

Sets data of a frame by point and two frame creation operation

##### Declaration

```
[OperationContract]
[AvailableSinceVersion(7, 20, 400, 140)]
void SetFrameByPointAndTwoDirectionsCreation(ElementId inElementId, SmartPoint3D inOrigin, SmartDirection3D inFirstDirection, SmartDirection3D inSecondDirection, bool isSecondDirectionOY)
```

##### Parameters

|  Type |  Name |  Description |  
| --- | --- | --- | 
   
|  ElementId |  inElementId |  Identifier of the element to analyze  |  
 
|  SmartPoint3D |  inOrigin |  the origin point>  |  
 
|  SmartDirection3D |  inFirstDirection |  the first direction  |  
 
|  SmartDirection3D |  inSecondDirection |  the second direction  |  
 
|  bool |  isSecondDirectionOY |  true if second direction is OY  |  

##### Remarks

StartModification(string, bool) and EnsureIsDirty(ref DocumentId) must be called before calling this method.
 
This method is available since v7.20.
