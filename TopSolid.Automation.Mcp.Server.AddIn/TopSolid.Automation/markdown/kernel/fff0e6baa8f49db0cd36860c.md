Source: https://help.topsolid.com/7.20/en/TopSolid'Automation/api/kernel/TopSolid.Kernel.Automating.ITextures.SetRedWayRealTimeFile.html

Source SHA-256: `55a933555c0d2c91c5816c217f6f67c5a3bbfe40397018aabafd6dd9e81719d1`

# Method SetRedWayRealTimeFile

#### SetRedWayRealTimeFile(DocumentId, string)

Sets the redway real-time file.

##### Declaration

```
[OperationContract]
[AvailableSinceVersion(7, 16, 0, 0)]
void SetRedWayRealTimeFile(DocumentId inDocumentId, string inFullPath)
```

##### Parameters

|  Type |  Name |  Description |  
| --- | --- | --- | 
   
|  DocumentId |  inDocumentId |  Identifier of the document to modify.  |  
 
|  string |  inFullPath |  File path of the picture.  |  

##### Remarks

StartModification(string, bool) and EnsureIsDirty(ref DocumentId) must be called before calling this method.
 
This method is available since v7.16.
