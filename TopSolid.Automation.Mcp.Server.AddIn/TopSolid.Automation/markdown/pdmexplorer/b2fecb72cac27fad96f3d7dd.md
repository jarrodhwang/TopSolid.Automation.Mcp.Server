Source: https://help.topsolid.com/7.20/en/TopSolid'Automation/api/pdmexplorer/TopSolid.Pdm.Explorer.Automating.IPdmAdmin.GetUserEmailAddress.html

Source SHA-256: `acdb39ba0715b5e50662d5cafa10120262a5d6d2a108fa0f022f31262a9b9149`

# Method GetUserEmailAddress

#### GetUserEmailAddress(string)

Gets the e-mail address of a user.

##### Declaration

```
[OperationContract]
[AvailableSinceVersion(7, 11, 300, 80)]
string GetUserEmailAddress(string inAccountId)
```

##### Parameters

|  Type |  Name |  Description |  
| --- | --- | --- | 
   
|  string |  inAccountId |  User account identifier.  |  

##### Returns

|  Type |  Description |  
| --- | --- | 
   
|  string |  User e-mail address.  |  

##### Remarks

This method is available since v7.10.
