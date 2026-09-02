---
knowledge_id: "delivery.native-testing"
knowledge_type: "guide"
status: "current"
summary: "opencv_helper_test 的实际入口、工具集与配置映射、专项参数、DLL/样本前提和退出码边界；默认运行与真实样本验收不同。"
aliases: ["原生测试", "原生调试", "opencv_helper_test", "BUILD_AND_DEBUG_GUIDE", "build_test_find_luminous.bat", "v145", "--luminous-v2-only", "--find-cross-cvraw", "--calibration-smoke", "--calibration-cache-small-budget", "--cuda-fusion-benchmark", "--cuda-fusion-verify", "--cuda-fusion-compare", "--cuda-fusion-ab-benchmark", "--find-cross-only", "--luminous-v2-cvraw", "--luminous-v2-cvraw-reject", "--p2-only", "--native-log", "--pseudo-color", "--calibration-real-data", "--calibration-legacy-color", "--surface-defect-equivalence", "--surface-defect-benchmark", "COLORVISION_POI_BATCH_BASELINE_DLL", "COLORVISION_CALIBRATION_TEST_THREADS", "All tests completed", "LNK2019", "MSB8020"]
code_paths: ["Test/opencv_helper_test/BUILD_AND_DEBUG_GUIDE.md", "Test/opencv_helper_test/opencv_helper_test.vcxproj", "Test/opencv_helper_test/test_find_luminous_area.cpp", "Test/opencv_helper_test/build_test_find_luminous.bat", "Test/opencv_helper_test/test_cuda_fusion.cpp", "Test/opencv_helper_test/test_calibration.cpp", "Test/opencv_helper_test/test_find_cross.cpp", "Native/opencv_helper/opencv_helper.vcxproj", "packages/OpenCV.Debug.x64.props", "packages/OpenCV.Release.x64.props", "packages/NativeCopy.targets", "scgd_general_wpf.sln", "build.sln"]
test_paths: ["Test/opencv_helper_test/test_find_luminous_area.cpp", "Test/opencv_helper_test/test_calibration.cpp", "Test/opencv_helper_test/test_cuda_fusion.cpp", "Test/opencv_helper_test/test_find_cross.cpp", "Test/opencv_helper_test/test_p2_algorithms.cpp", "Test/opencv_helper_test/test_native_logging.cpp", "Test/opencv_helper_test/test_pseudo_color.cpp", "Test/opencv_helper_test/data/sfrmat5"]
related: ["delivery.testing", "delivery.prerequisites", "engine.native-integration", "engine.opencv-helper-api", "algorithms.find-light-area", "ui.image-fusion"]
---

# 原生 helper 测试与调试

`Test/opencv_helper_test/` 是 `opencv_helper.dll` 的 C++ 控制台验证程序，覆盖图像缓冲、检测、校准/POI、SFR、P2、视频和日志等入口，也提供按参数选择的专项测试。它与托管 P/Invoke 测试、真实设备验收分别验证不同边界。

本页说明如何构建和选择已有测试。API 参数、结果结构与内存释放见 [opencv_helper API 参考](../../04-api-reference/engine-components/opencv-helper-api.md)，不在测试指南重复一套函数错误码。

## 构建前提与配置选择

在 Windows x64 的 Visual Studio Developer PowerShell 中，从仓库根目录工作。读取项目和配置不需要启动测试；构建写本地产物，helper 的 `NativeCopy.targets` 还会把 DLL/OpenCV runtime 复制到主程序对应配置的 `runtimes/win-x64/native` 输出。确认该输出没有被正在运行的应用占用。

| 输入 | 当前项目定义 |
| --- | --- |
| 测试项目 | `opencv_helper_test.vcxproj` 使用 `PlatformToolset=v145`、Windows SDK `10.0`、C++17 |
| helper 项目 | `Native/opencv_helper/opencv_helper.vcxproj` 使用 `PlatformToolset=v143` |
| OpenCV | x64 属性表使用 `packages/opencv/x64/vc18/bin`、`lib` 和版本后缀 `4140`；Debug 库/DLL 另带 `d` 后缀 |
| JSON 头文件 | 测试项目导入 `packages/nlohmann.props` |
| 编译入口 | x64 下的 `main` 位于 `test_find_luminous_area.cpp`；`opencv_helper_test.cpp` 被排除，已有专项文件分别参与编译 |

其它编译依赖以 helper 导入的属性表为准，包括其 OpenCV、JSON、日志和算法头文件依赖。需要能提供两个项目所要求工具集的构建环境。不能仅凭“装了 Visual Studio 2022”判断全部前提满足，也不要为消除 `MSB8020` 临时把项目降到另一工具集。实际库文件、工具集和 ABI 是否配套仍须编译及运行确认。

### 解决方案与直接项目构建

| 入口 / 配置 | helper | 测试程序 |
| --- | --- | --- |
| `scgd_general_wpf.sln` / Debug x64 | Release x64 | Debug x64 |
| `scgd_general_wpf.sln` / Release x64 | Release x64 | Release x64 |
| `build.sln` / Debug 或 Release x64 | 与所选配置相同 | 不包含此测试项目 |
| 直接构建测试 `.vcxproj` | ProjectReference 使用所选项目配置 | 与所选配置相同 |

完整解决方案 Debug 构建不是 Debug helper 调试环境。测试项目自己的 Debug 属性表还引用 Debug OpenCV；判断实际加载的 DLL 时须同时看项目引用、输出路径和调试器模块列表，不能用解决方案下拉框推断所有 DLL 的配置。

常规原生回归优先使用 Release x64。以下仅构建测试项目及其 helper 引用，显式给出仓库和共享输出目录，避免直接构建时依赖缺失的 `SolutionDir`：

```powershell
$nativeRepoRoot = (Resolve-Path -LiteralPath .).Path
$nativeOutput = Join-Path $nativeRepoRoot 'x64\Release'
msbuild .\Test\opencv_helper_test\opencv_helper_test.vcxproj /m:1 /nodeReuse:false /p:Configuration=Release /p:Platform=x64 "/p:SolutionDir=$nativeRepoRoot\" "/p:OutDir=$nativeOutput\"
if ($LASTEXITCODE -ne 0) { throw '原生测试项目构建失败，先检查构建输出。' }
```

需要逐步调试 Debug helper 时，对直接项目构建使用 `Configuration=Debug` 和 `x64/Debug` 输出；不要修改完整解决方案的映射。Release 项目也配置了调试信息，但优化会影响断点、变量和单步顺序，不能保证与 Debug 相同。

## 运行已有测试

运行前准备测试 exe、同一构建的 helper DLL、OpenCV 及相应 VC++ runtime。可在当前 PowerShell 会话临时提供 OpenCV 搜索路径，不必修改系统 PATH：

```powershell
$nativeTestExe = Join-Path $nativeOutput 'opencv_helper_test.exe'
$nativeOpenCvBin = Join-Path $nativeRepoRoot 'packages\opencv\x64\vc18\bin'
$env:PATH = "$nativeOpenCvBin;$env:PATH"
& $nativeTestExe --luminous-v2-only
$nativeTestExit = $LASTEXITCODE
if ($nativeTestExit -ne 0) { throw "亮区专项测试失败，退出码 $nativeTestExit" }
```

选择与改动对应的模式，不用默认运行替代每个专项。测试可能写入并清理临时图像/校准文件，部分路径固定，同一临时目录下不要并发运行多份；基线 DLL 模式会加载并执行指定 DLL，CUDA 模式会调用 GPU，真实样本模式会读取所选数据。执行范围应与任务授权一致。

### 合成与专项回归

| 参数 | 执行范围 / 前提 |
| --- | --- |
| `--luminous-v2-only` | 本地亮区 V2 合成回归 |
| `--find-cross-only` | FindCross 合成图形、质量门禁和失败契约 |
| `--p2-only` | P2 本地算法回归 |
| `--native-log` | 原生日志桥接测试 |
| `--pseudo-color` | 伪彩回归 |
| `--calibration-smoke` | 校准 Context、合成校准和 POI V2；设置 `COLORVISION_POI_BATCH_BASELINE_DLL` 时还执行该 DLL 的基线比较 |
| `--calibration-cache-small-budget` | 新测试进程须在启动前设置 `COLORVISION_CALIBRATION_CACHE_MB=1`，检查小预算缓存；该环境变量影响进程级缓存初始化 |
| `--surface-defect-equivalence` | 表面缺陷实现等价性回归 |
| `--surface-defect-benchmark` | 表面缺陷基准模式；性能输出只适用于该输入和运行环境 |

以上布尔结果的专项分支通常成功返回 0、失败返回 1；异常退出和进程加载失败仍须看实际输出。主入口按精确参数数量匹配，没有通用 `--help` / 未知参数拒绝层。拼错选项或多传参数可能落入默认回归，再把第一个参数当普通图片路径，不能据此判定预期专项已执行。

### 文件样本与显式预期

| 参数形式 | 完成条件 |
| --- | --- |
| `--luminous-v2-cvraw <file>` | 读取 16 位 cvraw，检查 V2 `Success=true` 且四角点数量为 4；不是逐点精度断言 |
| `--luminous-v2-cvraw-reject <file>` | 要求 `Success=false`、角点为空且有失败原因；退出 0 表示拒绝契约通过 |
| `--find-cross-cvraw <file>` | 检查结果结构与通用范围；没有标注预期时，结构正确的算法拒绝也可返回 0 |
| `--find-cross-cvraw <file> x y width height` | 在给定 ROI 内检查相同契约 |
| `--find-cross-cvraw <file> x y width height expectedCenterX expectedCenterY expectedRotation centerTolerance rotationTolerance minConfidence` | 必须检测成功，并满足中心距离、旋转差和置信度阈值；中心/旋转容差须非负，置信度阈值在 0..1 |
| `--calibration-real-data <root>` | 使用 `test_calibration.cpp` 指定的目录、文件名和哈希预期，不能换成任意样本目录；可用 `COLORVISION_CALIBRATION_TEST_THREADS` 设定 OpenCV 线程数 |
| `--calibration-legacy-color <raw> <color-file> <legacy-dll>` | 比较三通道 RAW 的旧校准/POI 行为，需要匹配的校准文件和可信 legacy DLL |

FindCross 的用法/读文件/参数错误返回 2，结果断言失败返回 1；无标注样本退出 0 不证明目标一定被找到。Luminous 的文件模式使用布尔结果返回 0/1，必须同时确认选的是成功模式还是拒绝模式。

### CUDA 比较与性能

这些模式会加载参数指定的 native DLL，并创建本地图像夹具。须先确认 GPU、驱动和 DLL 依赖；支持的图像数量及当前 CUDA 限制见[景深融合](../../04-api-reference/ui-components/image-fusion.md)。

| 参数形式 | 用途 |
| --- | --- |
| `--cuda-fusion-verify <candidate-dll>` | 候选 DLL 的失败/边界行为检查 |
| `--cuda-fusion-compare <reference-dll> <candidate-dll> <width> <height> <images> [channels]` | 基线和候选结果比较；channels 缺省为 3，夹具接受 1 或 3 |
| `--cuda-fusion-benchmark <dll> <width> <height> <images> <warm-iterations> [prewarm] [function]` | 单 DLL 基准，迭代数须为正；只有对应位置的字面值 `prewarm` 才启用预热，function 缺省 `CM_Fusion` |
| `--cuda-fusion-ab-benchmark <reference-dll> <candidate-dll> <width> <height> <images> <iterations>` | 两个 DLL 的 A/B 基准 |

CUDA 分派的未知模式返回 2，捕获到标准异常时返回 1；各模式的参数和结果断言由 `test_cuda_fusion.cpp` 负责。记录 GPU、尺寸、图像数量、通道、DLL、预热与迭代条件，不能用某次基准代替现场吞吐或稳定性验收。

## 默认运行与输出判断

无参数运行会依次执行多项布尔回归，遇到 false 返回 1，随后输出几个旧亮区演示。它包含比名字所示更多的校准、SFR、P2、视频及资源检查，不能当成仅一次亮区计算。

- SFR 夹具位于 `Test/opencv_helper_test/data/sfrmat5`。查找函数从当前工作目录向上搜索，并尝试各层的测试目录；保持仓库根目录作为工作目录有助于定位，不能只复制 exe 就保证夹具可用。
- 桌面 DistortionP9 夹具缺失时跳过；`opencv_cuda.dll` 无法加载时，默认 CUDA batch 失败清理测试也记录跳过并返回成功。
- 可选普通图片参数会先跑默认回归，再进入 `testWithRealImage`。该演示用 `imread(..., IMREAD_UNCHANGED)`，不能解码时仅提示并跳过；算法失败也不统一转为非零退出。它适合人工看输出，不是可靠的图片批量验收入口。
- `All tests completed!` 只说明执行到了 main 末尾。记录时保留完整命令、退出码、实际 DLL/配置和跳过信息；API 返回正 JSON 长度与进程退出 0 是两种不同结果。

PNG/JPEG/TIFF/BMP 是否可读取由所链接的 OpenCV 编码器和实际输入决定。需要自动批量验收时，在现有测试分派中编写会累计失败并返回非零的用例，不能仅循环调用返回 `void` 的图片演示。

## 断点、扩展与故障处理

在 Visual Studio 选择 `opencv_helper_test` 为启动项目，填写相同命令参数，在 `test_find_luminous_area.cpp` 或其调用的专项函数设置断点。通过“模块”窗口确认 helper 实际路径、配置和对应 PDB，再使用 F10/F11/Shift+F11；不要通过再定义一个 main、包含另一个 `.cpp` 或把源文件名填进链接器 EntryPoint 来选择专项。

新增用例应放入相应测试文件，使用显式结果检查并由现有 main / 专项 runner 将失败传回退出码；Release 的 `NDEBUG` 会禁用普通 C `assert`，不能只依赖它。`createHImageFromMat` 借用 Mat 的像素，调用期间保活 Mat；native 输出按 API 的匹配函数释放，包括失败路径，JSON 解析失败也要释放。不要修改生产接口来适配测试快捷入口。

| 现象 | 检查顺序 |
| --- | --- |
| 找不到 `cl` / `msbuild` 或 `MSB8020` | 确认 Developer PowerShell、两个项目实际 toolset 和 Windows SDK；记录缺项，不随意改 toolset |
| `LNK2019` 或找不到 import library | 核对 ProjectReference、helper 实际生成配置、OutDir/LibraryPath，以及 Debug/Release OpenCV `.lib`；不能固定假设都在 Release |
| 找不到 helper / OpenCV DLL | 核对 exe 邻近 DLL、当前进程 PATH、`vc18/bin` 中对应后缀的 runtime 及其传递依赖；托管 `OpenCvSharp` 包不是这些 C++ import library 的替代 |
| DLL 已找到但断点不命中 | 看模块路径和符号加载状态；源码、DLL、PDB 必须属于同一构建，Release 优化也影响单步 |
| 退出 0 但真实图像未检测 | 检查是否进入正确专项、是否跳过样本、是否仅运行 void 演示，以及是否给了明确成功/拒绝/几何预期 |

`build_test_find_luminous.bat` 是仍在源码中的单文件编译脚本：它引用旧 `opencv_world4100d.lib` 和目录，只编译一个 `.cpp`，没有当前 main 所需的其它专项对象；它还会删除当前目录同名 exe/obj。该脚本不是当前测试构建入口，应使用项目文件，不能把脚本提示当作现有依赖清单。

本页核对的是项目定义、分派和检查范围。原生编译、DLL 装载、算法回归、GPU 测量和真图验收分别需要实际执行证据；文档站构建通过不证明这些步骤已经完成。
