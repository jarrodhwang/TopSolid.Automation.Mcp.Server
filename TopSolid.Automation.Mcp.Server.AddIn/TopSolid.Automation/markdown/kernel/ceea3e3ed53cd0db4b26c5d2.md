Source: https://help.topsolid.com/7.20/en/TopSolid'Automation/api/kernel/TopSolid.Kernel.Automating.IFamilies.AddExplicitInstance.html

Source SHA-256: `c9fa4fab7112cb6a1d087f1b9c57e04bf3eca7b582ae2860b60998a6cef35524`

# Method AddExplicitInstance

#### AddExplicitInstance(DocumentId, string, DocumentId)

Adds an explicit instance to a family document catalog

##### Declaration

```
[OperationContract]
[AvailableSinceVersion(7, 18, 400, 160)]
void AddExplicitInstance(DocumentId inDocumentId, string inCode, DocumentId instanceDocumentId)
```

##### Parameters

|  Type |  Name |  Description |  
| --- | --- | --- | 
   
|  DocumentId |  inDocumentId |  Family document  |  
 
|  string |  inCode |  Code to add  |  
 
|  DocumentId |  instanceDocumentId |  Document used as an instance  |
