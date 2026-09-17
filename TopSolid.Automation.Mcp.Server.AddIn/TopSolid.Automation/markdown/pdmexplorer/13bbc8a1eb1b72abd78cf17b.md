Source: https://help.topsolid.com/7.20/en/TopSolid'Automation/api/pdmexplorer/TopSolid.Pdm.Explorer.Automating.IPdm.GetMinorRevisionText.html

Source SHA-256: `9751b96b774d4bee77105c4c1b139920aae4c1e979e386b0b757623232021818`

# Method GetMinorRevisionText

#### GetMinorRevisionText(PdmMinorRevisionId)

Gets the text of a minor revision.

##### Declaration

```
[OperationContract]
[AvailableSinceVersion(7, 11, 300, 80)]
string GetMinorRevisionText(PdmMinorRevisionId inMinorRevisionId)
```

##### Parameters

|  Type |  Name |  Description |  
| --- | --- | --- | 
   
|  PdmMinorRevisionId |  inMinorRevisionId |  Identifier of the minor revision.  |  

##### Returns

|  Type |  Description |  
| --- | --- | 
   
|  string |  Text of the minor revision.  |  

##### Remarks

This method is available since v7.10.
