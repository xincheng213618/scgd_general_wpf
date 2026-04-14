# WindowsServicePlugin (服务与工具扩展插件)

[![Version](https://img.shields.io/badge/version-1.4.3.8-blue.svg)](manifest.json)
[![ColorVision](https://img.shields.io/badge/ColorVision-Plugin-orange.svg)](../../README.md)

> 增强的本地 Windows 服务与运维辅助工具集合。提供服务管理器、服务安装/更新向导、日志快速访问、第三方工具一键获取、MySQL 运维管理，以及与 CVWinSMS 服务管理工具的联动。

---

## 功能概览

| 模块 | 功能 | 说明 |
|------|------|------|
| 服务管理器 | CVWinSMS / 内置服务管理器 | 查看服务状态、一键启停、打开服务目录与配置 |
| 安装向导 (WizardStep) | 服务管理器 / MySQL / MQTT / Navicat | 一键安装或打开相关运维工具 |
| 服务更新 | 完整安装包 / 增量更新包 | 自动检测版本、下载、备份、停止服务、覆盖升级 |
| 日志访问 | RC / x64 / x86 / Camera / Spectrometer | HTTP 地址或本地物理目录一键打开 |
| 第三方工具 | ImageJ / Navicat | 下载与调用；ImageJ 支持图像右键菜单打开 |
| 配置中心集成 | 可在全局配置界面编辑路径与开关 | `ServiceManagerConfig`, `CVWinSMSConfig` |
| 菜单集成 | 插入主菜单/视图/帮助/日志分类 | 统一 GUID 与排序体系 |
| 多语言资源 | 提供多语言 `Resources.*.resx` | Header/菜单文案国际化 |

---

## 快速开始

1. 启动 ColorVision 主程序。
2. 打开 设置 / 插件 / 确认已加载 `WindowsServicePlugin`。
3. 在菜单栏中找到：
   - **服务日志**: 打开各类服务日志 / 物理目录。
   - **帮助**: 服务管理器 / 其他工具入口。
4. 若首次使用第三方工具，按提示选择 "下载" 或手动定位已存在的可执行文件。
5. 如需更新后台服务，打开 **服务管理器** → **安装/更新** 页面，检测版本后执行更新。

---

## 安装向导步骤（Wizard）

所有安装步骤实现自 `WizardStepBase`，在 ColorVision 的安装或运维向导中自动聚合：

| 类 | Header | 功能 | 备注 |
|----|--------|------|------|
| `InstallServiceManager` | 服务管理器 | 打开内置服务管理器窗口 | `ServiceManagerWindow` |
| `SetMysqlConfig` | 读取服务的配置 | 从 CVWinSMS 的 `App.config` 读取并应用 MySQL 配置 | 同步到 `MySqlSetting` |
| `SetServiceConfigStep` | 替换服务的CFG | 将当前系统配置写入各服务的 `cfg/*.config` | 覆盖 MySQL / MQTT / WinService.config |
| `InstallNavicateAppProvider` | Navicat | 第三方数据库客户端下载与安装 | 通过 `IThirdPartyAppProvider` 集成 |

---

## 服务管理与更新

### 内置服务管理器 (`ServiceManagerWindow`)
- 类：`ServiceManagerViewModel` + `ServiceManagerWindow`
- 功能：
  - 显示默认服务列表（RegistrationCenterService、CVMainService_x64、CVMainService_dev、CVArchService）
  - 显示 MySQL / MQTT 安装与运行状态
  - 一键启动 / 一键停止所有服务
  - 打开服务目录、打开 `cfg/MySql.config`、`cfg/MQTT.config`、`cfg/WinService.config`、`log4net.config`
  - 同步当前系统配置到所有服务的 `cfg` 目录，并可选择重启 RegistrationCenterService
  - 打开旧版 `App.config`（CVWinSMS 遗留配置）

### 服务安装/更新窗口 (`ServiceInstallWindow`)
- 类：`ServiceInstallViewModel` + `ServiceInstallWindow`
- 功能：
  - **在线检测版本**：访问更新服务器 API `/api/tool/cvwindowsservice/releases` 获取最新版本与下载地址
  - **在线下载**：服务完整安装包、MySQL ZIP、MQTT 安装程序
  - **一键安装包解析**：自动识别 `FullPackage.zip` 内的各组件并填充路径
  - **完整安装**：解压覆盖、重新注册 Windows 服务、自动复制 `CommonDll`、清理 `pack` 目录
  - **增量更新**：识别 `X.X.X.X` 根目录格式的增量包，仅覆盖变更文件，**跳过 `cfg/` 保留现有配置**，不重新注册服务
  - **数据库备份/恢复**：在安装前自动备份，或手动执行备份/恢复
  - **服务文件夹备份/恢复**：支持 WinRAR (RAR) 或 ZIP 全量/仅配置(cfg) 备份

### 更新文件命名规范
```
FullPackage[{Version}].zip          # 一键安装包（含服务/MySQL/MQTT）
CVWindowsService.zip                # 完整服务安装包
{X.X.X.X}/                          # 增量更新包根目录（版本号文件夹）
InstallTool[{Version}].zip          # CVWinSMS 管理工具更新包
```

---

## 日志访问

| 菜单类 | 访问类型 | 默认地址 / 目录 | 说明 |
|--------|----------|----------------|------|
| `ExportRCServiceLog` | HTTP | `http://localhost:8080/system/log` | RC 服务日志接口 |
| `Exportx64ServiceLog` | HTTP | `http://localhost:8064/system/log` | x64 主服务日志 |
| `Exportx86ServiceLog` | HTTP | `http://localhost:8086/system/log` | x86 服务日志 |
| `ExportCameraLog` | HTTP | `http://localhost:8064/system/device/camera/log` | 摄像头设备日志 |
| `ExportSpectrometerLog` | HTTP | `http://localhost:8064/system/device/Spectrum/log` | 光谱仪日志 |
| `ExportRCServiceLog1` 等 | 本地目录 | 根据 `CVWinSMSConfig.BaseLocation` 衍生 | 解析外部配置文件路径后拼接 |

---

## 第三方工具集成

| 工具 | 类 / 菜单 | 行为 | 存储路径 |
|------|-----------|------|----------|
| ImageJ | `MenuImageJ` / `ImageViewExTension` | 通过配置路径调用；支持右键在图像视图中通过 ImageJ 打开；CIE 文件自动转临时 TIF | 由 `ExternalToolsConfig.ImageJPath` 指定 |
| Navicat | `InstallNavicateAppProvider` | 在线下载并执行安装程序 | `%AppData%/ColorVision` |

---

## 配置项说明

### ServiceManagerConfig
| 字段 | 说明 |
|------|------|
| `BaseLocation` | 服务安装根目录（CVWindowsService 所在目录） |
| `MySqlPort` | MySQL 端口（默认 3306） |
| `UpdateServerUrl` | 服务更新服务器基础地址（默认 `http://xc213618.ddns.me:9998`） |
| `DownloadLocation` | 在线下载保存目录 |
| `InstallServiceChecked` | 安装向导默认勾选安装服务包 |
| `InstallMySqlChecked` | 安装向导默认勾选安装 MySQL |
| `InstallMqttChecked` | 安装向导默认勾选安装 MQTT |

### CVWinSMSConfig
| 字段 | 说明 |
|------|------|
| `CVWinSMSPath` | CVWinSMS 管理器可执行文件路径 |
| `UpdatePath` | CVWinSMS 版本更新目录基础 URL |
| `IsAutoUpdate` | 是否启动时自动检查 CVWinSMS 更新 |
| `BaseLocation` | 解析自外部 `App.config` 的基础目录（只读） |

所有配置通过 ColorVision 全局配置/属性编辑器写入（实现 `IConfig`）。

---

## 典型运维流程

1. 打开 **服务管理器**，确认 `BaseLocation` 已自动检测或手动设置。
2. 点击 **安装/更新**，检查新版本并下载服务包。
3. 选择更新后：
   - 下载新版 Zip（完整包或增量包）
   - 备份数据库（可选）
   - 停止所有相关 Windows 服务
   - 解压/覆盖 / 复制 CommonDll / 清理 pack
   - 重新启动服务
4. 使用日志菜单验证服务运行状态。
5. 借助 ImageJ 进行数据/结果核对。

---

## 安全与合规注意

- 远程下载地址为内网/私有仓库示例，部署正式环境前请迁移至受信任源并启用 HTTPS。
- 运行安装器时均通过 `Verb = runas` 触发 UAC，必要时请核验文件来源与签名。

---

## 目录结构（核心）

```
WindowsServicePlugin/
 ├─ App.xaml.cs                    # 独立入口（调试/测试用）
 ├─ ServiceManager/                # 内置服务管理器核心
 │   ├─ ServiceManagerWindow.xaml.cs
 │   ├─ ServiceManagerViewModel.cs          # 主 VM（属性、命令、初始化）
 │   ├─ ServiceManagerViewModel.OneKey.cs   # 一键启停
 │   ├─ ServiceManagerViewModel.Config.cs   # 配置同步
 │   ├─ ServiceManagerViewModel.MySql.cs    # MySQL 密码/用户管理
 │   ├─ ServiceManagerViewModel.Helpers.cs  # 辅助方法
 │   ├─ ServiceInstallWindow.xaml.cs
 │   ├─ ServiceInstallViewModel.cs          # 安装 VM（属性、命令）
 │   ├─ ServiceInstallViewModel.Download.cs # 在线下载
 │   ├─ ServiceInstallViewModel.Backup.cs   # 备份/恢复
 │   ├─ ServiceInstallViewModel.Install.cs  # 安装编排
 │   ├─ ServiceInstallViewModel.IncrementalUpdate.cs # 增量更新
 │   ├─ ServiceEntry.cs
 │   ├─ ServiceManagerConfig.cs
 │   ├─ ServicePackageInfo.cs
 │   ├─ WinServiceHelper.cs
 │   └─ MySqlServiceHelper.cs
 ├─ CVWinSMS/                      # CVWinSMS 协作与更新
 │   ├─ InstallTool.cs
 │   └─ CVWinSMSConfig.cs
 ├─ Menus/                         # 菜单与日志访问
 │   ├─ Export*.cs
 │   └─ ServiceLog.cs
 ├─ Tools/                         # 第三方工具与扩展
 │   └─ MenuImageJ.cs
 ├─ SetMysqlConfig.cs              # Wizard：读取服务 MySQL 配置
 ├─ SetServiceConfig.cs            # Wizard：替换服务 CFG
 ├─ InstallNavicate.cs             # Navicat 下载安装
 ├─ manifest.json                  # 插件元数据
 ├─ README.md
 └─ CHANGELOG.md
```

---

## 主要类 & API 摘要

| 类 | 责任 |
|----|------|
| `ServiceManagerViewModel` | 服务管理器主视图模型（状态、命令、日志） |
| `ServiceInstallViewModel` | 服务安装/更新视图模型（下载、备份、安装、增量更新） |
| `ServiceEntry` | 单个 Windows 服务的信息与状态 |
| `ServiceManagerConfig` | 服务管理器配置（路径、更新地址、下载目录） |
| `WinServiceHelper` | Windows 服务操作辅助（查询、启动、停止、安装、卸载） |
| `MySqlServiceHelper` | MySQL 安装、备份、恢复、密码管理、用户管理 |
| `InstallTool` | CVWinSMS 管理工具定位、下载、更新执行 |
| `ExportLogBase` | 抽象日志打开基类（HTTP / 本地目录） |
| `MenuImageJ` / `ImageViewExTension` | ImageJ 调用与图像右键菜单扩展 |
| `SetServiceConfigStep` | Wizard 步骤：将当前配置写入各服务 cfg |
| `SetMysqlConfig` | Wizard 步骤：从 CVWinSMS App.config 读取 MySQL 配置 |

---

## 常见问题 (FAQ)

1. **找不到工具且没有下载按钮？**
   - 检查是否被系统防火墙或代理阻断 HTTP 访问。
2. **服务更新后路径异常？**
   - 确认 `BaseLocation` 是否正确，或重新通过注册表自动检测。
3. **日志目录为空？**
   - 服务可能尚未启动或日志级别过低，尝试通过 HTTP 接口确认。
4. **ImageJ 打不开 CIE 文件？**
   - 插件会自动转换为 TIF，确认临时目录写入是否有权限。
5. **MySQL 安装后 root 密码丢失？**
   - 安装成功后会自动生成随机 root 密码并显示在日志中，请妥善保存；也可通过服务管理器的 "强制重置 root 密码" 功能恢复。

---

## 贡献

1. Fork / 新建分支：`feature/service-enhancement`
2. 遵循现有类命名与目录划分
3. 新增下载器务必：
   - 增加超时 / 失败回滚
   - 校验文件存在再执行
4. 提交 PR 前请附测试说明与必要截图

---

## 更新日志

查看 [CHANGELOG](CHANGELOG.md)

---

## License

本插件遵循主工程 License。

---

如需新增功能或反馈问题，请在主仓库 Issue 中提交：
https://github.com/xincheng213618/scgd_general_wpf/issues
