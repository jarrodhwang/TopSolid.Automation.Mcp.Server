Source: https://help.topsolid.com/7.20/en/TopSolid'Automation/api/kernel/TopSolid.Kernel.Automating.IPdm.HasConstituents.html

Source SHA-256: `eed5134a02cfbb77c67155c4ffcc9791a2a5b245b3ca71f3eef6a1f30f950230`

# Method HasConstituents

#### HasConstituents(PdmObjectId)

Tells whether an object has constituents.

##### Declaration

```
[OperationContract]
[AvailableSinceVersion(7, 9, 0, 0)]
bool HasConstituents(PdmObjectId inObjectId)
```

##### Parameters

|  Type |  Name |  Description |  
| --- | --- | --- | 
   
|  PdmObjectId |  inObjectId |  PDM object identifier of a project, a folder or a document.  |  

##### Returns

|  Type |  Description |  
| --- | --- | 
   
|  bool |  The object has constituents, i.e. GetConstituents(PdmObjectId, out List<PdmObjectId>, out List<PdmObjectId>) would not return only empty lists.  |  

##### Remarks

This method is available since v7.9.
