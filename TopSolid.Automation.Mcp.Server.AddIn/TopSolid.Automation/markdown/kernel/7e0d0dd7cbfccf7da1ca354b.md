Source: https://help.topsolid.com/7.20/en/TopSolid'Automation/api/kernel/TopSolid.Kernel.Automating.Frame3D.-ctor.html

Source SHA-256: `67a5b752eca71a4c740b2eb3d869e466f0e9655f1314484541ee45e88048b095`

# Constructor Frame3D

#### Frame3D(Point3D, Direction3D, Direction3D, Direction3D)

Initializes a new instance of the Frame3D structure with a specified origin and directions.

##### Declaration

```
public Frame3D(Point3D inOrigin, Direction3D inXDirection, Direction3D inYDirection, Direction3D inZDirection)
```

##### Parameters

|  Type |  Name |  Description |  
| --- | --- | --- | 
   
|  Point3D |  inOrigin |  Frame origin.  |  
 
|  Direction3D |  inXDirection |  Frame X direction.  |  
 
|  Direction3D |  inYDirection |  Frame Y direction, orthogonal to inXDirection.  |  
 
|  Direction3D |  inZDirection |  Frame Z direction, equal to inXDirection ^ inYDirection.  |
