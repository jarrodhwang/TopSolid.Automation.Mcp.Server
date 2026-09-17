Source: https://help.topsolid.com/7.20/en/TopSolid'Automation/api/cad/TopSolid.Cad.Design.Automating.IMaterials.SetHomogeneousParameterValue.html

Source SHA-256: `844a2fd7f4f55e35e43e7c90f760a8ee6afbd1d7979ce352d2999c6ede1dc76d`

# Method SetHomogeneousParameterValue

#### SetHomogeneousParameterValue(DocumentId, bool?)

Sets the Homogeneous Parameter

##### Declaration

```
[OperationContract]
[AvailableSinceVersion(7, 20, 400, 120)]
bool SetHomogeneousParameterValue(DocumentId inDocumentId, bool? inBool)
```

##### Parameters

|  Type |  Name |  Description |  
| --- | --- | --- | 
   
|  DocumentId |  inDocumentId |  Identifier of the document to modify.  |  
 
|  bool? |  inBool |  Value  |  

##### Returns

|  Type |  Description |  
| --- | --- | 
   
|  bool |  True if value is correctly set.  |  

##### Remarks

This method is available since v7.20.
