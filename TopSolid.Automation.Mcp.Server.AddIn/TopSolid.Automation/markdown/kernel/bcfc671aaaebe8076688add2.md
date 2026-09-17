Source: https://help.topsolid.com/7.20/en/TopSolid'Automation/api/kernel/TopSolid.Kernel.Automating.ISketches2D.GetFrame.html

Source SHA-256: `7f17302308e9ef6d6db51ba2815eff4bbb211a0e0bae87f88a3d198941e8de70`

# Method GetFrame

#### GetFrame(ElementId)

Gets the definition frame of a sketch in a 2D document.

##### Declaration

```
[OperationContract]
[AvailableSinceVersion(7, 6, 0, 0)]
Frame2D GetFrame(ElementId inElementId)
```

##### Parameters

|  Type |  Name |  Description |  
| --- | --- | --- | 
   
|  ElementId |  inElementId |  Identifier of the sketch entity to analyze.  |  

##### Returns

|  Type |  Description |  
| --- | --- | 
   
|  Frame2D |  Definition frame of the sketch.  |  

##### Remarks

This method is available since v7.6.
