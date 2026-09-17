Source: https://help.topsolid.com/7.20/en/TopSolid'Automation/api/kernel/TopSolid.Kernel.Automating.Vector2D.op_Multiply.html

Source SHA-256: `441f74160e0956a2e4b3a9b5ad505893dac219e3c952c9464b7c4185d1ad2e44`

# Operator operator *

#### operator *(double, Vector2D)

Multiplies a double value and a vector.

##### Declaration

```
public static Vector2D operator *(double inD, Vector2D inVector)
```

##### Parameters

|  Type |  Name |  Description |  
| --- | --- | --- | 
   
|  double |  inD |  Left hand side double.  |  
 
|  Vector2D |  inVector |  Right hand side vector.  |  

##### Returns

|  Type |  Description |  
| --- | --- | 
   
|  Vector2D |  Scaled vector.  |  

#### operator *(Vector2D, double)

Multiplies a vector and a double value.

##### Declaration

```
public static Vector2D operator *(Vector2D inVector, double inD)
```

##### Parameters

|  Type |  Name |  Description |  
| --- | --- | --- | 
   
|  Vector2D |  inVector |  Left hand side vector.  |  
 
|  double |  inD |  Right hand side double.  |  

##### Returns

|  Type |  Description |  
| --- | --- | 
   
|  Vector2D |  Scaled vector.  |  

#### operator *(Vector2D, Vector2D)

Computes the dot product of two vectors.

##### Declaration

```
public static double operator *(Vector2D inVector1, Vector2D inVector2)
```

##### Parameters

|  Type |  Name |  Description |  
| --- | --- | --- | 
   
|  Vector2D |  inVector1 |  Left hand side vector.  |  
 
|  Vector2D |  inVector2 |  Right hand side vector.  |  

##### Returns

|  Type |  Description |  
| --- | --- | 
   
|  double |  Vectors' dot product.  |
