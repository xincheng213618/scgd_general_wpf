# CHANGELOG

## [1.4.3.46] 2026.10.23

- 新增“移除归档服务检测”按钮，同步清理注册中心监控列表并恢复运行状态。
- 修复旧配置反复尝试启动已移除归档服务的问题。

## [1.4.3.45] 2026.10.22

- 修复在线下载受系统代理影响失败的问题，服务包保留完整 ZIP 文件名。
- 下载成功的组件立即回填，失败时提示具体组件和原因。
- 请先升级 ColorVision 至 1.4.17.36。

## [1.4.3.44] 2026.10.22

- 简化安装选项，移除服务文件夹备份和安装前数据库备份，保留数据库手动备份与恢复。
- 安装成功后自动关闭安装窗口，失败时保留窗口查看日志。

## [1.4.3.43] 2026.10.21

- 移除“检查旧服务管理工具更新”入口。
- 初始化向导直接进入服务安装，移除旧服务配置导入选择。

## [1.4.3.42] 2026.10.21

- 服务重装时停止并卸载已有归档服务，后续不再安装或恢复。
- 注册中心不再监控或自动启动归档服务；请先升级 ColorVision 至 1.4.17.33。

## [1.4.3.41] 2026.10.16

- 适配本机 MQTT 自动连接，服务配置统一使用本机地址及匿名连接。
- MySQL 安装与旧配置导入直接同步当前业务连接；请先升级 ColorVision 至 1.4.16.38。

## [1.4.3.40] 2026.10.08

- 随 ColorVision 1.4.16.5 同步更新，请先升级主程序。

## [1.4.3.39] 2026.10.07

- 随 ColorVision 1.4.16.1 更新，适配新版图像处理组件；需要先升级主程序。

## [1.4.3.38] 2026.10.03

- 随 ColorVision 1.4.15.54 同步更新插件包。

## [1.4.3.37] 2026.10.02

- 随 ColorVision 1.4.15.51 同步更新插件包。

## 1.4.3.36 - 2026-09-30

### Changed

- Localized the legacy service manager update menu, status messages, and download prompts.

## 1.4.3.35 - 2026-09-28

### Fixed

- Fixed MySQL reset stopping before the installation SQL runs because the import configuration lacked a database name.

## 1.4.3.34 - 2026-09-28

### Changed

- After resetting the MySQL database, offers to restart the registration center service and then ColorVision.

## 1.4.3.33 - 2026-09-28

### Fixed

- The MySQL Reset Database action now rebuilds the selected database from the installation SQL without restoring previous flows, templates, or resource data.
- Reset results clearly distinguish completion from a failed or partial import.

## 1.4.3.31 - 2026-09-11

### Fixed

- Synchronized the registration center monitor list with the database during service updates, retaining `CVArchService` only when it is installed.

## 1.4.3.30 - 2026-09-07

### Changed

- Restored the themed secondary-button treatment in the service installation window instead of falling back to gray hard-edged buttons.
- Tightened the primary install action while retaining the default checkbox selection visuals.

## 1.4.3.29 - 2026-09-06

### Changed

- Rebuilt the service manager for the ColorVision 1.4.14.15 release, retaining the existing service-termination client API that routes to the shared process-termination implementation in the updated service host.

## 1.4.3.28 - 2026-09-06

### Changed

- Refreshed the service manager, setup choice and installation windows with consistent light and dark themes and simpler service actions.
- Moved service logs to the last manager tab after MQTT, while preserving log capture across tab changes and simplifying installation-directory access.
- Kept installation packages, configuration and backup options visible together with the installation log and progress.

## 1.4.3.27 - 2026-09-04

### Fixed

- Restored the frequently used service manager shortcut under the Help menu
  while keeping the existing Applications and Tools entry and administrator
  permission requirement.

## 1.4.3.26 - 2026-08-19

### Changed

- Unified MySQL reset and restore operations with the shared database
  maintenance workflow, including preserved resource data and installed-service
  configuration synchronization.
- Added staged restore progress and stopped service restart when required MySQL
  configuration files cannot be synchronized.

## 1.4.3.25 - 2026-08-13

### Fixed

- Rebuilt the service manager against the current database migration API so
  preserved-table backup no longer fails with `MissingMethodException`.

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
