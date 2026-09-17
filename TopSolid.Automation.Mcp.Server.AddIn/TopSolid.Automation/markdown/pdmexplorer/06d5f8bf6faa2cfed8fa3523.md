Source: https://help.topsolid.com/7.20/en/TopSolid'Automation/api/pdmexplorer/TopSolid.Pdm.Explorer.Automating.IPdm.GetCustomer.html

Source SHA-256: `140eef043f56e00dce8bf1a20820120666125690da0fd4d49566b865f6fbe024`

# Method GetCustomer

#### GetCustomer(PdmObjectId)

Gets the customer of an object.

##### Declaration

```
[OperationContract]
[AvailableSinceVersion(7, 11, 300, 80)]
string GetCustomer(PdmObjectId inObjectId)
```

##### Parameters

|  Type |  Name |  Description |  
| --- | --- | --- | 
   
|  PdmObjectId |  inObjectId |  Object identifier.  |  

##### Returns

|  Type |  Description |  
| --- | --- | 
   
|  string |  Object customer.  |  

##### Remarks

This method is available since v7.10.300.120.
