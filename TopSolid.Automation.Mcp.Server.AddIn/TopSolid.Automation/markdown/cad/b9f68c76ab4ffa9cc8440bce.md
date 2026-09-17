Source: https://help.topsolid.com/7.20/en/TopSolid'Automation/api/cad/TopSolid.Cad.Design.Automating.ISubstitutions.GetCurrentSubstitutionRule.html

Source SHA-256: `dd325be19352bac6dbb4e5d5269986a0d19716905b01ee701b685da20d29ca48`

# Method GetCurrentSubstitutionRule

#### GetCurrentSubstitutionRule(ElementId)

Gets current substitution rule

##### Declaration

```
[OperationContract]
[AvailableSinceVersion(7, 19, 0, 0)]
ElementId GetCurrentSubstitutionRule(ElementId inOperationId)
```

##### Parameters

|  Type |  Name |  Description |  
| --- | --- | --- | 
   
|  ElementId |  inOperationId |  Identifier of the operation holding substitutions.  |  

##### Returns

|  Type |  Description |  
| --- | --- | 
   
|  ElementId |  The substitution rules.  |  

##### Remarks

This method is available since v7.19.
