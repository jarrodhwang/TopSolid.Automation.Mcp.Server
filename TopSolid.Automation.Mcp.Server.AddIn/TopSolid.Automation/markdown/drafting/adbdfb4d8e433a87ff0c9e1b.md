Source: https://help.topsolid.com/7.20/en/TopSolid'Automation/api/drafting/TopSolid.Cad.Drafting.Automating.ITables.GetDraftTableCellText.html

Source SHA-256: `ec0f48390a1809f9b2d838556b7a577b8f8c6351f2da19a3cc62dfee4af2369f`

# Method GetDraftTableCellText

#### GetDraftTableCellText(ElementId, int, int, int)

Gets the text value of a cell.

##### Declaration

```
[OperationContract]
[AvailableSinceVersion(7, 14, 300, 140)]
string GetDraftTableCellText(ElementId inElementId, int inColumnIdx, int inRowIdx, int inSplitColumnIdx)
```

##### Parameters

|  Type |  Name |  Description |  
| --- | --- | --- | 
   
|  ElementId |  inElementId |  Identifier of the entity to analyze.  |  
 
|  int |  inColumnIdx |  Index of the column.  |  
 
|  int |  inRowIdx |  Index of the row.  |  
 
|  int |  inSplitColumnIdx |  Index of the split column. 0 if default.  |  

##### Returns

|  Type |  Description |  
| --- | --- | 
   
|  string |  Text value of a cell.  |  

##### Remarks

This method is available since v7.14.300.140.
