Source: https://help.topsolid.com/7.20/en/TopSolid'Automation/api/drafting/TopSolid.Cad.Drafting.Automating.IDimensions.SetCurrentDimensionStyle.html

Source SHA-256: `380eceb9d5f3b05f293115004e73039b85fea184c2725c8c9922304aef7cc076`

# Method SetCurrentDimensionStyle

#### SetCurrentDimensionStyle(DocumentId, ElementId)

Sets current dimension style of a dimension

##### Declaration

```
[OperationContract]
[AvailableSinceVersion(7, 20, 255, 0)]
void SetCurrentDimensionStyle(DocumentId inDocumentId, ElementId inStyleElementId)
```

##### Parameters

|  Type |  Name |  Description |  
| --- | --- | --- | 
   
|  DocumentId |  inDocumentId |  The document  |  
 
|  ElementId |  inStyleElementId |  the style to use  |  

##### Remarks

StartModification(string, bool) and EnsureIsDirty(ref DocumentId) must be called before calling this method.
 
This method is available since v7.19.
