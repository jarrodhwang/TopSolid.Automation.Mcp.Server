The shell uses the light and dark UI palettes supplied in the user's TopSolid color screenshots.
`TopSolidThemeFollower` supports `topsolid` (default), `system`, `light`, and `dark`. Pass the saved
mode to its constructor before first paint and call `SetMode` when the selection changes. `Mode`
reports the normalized choice, while `Current` reports the resolved palette and its source. An
unknown mode uses TopSolid. Fixed Light/Dark use the corresponding TopSolid base palette and skip
background source reads. TopSolid and System refresh every two seconds on a background task.
System reads `HKCU\Software\Microsoft\Windows\CurrentVersion\Themes\Personalize\AppsUseLightTheme`
without changing it (an absent value defaults to light); no operating-system event handler is attached.
The follower replaces WPF resources only when the resolved appearance changes, ignores stale reads
after a mode switch, and retains the last valid palette while a source is temporarily unavailable.
Dispose the follower with its owner.

Chrome uses TopSolid title/toolbar gradients and blue selection highlights. Light appearance uses
the reference white edit surfaces, black dialog text, lavender-gray `#E4E4EB` panels, native grayscale
button gradients and peach/orange command feedback. Dark appearance uses blue hover and pressed
command feedback, including buttons, combo boxes, lists and tabs. The chat composer uses rounded, compact controls on quiet surfaces, with
neutral conversation text and blue role labels. `ControlChrome` shares corner radius and selected
state between templates. Brushes are frozen and replaced only when the theme changes; hover,
focus and selection are WPF triggers rather than per-control timers or animations.

Observed TopSolid 7.20 settings location:

`%APPDATA%\TOPSOLID\TopSolid\7.20\TopSolid\Kernel\SX\Config.ConfigData.xml`

`CurrentTheme` beneath an `Application` folder selects `TopSolid Classic`, `TopSolid Dark`, or a
user theme. Absence means Classic. `StartPage/StartTabPage/ColorTheme` is unrelated and is ignored.
When several versions have settings, the newest available version is used. No installed TopSolid
assemblies are loaded by the application, and no TopSolid setting is written.

User themes live in the version's `Themes\<name>.xml` directory. Relevant `Colors/Color` entries
such as `global_dialog_back` override the matching UI resources. Dialog background luminance
determines whether to use the dark or light base palette. `enableLowBrightness` is deliberately
not used as a UI-dark-mode flag because TopSolid uses it to invert CAD entity colors.

This follows persisted settings, so a TopSolid change becomes visible once TopSolid saves it.
No TopSolid settings yields a clearly identified Classic fallback. Polling is bounded, overlapping
reads are suppressed, XML DTDs and external entities are prohibited, and file access permits
TopSolid to replace its files. Custom theme names cannot escape the Themes directory.

Native Windows caption colors are applied where supported; the standard titlebar remains usable
on versions without the relevant DWM attributes. WPF theme resources are dynamic, including text
already present in the conversation, controls, settings, approvals and the developer dashboard.

The setting names and XML format were inspected read-only in the installed TopSolid 7.20 binaries.
Official user-facing theme behavior is documented at:
https://help.topsolid.com/7.18/en/TopSolid%27Design/TopSolid/Kernel/WX/Options/themesapplicationoptionscontrol.htm
