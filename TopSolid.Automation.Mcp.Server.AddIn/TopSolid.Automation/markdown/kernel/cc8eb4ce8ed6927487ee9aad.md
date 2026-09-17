Source: https://help.topsolid.com/7.20/en/TopSolid'Automation/api/kernel/TopSolid.Kernel.Automating.IParameters.GetTextParameterizedValue.html

Source SHA-256: `03afd7fa01ec857afe65ca5f89a7e755de86d7ad7eb357e49fc422fa105c2377`

# Method GetTextParameterizedValue

#### GetTextParameterizedValue(ElementId)

Gets the parameterized value of a parameterized text parameter.

##### Declaration

```
[OperationContract]
[AvailableSinceVersion(7, 7, 201, 200)]
string GetTextParameterizedValue(ElementId inElementId)
```

##### Parameters

|  Type |  Name |  Description |  
| --- | --- | --- | 
   
|  ElementId |  inElementId |  Identifier of the parameter to analyze.  |  

##### Returns

|  Type |  Description |  
| --- | --- | 
   
|  string |  Parameterized value of the parameter (ex: "Hexagon Bolt ISO 4014 - [$Code]").  |  

##### Remarks

If the text is not parameterized, this method returns null.
 
This method is available since v7.7.201.200.
