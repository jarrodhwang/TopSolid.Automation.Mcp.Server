Source: https://help.topsolid.com/7.20/en/TopSolid'Automation/api/cad/TopSolid.Cad.Design.Automating.ICoatings.SetMetalnessFactor.html

Source SHA-256: `5b0a16e918778c254009b948370611d19032a5e9dd0dc69eaa74bef184324d5e`

# Method SetMetalnessFactor

#### SetMetalnessFactor(DocumentId, double)

Sets the Metalness factor.

##### Declaration

```
[OperationContract]
[AvailableSinceVersion(7, 16, 0, 0)]
void SetMetalnessFactor(DocumentId inDocumentId, double inValue)
```

##### Parameters

|  Type |  Name |  Description |  
| --- | --- | --- | 
   
|  DocumentId |  inDocumentId |  Identifier of the document to modify.  |  
 
|  double |  inValue |  Wanted value.  |  

##### Remarks

StartModification(string, bool) and EnsureIsDirty(ref DocumentId) must be called before calling this method.
 
This method is available since v7.16.
