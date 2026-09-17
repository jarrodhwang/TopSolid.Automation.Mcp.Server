Source: https://help.topsolid.com/7.20/en/TopSolid'Automation/api/kernel/TopSolid.Kernel.Automating.TopSolidHostInstance.HandleDocumentsEvents.html

Source SHA-256: `4db076d299ae58ebf7257ff90f866a8169545a1ee74594c683e4d8553c17ff0f`

# Method HandleDocumentsEvents

#### HandleDocumentsEvents(Type)

Starts documents events handling.

##### Declaration

```
public void HandleDocumentsEvents(Type inClassType)
```

##### Parameters

|  Type |  Name |  Description |  
| --- | --- | --- | 
   
|  Type |  inClassType |  Type of class implementing IDocumentsEvents interface.  |  

##### Remarks

To handle documents events, one needs to make a class implementing IDocumentsEvents interface, and call this method with that type in the inClassType argument.
 
This method creates a new service host, returned afterwards by the DocumentsEventsHost property.
 
This method then subscribes to documents events using the SubscribeToEvents(int, string) method.
 
This method may only be called when connected to TopSolid, otherwise an exception is thrown.
