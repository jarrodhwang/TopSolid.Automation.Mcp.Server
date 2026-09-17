Source: https://help.topsolid.com/7.20/en/TopSolid'Automation/api/cam/TopSolid.Cam.NC.Kernel.Automating.PathPoint.html

Source SHA-256: `857cac201d05fc47e8e466c90eb6f8918cf862cc7b400c244ff2e6a7171c1de2`

# Struct PathPoint

Represents a tool path point.

##### Inherited Members
 
 ValueType.Equals(object) 
 
 ValueType.GetHashCode() 
 
 ValueType.ToString() 
 
 object.Equals(object, object) 
 
 object.ReferenceEquals(object, object) 
 
 object.GetType() 

###### Namespace: TopSolid.Cam.NC.Kernel.Automating

###### Assembly: TopSolid.Cam.NC.Kernel.Automating.dll

##### Syntax

```
[DataContract]
[AvailableSinceVersion(7, 8, 0, 0)]
public struct PathPoint
```

### Constructors 

|  Name |  Description |  
| --- | --- | 
   
|   PathPoint(Point3D, Vector3D, double)  |  Initializes a new instance of the ParameterId structure.  |  

### Fields 

|  Name |  Description |  
| --- | --- | 
   
|   Empty  |  Empty identifier.  |  

### Properties 

|  Name |  Description |  
| --- | --- | 
   
|   FeedrateFactor  |  Gets the feedrate to reach the point.  |  
 
|   IsEmpty  |  Tells whether the parameter identifier is empty.  |  
 
|   Normal  |  Gets the normal of this point.  |  
 
|   Point  |  Gets the point.  |
