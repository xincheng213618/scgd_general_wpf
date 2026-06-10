# WindowsServicePlugin 插件

WindowsServicePlugin 是当前仓库里的本地服务管理插件，源码位于 `Plugins/WindowsServicePlugin/`。它的中心任务是安装、注册、维护本机 ColorVision Windows 服务，以及同步 MySQL、MQTT 和服务配置。

## manifest 信息

| 字段 | 当前值 |
| --- | --- |
| `Id` | `WindowsServicePlugin` |
| `name` | `视彩服务插件` |
| `version` | `1.0` |
| `dllpath` | `WindowsServicePlugin.dll` |
| `requires` | `1.3.12.34` |

## 当前能力边界

当前实现聚焦 `CVWindowsService` 工作流：

- 管理 `RegistrationCenterService`、`CVMainService_x64`、`CVMainService_dev`。
- 安装或更新完整 `CVWindowsService` 服务包。
- 安装/注册 MySQL，并执行服务数据库 SQL。
- 按需安装或打开 MQTT。
- 同步 `cfg/MySql.config`、`cfg/MQTT.config`、`cfg/WinService.config`。
- 提供本地数据库和服务文件备份/恢复。

旧的 CVWinSMS 在线下载、增量更新、外部管理工具入口、服务日志菜单、归档服务注销、License、RESTful 等旧文档描述不再是当前插件表面能力。

## 主要入口

| 文件 | 作用 |
| --- | --- |
| `ServiceManager/MenuServiceManager.cs` | Help 菜单入口，打开服务管理器 |
| `ServiceManager/InstallServiceManager.cs` | 向导入口，打开服务管理器 |
| `ServiceManager/ServiceManagerWindow.xaml(.cs)` | 服务管理主窗口 |
| `ServiceManager/ServiceManagerViewModel*.cs` | 服务状态、命令、MySQL/MQTT 管理、配置同步 |
| `ServiceManager/ServiceInstallWindow.xaml(.cs)` | 本地安装/更新窗口 |
| `ServiceManager/ServiceInstallViewModel*.cs` | 服务包、MySQL ZIP、MQTT installer 的安装编排 |
| `ServiceManager/ServiceManagerConfig.cs` | 服务根目录和安装选项 |
| `ServiceManager/Mysql/` | MySQL 状态、安装和 SQL 执行 |
| `ServiceManager/Mqtt/` | MQTT 状态和安装 |
| `CVWinSMS/CVWinSMSConfig.cs` | 仅用于读取遗留 `App.config` 路径 |

## 服务管理器怎么工作

`ServiceManagerWindow` 只是窗口承载，核心状态在 `ServiceManagerViewModel.Instance`。

它负责：

- 刷新服务安装状态和运行状态。
- 一键启动、停止服务。
- 根据服务安装路径定位 `BaseLocation`。
- 打开服务目录和配置文件。
- 注册完整服务包中的服务。
- 将当前 MySQL/MQTT/服务配置写回服务目录。
- 管理 MySQL 和 MQTT 状态。
- 以管理员权限重启主程序。

服务相关操作通常需要管理员权限。文档、测试和现场操作都应按管理员模式处理。

## 完整安装流程

推荐流程：

1. 以管理员身份启动 ColorVision。
2. 打开服务管理器。
3. 确认 `BaseLocation`，例如 `D:\CVService`。
4. 打开安装窗口。
5. 选择完整 `CVWindowsService` 包，例如 `CVWindowsService[4.0.6.603]-0603.zip`。
6. 按需要选择 MySQL ZIP 和 MQTT 安装程序。
7. 执行安装。

完整服务包安装时会：

- 校验包内是否包含服务根目录，例如 `RegWindowsService`、`CVMainWindowsService_x64` 或 `CVMainWindowsService_dev`。
- 安装前停止受管理服务。
- 只清理选中服务包中实际存在的顶层目标。
- 解压服务包到 `BaseLocation`。
- 将 `CommonDll` 复制到包内服务目录，并删除临时 `CommonDll`。
- 注销并重新注册包内 Windows 服务。
- 同步 MySQL、MQTT、WinService 配置。
- 可选执行 `SQL/color_vision_all.sql`。
- 可选启动安装后的服务。

当前流程不支持增量更新包。

## MySQL

MySQL 管理代码集中在：

- `ServiceManager/MySqlServiceHelper.cs`
- `ServiceManager/Mysql/MySqlServiceManager.cs`
- `ServiceManager/Mysql/MySqlServiceConfig.cs`

当前默认业务数据库是 `color_vision_4xx`，业务用户是 `cv`。SQL 文件优先按 UTF-8 读取，失败时回退 GB18030，并以 UTF-8 送入 `mysql.exe`，避免中文 SQL 导入失败。

如果从 ZIP 安装 MySQL，通常会放在服务根目录旁边：

```text
D:\CVService
D:\mysql-5.7.37-winx64
```

## MQTT

MQTT 管理代码集中在：

- `ServiceManager/Mqtt/MqttServiceManager.cs`
- `ServiceManager/Mqtt/MqttServiceConfig.cs`

安装流程只在需要时安装或打开 MQTT，不再把 MQTT 写成完整独立运维平台。

## 配置

`ServiceManagerConfig` 当前保留服务管理器所需的主动配置：

| 字段 | 说明 |
| --- | --- |
| `BaseLocation` | 服务安装根目录 |
| `MySqlPort` | MySQL 端口 |
| `InstallServiceChecked` | 安装窗口默认是否选择服务包 |
| `InstallMySqlChecked` | 安装窗口默认是否选择 MySQL |
| `InstallMqttChecked` | 安装窗口默认是否选择 MQTT |

`MySqlServiceConfig` 保存 MySQL 服务路径、端口、凭据和数据库。

`MqttServiceConfig` 保存 MQTT 进程/服务状态和连接配置。

`CVWinSMSConfig` 只作为读取遗留 `App.config` 的辅助配置存在，不再代表一个外部工具下载/更新入口。

## 构建与打包

构建：

```powershell
dotnet build Plugins/WindowsServicePlugin/WindowsServicePlugin.csproj -c Release -p:Platform=x64
```

打包：

```powershell
Scripts\package_plugin.bat WindowsServicePlugin --no-upload
```

PostBuild 会复制主 DLL、`manifest.json`、`README.md` 和 `CHANGELOG.md` 到主程序插件目录。

## 交接验收表

| 验收项 | 操作 | 通过标准 |
| --- | --- | --- |
| 插件装载 | 检查 `manifest.json`、`dllpath` 和 Help 菜单 | 服务管理器入口和安装向导入口可见，窗口能打开 |
| 权限边界 | 分别以普通用户和管理员运行关键命令 | 普通用户下危险操作失败可理解；管理员测试环境下注册、启动、停止可执行 |
| 只读刷新 | 打开服务管理器并刷新 | 能显示 `BaseLocation`、服务安装状态、运行状态和配置路径 |
| 打开目录/配置 | 点击打开服务目录和 CFG | 打开的是当前受管理服务目录及其 `cfg` 文件 |
| MySQL 管理 | 查看状态、写入配置或执行测试 SQL | `MySqlServiceManager` 能读取状态，配置变更能同步到服务目录 |
| MQTT 管理 | 查看状态、安装或打开 MQTT | `MqttServiceManager` 能识别进程/服务状态，连接配置可同步 |
| 服务包校验 | 分别选择无效 ZIP 和完整服务包 ZIP | 无效包被拒绝；有效包能识别服务根目录和 `CommonDll` |
| 备份与覆盖 | 在已有服务目录上执行安装 | 安装前生成备份，覆盖范围只限服务包中存在的顶层目录 |
| 注册与启停 | 在测试环境注册、启动、停止、重启、注销服务 | 服务状态变化和窗口日志一致，失败时能看到可追踪原因 |
| 交付结构 | 构建或打包插件 | `WindowsServicePlugin.dll`、`manifest.json`、`README.md`、`CHANGELOG.md` 存在 |

## 故障首查

| 现象 | 先查什么 |
| --- | --- |
| Help 菜单没有服务入口 | 插件目录、`manifest.json`、`WindowsServicePlugin.dll`、`MenuServiceManager` 和 `InstallServiceManager` 是否装载 |
| 操作提示拒绝访问 | 是否管理员启动、UAC、服务控制权限、服务目录 ACL |
| `BaseLocation` 为空 | 服务是否已安装、注册表/服务路径是否可读、`ServiceManagerConfig` 是否配置 |
| 服务包无法安装 | ZIP 是否为完整 `CVWindowsService` 包，是否包含服务根目录和 `CommonDll` |
| 注册服务失败 | 服务名是否冲突、旧服务是否停止/注销、安装路径是否包含预期 exe |
| 服务启动后立即停止 | 服务目录 `cfg`、MySQL/MQTT 可达性、Windows 事件日志和服务自身日志 |
| MySQL 配置没有生效 | `cfg/MySql.config` 写入路径、端口、账号、SQL 文件编码和 `mysql.exe` 调用 |
| MQTT 配置没有生效 | `cfg/MQTT.config` 写入路径、MQTT installer/进程状态和端口占用 |
| 回退困难 | 安装前备份目录、原服务包、原 `cfg` 文件和窗口日志是否留存 |
| 窗口卡住或状态不刷新 | 后台命令是否超时、窗口日志、服务控制命令返回码和刷新命令状态 |

## 交接注意事项

- 服务安装、注册和配置同步都可能修改本机服务状态，必须以管理员权限验证。
- 如果配置同步失败，当前安装流程应失败，而不是带着旧配置启动服务。
- 旧 CVWinSMS 相关代码只保留兼容读取，不要把旧下载/更新功能重新写进当前文档。
- 现场问题先看窗口日志，再看 Windows 服务状态、MySQL 状态、MQTT 状态和服务目录下 CFG 文件。

## 推荐阅读顺序

1. `Plugins/WindowsServicePlugin/README.md`
2. `Plugins/WindowsServicePlugin/ServiceManager/MenuServiceManager.cs`
3. `Plugins/WindowsServicePlugin/ServiceManager/ServiceManagerViewModel.cs`
4. `Plugins/WindowsServicePlugin/ServiceManager/ServiceManagerViewModel.Config.cs`
5. `Plugins/WindowsServicePlugin/ServiceManager/ServiceInstallViewModel.cs`
6. `Plugins/WindowsServicePlugin/ServiceManager/ServiceInstallViewModel.Install.cs`
7. `Plugins/WindowsServicePlugin/ServiceManager/Mysql/MySqlServiceManager.cs`
8. `Plugins/WindowsServicePlugin/ServiceManager/Mqtt/MqttServiceManager.cs`
9. `Plugins/WindowsServicePlugin/manifest.json`

## 继续阅读

- [现有插件现场验收与交接清单](../plugin-field-acceptance.md)
- [插件能力与交接矩阵](../plugin-capability-matrix.md)
- [插件运行与交接场景手册](../plugin-handoff-playbook.md)
