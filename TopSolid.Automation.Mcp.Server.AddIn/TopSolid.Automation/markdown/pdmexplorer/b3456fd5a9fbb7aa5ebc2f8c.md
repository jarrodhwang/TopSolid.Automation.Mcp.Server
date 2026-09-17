Source: https://help.topsolid.com/7.20/en/TopSolid'Automation/api/pdmexplorer/TopSolid.Pdm.Explorer.Automating.TopSolidPdmExplorerHostInstance.html

Source SHA-256: `414b4cba2ced1e841c56b6f759ac362c66b0278cc34d429a1176a2b7c8985387`

# Class TopSolidPdmExplorerHostInstance

Represents a TopSolid'Pdm Explorer host instance providing WCF services for automating.

##### Inheritance
 
object
 
TopSolidPdmExplorerHostInstance

##### Inherited Members
 
 object.ToString() 
 
 object.Equals(object) 
 
 object.Equals(object, object) 
 
 object.ReferenceEquals(object, object) 
 
 object.GetHashCode() 
 
 object.GetType() 

###### Namespace: TopSolid.Pdm.Explorer.Automating

###### Assembly: TopSolid.Pdm.Explorer.Automating.dll

##### Syntax

```
[AvailableSinceVersion(7, 11, 300, 80)]
public sealed class TopSolidPdmExplorerHostInstance
```

##### Remarks

When automating several instances of TopSolid at the same time one must make and use several instances of this class (one per TopSolid instance), otherwize if only one instance of TopSolid is to be automated, it is possible to simply use the TopSolidPdmExplorerHost static class.
 
This class is available since v7.10.

### Constructors 

|  Name |  Description |  
| --- | --- | 
   
|   TopSolidPdmExplorerHostInstance()  |  Initializes a new instance of the TopSolidPdmExplorerHostInstance class.  |  

### Properties 

|  Name |  Description |  
| --- | --- | 
   
|   Application  |  Gets access to the application, or null if not available.  |  
 
|   Arguments  |  Gets or sets the arguments to declare to the process when the process needs to be started. To set before the call to the Connect() methods.  |  
 
|   ClientAddress  |  Gets the client IP address used for receiving events when using TCP, or null for not managing events.  |  
 
|   ClientName  |  Gets the client name, or null if anonymous.  |  
 
|   ClientPort  |  Gets the client port used for receiving events when using TCP, or 0 for not managing events.  |  
 
|   Erp  |  Gets access to the PDM, or null if not available.  |  
 
|   HostAddress  |  Gets the host IP address, or null if not using TCP.  |  
 
|   HostPort  |  Gets the host port, or 0 if not using TCP.  |  
 
|   IsConnected  |  Tells whether TopSolid is connected.  |  
 
|   Pdm  |  Gets access to the PDM, or null if not available.  |  
 
|   PdmAdmin  |  Gets access to the PDM administration, or null if not available.  |  
 
|   PdmSecurity  |  Gets access to the PDM security, or null if not available.  |  
 
|   PdmVisualization  |  Gets access to the IPdmVisualization, or null if not available.  |  
 
|   PdmWorkflow  |  Gets access to the PDM administration, or null if not available.  |  
 
|   PipeName  |  Gets or sets the name used to make named pipe, or null for default.  |  
 
|   Version  |  Gets the version of TopSolid host, or 0 if not connected.  |  

### Methods 

|  Name |  Description |  
| --- | --- | 
   
|   Connect()  |  Connects to TopSolid application.  |  
 
|   Connect(bool, int)  |  Connects to TopSolid application.  |  
 
|   Connect(bool, int, string)  |  Connects to TopSolid application.  |  
 
|   Connect(string)  |  Connects to TopSolid application.  |  
 
|   DefineConnection(string, int, string, int)  |  Defines the connection between this client application and TopSolid host for remote access.  |  
 
|   Disconnect()  |  Disconnects from TopSolid application.  |  
 
|   MakeClientHost(Type, Type)  |  Makes a TopSolid client WCF host.  |  

### Events 

|  Name |  Description |  
| --- | --- | 
   
|   Exited  |  This event is raised when TopSolid has exited.  |
