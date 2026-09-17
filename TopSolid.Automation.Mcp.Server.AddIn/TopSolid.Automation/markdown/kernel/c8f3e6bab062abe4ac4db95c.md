Source: https://help.topsolid.com/7.20/en/TopSolid'Automation/api/kernel/TopSolid.Kernel.Automating.IParameters.GetEnumerationPublishingDefinition.html

Source SHA-256: `2e1e674696fd24c404110b443bba92781d4c7c2361a71caa260a07199e142276`

# Method GetEnumerationPublishingDefinition

#### GetEnumerationPublishingDefinition(ElementId)

Gets the definition of an enumeration publishing entity.

##### Declaration

```
[OperationContract]
[AvailableSinceVersion(7, 8, 0, 0)]
SmartEnumeration GetEnumerationPublishingDefinition(ElementId inElementId)
```

##### Parameters

|  Type |  Name |  Description |  
| --- | --- | --- | 
   
|  ElementId |  inElementId |  Identifier of the publishing entity to analyze.  |  

##### Returns

|  Type |  Description |  
| --- | --- | 
   
|  SmartEnumeration |   |  

##### Remarks

The value of the publishing entity may be obtained with the GetEnumerationValue(ElementId) method.
 
This method is available since v7.8.
