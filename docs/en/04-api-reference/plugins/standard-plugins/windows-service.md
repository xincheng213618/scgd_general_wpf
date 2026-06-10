# WindowsServicePlugin

WindowsServicePlugin is the local service management plugin in the current repository. Its source is under `Plugins/WindowsServicePlugin/`. Its central job is to install, register, and maintain local ColorVision Windows services, and to synchronize MySQL, MQTT, and service configuration.

## manifest

| Field | Current value |
| --- | --- |
| `Id` | `WindowsServicePlugin` |
| `name` | `视彩服务插件` |
| `version` | `1.0` |
| `dllpath` | `WindowsServicePlugin.dll` |
| `requires` | `1.3.12.34` |

## Current Capability Boundary

The current implementation focuses on the `CVWindowsService` workflow:

- Manage `RegistrationCenterService`, `CVMainService_x64`, and `CVMainService_dev`.
- Install or update a complete `CVWindowsService` service package.
- Install/register MySQL and execute service database SQL.
- Install or open MQTT when required.
- Synchronize `cfg/MySql.config`, `cfg/MQTT.config`, and `cfg/WinService.config`.
- Provide local database and service file backup/restore.

Old CVWinSMS online download, incremental update, external management tool entry, service log menu, archive service unregister, License, and RESTful descriptions are no longer the current plugin surface.

## Main Entry Files

| File | Purpose |
| --- | --- |
| `ServiceManager/MenuServiceManager.cs` | Help menu entry, opens the service manager |
| `ServiceManager/InstallServiceManager.cs` | Wizard entry, opens the service manager |
| `ServiceManager/ServiceManagerWindow.xaml(.cs)` | Service manager window |
| `ServiceManager/ServiceManagerViewModel*.cs` | Service state, commands, MySQL/MQTT management, config synchronization |
| `ServiceManager/ServiceInstallWindow.xaml(.cs)` | Local install/update window |
| `ServiceManager/ServiceInstallViewModel*.cs` | Service package, MySQL ZIP, MQTT installer orchestration |
| `ServiceManager/ServiceManagerConfig.cs` | Service root directory and install options |
| `ServiceManager/Mysql/` | MySQL status, installation, SQL execution |
| `ServiceManager/Mqtt/` | MQTT status and installation |
| `CVWinSMS/CVWinSMSConfig.cs` | Only used to read legacy `App.config` paths |

## How the Service Manager Works

`ServiceManagerWindow` is mostly a window shell. The central state is `ServiceManagerViewModel.Instance`.

It is responsible for:

- Refreshing service installation and running status.
- Starting and stopping services.
- Resolving `BaseLocation` from service installation paths.
- Opening service folders and configuration files.
- Registering services from a complete service package.
- Writing current MySQL/MQTT/service configuration back to the service directory.
- Managing MySQL and MQTT status.
- Restarting the main program with administrator privileges.

Service operations usually require administrator privileges. Documentation, testing, and field operation should assume administrator mode.

## Complete Installation Flow

Recommended flow:

1. Start ColorVision as administrator.
2. Open the service manager.
3. Confirm `BaseLocation`, for example `D:\CVService`.
4. Open the install window.
5. Select a complete `CVWindowsService` package, for example `CVWindowsService[4.0.6.603]-0603.zip`.
6. Select MySQL ZIP and MQTT installer if needed.
7. Execute installation.

The full package install will:

- Check whether the package contains service root folders such as `RegWindowsService`, `CVMainWindowsService_x64`, or `CVMainWindowsService_dev`.
- Stop managed services before installation.
- Clean only top-level targets that actually exist in the selected service package.
- Extract the package to `BaseLocation`.
- Copy `CommonDll` into service directories and remove the temporary `CommonDll`.
- Unregister and register Windows services from the package.
- Synchronize MySQL, MQTT, and WinService configuration.
- Optionally execute `SQL/color_vision_all.sql`.
- Optionally start services after installation.

The current flow does not support incremental update packages.

## MySQL and MQTT

MySQL management code is concentrated in:

- `ServiceManager/MySqlServiceHelper.cs`
- `ServiceManager/Mysql/MySqlServiceManager.cs`
- `ServiceManager/Mysql/MySqlServiceConfig.cs`

The default business database is `color_vision_4xx`, and the business user is `cv`. SQL files are read as UTF-8 first, then GB18030 as fallback, and are sent to `mysql.exe` as UTF-8 to avoid Chinese SQL import failures.

MQTT management code is concentrated in:

- `ServiceManager/Mqtt/MqttServiceManager.cs`
- `ServiceManager/Mqtt/MqttServiceConfig.cs`

The install flow installs or opens MQTT only when required. It is not documented as a standalone operations platform.

## Build and Package

Build:

```powershell
dotnet build Plugins/WindowsServicePlugin/WindowsServicePlugin.csproj -c Release -p:Platform=x64
```

Package:

```powershell
Scripts\package_plugin.bat WindowsServicePlugin --no-upload
```

PostBuild copies the main DLL, `manifest.json`, `README.md`, and `CHANGELOG.md` to the host plugin directory.

## Handoff Acceptance

| Item | Action | Pass criteria |
| --- | --- | --- |
| Plugin loading | Check `manifest.json`, `dllpath`, and the Help menu | The service manager entry and install wizard entry are visible, and their windows open |
| Permission boundary | Run key commands as a normal user and as administrator | Destructive actions fail clearly as a normal user; register/start/stop work in an administrator test environment |
| Read-only refresh | Open the service manager and refresh | `BaseLocation`, service installation state, running state, and configuration paths are visible |
| Open folder/config | Use open service folder and CFG actions | The current managed service directory and its `cfg` files open |
| MySQL management | Check status, write config, or run test SQL | `MySqlServiceManager` reads state, and config changes sync to the service directory |
| MQTT management | Check status, install, or open MQTT | `MqttServiceManager` recognizes process/service state, and connection config can sync |
| Service package validation | Select an invalid ZIP and a complete service ZIP | Invalid packages are rejected; valid packages expose service root directories and `CommonDll` |
| Backup and overwrite | Install over an existing service directory | A backup is created before install, and overwrite scope is limited to top-level folders present in the package |
| Register and control services | Register, start, stop, restart, and unregister in a test environment | Service state changes match window logs, and failures expose traceable causes |
| Delivery structure | Build or package the plugin | `WindowsServicePlugin.dll`, `manifest.json`, `README.md`, and `CHANGELOG.md` exist |

## First Checks

| Symptom | Check first |
| --- | --- |
| Help menu has no service entry | Plugin folder, `manifest.json`, `WindowsServicePlugin.dll`, `MenuServiceManager`, and `InstallServiceManager` loading |
| Operation reports access denied | Administrator startup, UAC, service-control permission, and service folder ACL |
| `BaseLocation` is empty | Whether service is installed, service path/registry is readable, and `ServiceManagerConfig` is set |
| Service package cannot install | Whether the ZIP is a complete `CVWindowsService` package with service root folders and `CommonDll` |
| Service registration fails | Service-name conflict, old service stop/unregister state, and expected exe under install path |
| Service starts then stops immediately | Service directory `cfg`, MySQL/MQTT reachability, Windows Event Log, and service logs |
| MySQL config does not apply | `cfg/MySql.config` write path, port, account, SQL encoding, and `mysql.exe` call |
| MQTT config does not apply | `cfg/MQTT.config` write path, MQTT installer/process state, and port occupancy |
| Rollback is hard | Pre-install backup folder, original service package, original `cfg` files, and window logs |
| Window stalls or state does not refresh | Background command timeout, window logs, service-control return codes, and refresh-command state |

## Handoff Notes

- Service install, registration, and configuration synchronization may modify local Windows service state. Verify as administrator.
- If configuration synchronization fails, the install flow should fail rather than starting services with old configuration.
- Legacy CVWinSMS code is only retained for compatibility reading; do not re-document old download/update behavior as current capability.
- For field issues, first inspect window logs, then Windows service state, MySQL state, MQTT state, and CFG files in the service directory.

## Recommended Reading Order

1. `Plugins/WindowsServicePlugin/README.md`
2. `Plugins/WindowsServicePlugin/ServiceManager/MenuServiceManager.cs`
3. `Plugins/WindowsServicePlugin/ServiceManager/ServiceManagerViewModel.cs`
4. `Plugins/WindowsServicePlugin/ServiceManager/ServiceManagerViewModel.Config.cs`
5. `Plugins/WindowsServicePlugin/ServiceManager/ServiceInstallViewModel.cs`
6. `Plugins/WindowsServicePlugin/ServiceManager/ServiceInstallViewModel.Install.cs`
7. `Plugins/WindowsServicePlugin/ServiceManager/Mysql/MySqlServiceManager.cs`
8. `Plugins/WindowsServicePlugin/ServiceManager/Mqtt/MqttServiceManager.cs`
9. `Plugins/WindowsServicePlugin/manifest.json`

## Continue Reading

- [Existing Plugin Field Acceptance And Handoff Checklist](../plugin-field-acceptance.md)
- [Plugin Capability & Handoff Matrix](../plugin-capability-matrix.md)
- [Plugin Runtime And Handoff Playbook](../plugin-handoff-playbook.md)
