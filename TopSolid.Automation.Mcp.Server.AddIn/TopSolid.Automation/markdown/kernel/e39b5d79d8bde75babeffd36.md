Source: https://help.topsolid.com/7.20/en/TopSolid'Automation/api/kernel/TopSolid.Kernel.Automating.IPdm.IsDirty.html

Source SHA-256: `c541249d4f008ac5d9588bf88d8be95de9c3cdc41279f1f35ed6efe59b6a2366`

# Method IsDirty

#### IsDirty(PdmObjectId)

Tells whether an object is dirty.

##### Declaration

```
[OperationContract]
[AvailableSinceVersion(7, 9, 300, 80)]
bool IsDirty(PdmObjectId inObjectId)
```

##### Parameters

|  Type |  Name |  Description |  
| --- | --- | --- | 
   
|  PdmObjectId |  inObjectId |  PDM object identifier.  |  

##### Returns

|  Type |  Description |  
| --- | --- | 
   
|  bool |  The object is dirty.  |  

##### Remarks

An object is said dirty when it has been modified since it was last saved.
 
This method is available since v7.9.300.80.
