Source: https://help.topsolid.com/7.20/en/TopSolid'Automation/api/kernel/TopSolid.Kernel.Automating.IDocuments.UnsubscribeFromEventsTcp.html

Source SHA-256: `4eb4e44b58926b82972147a55d2deb1c30ff1ac4817c3f09199d5f71baf0ff09`

# Method UnsubscribeFromEventsTcp

#### UnsubscribeFromEventsTcp(string, string, int)

Unsubscribes from documents events using TCP connection.

##### Declaration

```
[OperationContract]
[AvailableSinceVersion(7, 9, 0, 0)]
void UnsubscribeFromEventsTcp(string inClientName, string inClientAddress, int inClientPort)
```

##### Parameters

|  Type |  Name |  Description |  
| --- | --- | --- | 
   
|  string |  inClientName |  Client name.  |  
 
|  string |  inClientAddress |  Client IP address or "localhost" for local machine.  |  
 
|  int |  inClientPort |  Client port used.  |  

##### Remarks

This method is available since v7.9.
