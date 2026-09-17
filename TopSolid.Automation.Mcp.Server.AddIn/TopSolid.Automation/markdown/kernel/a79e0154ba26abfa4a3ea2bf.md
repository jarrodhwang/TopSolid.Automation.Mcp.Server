Source: https://help.topsolid.com/7.20/en/TopSolid'Automation/api/kernel/TopSolid.Kernel.Automating.IParameters.GetToleranceUpperDeviation.html

Source SHA-256: `48f2bce38535b5160dd9f6c11e1bfdefb28f78a03f9e6363c1216f3577a4ce12`

# Method GetToleranceUpperDeviation

#### GetToleranceUpperDeviation(ElementId)

Gets the upper deviation value of a Tolerance parameter.

##### Declaration

```
[OperationContract]
[AvailableSinceVersion(7, 14, 0, 0)]
Real GetToleranceUpperDeviation(ElementId inElementId)
```

##### Parameters

|  Type |  Name |  Description |  
| --- | --- | --- | 
   
|  ElementId |  inElementId |  Identifier of the parameter to analyze.  |  

##### Returns

|  Type |  Description |  
| --- | --- | 
   
|  Real |  Upper deviation value of the parameter.  |  

##### Remarks

This method is available since v7.14.
