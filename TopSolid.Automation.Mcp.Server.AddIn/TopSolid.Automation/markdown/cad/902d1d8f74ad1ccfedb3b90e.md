Source: https://help.topsolid.com/7.20/en/TopSolid'Automation/api/cad/TopSolid.Cad.Design.Automating.IMaterials.SetCategory.html

Source SHA-256: `8c25813014172018346f8741174b9f43383ed2f77fe1ed685f51a211cf76bf48`

# Method SetCategory

#### SetCategory(DocumentId, MaterialCategoryType)

Sets the material category of a material document.

##### Declaration

```
[OperationContract]
[AvailableSinceVersion(7, 16, 0, 0)]
void SetCategory(DocumentId inDocumentId, MaterialCategoryType inMaterialCategoryType)
```

##### Parameters

|  Type |  Name |  Description |  
| --- | --- | --- | 
   
|  DocumentId |  inDocumentId |  Identifier of the document to modify.  |  
 
|  MaterialCategoryType |  inMaterialCategoryType |  The material category.  |  

##### Remarks

StartModification(string, bool) and EnsureIsDirty(ref DocumentId) must be called before calling this method.
 
This method is available since v7.16.
