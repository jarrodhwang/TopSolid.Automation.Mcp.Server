Source: https://help.topsolid.com/7.20/en/TopSolid'Automation/api/cam/TopSolid.Cam.NC.Kernel.Automating.IToolPath.html

Source SHA-256: `51ca5588889e0aef0c3f4f3b4002347a55e9fada8123787a8c4c16d5f3b96c51`

# Interface IToolPath

Gives access to operation tool path. A tool path is a list of lines in a table. Because a lot of possibilities is available to define a tool path item, TopSolid'Cam creates columns for all capabilities in the table, and each item only fill relevant columns.
 
Event | X | Y | Z | Fr | ...
 
Move | 10 | 10 | 20 | 150 | ...

###### Namespace: TopSolid.Cam.NC.Kernel.Automating

###### Assembly: TopSolid.Cam.NC.Kernel.Automating.dll

##### Syntax

```
[ServiceContract]
[AvailableSinceVersion(7, 8, 0, 0)]
public interface IToolPath
```

### Methods 

|  Name |  Description |  
| --- | --- | 
   
|   EndToolPath(ElementId)  |  Ends the scan of the given operation.  |  
 
|   NextToolPathItem(ElementId)  |  Gets the next tool path item values of the given operation.  |  
 
|   StartToolPath(ElementId)  |  Gets the tool path table columns of the given operation.  |
