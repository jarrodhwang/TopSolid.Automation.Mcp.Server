Source: https://help.topsolid.com/7.20/en/TopSolid'Automation/api/kernel/TopSolid.Kernel.Automating.IShapes.GetShapeVolume.html

Source SHA-256: `02044bbd1d41e9d7c30ed938bbd246e83cc63b29a0bdc039bd2275ca43044a15`

# Method GetShapeVolume

#### GetShapeVolume(ElementId)

Gets the volume of a shape entity.

##### Declaration

```
[OperationContract]
[AvailableSinceVersion(7, 14, 0, 0)]
double GetShapeVolume(ElementId inElementId)
```

##### Parameters

|  Type |  Name |  Description |  
| --- | --- | --- | 
   
|  ElementId |  inElementId |  Identifier of the shape entity to analyze.  |  

##### Returns

|  Type |  Description |  
| --- | --- | 
   
|  double |  Type of the shape.  |  

##### Remarks

This method is available since v7.14.
