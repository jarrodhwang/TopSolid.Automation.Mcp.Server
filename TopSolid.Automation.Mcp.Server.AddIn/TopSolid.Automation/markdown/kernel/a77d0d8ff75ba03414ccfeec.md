Source: https://help.topsolid.com/7.20/en/TopSolid'Automation/api/kernel/TopSolid.Kernel.Automating.ISketches2D.IsItemFixed.html

Source SHA-256: `ba4942b139a8550f088ce2d26677d31406a14075f09e388665135cce307d1c80`

# Method IsItemFixed

#### IsItemFixed(ElementItemId)

Tells whether a segment or a vertex is fixed.

##### Declaration

```
[OperationContract]
[AvailableSinceVersion(7, 7, 201, 80)]
bool IsItemFixed(ElementItemId inItemId)
```

##### Parameters

|  Type |  Name |  Description |  
| --- | --- | --- | 
   
|  ElementItemId |  inItemId |  Identifier of the segment or the vertex to analyze.  |  

##### Returns

|  Type |  Description |  
| --- | --- | 
   
|  bool |  Whether the segment or the vertex is fixed.  |  

##### Remarks

This method is available since v7.7.201.80.
