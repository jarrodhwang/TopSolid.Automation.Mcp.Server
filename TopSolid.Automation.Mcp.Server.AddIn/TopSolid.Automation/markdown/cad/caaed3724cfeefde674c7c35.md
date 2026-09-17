Source: https://help.topsolid.com/7.20/en/TopSolid'Automation/api/cad/TopSolid.Cad.Design.Automating.IMultiLayer.SetCategory.html

Source SHA-256: `20b9158e8fec75d5c344555c72a32089995f5f6e712b2b0595666bce9b32468c`

# Method SetCategory

#### SetCategory(DocumentId, MultiLayerMaterialCategoryType)

Sets the multi layer material category of a material document.

##### Declaration

```
[OperationContract]
[AvailableSinceVersion(7, 19, 0, 0)]
void SetCategory(DocumentId inDocumentId, MultiLayerMaterialCategoryType inMultiLayerMaterialCategoryType)
```

##### Parameters

|  Type |  Name |  Description |  
| --- | --- | --- | 
   
|  DocumentId |  inDocumentId |  Identifier of the document to modify.  |  
 
|  MultiLayerMaterialCategoryType |  inMultiLayerMaterialCategoryType |  The multi layer material category.  |  

##### Remarks

StartModification(string, bool) and EnsureIsDirty(ref DocumentId) must be called before calling this method.
 
This method is available since v7.19.
