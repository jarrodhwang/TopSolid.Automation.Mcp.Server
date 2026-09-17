Source: https://help.topsolid.com/7.20/en/TopSolid'Automation/api/cad/TopSolid.Cad.Design.Automating.IMaterials.GetCuttingForceIncrement.html

Source SHA-256: `6a1ad422ad9f70712494373fba2aa0e9f08f5122c77777273b32397aa37f42a9`

# Method GetCuttingForceIncrement

#### GetCuttingForceIncrement(DocumentId, out double)

Gets Cutting Force Increment

##### Declaration

```
[OperationContract]
[AvailableSinceVersion(7, 20, 400, 120)]
bool GetCuttingForceIncrement(DocumentId inDocumentId, out double outValue)
```

##### Parameters

|  Type |  Name |  Description |  
| --- | --- | --- | 
   
|  DocumentId |  inDocumentId |  Id of the container document  |  
 
|  double |  outValue |  The value  |  

##### Returns

|  Type |  Description |  
| --- | --- | 
   
|  bool |  true if a parameter has been fetched with success  |
