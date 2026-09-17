Source: https://help.topsolid.com/7.20/en/TopSolid'Automation/api/kernel/TopSolid.Kernel.Automating.IVisualization3D.SetViewRenderMode.html

Source SHA-256: `e4c5111501cf9e8e3c73f6e518535fd0fdb6494206d592307056a79104137b5b`

# Method SetViewRenderMode

#### SetViewRenderMode(DocumentId, int, RenderMode)

Sets the render mode of a graphic view of a document.

##### Declaration

```
[OperationContract]
[AvailableSinceVersion(7, 7, 0, 0)]
void SetViewRenderMode(DocumentId inDocumentId, int inViewId, RenderMode inRenderMode)
```

##### Parameters

|  Type |  Name |  Description |  
| --- | --- | --- | 
   
|  DocumentId |  inDocumentId |  Identifier of the document to modify.  |  
 
|  int |  inViewId |  Identifier of the graphic view to modify.  |  
 
|  RenderMode |  inRenderMode |  View render mode.  |  

##### Remarks

This method is available since v7.7.
