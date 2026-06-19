# WindowsServicePlugin

WindowsServicePlugin provides the in-app service manager used to install and maintain
the local ColorVision Windows services.

The current implementation is intentionally focused on the workflow needed for
`CVWindowsService`:

- manage `RegistrationCenterService`, `CVMainService_x64`, and `CVMainService_dev`;
- install or update a full `CVWindowsService` service package;
- install/register MySQL and run the service database SQL;
- install/open MQTT only as needed for the service to start;
- synchronize `cfg/MySql.config`, `cfg/MQTT.config`, and `cfg/WinService.config`;
- keep optional local backups for database and service files.

The older CVWinSMS-related online download, incremental update, service log menu,
archive-service unregister, license, RESTful, and external management-tool entry
points have been removed from the plugin surface.

## Entry Points

| Entry | Purpose |
| --- | --- |
| `ServiceManager/MenuServiceManager.cs` | Help menu entry for the in-app service manager. |
| `ServiceManager/InstallServiceManager.cs` | Wizard entry that opens the in-app service manager. |
| `ServiceManager/ServiceManagerWindow.xaml` | Main service manager window. |
| `ServiceManager/ServiceInstallWindow.xaml` | Local install/update window. |

`CVWinSMS/CVWinSMSConfig.cs` remains only for reading a legacy `App.config` path when
one already exists. It no longer downloads, updates, or launches the external
`CVWinSMS.exe` tool.

## Install Flow

1. Start ColorVision as administrator.
2. Open `ServiceManagerWindow`.
3. Confirm `BaseLocation`, for example `D:\CVService`.
4. Open the install window and select a full `CVWindowsService` package, such as
   `CVWindowsService[4.0.6.603]-0603.zip`.
5. Optionally select a local MySQL ZIP and MQTT installer.
6. Run install.

For a full service package, the installer:

- validates that the package contains service roots such as `RegWindowsService` and
  `CVMainWindowsService_x64` or `CVMainWindowsService_dev`;
- stops managed services before replacing service files;
- cleans only the top-level targets present in the selected service package;
- extracts the package into `BaseLocation`;
- copies `CommonDll` into the packaged service folders and removes the temporary
  `CommonDll` directory;
- unregisters and re-registers the packaged Windows services;
- synchronizes service config files before database SQL execution;
- optionally executes `SQL/color_vision_all.sql`;
- optionally starts the managed services.

Incremental update packages are not supported by this workflow.

## MySQL

The MySQL workflow defaults to the `color_vision_4xx` database and the `cv` business
user. SQL files are read as UTF-8 when possible, with GB18030 fallback, and are sent
to `mysql.exe` as UTF-8 to avoid Chinese text import failures.

MySQL installed from a ZIP is placed beside the service root, for example:

```text
D:\CVService
D:\mysql-5.7.37-winx64
```

## Configuration

`ServiceManagerConfig` stores only the active service-manager settings:

| Field | Purpose |
| --- | --- |
| `BaseLocation` | Service installation root. |
| `MySqlPort` | MySQL port. |
| `InstallServiceChecked` | Default service package selection state. |
| `InstallMySqlChecked` | Default MySQL package selection state. |
| `InstallMqttChecked` | Default MQTT installer selection state. |

`MySqlServiceConfig` stores MySQL service path, port, credentials, and database.
`MqttServiceConfig` stores MQTT process/service state and connection settings.

## Notes

- Full install and service registration require administrator privileges.
- If config synchronization fails after file extraction, install fails instead of
  starting services with stale config.
- The install log shown in the window is retained for operational feedback; the old
  service-log menu entries are removed.
