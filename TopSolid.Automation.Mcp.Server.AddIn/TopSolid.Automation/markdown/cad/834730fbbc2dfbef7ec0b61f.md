Source: https://help.topsolid.com/7.20/en/TopSolid'Automation/api/cad/TopSolid.Cad.Design.Automating.IAssemblies.DerivePartForModification.html

Source SHA-256: `8acafdf5e2c4270556c085b9e589ee36e13ea82fedcfbb4e85eb7e9dbca4d11f`

# Method DerivePartForModification

#### DerivePartForModification(ElementId, bool)

Derives a part for modification in an assembly.

##### Declaration

```
[OperationContract]
[AvailableSinceVersion(7, 10, 0, 0)]
DocumentId DerivePartForModification(ElementId inOccurrenceId, bool inUseDefaultTemplate)
```

##### Parameters

|  Type |  Name |  Description |  
| --- | --- | --- | 
   
|  ElementId |  inOccurrenceId |  Identifier of the occurrence of the part to derive in the assembly document.  |  
 
|  bool |  inUseDefaultTemplate |  Tells whether to use the default template if present.  |  

##### Returns

|  Type |  Description |  
| --- | --- | 
   
|  DocumentId |  Derived part document identifier.  |  

##### Remarks

The occurrence of the part must be in the parts folder of the assembly, or at the first level of an assembly occurrence that is in the parts folder of the assembly (in which case a derived sub-assembly will also be created).
 
StartModification(string, bool) and EnsureIsDirty(ref DocumentId) must be called before calling this method.
 
This method is available since v7.10.
