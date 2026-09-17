Source: https://help.topsolid.com/7.20/en/TopSolid'Automation/api/cad/TopSolid.Cad.Design.Automating.IMaterials.GetUltimateStress.html

Source SHA-256: `1ac9356a955454f58711c7a99eb56aa3e82fac4cbc51a864a3855f8521b298ed`

# Method GetUltimateStress

#### GetUltimateStress(DocumentId, out double)

Gets Ultimate Stress

##### Declaration

```
[OperationContract]
[AvailableSinceVersion(7, 20, 400, 120)]
bool GetUltimateStress(DocumentId inDocumentId, out double outValue)
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
