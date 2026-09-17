Source: https://help.topsolid.com/7.20/en/TopSolid'Automation/api/kernel/TopSolid.Kernel.Automating.IPdm.GetCurrentProject.html

Source SHA-256: `63aaac1d20885ee44601353a73c8f3ca35e997ce99ddfcd7f66b86aea8227c27`

# Method GetCurrentProject

#### GetCurrentProject()

Gets the current projects.

##### Declaration

```
[OperationContract]
[AvailableSinceVersion(7, 14, 300, 140)]
PdmObjectId GetCurrentProject()
```

##### Returns

|  Type |  Description |  
| --- | --- | 
   
|  PdmObjectId |  Current project PDM object identifier.  |  

##### Remarks

If there is no current project, the returned PDM object identifier is empty.
 
This method is available since v7.14.
