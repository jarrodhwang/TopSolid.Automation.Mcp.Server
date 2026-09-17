Source: https://help.topsolid.com/7.20/en/TopSolid'Automation/api/kernel/TopSolid.Kernel.Automating.ISketches3D.IsSegmentConstruction.html

Source SHA-256: `3e2970fc388d7afef32a92a6183dc5860acafdbcfcf288882a90709c16778834`

# Method IsSegmentConstruction

#### IsSegmentConstruction(ElementItemId)

Tells whether a segment is construction.

##### Declaration

```
[OperationContract]
[AvailableSinceVersion(7, 7, 201, 80)]
bool IsSegmentConstruction(ElementItemId inSegmentId)
```

##### Parameters

|  Type |  Name |  Description |  
| --- | --- | --- | 
   
|  ElementItemId |  inSegmentId |  Identifier of the segment to analyze.  |  

##### Returns

|  Type |  Description |  
| --- | --- | 
   
|  bool |  Whether the segment is construction.  |  

##### Remarks

This method is available since v7.7.201.80.
