Source: https://help.topsolid.com/7.20/en/TopSolid'Automation/api/cad/TopSolid.Cad.Design.Automating.IMaterials.GetYoungModulus.html

Source SHA-256: `6b6595cc8893c7ddcc69b09b486d81134be4cbb384c555798338cb878a7830cd`

# Method GetYoungModulus

#### GetYoungModulus(DocumentId, out double)

Gets Young Modulus

##### Declaration

```
[OperationContract]
[AvailableSinceVersion(7, 20, 400, 120)]
bool GetYoungModulus(DocumentId inDocumentId, out double outValue)
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
