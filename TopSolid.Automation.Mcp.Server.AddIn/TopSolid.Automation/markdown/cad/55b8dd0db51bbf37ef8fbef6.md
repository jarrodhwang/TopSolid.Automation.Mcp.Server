Source: https://help.topsolid.com/7.20/en/TopSolid'Automation/api/cad/TopSolid.Cad.Design.Automating.ICoatings.SetAlbedoColor.html

Source SHA-256: `ea03e3dbc84a4a705ee4c444ef1cb8994b6e1cb9980be8b402ff358dec165c2c`

# Method SetAlbedoColor

#### SetAlbedoColor(DocumentId, Color)

Sets the Albedo color.

##### Declaration

```
[OperationContract]
[AvailableSinceVersion(7, 16, 0, 0)]
void SetAlbedoColor(DocumentId inDocumentId, Color inValue)
```

##### Parameters

|  Type |  Name |  Description |  
| --- | --- | --- | 
   
|  DocumentId |  inDocumentId |  Identifier of the document to modify.  |  
 
|  Color |  inValue |  Wanted value.  |  

##### Remarks

StartModification(string, bool) and EnsureIsDirty(ref DocumentId) must be called before calling this method.
 
This method is available since v7.16.
