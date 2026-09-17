Source: https://help.topsolid.com/7.20/en/TopSolid'Automation/api/pdmexplorer/TopSolid.Pdm.Explorer.Automating.IPdm.GetDeliveryDate.html

Source SHA-256: `6069e5dbc2bd1405a0e6a00a64d6182f6eb2a0d821f04c5e1a4dd7acd4b593e5`

# Method GetDeliveryDate

#### GetDeliveryDate(PdmObjectId)

Gets the delivery date of an object.

##### Declaration

```
[OperationContract]
[AvailableSinceVersion(7, 11, 300, 80)]
string GetDeliveryDate(PdmObjectId inObjectId)
```

##### Parameters

|  Type |  Name |  Description |  
| --- | --- | --- | 
   
|  PdmObjectId |  inObjectId |  Object identifier.  |  

##### Returns

|  Type |  Description |  
| --- | --- | 
   
|  string |  Object delivery date.  |  

##### Remarks

This method is available since v7.10.300.140.
