Source: https://help.topsolid.com/7.20/en/TopSolid'Automation/api/kernel/TopSolid.Kernel.Automating.IFamilies.GetCatalogColumnParameters.html

Source SHA-256: `57d7b831436f70e4aee9beeee7cbfe1b9775c7016c9c3dac69db8cfcf6b90c5f`

# Method GetCatalogColumnParameters

#### GetCatalogColumnParameters(DocumentId)

Gets the catalog column parameters.

##### Declaration

```
[OperationContract]
[AvailableSinceVersion(7, 15, 0, 60)]
List<ElementId> GetCatalogColumnParameters(DocumentId inDocumentId)
```

##### Parameters

|  Type |  Name |  Description |  
| --- | --- | --- | 
   
|  DocumentId |  inDocumentId |  Identifier of the family document to analyze.  |  

##### Returns

|  Type |  Description |  
| --- | --- | 
   
|  List<ElementId> |  Column parameter identifiers.  |  

##### Remarks

This method is available since v7.15.
