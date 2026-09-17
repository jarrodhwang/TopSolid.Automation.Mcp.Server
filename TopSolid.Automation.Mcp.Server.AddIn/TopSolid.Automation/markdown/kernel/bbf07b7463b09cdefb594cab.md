Source: https://help.topsolid.com/7.20/en/TopSolid'Automation/api/kernel/TopSolid.Kernel.Automating.TopSolidHostInstance.Version.html

Source SHA-256: `96bb9ced697eef2c8f4af5864b8de544a4e3b52ef3972ba2bcec904f616976c5`

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
