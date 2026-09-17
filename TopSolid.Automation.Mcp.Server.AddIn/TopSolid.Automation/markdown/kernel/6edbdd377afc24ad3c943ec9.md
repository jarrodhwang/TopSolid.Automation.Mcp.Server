Source: https://help.topsolid.com/7.20/en/TopSolid'Automation/api/kernel/TopSolid.Kernel.Automating.IGeometries2D.GetAxisPublishingDefinition.html

Source SHA-256: `808bdabd335acba8facaede6ef64081430b1d6f00a0db00f912d94ec1fad6fb0`

# Method GetAxisPublishingDefinition

#### GetAxisPublishingDefinition(ElementId)

Gets the definition of an axis publishing entity.

##### Declaration

```
[OperationContract]
[AvailableSinceVersion(7, 8, 0, 0)]
SmartAxis2D GetAxisPublishingDefinition(ElementId inElementId)
```

##### Parameters

|  Type |  Name |  Description |  
| --- | --- | --- | 
   
|  ElementId |  inElementId |  Identifier of the publishing entity to analyze.  |  

##### Returns

|  Type |  Description |  
| --- | --- | 
   
|  SmartAxis2D |   |  

##### Remarks

The geometry of the publishing entity may be obtained with the GetAxisGeometry(ElementId) method.
 
This method is available since v7.8.
