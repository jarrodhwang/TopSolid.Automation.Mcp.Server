# License

Registered tools:

- `topsolid_list_active_licenses` — inspection/reference.

Returns expiry, `IsActive`, license type, floating-license user, licensed owner, status, version and module validity. Null expiry is not a perpetual-license guarantee. Product packages with module zero have unknown module validity; they must not be passed to `IsLicenseValid(0)`.

Studio startup uses the dedicated read-only `topsolid/licenseStatus` protocol method and the authoritative `IApplication.IsLicenseValid(1000)` result. The API may omit an individual Kernel Base row from its active-license list even when that entitlement is valid. Optional metadata cannot override the validity check. See [startup behavior](../../../docs/LICENSE-STARTUP-0.5.17.md).

See [API coverage](../../../docs/api/COVERAGE.md) and the root validation report for source contracts and runtime limits.
