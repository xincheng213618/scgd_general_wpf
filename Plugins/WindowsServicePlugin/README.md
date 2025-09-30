# WindowsServicePlugin (服务与工具扩展插件)

[![Version](https://img.shields.io/badge/version-1.0-blue.svg)](manifest.json)
[![ColorVision](https://img.shields.io/badge/ColorVision-Plugin-orange.svg)](../../README.md)
[![License](https://img.shields.io/badge/license-MIT-green.svg)](../../LICENSE)

> 增强的本地/远程 Windows 服务与相关运维辅助工具集合。提供服务安装/更新向导、日志快速访问、第三方工具一键获取、以及与 CVWinSMS 服务管理工具的联动。

---
## 🎯 功能概览

| 模块 | 功能 | 说明 |
|------|------|------|
| 安装向导 (WizardStep) | WinRAR / MQTT / MySQL 压缩包 / Navicat / Everything | 一键下载并可交互安装或打开目录 |
| 服务管理 | CVWinSMS 管理工具下载/定位/自动更新 | 通过 `InstallTool` 联动外部服务管理器 |
| 服务更新 | Windows 服务核心包比对与增量更新 | `UpdateService` 自动检测新版本 (读取 `LATEST_RELEASE`) |
| 日志访问 | RC / x64 / x86 / Camera / NewAlgorithm | HTTP 地址或本地物理目录一键打开 |
| 第三方工具 | ImageJ / BeyondCompare 下载与调用 | 自动解压到 `%AppData%/ColorVision` 下并记录路径 |
| Windows 激活脚本 | 托管激活脚本快速执行 | 从内嵌 `activate.ps1` 临时释放执行 |
| 配置中心集成 | 可在全局配置界面编辑路径与开关 | `CVWinSMSConfig`, `ImageJConfig` |
| 菜单集成 | 插入主菜单/视图/帮助/日志分类 | 统一 GUID 与排序体系 |
| 多语言资源 | 提供多语言 `Resources.*.resx` | Header/菜单文案国际化 |

---
## 🚀 快速开始

1. 启动 ColorVision 主程序。
2. 打开 设置 / 插件 / 确认已加载 `WindowsServicePlugin`。
3. 在菜单栏中找到：
   - 服务日志: 打开各类服务日志 / 物理目录。
   - 视图工具: ImageJ / BeyondCompare / Windows 激活 / CVWinSMS 管理。
4. 若首次使用第三方工具，按提示选择 “下载” 或手动定位已存在的可执行文件。
5. 如需更新后台服务，触发 `服务更新`（自动检测版本，显示交互弹窗）。

---
## 🧩 安装向导步骤（Wizard）
所有安装步骤实现自 `WizardStepBase`，在 ColorVision 的安装或运维向导中自动聚合：

| 类 | Header | 功能 | 备注 |
|----|--------|------|------|
| `InstallWinrar` | 安装压缩软件 | 下载并安装 WinRAR 7.0 | 执行 EXE（可改静默参数） |
| `InstallMQTT` | 安装MQTT | 下载 Mosquitto 安装器并尝试启动服务 | 默认尝试 `net start mosquitto` |
| `InstallMySql` | 下载MySql | 下载 MySQL 5.7 Zip 包 | 只下载不自动安装 |
| `InstallNavicate` | 下载 Navicate | 第三方数据库客户端 | 需用户手动安装 |
| `InstallEveryThing` | 安装Everything | 下载 Everything 搜索工具 | 默认执行安装器 |

全部下载路径当前指向内部私有 HTTP 分发服务器（`http://xc213618.ddns.me:9999/...`）。可通过替换配置或代码中 `url` 字段迁移。

---
## 🔧 服务管理与更新

### CVWinSMS 管理工具
- 类：`InstallTool`
- 作用：定位或下载 `CVWinSMS.exe`（服务管理 GUI），支持自动更新（解压覆盖并迁移配置）。
- 版本检测：读取 `UpdatePath + /LATEST_RELEASE`。
- 配置：`CVWinSMSConfig` 中 `CVWinSMSPath`、`IsAutoUpdate`、`UpdatePath`。

### 后台服务更新
- 类：`UpdateService`
- 检测逻辑：
  1. 读取注册表获取服务 ImagePath。
  2. 解析当前版本号。
  3. 访问 `LATEST_RELEASE` 获取最新版本号。
  4. 用户确认后下载对应 zip，备份 MySQL（调用 `MySqlLocalServicesManager`），停止服务，执行更新并提示恢复。
- 数据库结构变更：自动执行内置 `ALTER TABLE` 语句为结果表新增 `version` 字段。

### 更新文件命名规范
```
CVWindowsService[{Version}]-{Revision4}.zip
InstallTool[{Version}].zip
```

---
## 📜 日志访问

| 菜单类 | 访问类型 | 默认地址 / 目录 | 说明 |
|--------|----------|----------------|------|
| `ExportRCServiceLog` | HTTP | `http://localhost:8080/system/log` | RC 服务日志接口 |
| `Exportx64ServiceLog` | HTTP | `http://localhost:8064/system/log` | x64 主服务日志 |
| `Exportx86ServiceLog` | HTTP | `http://localhost:8086/system/log` | x86 服务日志 |
| `ExportCameraLog` | HTTP | `http://localhost:8064/system/device/camera/log` | 摄像头设备日志 |
| `ExportRCServiceLog1` / `Exportx64ServiceLog1` / `ExportCameraLog1` | 本地目录 | 根据 `CVWinSMSConfig.BaseLocation` 衍生 | 解析外部配置文件路径后拼接 | 
| `ExportNewAlgorithmLog` | 本地目录 | `C:\Windows\System32\log` | 存在即打开 |

内部目录路径依赖：`CVWinSMSConfig.Init()` 解析外部 `App.config` 中的键值（`BaseLocation`）。

---
## 🛠 第三方工具集成

| 工具 | 类 / 菜单 | 行为 | 存储路径 |
|------|-----------|------|----------|
| ImageJ | `MenuImageJ` / `ImageViewExTension` | 下载 zip 解压并可右键在图像视图中调用 | `%AppData%/ColorVision/ImageJ/ImageJ.exe` |
| BeyondCompare | `MenuBeyondCompare` | 下载 zip，解压并执行 | `%AppData%/ColorVision/Beyond Compare 5/BCompare.exe` |
| Windows 激活脚本 | `MenuAcitveWindows` | 释放内嵌 `activate.ps1` 并提升执行 | Temp 路径临时脚本 |

`ImageViewExTension`：在图像右键菜单中增加 “通过ImageJ打开”，若源文件是 CIE 文件先转换/导出临时 TIF。

---
## ⚙️ 配置项说明

### CVWinSMSConfig
| 字段 | 说明 |
|------|------|
| `CVWinSMSPath` | CVWinSMS 管理器可执行文件路径 |
| `UpdatePath` | 版本更新目录基础 URL |
| `IsAutoUpdate` | 是否启动时自动检查更新 |
| `BaseLocation` | 解析自外部 `App.config` 的基础目录（只读） |

### ImageJConfig
| 字段 | 说明 |
|------|------|
| `ImageJPath` | ImageJ 可执行路径 |
| `BeyondComparePath` | Beyond Compare 可执行路径 |

所有配置通过 ColorVision 全局配置/属性编辑器写入（实现 `IConfigSettingProvider`）。

---
## 🧪 典型运维流程

1. 打开 CVWinSMS（若缺失 → 触发下载）。
2. 在插件菜单中点击 “服务更新” 弹出版本提示。
3. 选择更新后：
   - 下载新版 Zip
   - 备份数据库
   - 停止服务 `RegistrationCenterService`
   - 解压/覆盖 / 迁移配置
   - 重新启动并提示恢复数据库
4. 使用日志菜单验证服务运行状态。
5. 借助 ImageJ / BeyondCompare 进行数据/结果核对。

---
## 🔒 安全与合规注意
- Windows 激活脚本仅供内部测试与实验环境使用，生产与商业环境请遵守当地法规与授权协议。
- 远程下载地址为内网/私有仓库示例，部署正式环境前请迁移至受信任源并启用 HTTPS。
- 运行安装器时均通过 `Verb = runas` 触发 UAC，必要时请核验文件来源与签名。

---
## 📦 目录结构（核心）
```
WindowsServicePlugin/
 ├─ Install*.cs                 # 安装向导步骤
 ├─ CVWinSMS/                   # CVWinSMS 协作与更新逻辑
 │   ├─ InstallTool.cs
 │   ├─ UpdateService.cs
 │   └─ CVWinSMSConfig.cs
 ├─ Menus/                      # 菜单与日志访问
 │   ├─ Export*.cs              # 各类日志访问
 │   └─ ServiceLog.cs
 ├─ Tools/                      # 第三方工具与扩展
 │   ├─ MenuImageJ.cs
 │   ├─ MenuBeyondCompare.cs
 │   └─ MenuAcitveWindows.cs
 ├─ Assets/activate.ps1         # 激活脚本资源
 ├─ manifest.json               # 插件元数据
 └─ README.md
```

---
## 🧩 主要类 & API 摘要

| 类 | 责任 |
|----|------|
| `InstallWinrar` / `InstallMQTT` / ... | 各独立下载+执行步骤 |
| `InstallTool` | CVWinSMS 管理工具定位、下载、更新执行 |
| `UpdateService` | 核心服务版本检测与升级流水线 |
| `ExportLogBase` | 抽象日志打开基类（HTTP / 本地目录） |
| `ExportRCServiceLog` 等 | 具体日志菜单派生实现 |
| `MenuImageJ` / `MenuBeyondCompare` | 第三方工具下载与执行入口 |
| `ImageViewExTension` | 图像右键菜单扩展（ImageJ 打开） |
| `MenuAcitveWindows` | 内嵌激活 PowerShell 脚本执行 |
| `CVWinSMSConfig` | CVWinSMS 路径与自动更新配置 |
| `ImageJConfig` | 第三方工具路径配置 |

---
## ❓ 常见问题 (FAQ)

1. 找不到工具且没有下载按钮？
   - 检查是否被系统防火墙或代理阻断 HTTP 访问。
2. 服务更新后路径异常？
   - 确认 `App.config` 中 BaseLocation 是否正确，或重新定位 CVWinSMS。
3. 日志目录为空？
   - 服务可能尚未启动或日志级别过低，尝试通过 HTTP 接口确认。
4. ImageJ 打不开 CIE 文件？
   - 插件会自动转换为 TIF，确认 TIF 写入是否有权限。

---
## 🤝 贡献
1. Fork / 新建分支：`feature/service-enhancement`
2. 遵循现有类命名与目录划分
3. 新增下载器务必：
   - 增加超时 / 失败回滚
   - 校验文件存在再执行
4. 提交 PR 前请附测试说明与必要截图

---
## 🗓 更新日志
查看 [CHANGELOG](CHANGELOG.md)

---
## 📄 License
本插件遵循主工程 License（MIT）。

---
## 🔍 后续规划 (Roadmap)
- [ ] 增加下载源镜像与超时重试策略
- [ ] 引入 SHA256 校验防篡改
- [ ] 服务状态可视化面板
- [ ] 可配置的日志抓取与压缩上传
- [ ] 支持 HTTPS 私有制品仓库凭证管理

---
如需新增功能或反馈问题，请在主仓库 Issue 中提交：
https://github.com/xincheng213618/scgd_general_wpf/issues
