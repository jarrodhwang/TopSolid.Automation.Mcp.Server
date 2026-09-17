Source: https://help.topsolid.com/7.20/en/TopSolid'Automation/api/cad/TopSolid.Cad.Design.Automating.IAssemblies.SetCollisionsManagement.html

Source SHA-256: `874fb2fd9988b8a75a3655d20af167e1f696c983e93fb623d134466a3f239419`

# Method SetCollisionsManagement

#### SetCollisionsManagement(DocumentId, ElementId, bool, bool, bool)

Sets the collisions management of an assembly.

##### Declaration

```
[OperationContract]
[AvailableSinceVersion(7, 8, 0, 0)]
void SetCollisionsManagement(DocumentId inDocumentId, ElementId inRepresentationId, bool inFindsIntersections, bool inExcludesThreadingTapping, bool inIsRefreshAuto)
```

##### Parameters

|  Type |  Name |  Description |  
| --- | --- | --- | 
   
|  DocumentId |  inDocumentId |  Identifier of assembly document to modify.  |  
 
|  ElementId |  inRepresentationId |  Identifier of the analyzed representation entity, or empty for no collisions management.  |  
 
|  bool |  inFindsIntersections |  Whether to find collisions intersections.  |  
 
|  bool |  inExcludesThreadingTapping |  Whether to exclude threading-tapping collisions.  |  
 
|  bool |  inIsRefreshAuto |  Whether refresh is automatic.  |  

##### Remarks

StartModification(string, bool) and EnsureIsDirty(ref DocumentId) must be called before calling this method.
 
This method is available since v7.8.
