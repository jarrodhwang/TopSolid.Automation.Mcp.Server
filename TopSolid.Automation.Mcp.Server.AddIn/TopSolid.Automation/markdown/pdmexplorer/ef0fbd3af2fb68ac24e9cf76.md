Source: https://help.topsolid.com/7.20/en/TopSolid'Automation/api/pdmexplorer/TopSolid.Pdm.Explorer.Automating.IPdm.GetMinorRevisions.html

Source SHA-256: `b3dd5f17b621e6da683ff85270423e5ecaa180dceb1631c65d7621733b9701a9`

# Method GetMinorRevisions

#### GetMinorRevisions(PdmMajorRevisionId)

Gets the minor revisions contained in a major revision.

##### Declaration

```
[OperationContract]
[AvailableSinceVersion(7, 11, 300, 80)]
List<PdmMinorRevisionId> GetMinorRevisions(PdmMajorRevisionId inMajorRevisionId)
```

##### Parameters

|  Type |  Name |  Description |  
| --- | --- | --- | 
   
|  PdmMajorRevisionId |  inMajorRevisionId |  Identifier of the major revision.  |  

##### Returns

|  Type |  Description |  
| --- | --- | 
   
|  List<PdmMinorRevisionId> |  Identifiers of the contained minor revisions.  |  

##### Remarks

This method is available since v7.10.
