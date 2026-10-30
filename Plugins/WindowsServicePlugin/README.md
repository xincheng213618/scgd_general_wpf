# WindowsServicePlugin

Windows/x64 WPF plugin for the ColorVision in-app service manager. The manifest
loads `WindowsServicePlugin.dll`; the project depends on matching ColorVision
Engine/UI assemblies and currently targets `net10.0-windows`.

The service manager is available from both `Help > Service Manager` and
`Applications and Tools > Internal Tools`; both entry points are available to
all ColorVision roles.

Use the canonical [package selection, local installation, database migration,
and recovery contract](../../docs/04-api-reference/plugins/standard-plugins/windows-service.md).
The [CVWindowsService Backend contract](../../docs/02-developer-guide/backend/cvwindowsservice.md)
owns service ZIP publication and HTTP selection/download behavior. These links
require the matching full source checkout; `docs/` is not included merely because
this README is copied into a plugin package.

Package-local prerequisites and warnings:

- The registration center row on the CVWindowsService tab provides "Remove archive
  service monitoring" in its more-actions menu for old
  deployments that keep trying to start CVArchService. It updates the current
  registration center's database monitor list and local cfg/WinService.config,
  retaining other database monitor entries. After confirmation, a running
  registration center briefly stops and resumes so the list takes effect;
  a stopped registration center remains stopped. File/database rights and a
  compatible broker are required. This does not uninstall services or delete data.
- `WindowsServicePlugin` is not the `CVWindowsService` service ZIP or the separate
  `ColorVisionServiceHost` privilege broker. Broker-backed operations require a
  compatible installed broker and appropriate service, file, and database rights.
  Opening the manager does not grant Windows elevation.
- Current installation uses full service packages. It can stop/register/start
  services, replace files, execute SQL, and change credentials/configuration. Confirm the targets and obtain maintenance authority.
- Database backup and restore are manual operations. A completed backup file or
  installation log is not proof of a successful recovery or healthy services;
  installation is not transactional.
- Online package selection and local cache reuse are implemented. This consumer
  checks package directory names, not a publisher signature or package hash.
- Publishing this plugin through `Scripts/package_plugin.bat WindowsServicePlugin`
  uploads a `.cvxp`; it neither publishes the service ZIP nor authorizes installing it.

The standalone WinExe startup currently initializes shared components and exits;
it is not a standalone service-manager distribution. Refer to the project,
manifest, and canonical topics for the matching source version.
