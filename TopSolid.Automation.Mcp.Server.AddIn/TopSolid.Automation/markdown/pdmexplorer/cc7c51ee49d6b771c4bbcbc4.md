Source: https://help.topsolid.com/7.20/en/TopSolid'Automation/api/pdmexplorer/TopSolid.Pdm.Explorer.Automating.IPdm.ExportExecutablePackage.html

Source SHA-256: `3fa3b4968517f366d5e2e65b480a1b7d870cc4ba1c9ed3b762629235f9292957`

# Method ExportExecutablePackage

#### ExportExecutablePackage(List<PdmObjectId>, bool, bool, bool, string)

Exports an executable package.

##### Declaration

```
[OperationContract]
[AvailableSinceVersion(7, 11, 300, 80)]
void ExportExecutablePackage(List<PdmObjectId> inObjectIds, bool inExportsForDelivery, bool inExportsIncrementalPackage, bool inAllowsMeasurement, string inFileFullPath)
```

##### Parameters

|  Type |  Name |  Description |  
| --- | --- | --- | 
   
|  List<PdmObjectId> |  inObjectIds |  PDM object identifiers.  |  
 
|  bool |  inExportsForDelivery |  Whether exports for delivery.  |  
 
|  bool |  inExportsIncrementalPackage |  Whether exports an incremental package.  |  
 
|  bool |  inAllowsMeasurement |  Whether allows measurement.  |  
 
|  string |  inFileFullPath |  Full path of the exported package, with ".exe" extension.  |  

##### Remarks

This method produces Windows 32-bit executable that allows viewing the specified objects without installing TopSolid'Viewer.
 
This method is available since v7.10.
