Source: https://help.topsolid.com/7.20/en/TopSolid'Automation/api/pdmexplorer/TopSolid.Pdm.Explorer.Automating.IApplication.IsLicenseValid.html

Source SHA-256: `d32a3083e81c05869302a2a4c149966c728d8b2b26193b1235f78b671d79c88a`

# Method IsLicenseValid

#### IsLicenseValid(int)

Tells whether a valid TopSolid license is available.

##### Declaration

```
[OperationContract]
[AvailableSinceVersion(7, 11, 300, 80)]
bool IsLicenseValid(int inModuleId)
```

##### Parameters

|  Type |  Name |  Description |  
| --- | --- | --- | 
   
|  int |  inModuleId |  TopSolid module identifier.  |  

##### Returns

|  Type |  Description |  
| --- | --- | 
   
|  bool |   |  

##### Remarks

This method is available since v7.10.
