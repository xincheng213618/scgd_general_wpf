---
knowledge_id: "delivery.deployment"
knowledge_type: "index"
status: "current"
summary: "区分完整安装器、主程序更新、插件项目包和独立在线下载工具；ColorVisionSetup以Framework 4.8单文件查询并下载最新版，实际安装仍由完整安装包负责。"
aliases: ["部署","交付制品","安装器","完整安装包","源码构建输出","增量更新包","项目包交付","启动恢复入口","Advanced Installer","ColorVision.aip","ColorVisionSetup","CombinedUpdateCoordinator","StartupRecoveryWindow"]
code_paths: ["ColorVision/ColorVision.csproj","Scripts/release.bat","Scripts/build.py","ColorVision/Update/CombinedUpdateCoordinator.cs","ColorVision/Recovery/StartupRecoveryWindow.xaml.cs","src/ColorVisionSetup"]
test_paths: ["src/ColorVisionSetup/Tests/Program.cs"]
related: ["delivery.installation","delivery.prerequisites","delivery.update","delivery.scripts","plugins.getting-started","operations.first-run","delivery.backend","delivery.web-deployment"]
---

# 桌面交付制品与责任路由

本页回答“这次拿到或准备生成的是什么制品，后续由哪条实现负责”。源码输出、完整安装包、在线更新差异包和插件包不是可互换的交付物；安装完成、更新进程接管和主程序健康启动也不是同一个结果。

## 按制品和状态定位

| 要处理的对象 | 权威主题 | 实现责任 |
| --- | --- | --- |
| 本地源码构建输出 | [环境与构建前提](../../00-getting-started/prerequisites.md) | 项目引用、native/x64 和输出复制规则；编译命令不在部署索引重复维护 |
| 完整桌面安装包与目标安装目录 | [安装制品与运行输出](../../00-getting-started/installation.md) | 首次安装、目录权限、依赖与配置检查；完整安装工程边界见本页下一节 |
| 主程序、插件或项目包的发布制品 | [构建与发布脚本](../scripts/README.md) | `Scripts/release.bat` 及对应包脚本的构建、签名、上传与成功判定；发布入口会产生外部写入，不是文档验证命令 |
| 现有主程序安装的在线更新 | [更新与恢复](./auto-update.md) | `CombinedUpdateCoordinator` 协调主程序/插件计划，下载缓存、包校验和外部更新执行由该主题维护 |
| 插件与项目包 `.cvxp` | [插件产物、安装与交付](../plugin-development/getting-started.md) | HostCopy、manifest 身份、宿主共享依赖和安装交接；包上传仍归发布脚本 |
| 安装后的启动或未完成启动 | [启动与最小运行验证](../../00-getting-started/first-steps.md)、[更新与恢复](./auto-update.md) | 普通启动初始化与 `StartupRecoveryWindow` 的修复/插件处置分别核对；恢复窗口出现不等于问题已修复 |

网络请求复用、重试、增量复制、程序快照、插件备份和恢复交接的完整契约只在各自主题维护，不从“部署”一词推定这些阶段已经成功。客户项目的专用配置、资源与版本约束应回到对应项目包核对，不能把通用安装器当作所有项目的完整交付清单。

## 外部安装工程与独立助手

当前主程序发布 wrapper 调用 `Scripts/build.py`；后者以 `build.sln` 为解决方案，并指定仓库外的 Advanced Installer `ColorVision.aip`。拉取仓库并不同时取得该安装工程；要改安装组件、权限或文件清单，必须核对实际使用的外部工程与交付包，不能仅由托管项目编译成功推断。

`src/ColorVisionSetup/` 是独立的最新版在线下载工具，不引用主程序或 Engine，不要求目标机器装有 .NET 10。它未加入 `build.sln` 与 `Scripts/release.bat` 的主程序发布链；本地构建不代表正式签名或发布。当前客户端更新仍位于 `ColorVision/Update/`，启动恢复位于 `ColorVision/Recovery/`。

[Web 本地启动与 NAS 部署](./web.md)是独立责任链。Docker、云服务或集群方式不因后端存在就成为 Windows WPF 桌面程序的默认交付方式。

## 独立在线下载工具

`ColorVisionSetup.exe` 提供最新版完整安装包的联网下载。目标为 Windows x64 / .NET Framework 4.8，可运行在 4.8.1 上；单文件不内置 Framework 运行时，也不内置完整安装包。不使用 `logi_codecs_shared.dll`、`logi_installer_shared.dll`，界面和图标内嵌，只依赖 Framework 与 Windows 系统组件。光谱图形和动画使用 WPF 矢量；系统关闭客户端动画或软件渲染时保留静态图形。

打开窗口自动查询最新版；主按钮依次用于下载、校验后打开安装包。每次下载前重新查询版本，不使用上次失败查询的缓存版本。进度显示已传输大小、速度和百分比；服务器未提供长度时显示不定进度。支持取消、重试和重新检查，下载完成后可打开文件夹。取消与窗口关闭只取消准备任务；不会自动启动安装器、读取安装位置、修复指定版本或选择本地包。已有安装包直接使用其自身安装功能。

在线查询复用当前公共接口 `GET /api/app/latest-version` 与 `GET /api/app/releases/{version}/download`，不内置认证信息。当前部署地址仍为 HTTP，因此版本元数据不具备认证保证；安装包必须通过发行公钥与签名校验。HTTP 错误、无效版本、长度不符、空内容或传输超时不能得到可打开状态。下载使用本次独立目录中的 `.partial` 文件，成功后才改为 EXE；取消或失败会尽力清理部分文件，失败后可重新执行，不提供跨进程断点续传。

完整文件还须经过 Windows WinTrust 的 PE 内容摘要和密码学签名验证，签名者公钥 SHA-256 必须匹配 `PublisherSignature` 内置的发行公钥；并核对 `ProductName=ColorVision`、`FileDescription=ColorVision Installer` 和四段版本。在线包必须与请求的目标版本完全一致。这是应用级公钥固定信任：不靠证书 CN 文本、不向系统证书库写入，也不依赖机器信任项目自签名根；不声称提供在线吊销检查或按证书有效期判断发行资格。证书续期沿用相同公钥时兼容，更换发行密钥前必须先发布包含新公钥策略的助手。

用户明确点击“打开安装包”后，再次验证签名、版本与此前记录的整个文件 SHA-256，并持有拒绝写入/删除共享的文件句柄直到提权启动交接。取消 UAC 保留就绪包以便重试。下载工具不关闭业务进程、不处理增量包、插件、数据库或程序快照，不报告安装百分比或安装成功；“安装包已打开”只确认进程启动。主程序本身需要的运行环境仍由完整安装包负责。

日志位于 `%LOCALAPPDATA%\ColorVision\Setup\setup.log`，本次安装包位于同级 `Packages/<操作标识>/ColorVision-<版本>.exe`。完整包保留，可以从“打开文件夹”找到并自行复制、安装；本版没有自动缓存复用或过期清理。

### 本地构建与验证

开发机需要 Visual Studio MSBuild、WPF 工具及 .NET Framework 4.8 Targeting Pack。以下命令仅在本机构建助手，不执行主程序发布、不调用安装器、不签名或上传：

```powershell
pwsh -NoProfile -File .\Scripts\build_setup.ps1
```

输出为 `.artifacts/ColorVisionSetup/package/ColorVisionSetup.exe`。脚本检查交付目录仅有一个 EXE、程序集只引用 Framework 组件且不嵌入 DLL。根目录 `ColorVision.snk` 存在时继续强名称签名；强名称不等于 Authenticode 发行签名。正式分发前应使用既有受控签名流程，不能从本地构建成功推定已签名。

`src/ColorVisionSetup/Tests/ColorVisionSetup.Tests.csproj` 是同框架控制台验证程序，无 NuGet 或主程序运行依赖。用 Visual Studio MSBuild 以 Release/x64 构建后运行；可传入一个已有的官方完整安装 EXE 路径，验证真实签名、版本不符、篡改拒绝及最终交接前文件锁。测试不会启动该安装包。无参数时跳过真实签名制品验证，不应将其报告为完整校验通过。

需另外在目标电脑验证无 .NET 10 时单 EXE 启动、DPI/键盘操作、在线下载、UAC 取消和安装器交接；开发机测试和界面预览不等于这些现场验收。

## 验证边界

测试入口和最小验证方法随具体制品主题维护；本索引不声明完整安装器、远端发布或启动恢复的端到端自动化覆盖。文档和路径检查不能代替目标环境验收，也不授权启动应用、连接设备、安装/回退或执行发布。
