Source: https://help.topsolid.com/7.20/en/TopSolid'Automation/api/pdmexplorer/TopSolid.Pdm.Explorer.Automating.IPdm.MoveProject.html

Source SHA-256: `6d7f66d61d3f677c8f3082c6b994bda634d65966e5ed3e602736174ac67c4dc2`

# Method MoveProject

#### MoveProject(PdmObjectId, PdmProjectFolderId)

Moves a project into a specified project folder.

##### Declaration

```
[OperationContract]
[AvailableSinceVersion(7, 11, 300, 80)]
void MoveProject(PdmObjectId inProjectId, PdmProjectFolderId inDestinationFolderId)
```

##### Parameters

|  Type |  Name |  Description |  
| --- | --- | --- | 
   
|  PdmObjectId |  inProjectId |  Project object identifier.  |  
 
|  PdmProjectFolderId |  inDestinationFolderId |  Identifier of the destination project folder.  |  

##### Remarks

This method is available since v7.10.
