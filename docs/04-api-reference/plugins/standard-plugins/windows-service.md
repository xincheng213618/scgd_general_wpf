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

**WindowsServicePlugin** 是 ColorVision 的 Windows 服务与运维辅助工具插件，提供服务安装向导、日志快速访问、第三方工具集成以及 CVWinSMS 服务管理工具的联动功能。

### 基本信息

- **版本**: 1.0.0
- **目标框架**: .NET 8.0 / .NET 10.0 Windows
- **主要功能**: 服务管理、日志访问、第三方工具集成
- **依赖**: ColorVision.UI, ColorVision.Common
- **许可证**: MIT

## 主要功能

### 1. 安装向导

提供常用开发和运维工具的一键下载和安装：

- **WinRAR** - 压缩软件安装
- **MQTT** - Mosquitto MQTT 代理安装
- **MySQL** - MySQL 5.7 下载
- **Navicat** - 数据库客户端下载
- **Everything** - 文件搜索工具安装

### 2. 服务管理

- **CVWinSMS 管理工具** - 下载、定位、自动更新服务管理器
- **服务更新** - Windows 服务核心包比对与增量更新
- **版本检测** - 自动检测新版本并提示更新

### 3. 日志访问

提供多种服务日志的快速访问：

| 日志类型 | 访问方式 | 默认地址 |
|----------|----------|----------|
| RC 服务日志 | HTTP | http://localhost:8080/system/log |
| x64 服务日志 | HTTP | http://localhost:8064/system/log |
| x86 服务日志 | HTTP | http://localhost:8086/system/log |
| 摄像头日志 | HTTP | http://localhost:8064/system/device/camera/log |
| 本地日志目录 | 本地目录 | 根据 CVWinSMSConfig.BaseLocation 解析 |

### 4. 第三方工具集成

- **ImageJ** - 图像处理工具，支持右键在图像视图中调用
- **BeyondCompare** - 文件对比工具
- **Windows 激活脚本** - 托管激活脚本快速执行（仅供测试）

## 架构设计

```mermaid
graph TD
    A[WindowsServicePlugin] --> B[安装向导]
    A --> C[服务管理]
    A --> D[日志访问]
    A --> E[第三方工具]
    
    B --> B1[WinRAR]
    B --> B2[MQTT]
    B --> B3[MySQL]
    B --> B4[Navicat]
    B --> B5[Everything]
    
    C --> C1[CVWinSMS]
    C --> C2[服务更新]
    C --> C3[版本检测]
    
    D --> D1[HTTP日志]
    D --> D2[本地日志]
    
    E --> E1[ImageJ]
    E --> E2[BeyondCompare]
    E --> E3[激活脚本]
```

### 核心组件

```
WindowsServicePlugin/
├── Install*.cs                 # 安装向导步骤
├── CVWinSMS/                   # CVWinSMS 协作与更新
│   ├── InstallTool.cs
│   ├── UpdateService.cs
│   └── CVWinSMSConfig.cs
├── Menus/                      # 菜单与日志访问
│   ├── Export*.cs              # 各类日志访问
│   └── ServiceLog.cs
├── Tools/                      # 第三方工具
│   ├── MenuImageJ.cs
│   ├── MenuBeyondCompare.cs
│   └── MenuAcitveWindows.cs
├── Assets/
│   └── activate.ps1            # 激活脚本资源
└── manifest.json               # 插件清单
```

## 使用指南

### 快速开始

1. 启动 ColorVision 主程序
2. 打开 设置 / 插件 / 确认已加载 `WindowsServicePlugin`
3. 在菜单栏中找到：
   - **服务日志**: 打开各类服务日志 / 物理目录
   - **视图工具**: ImageJ / BeyondCompare / Windows 激活 / CVWinSMS 管理
4. 若首次使用第三方工具，按提示选择 "下载" 或手动定位已存在的可执行文件
5. 如需更新后台服务，触发 `服务更新`（自动检测版本，显示交互弹窗）

### 安装向导使用

```csharp
// 各安装步骤实现自 WizardStepBase
// 在 ColorVision 的安装或运维向导中自动聚合

// 示例：WinRAR 安装
var installWinrar = new InstallWinrar();
installWinrar.Execute();

// 示例：MQTT 安装
var installMqtt = new InstallMQTT();
installMqtt.Execute();
```

### 服务更新流程

```csharp
// 服务更新流程
var updateService = new UpdateService();

// 1. 检测版本
bool hasUpdate = updateService.CheckForUpdate();

// 2. 执行更新
if (hasUpdate)
{
    updateService.PerformUpdate();
    // 流程：下载新版 Zip -> 备份 MySQL -> 停止服务 -> 解压覆盖 -> 重新启动
}
```

### 第三方工具使用

```csharp
// ImageJ 集成
var menuImageJ = new MenuImageJ();
menuImageJ.Execute();

// BeyondCompare 集成
var menuBeyondCompare = new MenuBeyondCompare();
menuBeyondCompare.Execute();
```

## API参考

### InstallTool

CVWinSMS 管理工具类。

```csharp
public class InstallTool
{
    // 定位或下载 CVWinSMS.exe
    public void LocateOrDownloadCVWinSMS();
    
    // 检查并执行自动更新
    public void CheckAndUpdate();
    
    // 获取 CVWinSMS 路径
    public string GetCVWinSMSPath();
}
```

### UpdateService

服务更新管理类。

```csharp
public class UpdateService
{
    // 检查是否有更新
    public bool CheckForUpdate();
    
    // 执行更新
    public void PerformUpdate();
    
    // 获取当前版本
    public string GetCurrentVersion();
    
    // 获取最新版本
    public string GetLatestVersion();
}
```

### CVWinSMSConfig

CVWinSMS 配置类。

```csharp
public class CVWinSMSConfig : IConfig
{
    // CVWinSMS 管理器路径
    public string CVWinSMSPath { get; set; }
    
    // 更新路径基础 URL
    public string UpdatePath { get; set; }
    
    // 是否自动更新
    public bool IsAutoUpdate { get; set; }
    
    // 基础目录（从外部 App.config 解析）
    public string BaseLocation { get; }
    
    // 初始化配置
    public void Init();
}
```

### ImageJConfig

第三方工具配置类。

```csharp
public class ImageJConfig : IConfig
{
    // ImageJ 可执行路径
    public string ImageJPath { get; set; }
    
    // Beyond Compare 可执行路径
    public string BeyondComparePath { get; set; }
}
```

### ExportLogBase

日志导出基类。

```csharp
public abstract class ExportLogBase : MenuItemBase
{
    // 日志类型
    public abstract string LogType { get; }
    
    // 打开日志
    public abstract void OpenLog();
}
```

## 配置说明

### CVWinSMSConfig

| 字段 | 类型 | 说明 |
|------|------|------|
| CVWinSMSPath | string | CVWinSMS 管理器可执行文件路径 |
| UpdatePath | string | 版本更新目录基础 URL |
| IsAutoUpdate | bool | 是否启动时自动检查更新 |
| BaseLocation | string | 解析自外部 App.config 的基础目录（只读） |

### ImageJConfig

| 字段 | 类型 | 说明 |
|------|------|------|
| ImageJPath | string | ImageJ 可执行路径 |
| BeyondComparePath | string | Beyond Compare 可执行路径 |

### 更新文件命名规范

```
CVWindowsService[{Version}]-{Revision4}.zip
InstallTool[{Version}].zip
```

### 版本检测

通过读取 `UpdatePath + /LATEST_RELEASE` 文件获取最新版本号。

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
1. 确认 `App.config` 中 BaseLocation 是否正确
2. 重新定位 CVWinSMS
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
2. 确认 TIF 写入是否有权限
3. 检查临时目录空间是否充足

## 最佳实践

### 1. 服务管理

```csharp
// 推荐配置
{
    "CVWinSMSPath": "C:\\Tools\\CVWinSMS.exe",
    "UpdatePath": "http://your-server.com/updates/",
    "IsAutoUpdate": true
}
```

### 2. 工具路径配置

```csharp
// 标准路径配置
{
    "ImageJPath": "%APPDATA%\\ColorVision\\ImageJ\\ImageJ.exe",
    "BeyondComparePath": "%APPDATA%\\ColorVision\\Beyond Compare 5\\BCompare.exe"
}
```

### 3. 更新流程

1. 打开 CVWinSMS（若缺失 → 触发下载）
2. 在插件菜单中点击 "服务更新" 弹出版本提示
3. 选择更新后：
   - 下载新版 Zip
   - 备份数据库
   - 停止服务 `RegistrationCenterService`
   - 解压/覆盖 / 迁移配置
   - 重新启动并提示恢复数据库
4. 使用日志菜单验证服务运行状态
5. 借助 ImageJ / BeyondCompare 进行数据/结果核对

### 4. 安全注意事项

- Windows 激活脚本仅供内部测试与实验环境使用
- 生产与商业环境请遵守当地法规与授权协议
- 远程下载地址为内网/私有仓库示例，部署正式环境前请迁移至受信任源并启用 HTTPS
- 运行安装器时均通过 `Verb = runas` 触发 UAC，必要时请核验文件来源与签名

## 版本历史

### v1.0.0（2026-02）

**初始版本**:
- ✅ 安装向导（WinRAR、MQTT、MySQL、Navicat、Everything）
- ✅ CVWinSMS 管理工具集成
- ✅ 服务更新功能
- ✅ 多类型日志访问
- ✅ ImageJ / BeyondCompare 集成
- ✅ Windows 激活脚本

---

*文档版本: 1.0*  
*最后更新: 2026-04-02*

## 相关资源

- [源代码](../../../../Plugins/WindowsServicePlugin/)
- [CHANGELOG](../../../../Plugins/WindowsServicePlugin/CHANGELOG.md)
- [ColorVision 插件开发指南](../../../02-developer-guide/plugin-development/overview.md)

## 许可证

本插件遵循主工程 License（MIT）。

---

**版权**: Copyright (C) 2025-present ColorVision Development Team
