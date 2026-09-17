Source: https://help.topsolid.com/7.20/en/TopSolid'Automation/api/kernel/TopSolid.Kernel.Automating.IUser.AskFace.html

Source SHA-256: `6e19a11b159ef58fcabe49e481585718801e662eb5531d5c9b2e7f1fe7006ad2`

# Method AskFace

#### AskFace(UserQuestion, ElementItemId, out ElementItemId)

Asks the user for a face.

##### Declaration

```
[OperationContract]
[AvailableSinceVersion(7, 7, 0, 0)]
UserAnswerType AskFace(UserQuestion inQuestion, ElementItemId inSuggestion, out ElementItemId outAnswer)
```

##### Parameters

|  Type |  Name |  Description |  
| --- | --- | --- | 
   
|  UserQuestion |  inQuestion |  Question asked to the user.  |  
 
|  ElementItemId |  inSuggestion |  Suggested answer, or Empty if none.  |  
 
|  ElementItemId |  outAnswer |  User answer, or Empty if none.  |  

##### Returns

|  Type |  Description |  
| --- | --- | 
   
|  UserAnswerType |  Type of user answer.  |  

##### Remarks

This method is available since v7.7.
