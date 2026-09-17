Source: https://help.topsolid.com/7.20/en/TopSolid'Automation/api/drafting/TopSolid.Cad.Drafting.Automating.ITables.SetDraftTableCellBackgroundColor.html

Source SHA-256: `3240355f7278cb25be8ce1e9f2fd8dcf6ec80a986105f61e6cf9a1ee885426a8`

# Method SetDraftTableCellBackgroundColor

#### SetDraftTableCellBackgroundColor(ElementId, int, int, Color)

Sets draft table background color

##### Declaration

```
[OperationContract]
[AvailableSinceVersion(7, 20, 255, 0)]
void SetDraftTableCellBackgroundColor(ElementId inElementId, int inColumnIdx, int inRowIdx, Color inColorValue)
```

##### Parameters

|  Type |  Name |  Description |  
| --- | --- | --- | 
   
|  ElementId |  inElementId |  Table to modify  |  
 
|  int |  inColumnIdx |  index of column  |  
 
|  int |  inRowIdx |  index of row  |  
 
|  Color |  inColorValue |  color to use  |
