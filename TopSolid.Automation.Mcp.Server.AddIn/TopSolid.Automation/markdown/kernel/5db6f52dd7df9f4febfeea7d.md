Source: https://help.topsolid.com/7.20/en/TopSolid'Automation/api/kernel/TopSolid.Kernel.Automating.Direction3D.IsParallelTo.html

Source SHA-256: `e1729fdbe427ba10e9237cfa23f72b46737aa3c743bf57697b48c8915a8ed2b7`

# Method IsParallelTo

#### IsParallelTo(Direction3D, bool)

Tells whether this direction is parallel to another direction.

##### Declaration

```
public bool IsParallelTo(Direction3D inDirection, bool inSense)
```

##### Parameters

|  Type |  Name |  Description |  
| --- | --- | --- | 
   
|  Direction3D |  inDirection |  Direction to compare with.  |  
 
|  bool |  inSense |  Sense is meaningful.  |  

##### Returns

|  Type |  Description |  
| --- | --- | 
   
|  bool |  This direction is parallel to the specified direction.  |  

##### Remarks

Two directions are considered parallel if they make an angle of less than AngularPrecision.
