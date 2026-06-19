# CHANGELOG

## Unreleased

### Changed

- Focused `ServiceManager` on local full-package service installation, MySQL, and
  service config synchronization.
- Removed old online download and incremental-update code paths from the install
  window.
- Removed the external `CVWinSMS.exe` launch/download/update menu entry.
- Removed old service-log menu entries.
- Removed old wizard steps that depended on the external CVWinSMS `App.config` as
  the primary workflow.
- Changed install-time config synchronization to fail the install when config
  updates cannot be written, instead of continuing to start services.
- Changed SQL execution to read SQL text with UTF-8/GB18030 detection and send UTF-8
  to `mysql.exe`.
- Defaulted service MySQL database usage to `color_vision_4xx`.

### Kept

- Legacy `CVWinSMSPath` config remains only to locate an existing legacy
  `App.config` for migration/compatibility.
- Service install logs remain inside the install window as operational feedback.

## 1.4.3.8 - 2025-04-14

- Added the original in-app service manager and install/update window.
- Added MySQL/MQTT service-management helpers.
- Added legacy CVWinSMS compatibility.
