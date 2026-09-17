Source: https://help.topsolid.com/7.20/en/TopSolid'Automation/api/kernel/TopSolid.Kernel.Automating.IFamilies.html

Source SHA-256: `f3e108a0022677c9135212b8e4ee7e391d5ff3c6867592c3b326c48fb46112ff`

# Interface IFamilies

Gives access to families.

###### Namespace: TopSolid.Kernel.Automating

###### Assembly: TopSolid.Kernel.Automating.dll

##### Syntax

```
[ServiceContract]
[AvailableSinceVersion(7, 7, 0, 0)]
public interface IFamilies
```

##### Remarks

This interface is available since v7.7.

### Methods 

|  Name |  Description |  
| --- | --- | 
   
|   AddCatalogColumn(DocumentId, ElementId)  |  Add a new column to a family document  |  
 
|   AddCatalogNewRow(DocumentId)  |  Add a new row to the catalog cell.  |  
 
|   AddExplicitInstance(DocumentId, string, DocumentId)  |  Adds an explicit instance to a family document catalog  |  
 
|   CreateDerivedDocument(PdmObjectId, DocumentId, DocumentId)  |  Creates a new derived family document of a specified family document.  |  
 
|   DeleteCatalogRow(ElementId)  |  Delete the row of the catalog cell.  |  
 
|   GetBaseDocument(DocumentId)  |  Gets the base family of a derived family.  |  
 
|   GetCatalogCellBooleanValue(ElementId, ElementId)  |  Gets the boolean value of the catalog cell.  |  
 
|   GetCatalogCellCodeValue(ElementId, ElementId)  |  Gets the code value of the catalog cell.  |  
 
|   GetCatalogCellColorValue(ElementId, ElementId)  |  Gets the color value of the catalog cell.  |  
 
|   GetCatalogCellEnumerationValue(ElementId, ElementId)  |  Gets the enumeration value of the catalog cell.  |  
 
|   GetCatalogCellFamilyValue(ElementId, ElementId)  |  Gets the family value of the catalog cell.  |  
 
|   GetCatalogCellIntegerValue(ElementId, ElementId)  |  Gets the integer value of the catalog cell.  |  
 
|   GetCatalogCellRealValue(ElementId, ElementId)  |  Gets the real value of the catalog cell.  |  
 
|   GetCatalogCellTextValue(ElementId, ElementId)  |  Gets the text value of the catalog cell.  |  
 
|   GetCatalogCellUserEnumerationValue(ElementId, ElementId)  |  Gets the user enumeration value of the catalog cell.  |  
 
|   GetCatalogColumnParameters(DocumentId)  |  Gets the catalog column parameters.  |  
 
|   GetCatalogRow(DocumentId, string)  |  Gets the catalog row.  |  
 
|   GetCodes(DocumentId)  |  Gets the codes of a family document.  |  
 
|   GetCompleteConstraintData(ElementId)  |  Get the whole constraint data from a constraint entity  |  
 
|   GetConstrainedEntityCount(ElementId)  |  Get the number of entities constrained by a constraint entity  |  
 
|   GetDriverCondition(ElementId)  |  Gets condition on a driver, or null if no condition.  |  
 
|   GetDriverFolderCondition(ElementId)  |  Gets condition on a driver folder, or null if no condition.  |  
 
|   GetDriversFolderImage(ElementId)  |  Gets the drivers folder image.  |  
 
|   GetExplicitInstances(DocumentId, out List<string>, out List<PdmObjectId>)  |  Gets explicit family instances  |  
 
|   GetGenericDocument(DocumentId)  |  Gets the generic document of a family.  |  
 
|   GetOrderedConstrainedDrivers(ElementId)  |  Get the Driver entities constrained by a constraint entity  |  
 
|   IsDerived(DocumentId)  |  Tells whether a family document is a derived family document.  |  
 
|   IsExplicit(DocumentId)  |  Tells whether a family document is an explicit family document.  |  
 
|   IsFamily(DocumentId)  |  Tells whether a document is a family document.  |  
 
|   RemoveCatalogColumn(DocumentId, int)  |  Removes catalog column at a specified index.  |  
 
|   SetAsExplicit(DocumentId)  |  Sets family document as an explicit family document.  |  
 
|   SetCatalogCellBooleanValue(ElementId, ElementId, bool)  |  Sets the boolean value of the catalog cell.  |  
 
|   SetCatalogCellCodeValue(ElementId, ElementId, string)  |  Sets the code value of the catalog cell.  |  
 
|   SetCatalogCellColorValue(ElementId, ElementId, Color)  |  Sets the color value of the catalog cell.  |  
 
|   SetCatalogCellEnumerationValue(ElementId, ElementId, int)  |  Sets the enumeration value of the catalog cell.  |  
 
|   SetCatalogCellFamilyValue(ElementId, ElementId, PdmMinorRevisionId)  |  Sets the family value of the catalog cell.  |  
 
|   SetCatalogCellIntegerValue(ElementId, ElementId, int)  |  Sets the integer value of the catalog cell.  |  
 
|   SetCatalogCellRealValue(ElementId, ElementId, double)  |  Sets the real value of the catalog cell.  |  
 
|   SetCatalogCellTextValue(ElementId, ElementId, string)  |  Sets the text value of the catalog cell.  |  
 
|   SetCatalogCellUserEnumerationValue(ElementId, ElementId, int)  |  Sets the user enumeration value of the catalog cell.  |  
 
|   SetDriversFolderImage(ElementId, string)  |  Sets the drivers folder image.  |  
 
|   SetGenericDocument(DocumentId, DocumentId, DocumentId)  |  Sets the generic document of a family.  |  
 
|   SetInheritedCodes(DocumentId, List<string>)  |  Sets the inherited codes of a derived family.  |
