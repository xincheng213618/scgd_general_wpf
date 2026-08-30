---
knowledge_id: "engine.native-integration"
knowledge_type: "guide"
status: "current"
summary: "说明 native ABI、HImage 所有权、首次 helper 构建与 CUDA 发布输入的验证边界。"
aliases: ["新克隆构建为什么需要C++","OpenCVMediaHelper","opencv_helper.dll","opencv_cuda.dll","M_FindLuminousAreaV2"]
code_paths: ["UI/ColorVision.Core/ColorVision.Core.csproj","UI/ColorVision.Core/OpenCVMediaHelper.cs","Native/opencv_helper/opencv_helper.vcxproj","Native/include/opencv_media_export.h","Engine/ColorVision.Engine/Media/CVRawOpen.cs"]
test_paths: ["Test/ColorVision.UI.Tests/LuminousAreaNativeInteropTests.cs","Scripts/tests/test_algorithm_package_contract.py","Test/opencv_helper_test"]
related: ["engine.index","ui.core","engine.native-bindings","engine.file-io"]
---

# OpenCV 和 native 集成开发指南

本页说明当前仓库里 OpenCV/native 能力的真实边界。Engine 侧有 `cvColorVision` 这种设备 SDK / 算法 DLL 绑定层，UI/Core 侧有 `opencv_helper.dll` / `opencv_cuda.dll` 的 P/Invoke 包装，文件打开链路还包含 `.cvraw` / `.cvcie` 解析和缩略图。

## 当前分层

| 层级 | 目录或文件 | 职责 |
| --- | --- | --- |
| 设备 SDK 绑定 | `Engine/cvColorVision/` | 相机、光谱仪、传感器、OLED 算法、MQTTMessageLib 数据类型和 native DLL 入口 |
| UI/Core native 包装 | `UI/ColorVision.Core/` | `HImage`、`OpenCVMediaHelper`、`OpenCVCuda`、`ImageCompute`、native 日志桥 |
| 文件解析和展示 | `Engine/ColorVision.Engine/Media/` | `.cvraw`、`.cvcie` 打开、缩略图、CIE 导出、鼠标探针和图像工具 |
| 测试工程 | `Test/opencv_helper_test/` | C++ 验证工程，覆盖经典 `M_FindLuminousArea` 和鲁棒 `M_FindLuminousAreaV2` |
| 文档入口 | [cvColorVision](../../04-api-reference/engine-components/cvColorVision.md)、[ColorVision.Core](../../04-api-reference/ui-components/ColorVision.Core.md) | 模块边界和 DLL 发布注意事项 |

## 首次拉仓与 native 构建前提

`UI/ColorVision.Core/ColorVision.Core.csproj` 按实际文件选择 `OpenCvHelperBinary`：解决方案构建使用 `$(SolutionDir)x64/Release/opencv_helper.dll`；单独构建优先 `Native/opencv_helper/x64/Release/opencv_helper.dll`，其次仓库 `x64/Release/opencv_helper.dll`。缺少 helper 时设置 `UseProjectReference=true`，加入 `Native/opencv_helper/opencv_helper.vcxproj`，并要求 Release/x64。

这两个 helper 候选不是随 Git 跟踪的预编译输入；干净拉仓不能假设只需 .NET SDK。需要可用的 Visual Studio C++ 工具链及项目声明的 OpenCV/SDK 依赖，在 Visual Studio Developer PowerShell 中完成相应 native 构建，再构建托管项目；以 `Native/AGENTS.md` 和 vcxproj 为准，不通过关闭引用或遗漏 DLL 来让构建表面通过。

CUDA 是另一条边界：当前 Core 无条件打包仓库跟踪的 `x64/Release/opencv_cuda.dll`。不执行 CUDA 路径不等于允许缺少这个构建/发布输入。

## 修改落点

| 需求 | 首选落点 |
| --- | --- |
| 新增相机、光谱仪、传感器 SDK 导出 | `Engine/cvColorVision/` 对应 wrapper |
| 新增图像处理函数给 WPF 调用 | `UI/ColorVision.Core/OpenCVMediaHelper.cs` 或 `OpenCVCuda.cs` |
| 新增 `.cvraw` / `.cvcie` 打开或缩略图行为 | `Engine/ColorVision.Engine/Media/` |
| 调整亮区、伪彩、SFR、白平衡等 helper 行为 | native `opencv_helper.dll` 和 `UI/ColorVision.Core` 签名一起核对 |
| 调整 CUDA 融合 | `opencv_cuda.dll`、`OpenCVCuda`、`ImageCompute` |
| 验证 native helper | `Test/opencv_helper_test/` |

## P/Invoke 维护规则

- C# 签名必须和 native 导出保持一致，包括 calling convention、字符串编码、结构体布局和内存释放方式。
- `HImage` 带 native buffer，调用失败时要释放已经分配的输出，避免内存泄漏。
- 返回 `IntPtr` 字符串的 helper 要确认是否需要调用 `FreeResult()`。
- `M_FindLuminousAreaV2` 返回正数只表示成功生成了 JSON；业务成功还必须检查 JSON `Success`。其 native 角点为 ROI 局部坐标，统一托管包装会转换为整图坐标。
- x64 是主交付目标，native DLL、测试工程和主程序平台要一致。
- `opencv_helper.dll`、`opencv_cuda.dll`、OpenCV runtime 和项目输出目录要一起验证。
- 不要把 `cvColorVision` 写成纯托管算法库，它主要是 native 能力绑定层和消息数据类型集合。

## 为什么仓库单独跟踪 `opencv_cuda.dll`

`x64/Release/opencv_cuda.dll` 是有意保留的第一方发布输入，不是误提交的普通构建产物。标准 GitHub Windows runner 不保证具备 `opencv_cuda.vcxproj` 所需的 CUDA 12.9 与受支持的 Visual Studio 集成，当前 `build.sln` 也不编译该 CUDA 项目。托管构建、NuGet 打包和本地发布因此从这个固定路径取得已审核的 DLL，避免普通构建环境必须安装 CUDA 工具链。

不要因为 `x64/` 已在忽略规则中就直接删除该文件。只有同时满足以下条件后，才能移除 Git 中的 DLL：

- GitHub Actions 能在干净的 `windows-2022` runner 上先安装精简 CUDA 组件并生成 DLL，再构建托管解决方案。
- Actions 只缓存生成的 DLL，并以 CUDA 源码、共享 ABI 头文件、CUDA/OpenCV 属性表、OpenCV 导入库和工具链版本共同生成缓存键。
- 缓存缺失或被清理时必须自动执行真实 CUDA 编译，缓存不能成为 DLL 的唯一来源。
- 本地 `Scripts/release.bat` 流程能够自行编译或获取同一制品，不再依赖仓库中的兜底文件。
- 新生成的 DLL 通过现有 ABI/打包检查，并在带 NVIDIA GPU 的环境完成烟雾测试。

这项决定最后复核于 2026-08-17。当前阶段即使远端增加了按需生成和 DLL 缓存，也应先保留仓库里的兜底文件；待远端至少稳定跑通并补齐本地发布链路后再删除。

## `.cvraw` / `.cvcie` 链路

| 入口 | 说明 |
| --- | --- |
| `FileCVCIE` | 读取 CIE/RAW 文件头和图像数据 |
| `CVRawOpen` | 在图像编辑器中打开 `.cvraw`，提供 CIE 探针和图形工具 |
| `CVRawThumbnailProvider` | 为 `.cvraw` / `.cvcie` 生成缩略图 |
| `ColorVision.ShellExtension` | Windows Explorer 缩略图扩展，独立打包和注册 |

修改文件格式时，要同时验证主程序打开、缩略图、导出、ShellExtension 和旧文件兼容。

## 验证命令

```powershell
dotnet build UI/ColorVision.Core/ColorVision.Core.csproj -c Release -p:Platform=x64
dotnet build Engine/cvColorVision/cvColorVision.csproj -c Release -p:Platform=x64
msbuild .\Test\opencv_helper_test\opencv_helper_test.vcxproj /m:1 /nodeReuse:false /p:Configuration=Debug /p:Platform=x64
.\Test\opencv_helper_test\build_test_find_luminous.bat
```

如果当前机器没有 Visual Studio C++ 或 OpenCV native 依赖，至少要记录无法执行的原因，并在验证记录里说明由哪台构建机补验。

## 验收清单

| 项目 | 验收方式 |
| --- | --- |
| P/Invoke 签名 | Debug/Release x64 都能加载 DLL，没有 `BadImageFormatException` 或入口点缺失 |
| 内存 | 连续处理多张图，进程内存不会持续单向增长 |
| 图像结果 | 输出尺寸、通道、位深、stride 和颜色顺序正确 |
| 文件打开 | `.cvraw` / `.cvcie` 能打开、缩略图能生成、旧文件不崩溃 |
| 算法 helper | `M_FindLuminousArea` / `M_FindLuminousAreaV2` 的 native 测试、托管解析和真实 DLL 联调通过；错误码、拒绝原因和结果 JSON 可解释 |
| 打包 | 主程序、插件或项目包输出里包含需要的 native DLL 和 runtime |

## 相关文档

- [cvColorVision](../../04-api-reference/engine-components/cvColorVision.md)
- [ColorVision.Core](../../04-api-reference/ui-components/ColorVision.Core.md)
- [ColorVision.ShellExtension](../../04-api-reference/engine-components/ColorVision.ShellExtension.md)
- [测试与验证](../testing.md)

## 验证入口与缺口

关联测试：`Test/ColorVision.UI.Tests/LuminousAreaNativeInteropTests.cs`、`Scripts/tests/test_algorithm_package_contract.py`、`Test/opencv_helper_test`。

真正的 native 与 clean package 检查需要 Visual Studio C++ 工具链和真实 DLL；静态文档检查不能证明 ABI、CUDA/GPU 或设备 SDK 运行成功。
