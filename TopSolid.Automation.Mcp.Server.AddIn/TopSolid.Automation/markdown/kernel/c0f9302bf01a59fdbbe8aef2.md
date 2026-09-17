Source: https://help.topsolid.com/7.20/en/TopSolid'Automation/api/kernel/TopSolid.Kernel.Automating.IGeometries3D.GetProfilePublishingDefinition.html

Source SHA-256: `6b589d3a00ea5e109da27a31ad8ebf844aa75f5bfccf36a3d47041f81399155f`

# Method GetProfilePublishingDefinition

#### GetProfilePublishingDefinition(ElementId)

Gets the definition of a profile publishing entity.

##### Declaration

```
[OperationContract]
[AvailableSinceVersion(7, 20, 400, 180)]
SmartProfile3D GetProfilePublishingDefinition(ElementId inElementId)
```

##### Parameters

|  Type |  Name |  Description |  
| --- | --- | --- | 
   
|  ElementId |  inElementId |  Identifier of the publishing entity to analyze.  |  

##### Returns

|  Type |  Description |  
| --- | --- | 
   
|  SmartProfile3D |  The smart profile.  |
