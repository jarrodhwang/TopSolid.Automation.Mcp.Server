Source: https://help.topsolid.com/7.20/en/TopSolid'Automation/api/drafting/TopSolid.Cad.Drafting.Automating.IDraftings.GetViewProjectionSet.html

Source SHA-256: `b450b9c288b64c9c4bcebb0339a62a620e42ee75a20ddf566eafc4261406623e`

# Method GetViewProjectionSet

#### GetViewProjectionSet(ElementId, out DocumentId, out ElementId)

Gets view projection set

##### Declaration

```
[OperationContract]
[AvailableSinceVersion(7, 20, 255, 0)]
ElementId GetViewProjectionSet(ElementId inElementId, out DocumentId outViewProjectionSet, out ElementId outViewRepresentationId)
```

##### Parameters

|  Type |  Name |  Description |  
| --- | --- | --- | 
   
|  ElementId |  inElementId |  The view  |  
 
|  DocumentId |  outViewProjectionSet |  drawing set document used or empty  |  
 
|  ElementId |  outViewRepresentationId |  representation used or empty  |  

##### Returns

|  Type |  Description |  
| --- | --- | 
   
|  ElementId |   |
