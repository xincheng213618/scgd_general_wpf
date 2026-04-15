# WindowsServicePlugin - Windows服务管理插件

## 目录

1. [概述](#概述)
2. [主要功能](#主要功能)
3. [架构设计](#架构设计)
4. [使用指南](#使用指南)
5. [API参考](#api参考)
6. [配置说明](#配置说明)
7. [故障排除](#故障排除)
8. [最佳实践](#最佳实践)
9. [版本历史](#版本历史)

## 概述

**WindowsServicePlugin** 是 ColorVision 的 Windows 服务与运维辅助工具插件，提供内置服务管理器、服务安装/更新向导、日志快速访问、第三方工具集成、MySQL 运维管理，以及与 CVWinSMS 服务管理工具的联动功能。

### 基本信息

- **版本**: 1.4.3.8
- **目标框架**: .NET 8.0 / .NET 10.0 Windows
- **主要功能**: 服务管理、服务安装/更新、日志访问、MySQL 运维、第三方工具集成
- **依赖**: ColorVision.UI, ColorVision.UI.Desktop, ColorVision.Engine
- **许可证**: 主工程 License

## 主要功能

### 1. 内置服务管理器

提供统一的 Windows 服务状态监控与运维入口：

- **服务状态监控** - RegistrationCenterService、CVMainService_x64、CVMainService_dev、CVArchService
- **一键启动/停止** - 按依赖顺序批量启停服务
- **目录与配置快速打开** - 打开服务目录、`cfg/MySql.config`、`cfg/MQTT.config`、`cfg/WinService.config`、`log4net.config`
- **MySQL / MQTT 状态** - 显示安装状态、运行状态、版本号
- **配置同步** - 将当前系统配置写入所有服务的 `cfg` 目录
- **旧版配置兼容** - 读取并同步 CVWinSMS 遗留 `App.config`

### 2. 服务安装/更新向导

- **在线版本检测** - 访问更新服务器 API 获取最新版本与下载地址
- **在线下载** - 服务完整安装包、MySQL ZIP、MQTT 安装程序
- **一键安装包解析** - 自动识别 `FullPackage.zip` 内的各组件
- **完整安装** - 解压覆盖、重新注册 Windows 服务、自动复制 `CommonDll`、清理 `pack` 目录
- **增量更新** - 识别版本号文件夹格式的增量包，仅覆盖变更文件，**跳过 `cfg/` 保留现有配置**
- **数据库备份/恢复** - 安装前自动备份，或手动执行 SQL 备份/恢复
- **服务文件夹备份/恢复** - 支持 WinRAR (RAR) 或 ZIP 全量/仅配置(cfg) 备份

### 3. 安装向导步骤

| 步骤 | 说明 |
|------|------|
| `InstallServiceManager` | 打开内置服务管理器窗口 |
| `SetMysqlConfig` | 从 CVWinSMS 的 `App.config` 读取并应用 MySQL 配置 |
| `SetServiceConfigStep` | 将当前系统配置写入各服务的 `cfg/*.config` |
| `InstallNavicateAppProvider` | 第三方数据库客户端下载与安装 |

### 4. 日志访问

提供多种服务日志的快速访问：

| 日志类型 | 访问方式 | 默认地址 |
|----------|----------|----------|
| RC 服务日志 | HTTP | http://localhost:8080/system/log |
| x64 服务日志 | HTTP | http://localhost:8064/system/log |
| x86 服务日志 | HTTP | http://localhost:8086/system/log |
| 摄像头日志 | HTTP | http://localhost:8064/system/device/camera/log |
| 光谱仪日志 | HTTP | http://localhost:8064/system/device/Spectrum/log |
| 本地日志目录 | 本地目录 | 根据 `CVWinSMSConfig.BaseLocation` 解析 |

### 5. 第三方工具集成

- **ImageJ** - 图像处理工具，支持右键在图像视图中调用；CIE 文件自动转临时 TIF
- **Navicat** - 数据库客户端下载与安装

## 架构设计

```mermaid
graph TD
    A[WindowsServicePlugin] --> B[内置服务管理器]
    A --> C[服务安装/更新]
    A --> D[安装向导]
    A --> E[日志访问]
    A --> F[第三方工具]

    B --> B1[服务状态监控]
    B --> B2[一键启停]
    B --> B3[MySQL/MQTT状态]
    B --> B4[配置同步]

    C --> C1[在线检测版本]
    C --> C2[完整安装包]
    C --> C3[增量更新包]
    C --> C4[备份/恢复]

    D --> D1[服务管理器]
    D --> D2[MySQL配置]
    D --> D3[CFG替换]
    D --> D4[Navicat]

    E --> E1[HTTP日志]
    E --> E2[本地日志]

    F --> F1[ImageJ]
    F --> F2[Navicat]
```

### 核心组件

```
WindowsServicePlugin/
├── App.xaml.cs                    # 独立入口（调试/测试用）
├── ServiceManager/                # 内置服务管理器核心
│   ├── ServiceManagerWindow.xaml.cs
│   ├── ServiceManagerViewModel.cs          # 主 VM
│   ├── ServiceManagerViewModel.OneKey.cs   # 一键启停
│   ├── ServiceManagerViewModel.Config.cs   # 配置同步
│   ├── ServiceManagerViewModel.MySql.cs    # MySQL 密码/用户管理
│   ├── ServiceManagerViewModel.Helpers.cs  # 辅助方法
│   ├── ServiceInstallWindow.xaml.cs
│   ├── ServiceInstallViewModel.cs          # 安装 VM
│   ├── ServiceInstallViewModel.Download.cs # 在线下载
│   ├── ServiceInstallViewModel.Backup.cs   # 备份/恢复
│   ├── ServiceInstallViewModel.Install.cs  # 安装编排
│   ├── ServiceInstallViewModel.IncrementalUpdate.cs
│   ├── ServiceEntry.cs
│   ├── ServiceManagerConfig.cs
│   ├── ServicePackageInfo.cs
│   ├── WinServiceHelper.cs
│   └── MySqlServiceHelper.cs
├── CVWinSMS/                      # CVWinSMS 协作与更新
│   ├── InstallTool.cs
│   └── CVWinSMSConfig.cs
├── Menus/                         # 菜单与日志访问
│   ├── Export*.cs
│   └── ServiceLog.cs
├── Tools/                         # 第三方工具与扩展
│   └── MenuImageJ.cs
├── SetMysqlConfig.cs              # Wizard：读取服务 MySQL 配置
├── SetServiceConfig.cs            # Wizard：替换服务 CFG
├── InstallNavicate.cs             # Navicat 下载安装
├── manifest.json                  # 插件清单
├── README.md
└── CHANGELOG.md
```

## 使用指南

### 快速开始

1. 启动 ColorVision 主程序
2. 打开 设置 / 插件 / 确认已加载 `WindowsServicePlugin`
3. 在菜单栏中找到：
   - **服务日志**: 打开各类服务日志 / 物理目录
   - **帮助**: 服务管理器 / 其他工具入口
4. 若首次使用第三方工具，按提示选择 "下载" 或手动定位已存在的可执行文件
5. 如需更新后台服务，打开 **服务管理器** → **安装/更新** 页面，检测版本后执行更新

### 服务管理器使用

```csharp
// 打开服务管理器窗口
var window = new ServiceManagerWindow();
window.Show();

// 或通过菜单命令
new MenuServiceManager().Execute();
```

### 服务更新流程

```csharp
// 打开安装/更新窗口
var installWindow = new ServiceInstallWindow();
installWindow.Show();

// 核心流程由 ServiceInstallViewModel 编排：
// 1. CheckUpdateCommand -> 在线检测并下载最新包
// 2. DoInstallCommand -> 执行安装/更新（完整包或增量包）
```

### 第三方工具使用

```csharp
// ImageJ 集成
var menuImageJ = new MenuImageJ();
menuImageJ.Execute();

// 图像右键扩展由 ImageViewExTension 提供
```

## API参考

### ServiceManagerViewModel

服务管理器主视图模型。

```csharp
public partial class ServiceManagerViewModel : ViewModelBase
{
    public static ServiceManagerViewModel Instance { get; }
    public ServiceManagerConfig Config { get; }
    public ObservableCollection<ServiceEntry> Services { get; }
    public MySqlServiceHelper MySqlHelper { get; }

    // 一键启动/停止
    public RelayCommand OneKeyStartCommand { get; }
    public RelayCommand OneKeyStopCommand { get; }

    // 刷新所有状态
    public void RefreshAll();

    // 安装后配置同步
    public void ApplyConfigAndRefreshAfterInstall();
}
```

### ServiceInstallViewModel

服务安装/更新视图模型。

```csharp
public partial class ServiceInstallViewModel : ViewModelBase
{
    public string ServicePackagePath { get; set; }
    public string MySqlPackagePath { get; set; }
    public string MqttInstallerPath { get; set; }
    public bool AutoStartAfterInstall { get; set; }
    public bool BackupBeforeInstall { get; set; }
    public bool BackupServiceBeforeInstall { get; set; }
    public bool BackupServiceCfgOnly { get; set; }

    public RelayCommand CheckUpdateCommand { get; }
    public RelayCommand DoInstallCommand { get; }
    public RelayCommand OneKeyInstallAllCommand { get; }
    public RelayCommand BackupNowCommand { get; }
    public RelayCommand RestoreBackupCommand { get; }
    public RelayCommand BackupServiceNowCommand { get; }
    public RelayCommand RestoreServiceBackupCommand { get; }
}
```

### ServiceEntry

单个 Windows 服务的信息与状态。

```csharp
public class ServiceEntry : ViewModelBase
{
    public string ServiceName { get; set; }
    public string DisplayName { get; set; }
    public string ExePath { get; set; }
    public string FolderName { get; set; }
    public string ExecutableName { get; set; }
    public bool IsPackaged { get; set; }
    public string StatusText { get; }
    public string VersionText { get; }
    public bool IsInstalled { get; }
    public bool IsRunning { get; }

    public void RefreshStatus();
    public string GetExpectedExePath(string basePath);
}
```

### WinServiceHelper

Windows 服务操作辅助类。

```csharp
public static class WinServiceHelper
{
    public static bool IsServiceExisted(string serviceName);
    public static ServiceControllerStatus GetServiceStatus(string serviceName);
    public static bool IsServiceRunning(string serviceName);
    public static bool StartService(string serviceName, int timeoutSeconds = 30);
    public static bool StopService(string serviceName, int timeoutSeconds = 30);
    public static bool InstallService(string serviceName, string exePath);
    public static bool UninstallService(string serviceName);
    public static string? GetServiceInstallPath(string serviceName);
    public static Version? GetFileVersion(string exePath);
    public static void KillProcessByName(string processName);
    public static bool ExecuteCommand(string command, bool asAdmin = true);
}
```

### MySqlServiceHelper

MySQL 安装与运维辅助类。

```csharp
public class MySqlServiceHelper
{
    public string ServiceName { get; set; }
    public string BasePath { get; set; }
    public int Port { get; set; }
    public string LastGeneratedRootPassword { get; }

    public bool DetectFromRegistry();
    public bool DetectFromServicePath(string cvWindowsServicePath);
    public bool IsInstalled { get; }
    public bool IsRunning { get; }

    public Task<bool> InstallFromZipAsync(string zipFilePath, string targetPath, Action<string> logCallback, string appUser = "", string appPassword = "", string database = "color_vision");
    public bool DoFullInstall(Action<string> logCallback, string appUser = "", string appPassword = "", string database = "color_vision");

    public bool BackupDatabase(string user, string password, string database, string outputFile, Action<string> logCallback);
    public bool RestoreDatabase(string user, string password, string database, string sqlFile, Action<string> logCallback);
    public bool ExecuteSqlFile(string rootPwd, string database, string sqlFilePath, Action<string> logCallback);

    public bool Start(Action<string> logCallback);
    public bool Stop(Action<string> logCallback);
    public bool Uninstall(Action<string> logCallback);

    public bool TrySetRootPassword(string oldPassword, string newPassword, Action<string> logCallback);
    public bool ForceResetRootPassword(string newPassword, Action<string> logCallback);
    public bool CreateAppUser(string rootPwd, string userName, string userPwd, string database, Action<string> logCallback);
    public bool DeleteAppUser(string rootPwd, string userName, Action<string> logCallback);

    public static string GenerateRandomPassword(int length = 12);
}
```

### ServiceManagerConfig

服务管理器配置类。

```csharp
public class ServiceManagerConfig : ViewModelBase, IConfig
{
    public static ServiceManagerConfig Instance { get; }

    public string BaseLocation { get; set; }           // 服务安装根目录
    public int MySqlPort { get; set; }                 // MySQL 端口（默认 3306）
    public string UpdateServerUrl { get; set; }        // 更新服务器地址
    public string DownloadLocation { get; set; }       // 下载保存目录
    public bool InstallServiceChecked { get; set; }    // 默认勾选安装服务包
    public bool InstallMySqlChecked { get; set; }      // 默认勾选安装 MySQL
    public bool InstallMqttChecked { get; set; }       // 默认勾选安装 MQTT
    public string LatestReleaseUrl { get; }            // 合成的 LATEST_RELEASE 地址

    public static List<ServiceEntry> GetDefaultServiceEntries();
    public bool TryDetectInstallPath();                // 从注册表自动检测
    public bool ReadFromCVWinSMSConfig(string cvWinSMSPath);
}
```

### CVWinSMSConfig

CVWinSMS 配置类。

```csharp
public class CVWinSMSConfig : ViewModelBase, IConfig
{
    public static CVWinSMSConfig Instance { get; }

    public string CVWinSMSPath { get; set; }
    public string UpdatePath { get; set; }
    public bool IsAutoUpdate { get; set; }
    public string BaseLocation { get; }    // 从 App.config 解析（只读）

    public void Init();
}
```

## 配置说明

### ServiceManagerConfig

| 字段 | 类型 | 说明 |
|------|------|------|
| BaseLocation | string | 服务安装根目录（CVWindowsService 所在目录） |
| MySqlPort | int | MySQL 端口（默认 3306） |
| UpdateServerUrl | string | 服务更新服务器基础地址 |
| DownloadLocation | string | 在线下载保存目录 |
| InstallServiceChecked | bool | 安装向导默认勾选安装服务包 |
| InstallMySqlChecked | bool | 安装向导默认勾选安装 MySQL |
| InstallMqttChecked | bool | 安装向导默认勾选安装 MQTT |

### CVWinSMSConfig

| 字段 | 类型 | 说明 |
|------|------|------|
| CVWinSMSPath | string | CVWinSMS 管理器可执行文件路径 |
| UpdatePath | string | 版本更新目录基础 URL |
| IsAutoUpdate | bool | 是否启动时自动检查更新 |
| BaseLocation | string | 解析自外部 App.config 的基础目录（只读） |

### 更新文件命名规范

```
FullPackage[{Version}].zip          # 一键安装包（含服务/MySQL/MQTT）
CVWindowsService.zip                # 完整服务安装包
{X.X.X.X}/                          # 增量更新包根目录（版本号文件夹）
InstallTool[{Version}].zip          # CVWinSMS 管理工具更新包
```

### 版本检测

通过访问更新服务器 API `/api/tool/cvwindowsservice/releases` 获取最新版本号与下载地址。

## 故障排除

### 问题1: 找不到工具且没有下载按钮

**症状**: 第三方工具无法定位，也没有下载选项

**解决方案**:
1. 检查是否被系统防火墙或代理阻断 HTTP 访问
2. 验证下载 URL 是否可访问
3. 手动下载并配置工具路径

### 问题2: 服务更新后路径异常

**症状**: 服务更新后无法找到服务或路径错误

**解决方案**:
1. 确认 `BaseLocation` 是否正确
2. 使用注册表自动检测重新定位
3. 检查服务注册表项

### 问题3: 日志目录为空

**症状**: 日志菜单点击后目录为空或无法打开

**解决方案**:
1. 服务可能尚未启动或日志级别过低
2. 尝试通过 HTTP 接口确认服务是否运行
3. 检查日志路径配置是否正确

### 问题4: ImageJ 打不开 CIE 文件

**症状**: 使用 ImageJ 打开 CIE 文件失败

**解决方案**:
1. 插件会自动转换为 TIF 格式
2. 确认临时目录写入是否有权限
3. 检查临时目录空间是否充足

### 问题5: MySQL root 密码丢失

**症状**: 忘记或丢失 MySQL root 密码

**解决方案**:
1. 全新安装时会自动生成随机 root 密码并显示在日志中，请妥善保存
2. 使用服务管理器的 **强制重置 root 密码** 功能（需要管理员权限）
3. 确保重置后同步到 `MySqlSetting` 和旧版 `App.config`

## 最佳实践

### 1. 服务管理器配置

```csharp
// 推荐配置
{
    "BaseLocation": "C:\\CVWindowsService",
    "UpdateServerUrl": "http://your-server.com/",
    "MySqlPort": 3306,
    "InstallServiceChecked": true,
    "InstallMySqlChecked": false,
    "InstallMqttChecked": false
}
```

### 2. 更新流程

1. 打开 **服务管理器**，确认 `BaseLocation` 已自动检测或手动设置
2. 点击 **安装/更新**，检查新版本并下载服务包
3. 选择更新后：
   - 下载新版 Zip（完整包或增量包）
   - 备份数据库（可选）
   - 停止所有相关 Windows 服务
   - 解压/覆盖 / 复制 CommonDll / 清理 pack
   - 重新启动服务
4. 使用日志菜单验证服务运行状态
5. 借助 ImageJ 进行数据/结果核对

### 3. 安全注意事项

- 远程下载地址为内网/私有仓库示例，部署正式环境前请迁移至受信任源并启用 HTTPS
- 运行安装器时均通过 `Verb = runas` 触发 UAC，必要时请核验文件来源与签名
- MySQL 强制重置 root 密码需要管理员权限

## 版本历史

### v1.4.3.8（2025-04-14）

**新增**:
- 内置服务管理器（服务状态、一键启停、MySQL/MQTT 监控、配置同步）
- 服务安装/更新窗口（在线检测、完整包/增量更新、一键安装包解析）
- 增量更新包支持（跳过 `cfg/` 保留配置）
- MySQL 运维功能（安装、备份/恢复、密码管理、用户管理）
- MQTT 安装支持
- 旧版 `App.config` 兼容与同步

**改进**:
- 重构原有更新逻辑到统一安装/更新流水线
- 统一使用 `ServiceManagerConfig` 管理配置
- 优化服务安装流程（CommonDll 自动复制、pack 清理、注册表自动检测）

**修复**:
- 修复嵌套 `CVWindowsService` 目录下的路径解析问题
- 修复备份时日志目录被意外打包的问题
- 修复 MySQL 卸载残留导致的重新安装失败

### v1.2.3.4（2025-05-23）

- 规范插件代码结构

### v1.0.0（2025-02）

- 初始版本（旧版 CVWinSMS 联动、基础日志访问、ImageJ 集成）

---

*文档版本: 1.4.3.8*  
*最后更新: 2025-04-14*

## 相关资源

- [源代码](../../../../Plugins/WindowsServicePlugin/)
- [CHANGELOG](../../../../Plugins/WindowsServicePlugin/CHANGELOG.md)
- [ColorVision 插件开发指南](../../../02-developer-guide/plugin-development/overview.md)

## 许可证

本插件遵循主工程 License。

---

**版权**: Copyright (C) 2025-present ColorVision Development Team
