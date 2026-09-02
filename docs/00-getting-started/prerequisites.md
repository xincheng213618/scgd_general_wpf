---
knowledge_id: "delivery.prerequisites"
knowledge_type: "guide"
status: "current"
summary: "Windows x64 运行与源码构建前提：Desktop Runtime、SDK、C++ 工具集及已有 native DLL 的选择。"
aliases: ["环境要求","系统要求","干净克隆","只装.NET SDK能编译吗","opencv_helper.dll","OpenCvHelperBinary","MSBuild","Microsoft.WindowsDesktop.App","Desktop Runtime","Windows 7","Visual Studio 2026"]
code_paths: ["Directory.Build.props","ColorVision/ColorVision.csproj","UI/ColorVision.Core/ColorVision.Core.csproj","Engine/ColorVision.Engine/ColorVision.Engine.csproj","Engine/cvColorVision/cvColorVision.csproj","Native/opencv_helper/opencv_helper.vcxproj","Native/AGENTS.md","build.sln","package.json","package-lock.json"]
test_paths: ["Scripts/tests/test_verify_platform_policy.py"]
related: ["delivery.start","delivery.testing","delivery.native-testing","engine.native-integration","delivery.installation","operations.first-run"]
---

# 系统要求与首次构建

ColorVision 桌面宿主使用 Windows WPF、.NET 10 和 x64。运行已交付程序、编译源码和阅读文档需要不同环境，按实际任务准备即可。

## 选择所需环境

| 任务 | 所需环境 |
| --- | --- |
| 阅读源码与 Markdown | Git 检出或完整源码副本；不需要启动产品、安装 SDK 或设备驱动 |
| 本地知识查询、生成与校验 | Node.js 20+；独立 CLI 不需要安装站点依赖 |
| 构建文档网页 | Node.js 22+，在仓库根目录执行 `npm ci` 安装依赖；安装会联网，本地构建不会发布 |
| 运行桌面程序 | 与交付包一致的 Windows x64、.NET 桌面运行时、native DLL、配置和所用设备/服务依赖 |
| 构建托管项目 | .NET 10 SDK、PowerShell，以及项目需要的现有 native 制品；部分共享库还多目标构建 .NET 8，以各 `.csproj` 为准 |
| 构建 C++ 项目或完整解决方案 | 支持 .NET 10 的 Visual Studio/MSBuild、C++ 工作负载、Windows SDK、对应工具集与项目导入的依赖 |

## Windows 与 .NET 运行时

部署时核对 Microsoft 的 [.NET 与 Windows 支持表](https://learn.microsoft.com/en-us/dotnet/core/install/windows#supported-versions)及本次交付的驱动支持范围。Windows 10 的支持取决于具体版本/版本类型和生命周期，不能概括为所有 Windows 10 都受支持；.NET 10 不支持 Windows 7/8.1。Microsoft 对某一系统的 .NET 支持也不等于 ColorVision 已完成该系统的设备验收。

部分共享项目使用 `net8.0-windows7.0` / `net10.0-windows7.0`。其中 Windows 后缀用于编译期 API/资产选择，不能据此推断运行时支持 Windows 7；参见 [Windows 目标框架说明](https://learn.microsoft.com/en-us/windows/apps/desktop/modernize/winrt-apis-desktop-apps)。宿主、官方插件及客户包的 x64 限制以 `Directory.Build.props` 为准，独立 FileIO 包的例外见[构建平台边界](../02-developer-guide/README.md)。

运行依赖框架的 WPF 输出需要匹配的 **.NET Desktop Runtime**（`Microsoft.WindowsDesktop.App`）；仅有基础 `.NET Runtime` 或 ASP.NET Core Runtime 不包含 WPF 运行时。SDK 包含桌面运行时并用于开发，单纯运行程序不要求安装整个 SDK。选择 Windows x64，版本以实际 `ColorVision.runtimeconfig.json` 和交付包为准；不要把安装器一定补齐所有依赖当成前提。[Microsoft 的运行时选择说明](https://learn.microsoft.com/en-us/dotnet/core/install/windows#choose-the-correct-runtime)

以下命令只查询当前环境，不启动 ColorVision：

```powershell
dotnet --info
dotnet --list-sdks
dotnet --list-runtimes
Get-Command msbuild -ErrorAction SilentlyContinue
```

`dotnet` 不在 PATH 不直接证明程序不能运行：先核对安装位置及该包是否自带运行时。查询结果也不证明设备驱动、GPU 或数据库已经可用。

## 权限、资源与网络

- 安装器、驱动安装及服务管理可能要求提升权限；运行阶段按实际目录和操作授权，不把日常管理员运行作为统一要求。已有安装优先按[更新与恢复](../02-developer-guide/deployment/auto-update.md)处理，不以先卸载或删配置作为普通升级前提。
- 确认实际安装、配置、日志、结果和缓存目录的访问权限。配置可能位于相邻 `Config/` 或用户目录，具体解析规则见[配置持久化](../04-api-reference/ui-components/configuration.md)。
- 内存、磁盘和显示需求随图像尺寸、通道、并行流程与结果保留量变化；本仓库没有一组可代替项目验收的统一硬件最低值。连接设备前核对其驱动、通信及项目配置。
- 检查更新、插件市场、远程数据库/MQTT 和模型服务各需要对应网络；本地打开图像不等于启动过程完全离线，见[首次运行](./first-steps.md)。

## 源码构建：先确认 native 输入

在仓库根目录的 PowerShell 中检查输入文件：

```powershell
Test-Path .\Native\opencv_helper\x64\Release\opencv_helper.dll
Test-Path .\x64\Release\opencv_helper.dll
Test-Path .\x64\Release\opencv_cuda.dll
Test-Path .\ColorVision.snk
```

`ColorVision.Core.csproj` 默认按 `OpenCvHelperBinary` 选择 helper：有有效 `SolutionDir` 时固定取解决方案的 `x64\Release`；单项目构建依次查 native 项目的 `x64\Release`、仓库的 `x64\Release`。所选文件缺失时加入 C++ 项目引用，并指定 Release/x64。解决方案选定路径缺失时，不会因为另一个候选存在就自动改用它；完整选择和复制责任见 [native 集成](../02-developer-guide/engine-development/opencv-integration.md)。

`opencv_helper.dll` 不是干净克隆自带的生成结果。`x64/Release/opencv_cuda.dll` 则是有意跟踪的发布输入；当前 `build.sln` 不构建 CUDA 项目，不能要求所有普通构建都先安装 CUDA 工具链，也不能将这个 DLL 当临时产物删除。是否需要重建 CUDA 依赖由对应 native 变更决定。

文件存在只说明有候选，不保证版本、架构或 ABI 匹配。保留项目要求的依赖与签名条件；`ColorVision.snk` 存在时不关闭强名称签名来绕过构建问题。

## 执行本地构建

已有匹配的 helper、OpenCV 与其他项目依赖时，可执行托管构建：

```powershell
dotnet restore .\ColorVision\ColorVision.csproj -p:Platform=x64
if ($LASTEXITCODE -ne 0) { throw '还原失败；先处理首个依赖错误。' }
dotnet build .\ColorVision\ColorVision.csproj -p:Platform=x64
if ($LASTEXITCODE -ne 0) { throw '构建失败；检查实际项目与缺失输入。' }
```

首次构建或完整解决方案构建使用 Visual Studio Developer PowerShell。在 IDE 中开发 .NET 10 需要 Visual Studio 2026 18.0+，具体 SDK 对应关系见 [Microsoft 工具链要求](https://learn.microsoft.com/en-us/dotnet/core/install/windows#net-versions-and-visual-studio)；C++ 仍须安装项目指定工具集：helper 使用 `v143`，原生测试项目使用 `v145`，Windows SDK 为 `10.0`。测试项目和解决方案的配置差异见[原生测试指南](../02-developer-guide/engine-development/native-testing.md)。

以下命令会还原依赖并生成本地产物，不运行主程序，也不发布：

```powershell
dotnet restore .\build.sln -p:Platform=x64
if ($LASTEXITCODE -ne 0) { throw '解决方案还原失败；先处理首个依赖错误。' }
msbuild .\build.sln /m:1 /nodeReuse:false /p:Configuration=Release /p:Platform=x64
if ($LASTEXITCODE -ne 0) { throw '解决方案构建失败；检查失败项目及其工具链。' }
```

主程序通常输出到 `ColorVision/bin/x64/<Configuration>/net10.0-windows/`，以实际 MSBuild 输出和自定义属性为准。只编译特定功能时，优先使用该主题的最小项目/测试入口；`build.sln` 不代表仓库全部项目或全部测试。

## 构建完成后检查什么

运行输出必须按实际工程和交付清单核对，不能只复制 `ColorVision.exe`：

- `ColorVision.Core.csproj` 复制 helper、CUDA 和 OpenCV native 输入；`ColorVision.Engine.csproj` 引入 OpenCvSharp Windows runtime。两套 native 依赖分别核对。
- 供应商相机、校准等运行库由 `cvColorVision.csproj` 从 `DLL/scgd_internal_dll/` 引入，具体路径与复制条件见该项目及[设备 native 绑定](../04-api-reference/engine-components/cvColorVision.md)。`CVCommCore` / `MQTTMessageLib` 的当前类型来源与旧插件兼容要求见[命名空间与程序集](../04-api-reference/engine-components/cvColorVision.md#命名空间与程序集)。
- 主工程复制 `log4net.config`、资源以及配套工具；插件/客户包还要核对各自 manifest 和共享依赖。

| 现象 | 下一步 |
| --- | --- |
| 提示缺少 `Microsoft.WindowsDesktop.App` | 核对 runtimeconfig、x64 Desktop Runtime 和实际 .NET 安装位置 |
| 缺少 C++ targets/toolset，或普通 dotnet 无法构建 `.vcxproj` | 检查选中的 helper 输入与 VS Developer PowerShell/C++ 工具集，按 native 指南处理 |
| 无法复制 helper、CUDA 或 OpenCV DLL | 先确认报错路径来自哪个项目与配置；不要拿另一目录的旧文件冒充本次产物 |
| DLL/资源存在但启动失败 | 按[启动定位](./first-steps.md)检查实际加载位置、位数、配置和本次日志 |

## 验证范围

`test_verify_platform_policy.py` 检查平台声明与制品约定，不验证每台机器的 SDK、安装器或设备依赖。文档中的路径与项目声明不证明构建、启动或现场采集已执行；实际结果随任务报告。安装制品选择见[安装指南](./installation.md)，启动检查见[首次运行](./first-steps.md)，发布入口见[构建与发布脚本](../02-developer-guide/scripts/README.md)。
