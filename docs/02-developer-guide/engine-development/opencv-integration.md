---
knowledge_id: "engine.native-integration"
knowledge_type: "guide"
status: "current"
summary: "native ABI与HImage所有权、函数族返回值、视频异步/关闭边界，以及helper构建和CUDA发布输入；路由校准Context与POI原生参考。"
aliases: ["新克隆构建为什么需要C++","OpenCVMediaHelper","opencv_helper.dll","opencv_cuda.dll","HImage","isDispose","HImage.Dispose","M_FindLuminousAreaV2","M_CalibrationExecuteToV1","M_CalibrationGetLastError","M_CalibrationCacheReleaseV1","M_CalculatePoiBatchV2","M_VideoSeek","M_VideoPlay","M_VideoClose","opencv_helper API"]
code_paths: ["Native/README.md","Native/opencv_helper/API_Documentation.md","UI/ColorVision.Core/ColorVision.Core.csproj","UI/ColorVision.Core/OpenCVMediaHelper.cs","UI/ColorVision.Core/OpenCVCuda.cs","UI/ColorVision.Core/HImage.cs","Native/opencv_helper/opencv_helper.vcxproj","Native/include/opencv_media_export.h","Native/include/custom_structs.h","Native/include/video_export.h","Native/include/cuda_export.h","Native/opencv_helper/opencv_media_export.cpp","Native/opencv_helper/video_export.cpp","Native/opencv_helper/exports/calibration_export.cpp","Native/opencv_helper/exports/poi_export.cpp","Native/opencv_helper/exports/sfr_export.cpp","Native/opencv_cuda/opencv_cuda.vcxproj","Scripts/verify_native_contracts.py","Engine/ColorVision.Engine/Media/CVRawOpen.cs"]
test_paths: ["Test/ColorVision.UI.Tests/LuminousAreaNativeInteropTests.cs","Scripts/tests/test_algorithm_package_contract.py","Scripts/tests/test_verify_native_contracts.py","Test/opencv_helper_test"]
related: ["engine.index","ui.core","engine.native-bindings","engine.file-io","algorithms.local-native-analysis"]
---

# OpenCV 和 native 集成开发指南

本页说明当前仓库里 OpenCV/native 能力的真实边界。Engine 侧有 `cvColorVision` 这种设备 SDK / 算法 DLL 绑定层，UI/Core 侧有 `opencv_helper.dll` / `opencv_cuda.dll` 的 P/Invoke 包装，文件打开链路还包含 `.cvraw` / `.cvcie` 解析和缩略图。

`Native/README.md` 是可独立阅读的英文目录/构建风险入口；`Native/opencv_helper/API_Documentation.md` 保留英文函数参考，以及校准 Context/共享缓存、POI V2、原始焦点评分等独有细节，不是自动生成的完整导出清单。签名以对应头文件、实现和托管声明三者核对为准。ImageEditor 的 P2/FindLightBeads 调用与显示生命周期见[直接 native 分析](../../04-api-reference/algorithms/local-native-analysis.md)，不在本页复制整套算法契约。

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
- `HImage` 不自动说明拥有像素：须区分拥有与借用，调用失败时也清理已分配的拥有型输出。
- 返回 `IntPtr` 字符串的 helper 要确认是否需要调用 `FreeResult()`。
- `M_FindLuminousAreaV2` 返回正数只表示成功生成了 JSON；业务成功还必须检查 JSON `Success`。其 native 角点为 ROI 局部坐标，统一托管包装会转换为整图坐标。
- x64 是主交付目标，native DLL、测试工程和主程序平台要一致。
- `opencv_helper.dll`、`opencv_cuda.dll`、OpenCV runtime 和项目输出目录要一起验证。
- 不要把 `cvColorVision` 写成纯托管算法库，它主要是 native 能力绑定层和消息数据类型集合。

### HImage 布局和释放

当前 `Native/include/custom_structs.h` 使用 `pack(push, 8)`，`UI/ColorVision.Core/HImage.cs` 使用 `Pack = 8` 和 `[MarshalAs(UnmanagedType.I1)]`。x64 结构大小32字节，`isDispose` 偏移20、`pData` 偏移24；原生静态断言覆盖标准布局、1字节bool和各字段偏移。不要从旧示例恢复 `Pack=1` 或省略bool封送约束。

名字 `isDispose` 容易误读：`Dispose()` 仅在指针非空且 `isDispose=false` 时调用 `FreeCoTaskMem`；true表示此处不释放的借用缓冲，原持有者须保证调用期间有效。结构复制不会复制像素或创建第二份所有权，不能对两个拥有型副本各释放一次。输出从分配到拷贝/消费完成应放在 `try/finally` 生命周期内，既不能只在成功分支释放，也不能在 `Dispose()` 后再次手工释放同一指针。

### 返回值必须按函数族解释

| 入口族 | 判据与源码 |
| --- | --- |
| 常见图像处理 | 多数以0成功；`opencv_media_export.cpp` 的 `GuardIntExportImpl` / `GuardHImageExportImpl` 传播函数结果，并将JSON/OpenCV/标准/未知异常映射到−4/−5/−6/−7，不能只处理−1 |
| 校准 Context | `exports/calibration_export.cpp` 的变更/执行用 `M_CALIBRATION_OK=1`；`M_CalibrationGetLastError` 返回含终止符的所需UTF-8字节数，`M_CalibrationGetItemCount` 返回数量，不能统一按1判断全部查询 |
| POI batch | `exports/poi_export.cpp` 用 `M_POI_OK=1`；`M_CalculatePoiBatchV2` 在借用CIE输入上计算并写调用者输出数组，不转交整图内存 |
| JSON检测 / 焦点评分 | 正JSON长度只证明分配了结果，还须解析业务成功/拒绝；焦点返回原始评分，不能套0成功或归一化阈值 |
| 视频 | open返回正handle，position返回帧号；手动read透传 `MatToHImage` 转换/分配失败，−3既可表示已到末尾也可为分配失败；视频guard又将OpenCV/标准/未知异常映射到−2/−3/−4，代码不能唯一说明失败原因 |
| SFR | `exports/sfr_export.cpp` 使用自身参数/图像/计算及异常码，不套common export表 |

校准的只读RAW执行、共享缓存释放和调用者临界区，以及POI V2过滤/回退开关的完整说明保留在上述英文API参考；入口分别是 `M_CalibrationExecuteToV1`、`M_CalibrationCacheReleaseV1` 和 `M_CalculatePoiBatchV2`。缓存释放不等于磁盘文件删除或活动Context失效，不能借文档检查调用这些有状态入口。

### 视频执行与回调生命周期

`Native/include/video_export.h` 与 `Native/opencv_helper/video_export.cpp` 是视频入口。`M_VideoSeek` 返回0只表示已写入seek请求，实际解码/显示尚未完成；播放采用可覆盖的latest-frame单槽，不保证逐帧交付。帧回调运行在native工作线程，暂停也不等待已经在执行的回调返回。

`InvokeFrame` 传入的 `HImage*` 指向本次调用的栈变量，只能在回调内借用；其中由 `MatToHImage` 分配的 `pData` 所有权则交给接收方，须消费后释放一次或明确移交。保活托管delegate和userData，并将UI更新调度与buffer生命周期分别处理，不能保存栈指针等待UI线程稍后解引用。

调用者须串行化同一handle的关闭与其它操作。当前Close先移除handle、清除回调，再停止worker；从当前worker回调内部关闭时，`StopVideoWorkers` 对本线程detach而不是join，返回不等于当前回调结束。此外Close的 `cap.release()` 没有取得手动read使用的 `capMutex`，已取得Context的并发调用也不会因handle移除而自动消失。这是当前未修复的并发生命周期限制，不是“内部有mutex所以全部线程安全”，也不构成真实并发测试已通过的声明。

## 为什么仓库单独跟踪 `opencv_cuda.dll`

`x64/Release/opencv_cuda.dll` 是有意保留的第一方发布输入，不是误提交的普通构建产物。标准 GitHub Windows runner 不保证具备 `opencv_cuda.vcxproj` 所需的 CUDA 12.9 与受支持的 Visual Studio 集成，当前 `build.sln` 也不编译该 CUDA 项目。托管构建、NuGet 打包和本地发布因此从这个固定路径取得已审核的 DLL，避免普通构建环境必须安装 CUDA 工具链。

不要因为 `x64/` 已在忽略规则中就直接删除该文件。只有同时满足以下条件后，才能移除 Git 中的 DLL：

- GitHub Actions 能在干净的 `windows-2022` runner 上先安装精简 CUDA 组件并生成 DLL，再构建托管解决方案。
- Actions 只缓存生成的 DLL，并以 CUDA 源码、共享 ABI 头文件、CUDA/OpenCV 属性表、OpenCV 导入库和工具链版本共同生成缓存键。
- 缓存缺失或被清理时必须自动执行真实 CUDA 编译，缓存不能成为 DLL 的唯一来源。
- 本地 `Scripts/release.bat` 流程能够自行编译或获取同一制品，不再依赖仓库中的兜底文件。
- 新生成的 DLL 通过现有 ABI/打包检查，并在带 NVIDIA GPU 的环境完成烟雾测试。

这项决定最后复核于 2026-08-17。当前阶段即使远端增加了按需生成和 DLL 缓存，也应先保留仓库里的兜底文件；待远端至少稳定跑通并补齐本地发布链路后再删除。

### CUDA 检查器证明什么

`Scripts/verify_native_contracts.py` 将CUDA头文件/托管声明的返回值、参数、调用约定、DLL名称、HImage AMD64布局和native日志delegate，与被审核的跟踪DLL及其传播字节一起核对。CUDA字符串导入要求显式 `CharSet.Ansi`；可能改变P/Invoke默认值的module级属性会被拒绝。导出分支须为 `__declspec(dllexport)`，Release/x64须定义 `OPENCVCUDA_EXPORTS`。

默认模式在Visual Studio环境中求值真实Release/x64的 `ClCompile` / `CudaCompile` 项及CUDA host定义：创建唯一临时project/probe和隔离输出目录，在消费边界捕获项并于编译器运行前移除，结束后清理临时文件。不运行cl/nvcc、不加载DLL、不需要GPU，但缺少匹配CUDA BuildCustomizations时仍失败。它会创建临时文件、运行MSBuild求值，不是仅阅读Markdown。

普通Windows CI使用 `--static-native-project-only`：仍核对源码、项目XML、PE导出和包传播字节，但明确未验证求值后的MSBuild元数据，不能替代默认发布环境门禁。PE导出表不包含C参数类型；严格源码契约加字节一致性也不能证明任意替换DLL确由当前源码编译，新DLL仍须真实CUDA构建与ABI/GPU烟雾验收。

`Scripts/tests/test_verify_native_contracts.py` 提供契约变异用例，`Scripts/tests/test_algorithm_package_contract.py` 覆盖打包相关契约；发布链另核对运行目录及完整更新ZIP中的CUDA字节，包含 `create_full_zip` 回归。测试文件存在或文档构建成功不代表本次已执行这些门禁。

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
