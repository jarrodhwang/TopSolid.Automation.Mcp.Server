Source: https://help.topsolid.com/7.20/en/TopSolid'Automation/api/kernel/TopSolid.Kernel.Automating.IPdm.PurgeRevisions.html

Source SHA-256: `dac4daf43b057610d474b9152ad2e281ecd825f54ef451e5fdf45f37c1e9c212`

# Method PurgeRevisions

#### PurgeRevisions(List<PdmObjectId>, int, bool, int, int, out long, out int)

Purges the revisions of PDM objects.

##### Declaration

```
[OperationContract]
[AvailableSinceVersion(7, 19, 400, 140)]
void PurgeRevisions(List<PdmObjectId> inObjectIds, int inMaximumMajorRevisionCountToKeep, bool inOnlyObsoleteMajorRevisions, int inMaximumMinorRevisionCountToKeep, int inMaximumBackupCountToKeep, out long outFileSize, out int outFileCount)
```

##### Parameters

|  Type |  Name |  Description |  
| --- | --- | --- | 
   
|  List<PdmObjectId> |  inObjectIds |  PDM objects to purge.  |  
 
|  int |  inMaximumMajorRevisionCountToKeep |  Maximum major revisions count to keep, or -1 if major revisions are not purged.  |  
 
|  bool |  inOnlyObsoleteMajorRevisions |  Purge only the obsolete major revisions.  |  
 
|  int |  inMaximumMinorRevisionCountToKeep |  Maximum minor revisions count to keep, or -1 if minor revisions are not purged.  |  
 
|  int |  inMaximumBackupCountToKeep |  Maximum backup count to keep, or -1 if backups are not purged.  |  
 
|  long |  outFileSize |  Purged files size.  |  
 
|  int |  outFileCount |  Purged files count.  |  

##### Remarks

This method must not be called between StartModification(string, bool) and EndModification(bool, bool) methods calls.
 
This method is available since v7.7.201.140.
