Source: https://help.topsolid.com/7.20/en/TopSolid'Automation/api/kernel/TopSolid.Kernel.Automating.IHealing.HealShape.html

Source SHA-256: `886ead6c02e8a65fed131d9949695bfcf756277541fd847c542478d8a6636bcc`

# Method HealShape

#### HealShape(ElementId, double, double, double, bool, double, bool, double, bool, bool, bool, bool, double, bool, bool, bool)

Method to heal a shape

##### Declaration

```
[OperationContract]
[AvailableSinceVersion(7, 20, 205, 0)]
void HealShape(ElementId inElementId, double inCleanAngularTolerance, double inCleanLinearTolerance, double inSimplifyLinearTolerance, bool inCleanUseRepairEdgesTolerance, double inCleanRepairEdgesTolerance, bool inUseRemoveSmallEdges, double inSmallEdgesLength, bool inRemoveSmallEntities, bool inPreserveTangencies, bool inOptimize, bool inUseRepairEdgesTolerance, double inRepairEdgesTolerance, bool inOptimizeBlendFaces, bool inMerge, bool inKeepSplitHoles)
```

##### Parameters

|  Type |  Name |  Description |  
| --- | --- | --- | 
   
|  ElementId |  inElementId |  Shape to heal  |  
 
|  double |  inCleanAngularTolerance |  angular tolerance for cleaning  |  
 
|  double |  inCleanLinearTolerance |  linear tolerance for cleaning  |  
 
|  double |  inSimplifyLinearTolerance |  linear tolerance for cleaning  |  
 
|  bool |  inCleanUseRepairEdgesTolerance |  whether to use repair edges option for cleaning  |  
 
|  double |  inCleanRepairEdgesTolerance |  edges tolerance for cleaning  |  
 
|  bool |  inUseRemoveSmallEdges |  whether to use remove small edges option  |  
 
|  double |  inSmallEdgesLength |  Small edges minimum length  |  
 
|  bool |  inRemoveSmallEntities |  whether to use remove small entities option  |  
 
|  bool |  inPreserveTangencies |   |  
 
|  bool |  inOptimize |  whether to use optimisation option  |  
 
|  bool |  inUseRepairEdgesTolerance |  whether to use repair edges option>  |  
 
|  double |  inRepairEdgesTolerance |  edges tolerance for cleaning  |  
 
|  bool |  inOptimizeBlendFaces |  whether to use blend faces optimisation option  |  
 
|  bool |  inMerge |  whether to use merge option  |  
 
|  bool |  inKeepSplitHoles |  whether to use keep split holes option  |  

##### Remarks

StartModification(string, bool) and EnsureIsDirty(ref DocumentId) must be called before calling this method.
 
This method is available since v7.20
