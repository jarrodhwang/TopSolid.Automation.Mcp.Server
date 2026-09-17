Source: https://help.topsolid.com/7.20/en/TopSolid'Automation/api/cad/TopSolid.Cad.Design.Automating.IAssemblies.SetCollisionsManagementCheckingMechanisms.html

Source SHA-256: `40c00299b2f807daf17b0c11847217b335b5f2a62f49b1a1800abe5ed34b6633`

# Method SetCollisionsManagementCheckingMechanisms

#### SetCollisionsManagementCheckingMechanisms(DocumentId, bool)

Sets whether the collisions management of an assembly is checking for collisions between rigid groups of articulated subassemblies.

##### Declaration

```
[OperationContract]
[AvailableSinceVersion(7, 10, 0, 0)]
void SetCollisionsManagementCheckingMechanisms(DocumentId inDocumentId, bool inIsChecking)
```

##### Parameters

|  Type |  Name |  Description |  
| --- | --- | --- | 
   
|  DocumentId |  inDocumentId |  Identifier of assembly document to modify.  |  
 
|  bool |  inIsChecking |  Whether the collisions management of the assembly is checking for collisions between rigid groups of articulated subassemblies.  |  

##### Remarks

StartModification(string, bool) and EnsureIsDirty(ref DocumentId) must be called before calling this method.
 
This method is available since v7.10.
