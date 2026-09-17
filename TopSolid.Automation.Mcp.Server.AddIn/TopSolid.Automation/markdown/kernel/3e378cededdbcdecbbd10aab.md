Source: https://help.topsolid.com/7.20/en/TopSolid'Automation/api/kernel/TopSolid.Kernel.Automating.IPdm.SetPartNumber.html

Source SHA-256: `a55445b6fd29e33c7d3376ad5ba53f50d851c0d5522f7e2287f36551ed464429`

# Method SetPartNumber

#### SetPartNumber(PdmObjectId, string)

Sets the part number of an object.

##### Declaration

```
[OperationContract]
[AvailableSinceVersion(7, 8, 303, 100)]
void SetPartNumber(PdmObjectId inObjectId, string inPartNumber)
```

##### Parameters

|  Type |  Name |  Description |  
| --- | --- | --- | 
   
|  PdmObjectId |  inObjectId |  Object identifier.  |  
 
|  string |  inPartNumber |  Part number.  |  

##### Remarks

This method must not be called between StartModification(string, bool) and EndModification(bool, bool) methods calls.
 
This method is available since v7.8.303.100.
