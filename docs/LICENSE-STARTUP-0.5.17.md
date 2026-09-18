# Startup license verification — 0.5.17

Studio opens its main window only after the connected TopSolid application returns `true` from `IApplication.IsLicenseValid(1000)`. This happens before constructing the chat/Developer window or starting AI model discovery. A compact native progress dialog appears during the check; the total startup query has a 30-second deadline. Cancellation cannot reopen the main window when a late reply arrives.

An invalid license shows the license dialog with **Close Studio**. An unavailable host, unsupported/old MCP server, malformed response or timeout shows **Unable to verify the TopSolid license**, then closes Studio when dismissed. TopSolid must already be ready in the same Windows session. The check does not start/stop TopSolid, change license allocation, modify CAD documents or submit an AI request. A successful check hands the existing MCP connection to the main window; it does not launch a second helper.

## License details

Open the main toolbar's connection-status icon, then **TopSolid licenses / TopSolid 라이선스**. The dialog shows the startup snapshot and its check time. Select a license to see its API-provided name, validity, expiration date, `IsActive`, type, floating-license user, licensed owner, status, license version and running TopSolid version. Native license/type icons are copied unchanged from `Cad/Kernel/WX/Protection/Commands` and recorded in the asset provenance manifest. The shared light/dark/custom TopSolid chrome and English/Korean resources apply.

The official `GetActveLicenses()` spelling is intentional. On the verified 7.20.400.107 host it returns product packages with `Module=0`, and does not include a separate Kernel Base row even though `IsLicenseValid(1000)` is true. Studio therefore uses the dedicated validity query for startup. The Kernel Base selection explains when separate metadata was not supplied; package metadata is not reassigned to it. Package module zero is never passed to `IsLicenseValid` and its independent validity is shown as unverified. Its returned active flag, status and expiry remain visible.

A missing expiration date means **No expiration date provided**, not perpetual. Localized status text, names, active flags and dates do not replace TopSolid's authoritative module-validity decision. Other-module validity queries are deduplicated; failures to obtain optional metadata do not overturn a successful Kernel Base check. Startup diagnostics record allow/deny/unverified only, without license users or owners.

The server exposes read-only `topsolid/licenseStatus` version 1, independent of model dispatch and pagination. The existing `topsolid_list_active_licenses` tool additionally returns expiry, license user/owner and module validity. No new model-visible tools or write permissions are introduced.

## Try it

1. With a ready, licensed TopSolid session, start the 0.5.17 Studio bundle. Expect a brief license check followed by the main window.
2. Open **Connection status → TopSolid licenses**. Select a product license and check expiry, active state, user, type, status and both versions. The Kernel Base module may appear separately with unavailable metadata, as explained above.
3. Chat: **현재 TopSolid 라이선스의 만료일, 활성 상태, 종류, 사용자, 상태와 버전을 보여 줘.** / **Show my active TopSolid licenses with their expiration dates, active state, type, user, status and versions.**
4. For an unavailable-host check, first finish/save work and close TopSolid normally, then start Studio. Expect an unverified-license explanation and no main/Developer window. Reopen TopSolid before using Studio again.

Invalid, expired, missing, wrong-module, malformed, cancelled and timed-out lookup cases are exercised with fixtures; testing does not revoke, deactivate or alter a real license.

## Validation and limits

Debug and Release builds, 41 application test groups, 15,267 offline server checks, 2,095 protocol checks and WPF license/dialog fixtures passed. The exact configured Debug server was queried live: TopSolid 7.20.400.107 confirmed module 1000 valid and returned 11 license entries. UI captures cover checking, valid/invalid/unavailable, packaged entitlements, light/dark and English/Korean, and were visually inspected. These are offscreen WPF renders; an actual expired-license installation and manual multi-monitor/DPI startup were not exercised. The license-detail dialog shows the startup snapshot, not continuous license monitoring.

Primary SDK references, also preserved in the repository's official API corpus:

- [IApplication.IsLicenseValid](https://help.topsolid.com/7.20/en/TopSolid'Automation/api/kernel/TopSolid.Kernel.Automating.IApplication.IsLicenseValid.html)
- [ILicenses.GetActveLicenses](https://help.topsolid.com/7.20/en/TopSolid'Automation/api/kernel/TopSolid.Kernel.Automating.ILicenses.GetActveLicenses.html)
- [License fields](https://help.topsolid.com/7.20/en/TopSolid'Automation/api/kernel/TopSolid.Kernel.Automating.License.html)
