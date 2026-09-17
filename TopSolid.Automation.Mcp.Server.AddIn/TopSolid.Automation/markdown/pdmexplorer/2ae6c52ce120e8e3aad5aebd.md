Source: https://help.topsolid.com/7.20/en/TopSolid'Automation/api/pdmexplorer/TopSolid.Pdm.Explorer.Automating.TopSolidPdmExplorerHostInstance.Version.html

Source SHA-256: `6d88291ac28514267531b669f530266ad73d31050676919e043eb3711a780329`

# Property Version

#### Version

Gets the version of TopSolid host, or 0 if not connected.

##### Declaration

```
public int Version { get; }
```

##### Property Value

|  Type |  Description |  
| --- | --- | 
   
|  int |   |  

##### Remarks

The value returned is defined by: Major * 100000000 + Minor * 1000000 + Build * 1000 + Revision.
 
For example, v7.5.200.100 is returned as: 705200100.
