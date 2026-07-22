# CHANGELOG

## 1.4.3.24 - 2026-07-23

### Added

- Added localized resources for the refreshed service manager and service
  installation windows.

### Changed

- Preserved existing `CVArchService` installations during full service-package
  updates by uninstalling the legacy service before file replacement and
  reinstalling it afterward with its previous startup state.
- Focused `ServiceManager` on local full-package service installation, MySQL, and
  service config synchronization.
- Updated service manager UI labels, setup choices, install package controls, and
  file-picker captions to use plugin resources.
- Reworked service package, MySQL, MQTT, and VC++ 2013 package selection around
  the service installation window.
- Removed the external `CVWinSMS.exe` launch/download/update menu entry.
- Removed unused legacy localization keys for deleted helper menu actions.
- Removed old wizard steps that depended on the external CVWinSMS `App.config` as
  the primary workflow.
- Changed install-time config synchronization to fail the install when config
  updates cannot be written, instead of continuing to start services.
- Changed SQL execution to read SQL text with UTF-8/GB18030 detection and send UTF-8
  to `mysql.exe`.
- Defaulted service MySQL database usage to `color_vision_4xx`.
- Added version-aware database migration: CVWindowsService releases before 4.0 use
  `color_vision`, 4.0 and later use `color_vision_4xx`, and preserved resource data
  is restored into the target database before service config is switched.

### Kept

- Legacy `CVWinSMSPath` config remains only to locate an existing legacy
  `App.config` for migration/compatibility.
- Service install logs remain inside the install window as operational feedback.

## 1.4.3.8 - 2025-04-14

- Added the original in-app service manager and install/update window.
- Added MySQL/MQTT service-management helpers.
- Added legacy CVWinSMS compatibility.
