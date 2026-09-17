Source: https://help.topsolid.com/7.20/en/TopSolid'Automation/api/cam/TopSolid.Cam.NC.Kernel.Automating.IParameters.GetEnumTypeName.html

Source SHA-256: `ae1e1743f520c7aa5df252fcc1043b5f42fc4e25fdac99d1277ec6dd5970e6be`

# Method GetEnumTypeName

#### GetEnumTypeName(ParameterId)

Gets the enum type name corresponding to the parameter.

##### Declaration

```
[OperationContract]
[AvailableSinceVersion(7, 8, 0, 0)]
string GetEnumTypeName(ParameterId inParameterId)
```

##### Parameters

|  Type |  Name |  Description |  
| --- | --- | --- | 
   
|  ParameterId |  inParameterId |  Identifier of the parameter to analyze.  |  

##### Returns

|  Type |  Description |  
| --- | --- | 
   
|  string |  Enum type name corresponding to the parameter.  |  

##### Remarks

An integer parameter can hold a value taken from an enumeration. This function will give the enumeration type full name (like TopSolid.Cam.NC.Kernel.common.DrillingType) for later usage. If the parameter is not containing an enum value, the string will be empty.
