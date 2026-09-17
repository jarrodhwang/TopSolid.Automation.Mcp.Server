Source: https://help.topsolid.com/7.20/en/TopSolid'Automation/api/kernel/TopSolid.Kernel.Automating.IApplication.InvokeCommand.html

Source SHA-256: `a27e9069ee5351426dbde6de64ea5a0839a814198816762267950d242b2cdf56`

# Method InvokeCommand

#### InvokeCommand(string)

Invokes a specified menu command.

##### Declaration

```
[OperationContract]
[AvailableSinceVersion(7, 12, 0, 0)]
bool InvokeCommand(string inFullName)
```

##### Parameters

|  Type |  Name |  Description |  
| --- | --- | --- | 
   
|  string |  inFullName |  Full name of the command to invoke (i.e. "TopSolid.Kernel.UI.D3.Shapes.Creations.BlockCommand").  |  

##### Returns

|  Type |  Description |  
| --- | --- | 
   
|  bool |  The command has been found and invoked.  |  

##### Remarks

This method is available since v7.12.
