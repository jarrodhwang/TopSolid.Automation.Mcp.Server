Source: https://help.topsolid.com/7.20/en/TopSolid'Automation/api/kernel/TopSolid.Kernel.Automating.SmartFamily.html

Source SHA-256: `3ec723743293d879b030fde50f976bf818f4855f9808c2d48bede8b29a7f7183`

# Class SmartFamily

Represents a smart family.

##### Inheritance
 
object
 
SmartObject
 
SmartDocument
 
SmartFamily

##### Inherited Members
 
 SmartDocument.Type 
 
 SmartDocument.Document 
 
 SmartDocument.ElementId 
 
 SmartDocument.ReferenceType 
 
 object.ToString() 
 
 object.Equals(object) 
 
 object.Equals(object, object) 
 
 object.ReferenceEquals(object, object) 
 
 object.GetHashCode() 
 
 object.GetType() 
 
 object.MemberwiseClone() 

###### Namespace: TopSolid.Kernel.Automating

###### Assembly: TopSolid.Kernel.Automating.dll

##### Syntax

```
[DataContract]
[AvailableSinceVersion(7, 17, 0, 0)]
public class SmartFamily : SmartDocument
```

##### Remarks

This class is available since v7.17.

### Constructors 

|  Name |  Description |  
| --- | --- | 
   
|   SmartFamily(DocumentId)  |  Initializes a new instance of the SmartFamily class of type Basic  |  
 
|   SmartFamily(DocumentId, DocumentReferenceType)  |  Initializes a new instance of the SmartFamily class of type Basic  |  
 
|   SmartFamily(ElementId)  |  Initializes a new instance of the SmartFamily class of type Element.  |  
 
|   SmartFamily(ElementId, DocumentReferenceType)  |  Initializes a new instance of the SmartFamily class of type Element.  |
