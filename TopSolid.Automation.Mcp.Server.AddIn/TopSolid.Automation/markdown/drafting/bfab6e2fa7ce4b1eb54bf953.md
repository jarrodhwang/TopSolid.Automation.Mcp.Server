Source: https://help.topsolid.com/7.20/en/TopSolid'Automation/api/drafting/TopSolid.Cad.Drafting.Automating.IDimensions.GetDimensionSecondaryData.html

Source SHA-256: `ba363a947da85a2a4ea77c79e15089a1ea2a7f9a761703abcdabf64bad93f6a4`

# Method GetDimensionSecondaryData

#### GetDimensionSecondaryData(ElementId, out string, out UnitType, out string, out string, out string)

##### Declaration

```
[OperationContract]
[AvailableSinceVersion(7, 20, 255, 0)]
void GetDimensionSecondaryData(ElementId inElementId, out string outSecondaryText, out UnitType outSecondaryUnitType, out string outSecondarySymbol, out string outSecondaryPrefix, out string outSecondarySuffix)
```

##### Parameters

|  Type |  Name |  Description |  
| --- | --- | --- | 
   
|  ElementId |  inElementId |  The dimension  |  
 
|  string |  outSecondaryText |  Secondary displayed text  |  
 
|  UnitType |  outSecondaryUnitType |  Unit Type of the secondary value  |  
 
|  string |  outSecondarySymbol |  Unit Symbol of the secondary value  |  
 
|  string |  outSecondaryPrefix |  Secondary prefix  |  
 
|  string |  outSecondarySuffix |  Secondary suffix  |
