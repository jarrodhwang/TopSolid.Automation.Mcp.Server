# Studio CAM face preview add-in

Read-only native display bridge, protocol 1. This DLL is loaded **inside TopSolid**, separately from the MCP executable. It reads existing face facets and native identities on TopSolid's UI thread. Occurrence coordinates use the native definition-to-document transform. It never starts a modification, regenerates geometry, colors, executes a method, saves, or invokes an arbitrary command.

TopSolid's external add-in loader requires a valid **TOPSOLID registration certificate for this add-in**. This is not the customer's product license or an AI API key. GetRegistrationCertificate reads `StudioCamPreview.registration.xml` beside this DLL. The application does not generate a certificate or bypass the host loader. Without registration/loading, Studio reports exact face preview unavailable and prevents applying an unverified preview.

Class: `TopSolid.Automation.CamPreview.PreviewAddIn`

Class GUID: `413735B9-A3A5-4B42-960B-694F95C74F66`

Build against the deployed TopSolid SDK (`TopSolidAutomationDirectory`). Protocol and exact native assembly version are checked on each connection. Install the release's DLL with its issued certificate into a directory already configured for TopSolid external add-ins, then restart TopSolid. Do not change native TopSolid binaries or attach a debugger to evade registration.

From the packaged `CamPreviewAddIn` directory:

```powershell
./Install-CamPreviewAddIn.ps1 -AddInDirectory 'C:\your-configured-addin-directory' -RegistrationCertificate 'C:\issued\StudioCamPreview.registration.xml'
```

The installer only installs these owned files. A conflicting Newtonsoft.Json in the target directory stops installation; select a dedicated configured add-in directory. The named pipe is restricted to the current Windows user and the server verifies its owning PID against the selected TopSolid instance. No filesystem paths or executable commands are accepted by the bridge.

Live deployment qualification must include a disposable CAM fixture, repeated instances, trimmed/curved faces, method selection, stage positioning and Undo. A build or offline mesh test is not evidence of native add-in loading.
