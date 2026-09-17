Source: https://help.topsolid.com/7.20/en/TopSolid'Automation/api/cad/TopSolid.Cad.Design.Automating.IAssemblies.SetAssemblyVolumeManagement.html

Source SHA-256: `782726430e23902cd70182795fe6a36ee861e5c8c9fc0ea3421318c4bb32b2b6`

# Method SetAssemblyVolumeManagement

#### SetAssemblyVolumeManagement(DocumentId, bool)

Sets the volume management of the physical property management of an assembly.

##### Declaration

```
[OperationContract]
[AvailableSinceVersion(7, 13, 300, 220)]
void SetAssemblyVolumeManagement(DocumentId inDocumentId, bool inIsVolumeManaged)
```

##### Parameters

|  Type |  Name |  Description |  
| --- | --- | --- | 
   
|  DocumentId |  inDocumentId |  Identifier of assembly document to analyze.  |  
 
|  bool |  inIsVolumeManaged |  Tells whether the physical property management of an assembly is volume managed.  |  

##### Remarks

This method is available since v7.13.300.220.
