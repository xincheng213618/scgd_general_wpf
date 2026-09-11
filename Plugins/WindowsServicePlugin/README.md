# WindowsServicePlugin

Windows/x64 WPF plugin for the ColorVision in-app service manager. The manifest
loads `WindowsServicePlugin.dll`; the project depends on matching ColorVision
Engine/UI assemblies and currently targets `net10.0-windows`.

The service manager is available from both `Help > Service Manager` and
`Applications and Tools > Internal Tools`; both entry points require the
ColorVision Administrator permission.

Use the canonical [package selection, local installation, database migration,
and recovery contract](../../docs/04-api-reference/plugins/standard-plugins/windows-service.md).
The [CVWindowsService Backend contract](../../docs/02-developer-guide/backend/cvwindowsservice.md)
owns service ZIP publication and HTTP selection/download behavior. These links
require the matching full source checkout; `docs/` is not included merely because
this README is copied into a plugin package.

Package-local prerequisites and warnings:

- `WindowsServicePlugin` is not the `CVWindowsService` service ZIP or the separate
  `ColorVisionServiceHost` privilege broker. Broker-backed operations require a
  compatible installed broker and appropriate service, file, and database rights.
  The app Administrator permission is not proof of Windows elevation.
- Current installation uses full service packages. It can stop/register/start
  services, replace files, execute SQL, change credentials/configuration, and close
  the old CVWinSMS process. Confirm the targets and obtain maintenance authority.
- Backup options are off by default; backup failures do not always stop installation.
  An installation-complete log is not proof that all services started. Manual
  service-file restore deletes the entire configured installation directory before
  extraction and is not a transactional rollback.
- Online package selection and local cache reuse are implemented. This consumer
  checks package directory names, not a publisher signature or package hash.
- Legacy `CVWinSMS/InstallTool` and service-log menu classes are still compiled and
  discoverable. The old tool path can query/download from its separate endpoint,
  replace files, terminate processes, and launch an elevated executable after its
  confirmation flow; do not treat it as inert configuration compatibility code.
- Publishing this plugin through `Scripts/package_plugin.bat WindowsServicePlugin`
  uploads a `.cvxp`; it neither publishes the service ZIP nor authorizes installing it.

The standalone WinExe startup currently initializes shared components and exits;
it is not a standalone service-manager distribution. Refer to the project,
manifest, and canonical topics for the matching source version.
