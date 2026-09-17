Source: https://help.topsolid.com/7.20/en/TopSolid'Automation/api/cad/TopSolid.Cad.Design.Automating.IAssemblies.IsOptionalDriver.html

Source SHA-256: `32a4fdcb7f84d19321b07ae085002e963a7488b214da54ebd960eb3b6f0f6b89`

# Method IsOptionalDriver

#### IsOptionalDriver(ElementId, string)

Tells if a driver is optional

##### Declaration

```
[OperationContract]
[AvailableSinceVersion(7, 19, 400, 80)]
bool IsOptionalDriver(ElementId inOperationId, string inDriverName)
```

##### Parameters

|  Type |  Name |  Description |  
| --- | --- | --- | 
   
|  ElementId |  inOperationId |  inclusion operation  |  
 
|  string |  inDriverName |  name of the driver to consider  |  

##### Returns

|  Type |  Description |  
| --- | --- | 
   
|  bool |   |
