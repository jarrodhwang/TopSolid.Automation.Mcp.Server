Source: https://help.topsolid.com/7.20/en/TopSolid'Automation/api/kernel/TopSolid.Kernel.Automating.Point3D.html

Source SHA-256: `ecc83e11f31cd7c176c9d587c689a2b9fd6270aab528766d505a1cafe914baf1`

# Struct Point3D

Represents a geometric 3D point.

##### Inherited Members
 
 ValueType.Equals(object) 
 
 ValueType.GetHashCode() 
 
 ValueType.ToString() 
 
 object.Equals(object, object) 
 
 object.ReferenceEquals(object, object) 
 
 object.GetType() 

###### Namespace: TopSolid.Kernel.Automating

###### Assembly: TopSolid.Kernel.Automating.dll

##### Syntax

```
[DataContract]
[AvailableSinceVersion(7, 6, 0, 0)]
public struct Point3D
```

##### Remarks

This structure is available since v7.6.

### Constructors 

|  Name |  Description |  
| --- | --- | 
   
|   Point3D(double, double, double)  |  Initializes a new instance of the Point3D structure with its coordinates.  |  

### Fields 

|  Name |  Description |  
| --- | --- | 
   
|   P0  |  Origin point, i.e. (0,0,0).  |  
 
|   PX  |  X unit point, i.e. (1,0,0).  |  
 
|   PY  |  Y unit point, i.e. (0,1,0).  |  
 
|   PZ  |  Z unit point, i.e. (0,0,1).  |  
 
|   X  |  X coordinate.  |  
 
|   Y  |  Y coordinate.  |  
 
|   Z  |  Z coordinate.  |  

### Methods 

|  Name |  Description |  
| --- | --- | 
   
|   CoincidesWith(Point3D)  |  Tells whether this point coincides with another point.  |  

### Operators 

|  Name |  Description |  
| --- | --- | 
   
|   operator +(Point3D, Point3D)  |  Adds two points.  |  
 
|   operator +(Point3D, Vector3D)  |  Adds a point and a vector.  |  
 
|   operator |(Point3D, Point3D)  |  Computes the vector from one point to another point.  |  
 
|   operator /(Point3D, double)  |  Divides a point by a double value.  |  
 
|   operator *(double, Point3D)  |  Multiplies a double value and a point.  |  
 
|   operator *(Point3D, double)  |  Multiplies a point and a double value.  |  
 
|   operator -(Point3D, Vector3D)  |  Subtracts a point and a vector.  |
