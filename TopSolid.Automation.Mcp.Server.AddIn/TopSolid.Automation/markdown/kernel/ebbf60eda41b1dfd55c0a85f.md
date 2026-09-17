Source: https://help.topsolid.com/7.20/en/TopSolid'Automation/api/kernel/TopSolid.Kernel.Automating.ISketches2D.HighlightSketchProfile.html

Source SHA-256: `533f32d7ede9b7cf114211fbc74a07253a25db3e0eae9f92c2e6c267b25800ba`

# Method HighlightSketchProfile

#### HighlightSketchProfile(ElementItemId)

Highlights a 2D sketch profile.

##### Declaration

```
[OperationContract]
[AvailableSinceVersion(7, 20, 326, 0)]
void HighlightSketchProfile(ElementItemId inProfileId)
```

##### Parameters

|  Type |  Name |  Description |  
| --- | --- | --- | 
   
|  ElementItemId |  inProfileId |  Id of the profile to highlight.  |  

##### Remarks

Sketch container document needs to be active. Sketch has to be displayed in the window.
 
This method is available since v7.20.326.0.
