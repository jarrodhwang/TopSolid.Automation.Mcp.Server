Source: https://help.topsolid.com/7.20/en/TopSolid'Automation/api/cad/TopSolid.Cad.Design.Automating.IAssemblies.IsWizardInclusion.html

Source SHA-256: `66bddc361734684b6636a9fa7741195d318ea99efbba07c960a6ba84f81f087b`

# Method IsWizardInclusion

#### IsWizardInclusion(ElementId)

Tells whether an element is a wizard operation.

##### Declaration

```
[OperationContract]
[AvailableSinceVersion(7, 19, 0, 0)]
bool IsWizardInclusion(ElementId inElementId)
```

##### Parameters

|  Type |  Name |  Description |  
| --- | --- | --- | 
   
|  ElementId |  inElementId |  Identifier of the element to analyze.  |  

##### Returns

|  Type |  Description |  
| --- | --- | 
   
|  bool |  The element is an inclusion operation.  |  

##### Remarks

This method is available since v7.19.
