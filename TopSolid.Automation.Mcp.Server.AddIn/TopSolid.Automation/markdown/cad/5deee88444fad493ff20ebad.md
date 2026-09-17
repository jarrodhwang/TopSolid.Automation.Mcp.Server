Source: https://help.topsolid.com/7.20/en/TopSolid'Automation/api/cad/TopSolid.Cad.Design.Automating.IFeatures.GetDrillingHolePrimitive.html

Source SHA-256: `574d3408f9507664b1921d77b39f6bdcca13d919be22619e15f70be3210fb7ca`

# Method GetDrillingHolePrimitive

#### GetDrillingHolePrimitive(ElementId, int, out DrillingHolePrimitiveBottomType, out Real, out Real, out Real, out bool, out bool, out bool, out Real)

Get a drilling hole primitive information.

##### Declaration

```
[OperationContract]
[AvailableSinceVersion(7, 14, 0, 0)]
void GetDrillingHolePrimitive(ElementId inFeatureId, int inIndex, out DrillingHolePrimitiveBottomType bottomType, out Real bottomAngle, out Real depth, out Real diameter, out bool isTapered, out bool isThrough, out bool isToleranced, out Real taperAngle)
```

##### Parameters

|  Type |  Name |  Description |  
| --- | --- | --- | 
   
|  ElementId |  inFeatureId |  Identifiers of the element of the primitive to analyse.  |  
 
|  int |  inIndex |  Idex of the primitive to analyse.  |  
 
|  DrillingHolePrimitiveBottomType |  bottomType |  Type of the drilling primitive, or None if the primitive is not a drilling hole primitive.  |  
 
|  Real |  bottomAngle |  Bottom angle of the hole primitive.  |  
 
|  Real |  depth |  Detph of the hole primitive.  |  
 
|  Real |  diameter |  Diameter of the hole primitive.  |  
 
|  bool |  isTapered |  Tells wether the hole primitive is tapered.  |  
 
|  bool |  isThrough |  Tells wether the hole primitive is through.  |  
 
|  bool |  isToleranced |  Tells wether the hole primitive is toleranced.  |  
 
|  Real |  taperAngle |  Taper angle of the hole primitive.  |  

##### Remarks

This method is available since v7.14.
