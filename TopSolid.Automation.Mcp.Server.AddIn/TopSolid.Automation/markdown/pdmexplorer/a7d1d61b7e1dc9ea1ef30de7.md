Source: https://help.topsolid.com/7.20/en/TopSolid'Automation/api/pdmexplorer/TopSolid.Pdm.Explorer.Automating.IPdm.ExportPackage.html

Source SHA-256: `a66dd0d8ad5adc30122e7f70127c27d802a0d5f32186b2cc09ab7071b9a5a59e`

# Method ExportPackage

#### ExportPackage(List<PdmObjectId>, bool, bool, string)

Exports a package.

##### Declaration

```
[OperationContract]
[AvailableSinceVersion(7, 11, 300, 80)]
void ExportPackage(List<PdmObjectId> inObjectIds, bool inExportsForDelivery, bool inExportsIncrementalPackage, string inFileFullPath)
```

##### Parameters

|  Type |  Name |  Description |  
| --- | --- | --- | 
   
|  List<PdmObjectId> |  inObjectIds |  PDM object identifiers.  |  
 
|  bool |  inExportsForDelivery |  Whether exports for delivery.  |  
 
|  bool |  inExportsIncrementalPackage |  Whether exports an incremental package.  |  
 
|  string |  inFileFullPath |  Full path of the exported package, with ".TopPkg" extension.  |  

##### Remarks

This method is available since v7.10.
