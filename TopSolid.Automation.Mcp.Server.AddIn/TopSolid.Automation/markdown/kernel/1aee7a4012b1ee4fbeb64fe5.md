Source: https://help.topsolid.com/7.20/en/TopSolid'Automation/api/kernel/TopSolid.Kernel.Automating.SmartPlanarProfile3D.html

Source SHA-256: `b0ea74170c2c5e636e8e9bd035508cbd1ad62c38fb98bb939f9b3b196cb0935f`

# Class SmartPlanarProfile3D

Represents a 3D smart planar profile.

##### Inheritance
 
object
 
SmartObject
 
SmartPlanarProfile3D

##### Inherited Members
 
 object.ToString() 
 
 object.Equals(object) 
 
 object.Equals(object, object) 
 
 object.ReferenceEquals(object, object) 
 
 object.GetHashCode() 
 
 object.GetType() 
 
 object.MemberwiseClone() 

###### Namespace: TopSolid.Kernel.Automating

###### Assembly: TopSolid.Kernel.Automating.dll

##### Syntax

```
[DataContract]
[AvailableSinceVersion(7, 20, 400, 1000)]
public class SmartPlanarProfile3D : SmartObject
```

##### Remarks

This class is available since v7.20.

### Constructors 

|  Name |  Description |  
| --- | --- | 
   
|   SmartPlanarProfile3D(ElementId, bool)  |  Initializes a new instance of the SmartPlanarProfile3D class of type Element.  |  
 
|   SmartPlanarProfile3D(ElementId, ItemLabel, bool)  |  Initializes a new instance of the SmartPlanarProfile3D class of type Item.  |  
 
|   SmartPlanarProfile3D(ElementId, ItemLabel, ItemLabel, bool)  |  Initializes a new instance of the SmartPlanarProfile3D class of type Item.  |  
 
|   SmartPlanarProfile3D(SmartPlanarProfile3DType, ElementId, ItemLabel, bool)  |  Initializes a new instance of the SmartPlanarProfile3D class.  |  
 
|   SmartPlanarProfile3D(SmartPlanarProfile3DType, ElementId, ItemLabel, ItemLabel, bool)  |  Initializes a new instance of the SmartPlanarProfile3D class.  |  

### Fields 

|  Name |  Description |  
| --- | --- | 
   
|   ElementId  |  Providing element identifier, or empty if none.  |  
 
|   IsReversed  |  Whether the provided profile orientation is to be reversed.  |  
 
|   ItemLabel  |  Providing element item label, or empty if none.  |  
 
|   PlaneItemLabel  |  Providing element plane item label, or empty if none.  |  
 
|   Type  |  Type of profile.  |
