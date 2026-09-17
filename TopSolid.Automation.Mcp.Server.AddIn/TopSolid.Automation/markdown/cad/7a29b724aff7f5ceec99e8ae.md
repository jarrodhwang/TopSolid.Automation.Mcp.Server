Source: https://help.topsolid.com/7.20/en/TopSolid'Automation/api/cad/TopSolid.Cad.Design.Automating.IMaterials.GetMaterialApperanceInfo.html

Source SHA-256: `281ccfd0df874edb4db4c0a274b45901c4678c0636d9ace08dc8f272cba7023a`

# Method GetMaterialApperanceInfo

#### GetMaterialApperanceInfo(DocumentId, bool, bool, bool, bool)

Gets the material apperance information.

##### Declaration

```
[OperationContract]
[AvailableSinceVersion(7, 16, 400, 100)]
void GetMaterialApperanceInfo(DocumentId inDocumentId, bool outReceiveShadows, bool outCastShadows, bool outFresnelReflection, bool outTransparentReflection)
```

##### Parameters

|  Type |  Name |  Description |  
| --- | --- | --- | 
   
|  DocumentId |  inDocumentId |  Identifier of the document to analyze.  |  
 
|  bool |  outReceiveShadows |  Tells wether the material receive shadows.  |  
 
|  bool |  outCastShadows |  Tells wether the material cast shadows.  |  
 
|  bool |  outFresnelReflection |  Tells wether the material has a frenesl reflection.  |  
 
|  bool |  outTransparentReflection |  Tells wether the material has transparent reflection.  |  

##### Remarks

This method is available since v7.16.
