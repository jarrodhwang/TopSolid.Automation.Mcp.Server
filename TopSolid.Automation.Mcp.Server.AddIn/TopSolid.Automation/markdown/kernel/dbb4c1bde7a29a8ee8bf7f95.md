Source: https://help.topsolid.com/7.20/en/TopSolid'Automation/api/kernel/TopSolid.Kernel.Automating.IPdmWorkflow.GetWorkflowActionStates.html

Source SHA-256: `97f68fcf8c8a1659983f014a5f4c64d7a5b7709e84c82dd90a622d4bddb3407f`

# Method GetWorkflowActionStates

#### GetWorkflowActionStates(PdmMajorRevisionId, out List<PdmObjectId>, out List<WorkflowActionState>)

Get workflow action states of given major revision corresponding to returned workflow action identifiers.

##### Declaration

```
[OperationContract]
[AvailableSinceVersion(7, 14, 248, 0)]
void GetWorkflowActionStates(PdmMajorRevisionId inMajorRevisionId, out List<PdmObjectId> outWorkflowActionIds, out List<WorkflowActionState> outWorkflowActionStates)
```

##### Parameters

|  Type |  Name |  Description |  
| --- | --- | --- | 
   
|  PdmMajorRevisionId |  inMajorRevisionId |  Major revision identifier.  |  
 
|  List<PdmObjectId> |  outWorkflowActionIds |  Workflow action identifiers.  |  
 
|  List<WorkflowActionState> |  outWorkflowActionStates |  Workflow action states.  |  

##### Remarks

This method is available since v7.14.248.000.
