Source: https://help.topsolid.com/7.20/en/TopSolid'Automation/api/cad/TopSolid.Cad.Design.Automating.IAssemblies.AskOccurrenceList.html

Source SHA-256: `4f90844ce09a63bbd4e23e2959c02f1085b10fac630cdb4d5aace922a1309b32`

# Method AskOccurrenceList

#### AskOccurrenceList(UserQuestion, bool, bool, bool, bool, bool, List<ElementId>, out List<ElementId>)

Asks the user for a part or an assembly occurrence List .

##### Declaration

```
[OperationContract]
[AvailableSinceVersion(7, 20, 400, 140)]
UserAnswerType AskOccurrenceList(UserQuestion inQuestion, bool inAcceptsPositioned, bool inAcceptsInPlace, bool inAcceptsPart, bool inAcceptsAssembly, bool inAcceptsMechanism, List<ElementId> inSuggestions, out List<ElementId> outAnswer)
```

##### Parameters

|  Type |  Name |  Description |  
| --- | --- | --- | 
   
|  UserQuestion |  inQuestion |  Question asked to the user.  |  
 
|  bool |  inAcceptsPositioned |  Accepts positioned occurrences.  |  
 
|  bool |  inAcceptsInPlace |  Accepts in-place occurrences.  |  
 
|  bool |  inAcceptsPart |  Accepts part occurrences.  |  
 
|  bool |  inAcceptsAssembly |  Accepts assembly occurrences.  |  
 
|  bool |  inAcceptsMechanism |  Accepts mechanism occurrences.  |  
 
|  List<ElementId> |  inSuggestions |  Suggested answer, or null if none.  |  
 
|  List<ElementId> |  outAnswer |  User answer, or empty list if none.  |  

##### Returns

|  Type |  Description |  
| --- | --- | 
   
|  UserAnswerType |  Type of user answer.  |  

##### Remarks

Only occurrences that are directly in the parts folder of the document may be obtained by this method, not "deep" occurrences that are inside them.
 
This method is available since v7.20.
