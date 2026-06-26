# WindowsServicePlugin

`Plugins/WindowsServicePlugin/` 是本机 ColorVision Windows 服务管理插件。它面向管理员现场操作：安装完整 `CVWindowsService` 包、注册/启停服务、同步 MySQL/MQTT/WinService 配置，并保留备份和回退线索。

## 先查什么

| 现象 | 第一检查点 |
| --- | --- |
| Help 菜单没有入口 | 插件目录、`manifest.json`、`WindowsServicePlugin.dll`、菜单初始化 |
| 操作提示拒绝访问 | 是否管理员启动、UAC、服务控制权限、目录 ACL |
| `BaseLocation` 为空 | 服务是否已安装，服务路径或旧配置是否可读 |
| 服务包无法安装 | 是否完整 `CVWindowsService` 包，是否包含服务根目录和 `CommonDll` |
| 注册服务失败 | 服务名冲突、旧服务是否停止/注销、exe 路径是否正确 |
| 服务启动后立即停止 | 服务目录 `cfg`、MySQL/MQTT、Windows 事件日志、服务自身日志 |
| MySQL 配置未生效 | `cfg/MySql.config`、端口、账号、SQL 编码、`mysql.exe` 调用 |
| MQTT 配置未生效 | `cfg/MQTT.config`、进程/服务状态、端口占用 |

## manifest

| 字段 | 当前值 |
| --- | --- |
| `Id` | `WindowsServicePlugin` |
| `name` | `视彩服务插件` |
| `version` | `1.4.3.22` |
| `dllpath` | `WindowsServicePlugin.dll` |
| `requires` | `1.3.12.34` |

## 能力边界

当前插件聚焦本机服务管理：

- 管理 `RegistrationCenterService`、`CVMainService_x64`、`CVMainService_dev`。
- 安装或更新完整 `CVWindowsService` 服务包。
- 安装/注册 MySQL，并可执行服务数据库 SQL。
- 按需安装或打开 MQTT。
- 同步 `cfg/MySql.config`、`cfg/MQTT.config`、`cfg/WinService.config`。
- 提供本地服务目录备份/恢复线索。

不要把旧 CVWinSMS 的增量更新、外部工具入口、License、RESTful 等旧稿能力写回当前页面。

## 关键入口

| 文件 | 作用 |
| --- | --- |
| `ServiceManager/MenuServiceManager.cs` | Help 菜单入口 |
| `ServiceManager/InstallServiceManager.cs` | 安装向导入口 |
| `ServiceManager/ServiceManagerWindow.xaml.cs` | 服务管理主窗口 |
| `ServiceManager/ServiceManagerViewModel*.cs` | 服务状态、命令、配置同步 |
| `ServiceManager/ServiceInstallViewModel*.cs` | 服务包、MySQL、MQTT 安装编排 |
| `ServiceManager/Mysql/` | MySQL 状态、安装和 SQL 执行 |
| `ServiceManager/Mqtt/` | MQTT 状态和安装 |
| `CVWinSMS/CVWinSMSConfig.cs` | 仅兼容读取旧 `App.config` 路径 |

## 安装链路

推荐现场路径：

1. 以管理员身份启动 ColorVision。
2. 打开服务管理器，确认 `BaseLocation`，例如 `D:\CVService`。
3. 打开安装窗口，选择完整 `CVWindowsService` 包。
4. 按需要选择 MySQL ZIP 和 MQTT installer。
5. 执行安装，检查窗口日志。

安装完整服务包时会停止受管理服务、备份已有目录、解压到 `BaseLocation`、复制 `CommonDll`、重新注册服务、同步配置，并可选执行 `SQL/color_vision_all.sql` 和启动服务。当前安装链路只按完整服务包理解，不按增量包交付。

## MySQL 和 MQTT

| 组件 | 当前事实 |
| --- | --- |
| MySQL | 默认业务库 `color_vision_4xx`，业务用户 `cv` |
| SQL 编码 | 优先 UTF-8，失败回退 GB18030，再以 UTF-8 输入 `mysql.exe` |
| MySQL ZIP | 常见安装在服务根目录旁边，例如 `D:\mysql-5.7.37-winx64` |
| MQTT | 只做本机安装/打开/状态识别和连接配置同步 |

配置入口主要是 `ServiceManagerConfig`、`MySqlServiceConfig`、`MqttServiceConfig`。同步失败时应阻止继续带旧配置启动服务。

## 构建

```powershell
dotnet build Plugins/WindowsServicePlugin/WindowsServicePlugin.csproj -c Release -p:Platform=x64
Scripts\package_plugin.bat WindowsServicePlugin --no-upload
```

## 交付验收

| 验收项 | 通过标准 |
| --- | --- |
| 插件装载 | 服务管理器入口和安装向导入口可见 |
| 权限边界 | 普通用户失败可理解，管理员环境能注册/启停 |
| 状态刷新 | 能显示服务安装状态、运行状态、配置路径 |
| 服务包校验 | 无效 ZIP 被拒绝，完整服务包能识别根目录和 `CommonDll` |
| 安装覆盖 | 安装前有备份，覆盖范围只限包内实际顶层目录 |
| 注册启停 | 注册、启动、停止、重启、注销状态和窗口日志一致 |
| MySQL/MQTT | 配置能同步到服务目录，状态能识别 |
| 交付结构 | DLL、manifest、README、CHANGELOG 随包存在 |
