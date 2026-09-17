Source: https://help.topsolid.com/7.20/en/TopSolid'Automation/api/kernel/TopSolid.Kernel.Automating.PdmObjectState.html

Source SHA-256: `5b4291be3149d10addd448582f60a1bfd0032aa76ead84270dd7ae1f876d0e17`

# Enum PdmObjectState

Defines the various states of a PDM object.

###### Namespace: TopSolid.Kernel.Automating

###### Assembly: TopSolid.Kernel.Automating.dll

##### Syntax

```
[DataContract]
[AvailableSinceVersion(7, 6, 0, 0)]
public enum PdmObjectState
```

##### Remarks

This enum is available since v7.6.

### Fields 

|  Name |  Description |  
| --- | --- | 
   
|  CheckedIn |  CheckedIn state.  |  
 
|  Default |  Default state.  |  
 
|  Deleted |  Object is deleted.  |  
 
|  ExclusiveModification |  Exclusive modification state.  |  
 
|  IsLockedBecauseModifiedData |  Locked because data modified.  |  
 
|  LockedBecauseToAnnihilate |  Locked because to annihilate state.  |  
 
|  LockedBecauseToDelete |  Locked because to delete state.  |  
 
|  MultipleModification |  Multiple modification state.  |  
 
|  New |  New state.  |  
 
|  NotUpToDateBecauseDraggedAndDropped |  Position in treeview is not up-to-date because object has been dragged and dropped somewhere else.  |  
 
|  Obsolete |  Obsolete (last major revision is obsolete).  |  
 
|  ToAnnihilate |  To annihilate state.  |  
 
|  ToDelete |  To delete state.  |  
 
|  Validated |  Validated (last major revision is validated).  |
