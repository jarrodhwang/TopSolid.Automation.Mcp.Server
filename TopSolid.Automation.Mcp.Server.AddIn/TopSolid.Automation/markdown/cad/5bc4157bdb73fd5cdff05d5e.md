Source: https://help.topsolid.com/7.20/en/TopSolid'Automation/api/cad/TopSolid.Cad.Design.Automating.IFeatures.GetDrillingSpotFacingPrimitive.html

Source SHA-256: `9234c77e43ac43d05ae7cbc197a08d6784a291f479746f5adb5b33448e7a6115`

# Method GetDrillingSpotFacingPrimitive

#### GetDrillingSpotFacingPrimitive(ElementId, int, out Real, out Real, out bool, out Real)

Get a drilling spot facing primitive information.

##### Declaration

```
[OperationContract]
[AvailableSinceVersion(7, 14, 0, 0)]
void GetDrillingSpotFacingPrimitive(ElementId inFeatureId, int inIndex, out Real depth, out Real diameter, out bool isOpposed, out Real position)
```

##### Parameters

|  Type |  Name |  Description |  
| --- | --- | --- | 
   
|  ElementId |  inFeatureId |  Identifiers of the element of the primitive to analyse.  |  
 
|  int |  inIndex |  Idex of the primitive to analyse.  |  
 
|  Real |  depth |  Detph of the spot facing primitive.  |  
 
|  Real |  diameter |  Diameter of the spot facing primitive.  |  
 
|  bool |  isOpposed |  Tells wether the spot facing primitive is opposed.  |  
 
|  Real |  position |  Position of the spot facing primitive.  |  

##### Remarks

This method is available since v7.14.
