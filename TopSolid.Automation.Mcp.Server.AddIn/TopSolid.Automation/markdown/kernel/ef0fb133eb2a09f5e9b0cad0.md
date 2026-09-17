Source: https://help.topsolid.com/7.20/en/TopSolid'Automation/api/kernel/TopSolid.Kernel.Automating.ISketches2D.CreateRevolvedSilhouette.html

Source SHA-256: `8a94f37a9d92e1f1c3ce11c5940d3ee09751438cec371f047f4f9b51da8f5b44`

# Method CreateRevolvedSilhouette

#### CreateRevolvedSilhouette(SmartShape, SmartAxis3D, bool)

Creates the revolved silhouette of a shape.

##### Declaration

```
[OperationContract]
[AvailableSinceVersion(7, 15, 400, 100)]
ElementId CreateRevolvedSilhouette(SmartShape inShape, SmartAxis3D inAxis, bool inMerge)
```

##### Parameters

|  Type |  Name |  Description |  
| --- | --- | --- | 
   
|  SmartShape |  inShape |  Identifier of the shape to analyze.  |  
 
|  SmartAxis3D |  inAxis |  Identifier of the revolution axis.  |  
 
|  bool |  inMerge |  Tells whether the silhouette must be merged.  |  

##### Returns

|  Type |  Description |  
| --- | --- | 
   
|  ElementId |  Identifier of the revolved silhouette operation.  |  

##### Remarks

StartModification(ElementId) must be called before calling this method.
 
This method must be called inside a Building Operation.
 
This method is available since v7.15.400.100.
