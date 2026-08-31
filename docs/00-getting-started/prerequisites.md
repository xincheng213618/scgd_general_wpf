---
knowledge_id: "delivery.prerequisites"
knowledge_type: "guide"
status: "current"
summary: "首次构建所需Windows x64、.NET与C++工具链，区分已有native DLL与干净克隆。"
aliases: ["环境要求","干净克隆","只装.NET SDK能编译吗","opencv_helper.dll","MSBuild"]
code_paths: ["Directory.Build.props","UI/ColorVision.Core/ColorVision.Core.csproj","Native/opencv_helper/opencv_helper.vcxproj","Native/AGENTS.md"]
test_paths: ["Scripts/tests/test_verify_platform_policy.py"]
related: ["delivery.start","delivery.testing"]
---

# 系统要求

先区分任务：读取代码和知识不需要运行软件；构建需要工具链；启动主程序可能连接已有配置中的服务；真实设备验证还需要驱动、样本和明确授权。

## 任务与前提

| 任务 | 必需条件 | 不要混入的动作 |
| --- | --- | --- |
| 只读源码问答 | 当前检出的源码、`AGENTS.md` 和知识目录；Node查询工具可选 | 不必构建，不必装设备驱动，不运行发布脚本 |
| 生成/校验知识 | Node.js；无需安装站点依赖 | 不上传、不连接设备 |
| 构建网页 | Node.js 22+，仓库根目录 `npm ci` | 站点依赖要求22；独立知识CLI仍可使用Node20；不等于发布网页 |
| 托管项目构建/测试 | Windows、匹配项目目标框架的.NET SDK、匹配的native依赖 | 不把本机已有DLL视为新克隆自带 |
| 首次native构建 | 支持项目toolset的Visual Studio C++/MSBuild、Windows SDK及项目要求的OpenCV/CUDA依赖 | 不用普通dotnet MSBuild代替C++工具链 |
| 启动与设备验收 | 匹配的运行时DLL、配置、服务/设备驱动、现场安全条件 | 不把构建通过视为可以控制硬件 |

## 运行环境要求

### 操作系统

- Windows 10 或 Windows 11
- x64 系统环境
- 首次安装、升级或涉及系统服务配置时，建议使用具有管理员权限的账户

### 硬件与显示

- 建议使用 1920x1080 及以上分辨率
- 建议为图像处理和流程执行预留充足内存与磁盘空间
- 若需要连接相机、光谱仪、电机等设备，应提前确认对应驱动和通信环境已经准备好

### 网络与权限

- 首次安装后若需要检查更新、访问插件市场或连接远程服务，请保证网络可用
- 如果部署环境限制较严，请提前确认程序目录、日志目录和用户文档目录具有可写权限

## 从安装包运行时需要注意什么

- 安装程序会按自身流程检查并部署所需组件；如果提示缺少先决条件，请按安装器提示完成补齐
- 某些服务管理、设备驱动配置或系统级写入操作，可能需要管理员权限
- 若环境中存在旧版本，建议先完成升级或卸载，再做新安装

## 从源码构建时需要准备什么

当前仓库以 Windows WPF 和 x64 为主，准备以下环境并核对项目文件：

- .NET 10 SDK
- 能构建当前 `.csproj` / `.vcxproj` 目标的 Visual Studio / MSBuild 与 C++工作负载；版本和toolset以项目文件为准
- Git 与 PowerShell（用于获取仓库和执行脚本）

先执行只读检查，确认环境分支：

```powershell
dotnet --info
Get-Command msbuild -ErrorAction SilentlyContinue
Test-Path .\Native\opencv_helper\x64\Release\opencv_helper.dll
Test-Path .\x64\Release\opencv_helper.dll
Test-Path .\ColorVision.snk
```

`UI/ColorVision.Core/ColorVision.Core.csproj` 根据 `OpenCvHelperBinary` 的存在性决定是否加入 `Native/opencv_helper/opencv_helper.vcxproj` 项目引用。单项目构建优先复用native项目输出，solution构建使用solution输出。两个候选DLL都不存在时，只有.NET SDK并不能覆盖C++构建；不要通过强制禁用项目引用或签名来绕开依赖。

已有匹配native依赖时，使用 x64 平台做本地托管构建：

```powershell
dotnet restore .\ColorVision\ColorVision.csproj
dotnet build .\ColorVision\ColorVision.csproj -p:Platform=x64
```

首次构建或全solution验证在 Visual Studio Developer PowerShell 中执行；这些命令生成本地制品，不发布：

```powershell
dotnet restore .\build.sln
msbuild .\build.sln /m:1 /nodeReuse:false /p:Configuration=Release /p:Platform=x64
```

`Native/AGENTS.md`、`Native/opencv_helper/opencv_helper.vcxproj` 及其导入文件定义native工具链和ABI边界。缺失专有SDK、CUDA或厂商DLL时报告具体缺项；文档不能保证每台机器在没有这些依赖时可构建。

## 运行时依赖说明（源码场景）

如果你是从源码直接运行，而不是通过安装包部署，还需要注意运行输出中应包含以下依赖：

- OpenCvSharp Windows 运行时
- `DLL/CVCommCore.dll`
- `DLL/MQTTMessageLib.dll`
- 主程序需要的 `log4net.config` 与相关资源文件

这些内容通常由项目引用和复制规则处理；如果程序可以构建但启动失败，优先检查输出目录是否缺少上述依赖。

## 这页不讲什么

- 安装步骤本身请看 [安装指南](./installation.md)
- 启动副作用、最小运行验证和失败分流请看 [首次运行](./first-steps.md)
- 想理解系统模块和设计边界，请转到 [架构设计](../03-architecture/README.md)
