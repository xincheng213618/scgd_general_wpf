# WindowsServicePlugin 插件

本页只描述当前仓库里实际存在的 WindowsServicePlugin 实现，不再继续维护“运维平台总手册 + 大而全 API 目录”式旧稿。

## 先看这个插件现在是什么

按当前源码状态，WindowsServicePlugin 不是单纯的“服务日志快捷方式”集合，而是一个围绕本地 Windows 服务运维展开的插件包。当前最明确的几条能力线是：

- Help 菜单中的服务管理器入口。
- 服务安装与更新窗口。
- 服务日志与本地日志目录快捷入口。
- 与 CVWinSMS 配置文件和更新包的桥接。
- Wizard 步骤里的配置读取与 CFG 覆写。

因此它比旧文档里那种泛化的“服务工具箱”更具体，实际中心是 `ServiceManagerViewModel` 和 `ServiceInstallViewModel` 两条控制链。

## 当前最关键的文件

- `Plugins/WindowsServicePlugin/manifest.json`
- `Plugins/WindowsServicePlugin/ServiceManager/MenuServiceManager.cs`
- `Plugins/WindowsServicePlugin/ServiceManager/ServiceManagerWindow.xaml.cs`
- `Plugins/WindowsServicePlugin/ServiceManager/ServiceManagerViewModel.cs`
- `Plugins/WindowsServicePlugin/ServiceManager/ServiceInstallWindow.xaml.cs`
- `Plugins/WindowsServicePlugin/ServiceManager/ServiceInstallViewModel.cs`
- `Plugins/WindowsServicePlugin/ServiceManager/ServiceManagerConfig.cs`
- `Plugins/WindowsServicePlugin/CVWinSMS/InstallTool.cs`
- `Plugins/WindowsServicePlugin/SetMysqlConfig.cs`
- `Plugins/WindowsServicePlugin/SetServiceConfig.cs`
- `Plugins/WindowsServicePlugin/Menus/ServiceLog.cs`

如果只是想弄清插件如何进入宿主、如何打开服务管理器、如何做配置同步和更新，这些文件已经覆盖主体。

## 当前接入宿主的几条链

### Help 菜单中的服务管理器入口

`MenuServiceManager` 当前挂在 `Help` 菜单下，执行时直接打开 `ServiceManagerWindow`。

除此之外，同文件里的 `ServiceManagerAppProvider` 还实现了 `IThirdPartyAppProvider`，把“服务管理器”作为一个内部工具暴露给宿主的第三方应用入口。

这意味着它不只是一条菜单命令，而是至少有两条 UI 接入链。

### 服务日志菜单树

`ServiceLog` 当前也是 `Help` 菜单下的一个根菜单项。围绕它，插件继续注入多组日志快捷入口：

- HTTP 日志页面
- 依据 `CVWinSMSConfig.BaseLocation` 解析出来的本地日志目录

例如 `ExportRCServiceLog`、`Exportx64ServiceLog` 这类类型会直接打开本地 URL；而带后缀的目录版本则会拼接服务目录下的 `log` 文件夹。

### Wizard 与初始化入口

`InstallTool` 目前同时实现了：

- `MenuItemBase`
- `IWizardStep`
- `IMainWindowInitialized`

它既能作为菜单入口打开或定位 CVWinSMS，也能在主窗口初始化后检查更新，还能进入安装向导的聚合链。

因此这个插件当前并不是“只有服务管理窗口”这么简单，CVWinSMS 相关的引导和更新逻辑也是宿主接入面的一部分。

### manifest 信息

按当前 `manifest.json`，插件公开的装载信息是：

- `Id = "WindowsServicePlugin"`
- `name = "视彩服务插件"`
- `version = "1.0"`
- `dllpath = "WindowsServicePlugin.dll"`
- `requires = "1.3.12.34"`

这比旧文档里那些额外拼出来的依赖矩阵更接近当前真实装载模型。

## 服务管理器当前怎么工作

`ServiceManagerWindow` 本身很薄，窗口初始化时直接把 `DataContext` 设为 `ServiceManagerViewModel.Instance`，并在日志文本变化时自动滚动日志区域。

真正的运行时中心在 `ServiceManagerViewModel`。按当前实现，它至少负责：

- 维护默认服务列表。
- 维护 MySQL 和 MQTT 管理器。
- 维护当前版本、可用版本、忙碌状态、进度和日志文本。
- 暴露管理员模式状态和“以管理员身份重启”命令。
- 暴露一键启停、刷新、打开目录和打开配置文件等命令。

### 当前默认管理的服务

`ServiceManagerConfig.GetDefaultServiceEntries()` 里当前明确列出了：

- `RegistrationCenterService`
- `CVMainService_x64`
- `CVMainService_dev`
- `CVArchService`

所以这页文档应当围绕这些真实服务项写，而不是继续泛化成“任意服务编排框架”。

### 路径与版本检测

`ServiceManagerConfig` 当前会优先尝试：

1. 从注册表的 `RegistrationCenterService` 读取安装路径。
2. 如果失败，再尝试从 CVWinSMS 的 `App.config` 读取 `BaseLocation` 和 `MysqlPort`。

`RefreshAll()` 则会顺带刷新每个服务状态，并根据 `RegistrationCenterService` 的版本文本更新当前版本显示。

## 安装与更新链当前怎么展开

`ServiceInstallWindow` 本身同样很薄，核心逻辑在 `ServiceInstallViewModel`。当前这条链真正管理的是：

- 服务安装包选择
- MySQL ZIP 选择
- MQTT 安装程序选择
- 下载目录选择
- 在线检查更新
- 备份与恢复
- 一键安装全部组件

按当前实现，这个窗口关心的不是单一的“下载最新版”，而是完整的安装编排状态，包括进度、日志、自动启动、数据库更新和备份开关。

## 当前与 CVWinSMS 的关系

`CVWinSMSConfig` 负责维护 `CVWinSMSPath`、更新地址和自动更新开关，并提供从外部 `App.config` 解析出来的 `BaseLocation`。

`InstallTool` 则负责：

- 检测现有 CVWinSMS 可执行文件。
- 在需要时下载更新包。
- 解压并替换旧目录。
- 以管理员权限重新启动 CVWinSMS。

这说明 WindowsServicePlugin 当前不是单独封闭的一套服务运维 UI，而是明确带着对外部 CVWinSMS 工具的桥接和迁移逻辑。

## Wizard 步骤当前怎么落地

### 读取服务配置

`SetMysqlConfig` 会读取 CVWinSMS 目录下的 `config/App.config`，把其中的 MySQL 配置写回到当前宿主使用的数据库配置对象里。

### 覆写服务 CFG

`SetServiceConfigStep` 会读取同一个 `App.config`，然后用当前宿主里的：

- MySQL 设置
- MQTT 设置
- RC 设置

去更新服务目录中的：

- `cfg/MySql.config`
- `cfg/MQTT.config`
- `cfg/WinService.config`

写回完成后，它还会尝试重启 `RegistrationCenterService`。这是一条真正会修改服务端配置的运维链，不应继续被写成“普通向导按钮”。

## 当前几个最容易写错的点

### 它不只是日志菜单插件

虽然当前确实有一整组日志入口，但插件主体仍然是服务管理与安装更新控制链。如果只写日志菜单，会把主要实现面缩得过小。

### `Application` 壳不是宿主扩展重点

仓库里有 `App.xaml.cs`，但它当前更像独立启动或调试壳。对于主程序插件文档，更应该关注 manifest、菜单、provider、view model 和 wizard 步骤，而不是把这个 `Application` 类型误当成日常插件入口。

### 配置同步会真的改服务端 CFG

`SetServiceConfigStep` 不是只读检查器。它会把当前宿主配置写回多个服务目录下的配置文件，并尝试重启注册中心服务。

### 服务管理器当前是单例中心

`ServiceManagerViewModel.Instance` 是当前窗口和命令共用的状态中心。继续把它写成“每次打开窗口重新构造一套上下文”的模型，会和当前实现不符。

## 推荐阅读顺序

1. `Plugins/WindowsServicePlugin/ServiceManager/MenuServiceManager.cs`
2. `Plugins/WindowsServicePlugin/ServiceManager/ServiceManagerViewModel.cs`
3. `Plugins/WindowsServicePlugin/ServiceManager/ServiceManagerConfig.cs`
4. `Plugins/WindowsServicePlugin/ServiceManager/ServiceInstallViewModel.cs`
5. `Plugins/WindowsServicePlugin/CVWinSMS/InstallTool.cs`
6. `Plugins/WindowsServicePlugin/SetMysqlConfig.cs`
7. `Plugins/WindowsServicePlugin/SetServiceConfig.cs`
8. `Plugins/WindowsServicePlugin/Menus/ServiceLog.cs`
9. `Plugins/WindowsServicePlugin/manifest.json`

这样能先看到宿主入口，再看到状态中心、配置桥接和安装链。

## 继续阅读

- [Plugins/README.md](../../../../../Plugins/README.md)
- [docs/02-developer-guide/plugin-development/overview.md](../../../02-developer-guide/plugin-development/overview.md)
- [docs/04-api-reference/plugins/standard-plugins/eventvwr.md](./eventvwr.md)
