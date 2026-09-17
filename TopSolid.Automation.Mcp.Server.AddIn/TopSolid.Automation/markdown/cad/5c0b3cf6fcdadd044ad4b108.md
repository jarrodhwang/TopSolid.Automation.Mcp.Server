Source: https://help.topsolid.com/7.20/en/TopSolid'Automation/api/cad/TopSolid.Cad.Design.Automating.ITools.html

Source SHA-256: `9c19b3c61da088aba01cf265d9698e785a20122f6b339365a0088cd3fdd75712`

# Interface ITools

Gives access to various tools.

###### Namespace: TopSolid.Cad.Design.Automating

###### Assembly: TopSolid.Cad.Design.Automating.dll

##### Syntax

```
[ServiceContract]
[AvailableSinceVersion(7, 10, 0, 0)]
public interface ITools
```

##### Remarks

This interface is available since v7.10.

### Methods 

|  Name |  Description |  
| --- | --- | 
   
|   CreateAuxiliairyElement(DocumentId, List<ElementId>)  |  Creates an auxiliairy element in a document.  |  
 
|   CreateDerivedDocument(PdmObjectId, DocumentId, bool)  |  Creates a new derived part or assembly of a specified part or assembly.  |  
 
|   CreateMirrorDocument(PdmObjectId, DocumentId, bool)  |  Creates the derived mirror part or assembly of a specified part or assembly.  |  
 
|   CreatePlaneSymmetry(DocumentId, SmartPlane3D)  |  Creates a plane symmetry.  |  
 
|   CreateRevolutionSymmetry(DocumentId, SmartAxis3D)  |  Creates a revolution symmetry.  |  
 
|   GetBaseDocument(DocumentId)  |  Gets the base part or assembly of a derived part or assembly.  |  
 
|   GetCatalogCellCoatingValue(ElementId, ElementId)  |  Gets the coating value of the catalog cell.  |  
 
|   GetCatalogCellFinishingValue(ElementId, ElementId)  |  Gets the finishing value of the catalog cell.  |  
 
|   GetCatalogCellMaterialValue(ElementId, ElementId)  |  Gets the material value of the catalog cell.  |  
 
|   GetDerivationInheritances(DocumentId, out bool, out bool, out bool, out bool, out bool, out bool, out bool, out bool, out List<ElementId>, out bool, out bool, out bool, out bool, out bool, out bool, out bool, out bool, out bool, out bool, out bool, out bool, out bool, out bool)  |  Gets the derivation inheritances of a derived part or assembly.  |  
 
|   GetInstanceFamilyDocument(DocumentId)  |  Gets the family document of an instance.  |  
 
|   GetPlaneSymmetry(ElementId, out SmartPlane3D)  |  Gets a plane symmetry.  |  
 
|   GetRevolutionSymmetry(ElementId, out SmartAxis3D)  |  Gets a revolution symmetry.  |  
 
|   GetStockManagement(DocumentId, out StockManagementType, out DocumentId, out ElementId, out SmartText, out SmartText, out SmartText, out SmartText)  |  Superseeded by GetStockManagement2(DocumentId, out StockManagementType, out DocumentId, out ElementId, out SmartText, out SmartText, out SmartText, out SmartText, out SmartText).  |  
 
|   GetStockManagement2(DocumentId, out StockManagementType, out DocumentId, out ElementId, out SmartText, out SmartText, out SmartText, out SmartText, out SmartText)  |  Gets the stock management of a part or assembly.  |  
 
|   GetStockParameters(DocumentId, out ElementId, out ElementId, out ElementId, out ElementId)  |  Superseeded by GetStockParameters2(DocumentId, out ElementId, out ElementId, out ElementId, out ElementId, out ElementId).  |  
 
|   GetStockParameters2(DocumentId, out ElementId, out ElementId, out ElementId, out ElementId, out ElementId)  |  Gets the stock parameters of a part or assembly.  |  
 
|   GetSymmetries(DocumentId)  |  Gets the symmetry entities that are in the symmetries folder of a part or assembly document.  |  
 
|   GetSymmetryType(ElementId)  |  Gets the type of a symmetry entity.  |  
 
|   InheritDetailingsElements(ElementId, EntityCopyNameMode, string)  |  Inherits the detailings elements of a node enttiy.  |  
 
|   IsDerived(DocumentId)  |  Tells whether part or assembly is a derived part or assembly.  |  
 
|   IsMirror(DocumentId)  |  Tells whether part or assembly is a derived mirror.  |  
 
|   IsSymmetrical(DocumentId)  |  Tells whether part or assembly is symmetrical.  |  
 
|   SearchMirrorDocument(DocumentId)  |  Searches for the derived mirror part or assembly of a specified part or assembly.  |  
 
|   SearchSymmetricalDocument(DocumentId)  |  Searches for the symmetrical part or assembly of a specified part or assembly.  |  
 
|   SetCatalogCellCoatingValue(ElementId, ElementId, PdmMinorRevisionId)  |  Sets the coating value of the catalog cell.  |  
 
|   SetCatalogCellFinishingValue(ElementId, ElementId, PdmMinorRevisionId)  |  Sets the finishing value of the catalog cell.  |  
 
|   SetCatalogCellMaterialValue(ElementId, ElementId, PdmMinorRevisionId)  |  Sets the material value of the catalog cell.  |  
 
|   SetDerivationInheritances(DocumentId, bool, bool, bool, bool, bool, bool, bool, bool, List<ElementId>, bool, bool, bool, bool, bool, bool, bool, bool, bool, bool, bool, bool, bool, bool)  |  Sets the derivation inheritances of a derived part or assembly.  |  
 
|   SetPlaneSymmetry(ElementId, SmartPlane3D)  |  Sets a plane symmetry.  |  
 
|   SetRevolutionSymmetry(ElementId, SmartAxis3D)  |  Sets a revolution symmetry.  |  
 
|   SetStockManagement(DocumentId, StockManagementType, DocumentId, ElementId, SmartText, SmartText, SmartText, SmartText)  |  Superseeded by SetStockManagement2(DocumentId, StockManagementType, DocumentId, ElementId, SmartText, SmartText, SmartText, SmartText, SmartText).  |  
 
|   SetStockManagement2(DocumentId, StockManagementType, DocumentId, ElementId, SmartText, SmartText, SmartText, SmartText, SmartText)  |  Sets the stock management of a part or assembly.  |
