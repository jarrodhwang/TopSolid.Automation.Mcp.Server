Source: https://help.topsolid.com/7.20/en/TopSolid'Automation/api/kernel/TopSolid.Kernel.Automating.IParameters.GetNameParameter.html

Source SHA-256: `2fcfbcf17b7efc3915f99d891333d94a52304d730766fda85efa8172ad244f5d`

# Method GetNameParameter

#### GetNameParameter(DocumentId)

Gets the name parameter entity of a document.

##### Declaration

```
[OperationContract]
[AvailableSinceVersion(7, 6, 0, 0)]
ElementId GetNameParameter(DocumentId inDocumentId)
```

##### Parameters

|  Type |  Name |  Description |  
| --- | --- | --- | 
   
|  DocumentId |  inDocumentId |  Identifier of the document to analyze.  |  

##### Returns

|  Type |  Description |  
| --- | --- | 
   
|  ElementId |  Identifier of the name parameter entity found.  |  

##### Remarks

The name parameter is a text parameter, its value may be obtained and modified using the GetTextValue(ElementId) and SetTextValue(ElementId, string) methods.
 
This method is available since v7.6.
