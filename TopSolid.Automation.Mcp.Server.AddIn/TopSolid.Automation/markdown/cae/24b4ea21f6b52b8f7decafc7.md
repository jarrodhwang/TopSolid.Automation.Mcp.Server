Source: https://help.topsolid.com/7.20/en/TopSolid'Automation/api/cae/TopSolid.Cae.Kernel.Automating.ICAEDocuments.MeshNeedsRefreshing.html

Source SHA-256: `713691a37382c556aa62fd251e4c78ae18de426e9f063040e54d0aa2a53d7062`

# Method MeshNeedsRefreshing

#### MeshNeedsRefreshing(DocumentId)

Tells whether the mesh of an analysis preparation document needs to be refreshed.

##### Declaration

```
[OperationContract]
[AvailableSinceVersion(7, 19, 15, 0)]
bool MeshNeedsRefreshing(DocumentId inDocumentId)
```

##### Parameters

|  Type |  Name |  Description |  
| --- | --- | --- | 
   
|  DocumentId |  inDocumentId |  Identifier of the document to analyze.  |  

##### Returns

|  Type |  Description |  
| --- | --- | 
   
|  bool |  Whether mesh needs to be refreshed.  |  

##### Remarks

This method is available since v7.19.

##### Examples
 
```
DocumentId docId = TopSolidHost.Documents.EditedDocument;
string needsRefreshing = TopSolidCaeHost.Documents.MeshNeedsRefreshing(docId);
```
