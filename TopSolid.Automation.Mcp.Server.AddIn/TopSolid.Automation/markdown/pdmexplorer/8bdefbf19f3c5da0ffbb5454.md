Source: https://help.topsolid.com/7.20/en/TopSolid'Automation/api/pdmexplorer/TopSolid.Pdm.Explorer.Automating.IPdm.GetCustomerProjectIdentificationNumber.html

Source SHA-256: `518bf6a563d357877579f62034ebbca284baf8bb5322db00532c85522afa1d86`

# Method GetCustomerProjectIdentificationNumber

#### GetCustomerProjectIdentificationNumber(PdmObjectId)

Gets the project customer identification number of an object.

##### Declaration

```
[OperationContract]
[AvailableSinceVersion(7, 11, 300, 80)]
string GetCustomerProjectIdentificationNumber(PdmObjectId inObjectId)
```

##### Parameters

|  Type |  Name |  Description |  
| --- | --- | --- | 
   
|  PdmObjectId |  inObjectId |  Object identifier.  |  

##### Returns

|  Type |  Description |  
| --- | --- | 
   
|  string |  Object customer identification number.  |  

##### Remarks

This method is available since v7.10.300.120.
