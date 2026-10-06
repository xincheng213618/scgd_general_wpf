---
knowledge_id: "engine.native-bindings"
knowledge_type: "reference"
status: "current"
summary: "定位供应商 native DLL 的相机、光谱、PG 与源表绑定契约，以及内部版的交付边界。"
aliases: ["设备SDK入口在哪里","cvColorVision","cvCameraCSLib","CVCommCore","MQTTMessageLib","CVCommCore.dll","MQTTMessageLib.dll"]
code_paths: ["Engine/cvColorVision/README.md","Engine/cvColorVision/Camera","Engine/cvColorVision/CVCommCore","Engine/cvColorVision/MQTTMessageLib","Engine/cvColorVision/Devices/Spectrometer/Spectrometer.cs","Engine/cvColorVision/cvColorVision.csproj"]
test_paths: []
related: ["engine.index","engine.native-integration","ui.core"]
---

# cvColorVision

`Engine/cvColorVision/` 是原生能力绑定层，通过 `DllImport` 暴露 `cvCamera.dll` 等底层接口给 C#。它不是纯托管视觉算法库，也不负责 WPF 界面、模板或工作流编排。

本地相机取图由 `CM_GetFrame` 提供 RAW，校正由 `LocalFrameCalibrationService` 调用本地 `opencv_helper.dll`；POI 的 XYZ、xy/uv、CCT 和主波长测量由 `PoiMeasurementService` 通过 `OpenCVCalibration.M_CalculatePoiBatchV2` 或 RAW 测量入口计算，缓冲寿命由当前帧或 `PoiMeasurementBuffer` 管理。旧插件若引用早期 `cvColorVision.dll` 的句柄式 XYZ 采样 API，需要迁移到本地测量接口并重新编译；当前托管程序集不保证这些旧 API 的二进制兼容。

托管绑定不再提供旧 `FovImgCentre` / `FovImgCentreEX`、`SFRCalculation`、`GhostGlareDectect` 和 `cvCalArticulation` 入口，图像测量使用现有本地算法。对应原生维护源码已删除旧 FOV、SFR、畸变、鬼影入口及清晰度导出包装；自动对焦仍需要的公共计算实现保留。Simple 源码保留用于同步上游，但不参与当前 DLL 构建，其旧算法调用迁移前不能重新启用。原生构建与替换交付 DLL 是两个步骤，源码清理不代表已发布；更新 DLL 时须使用匹配版本，不能再依赖这些退役导出或原有导出序号。

## 绑定与交付前提

当前工程目标是 `net10.0-windows7.0`。`cvColorVision.csproj` 从 `DLL/scgd_internal_dll/` 复制和打包供应商 DLL 及配置，不在本工程编译这些 native 实现。部分输入显式指定 `runtimes/win-x64/native` 包路径，其他设备 DLL 和配置按各自的 Pack/Copy 元数据处理，不能假设所有资产都位于同一目录。除 `cvCamera.dll` 外，CommLibrary、OpenCV 和设备 SDK 相关 DLL 也在清单中；具体功能还受设备驱动与部署配置约束。校正文件创建由主程序执行，校正运行库为 `opencv_helper.dll` 和兼容后端 `cvCamera.dll`。

当前内部版不包含本地 OLED/CUDA 链路：`cvCamera.dll` 不再导入 `cvOled.dll`，默认交付不含 `cvoled.dll`、`cudart64_12.dll`，C# 的 `CvOledDLL` 及其专用枚举也不再提供。原生导出 `CM_LedCalInit`、`CM_LedCalFind`、`CM_LedCalComBine`、`CM_LedCalFindHighDensity`、`CM_FindHighIndensityLed` 已删除；需要这些入口的旧插件不能直接使用此版本，按序号导入的外部二进制也需要重新核对。本地普通 LED 检测、远端 CVOLED 服务消息和流程节点不属于这条 DLL 依赖链。

OLED/CUDA 备份位于仓库 `docs/_history/native-dependencies/oled-cuda/`，包含移除前的匹配 DLL、原生源码快照、C# 绑定、安装器配置与 SHA-256 清单；恢复步骤见其中的 `README.txt`。恢复须同时处理原生实现/链接输入、C# 声明、工程复制/打包项、共享清单与外部 AIP，不能只放回两个依赖 DLL。此功能裁剪不改变本页的许可证构建约定。

Release 构建的 `ValidateGaolitongNativeDependencies` 会在 Build 前检查 `glaDevSys64.dll`、`xGUSB64.dll`、`xGCOM64.dll`、`xserial64.dll` 和 `FTD2XX.dll` 是否存在；缺少其中任一文件就报错。这只是输入存在性门禁，不校验 DLL 能否加载、导出是否匹配或真实设备能否打开。README 也作为 NuGet 包说明打包，但其仓库相对链接不保证包内含有对应知识文件。

`cvCamera.dll` 的 log4z 日志在 `InitResource` 启动前读取进程环境变量 `COLORVISION_NATIVE_LOG_DIRECTORY`。C# 的 `cvCameraCSLib.InitResource` 在未显式设置该变量时，取当前 log4net 文件输出的实际目录，因此原生日志与主程序日志位于同一目录（安装版通常为 `%APPDATA%\ColorVision\Log`）；原生日志仍保留独立的 `.log` 文件、文件名和级别。外部程序可在首次初始化前设置该变量，例如 C# 调用 `Environment.SetEnvironmentVariable(cvCameraCSLib.NativeLogDirectoryEnvironmentVariable, @"D:\ColorVisionLogs")`，或 C++ 调用 `SetEnvironmentVariableW(L"COLORVISION_NATIVE_LOG_DIRECTORY", L"D:\\ColorVisionLogs")`。支持中文目录；路径按进程设置，不按相机句柄设置。直接调用 native 且未设置变量的程序保留原来的当前工作目录 `./log/` 默认值。日志目录在初始化时读取，运行中的日志配置更改不会自动同步；不要为切换日志目录额外调用设备资源初始化或释放。原生导出名称、序号及 `InitResource` 参数保持兼容。

默认交付不含 `cfg/sys.cfg`。配套内部版 `cvCamera.dll` 的相机创建接口在配置路径为 `null` 或空字符串时初始化内置曝光、相机、XYZ 通道和空校准列表；显式文件路径仍保留兼容支持。WPF 的本地相机、视频和 CameraTest 会话使用空路径创建，物理相机及校准参数继续通过既有 JSON 接口注入。不能混用尚未支持空路径初始化的旧 DLL；仅删除配置文件不能替代 native 更新。

配套内部版 `cvCamera.dll` 不再使用 `CameraAttribute.db` 或旧 `cvTableList` 数据表实现，也不再导入 `sqlite3.dll`。相机读出模式保留 SDK 默认值；制冷能力通过 SDK 查询，温控开关和目标温度使用现有相机配置，停止温控不再依赖属性库。默认交付不含 `cvTableList.dll` / `sqlite3.dll`，托管 SQLite 的 `SQLitePCLRaw` 与 `e_sqlite3.dll` 依赖继续保留。旧 DataTable / SQLiteBaseControl 的 C++ 导出已移除，依赖这些导出或导出序号的外部程序需重新核对。

默认交付也不捆绑 `cfg_files` 中的 IKap `510.vlcf` 和 MIL 的位深映射/DCF 采集配置。HK 的 MVS、GenTL、MVFG 驱动分支不使用这组默认配置；相机驱动仍须按实际设备安装。IKap/MIL 设备需要另行提供与设备匹配的采集配置，这不代表移除了这些相机 SDK 或改变了相机类型枚举。

内部版 `cvCamera.dll` 对 `HK_USB` 的 BV/LV/LVTOBV 普通测量及视频采集默认直接调用已安装的 MVS x64 SDK；WPF 继续使用原有 `CM_*` 接口，不另设 HK 会话或兼容分支。打开前通过 `CM_UpdateCfgJson(Cfg_Camera)` 顶层布尔字段 `useHikMvs` 选择后端：`true` 使用 MVS，`false` 使用旧 SCGDCamLayer。同一 JSON 的顶层整数 `hikBayerQuality` 可选 0 快速、1 均衡、2 最优、3 最优+，仅用于 MVS 彩色转换。顶层布尔字段 `hikOutputBgr` 选择彩色输出顺序：`true` 输出 BGR，与旧后端及现有校正契约一致；`false` 保留 RGB，仅供对照。新管理器默认后端 `true`、质量 3、BGR 输出 `true`；后续配置缺少字段时保留已设置值。只传后端、质量或通道顺序字段不重置物理相机参数；非布尔后端/通道顺序、非整数或超出 0–3 的质量、已打开时改变任一偏好均失败，已有连接及输出设置保持不变。WPF 的本机偏好允许随时编辑，仅下次实际打开时下发。彩色 MVS 输出默认使用 Bayer 插值质量 3（最优+），打开时调用 SDK 设置所选质量，先转换为 RGB24/RGB48，再按 `hikOutputBgr` 原地交换红蓝通道，默认交付 BGR24/BGR48；旧 SCGDCamLayer 的 HK 16 位转换也包含这一步，不能从 RGB16 中间格式推断最终输出顺序。LV 为 Mono8/Mono16，忽略通道顺序偏好。平均取图与自动曝光仍使用原生相机层的既有逻辑，本地校正、翻转、缓存和保存仍由现有上层链路处理。CV 滤轮模式、采集卡型号及其它相机继续走原接口；MVS 后端不支持 HK 的 QHY 专用 Fast/FastEx 模式。

MVS 在打开测量或视频会话、开始采集前自动配置单帧软件触发，无需用户先在海康客户端设置：保留设备当前支持的 FrameStart 或 FrameBurstStart 选择，当前选择不适用时优先选择支持的 FrameStart，否则选择 FrameBurstStart；先关闭其它触发选择器，设置 AcquisitionMode=Continuous、TriggerSource=Software、TriggerMode=On；设备提供 AcquisitionBurstFrameCount 时设为 1，使用 FrameBurstStart 时该节点必须可用。改动后的参数逐项读回确认，仅在打开时执行，不增加每帧设参。关闭或打开中途失败时，停止采集后按相反顺序恢复原参数，不调用 UserSetSave。参数设置或读回失败仍明确报错，原生日志记录参数名与 SDK 错误码；不能把通用 -10009 打开失败一概认定为相机离线。相机独占打开，SDK 不支持所选质量、像素格式或必要接口时明确失败，不静默降低质量或切回旧采集。`HikMvsSDK`、`HikMvsOpen`、`HikMvsCapture` 原生日志记录 SDK 版本、相机序列号、像素格式、质量、曝光/增益、帧号及等待/转换耗时。模拟接口验证覆盖缓冲释放和关闭重开，不代表实机验收；质量 3 与质量 2 的像素结果可能不同，启用前须对现场亮度、色度、均匀性和 MTF 等业务指标及连续采集耗时做对照。

原始配置在仓库 `docs/_history/device-configs/ikap-mil/` 归档，不参与运行输出和默认安装包。该目录的 `manifest.json` 记录原始路径、字节数和 SHA-256，`README.txt` 说明按设备恢复工程复制项、安装器和共享清单的方法。恢复前应核对设备型号与位深，并在真实采集卡上验收，不能直接将历史配置当作所有 IKap/MIL 设备的通用默认值。

## 命名空间与程序集

`CVCommCore/` 和 `MQTTMessageLib/` 是本项目中的 C# 源码目录，保留 `CVCommCore.*`、`MQTTMessageLib.*` 命名空间，随默认 Compile 项编入 `cvColorVision.dll`。当前 `cvColorVision.csproj` 不生成两个同名独立程序集，也没有从仓库根 `DLL/` 引用 `CVCommCore.dll` / `MQTTMessageLib.dll`；不能把 `using` 的命名空间直接当成缺失 DLL 的清单。

当前 Engine 通过项目引用消费 `cvColorVision`，依赖该 Engine 的插件沿实际程序集引用和复制规则取得所需库。部署故障应同时核对插件的依赖清单、实际 DLL 版本以及 native 输入；托管类型来源和供应商 DLL 是两种依赖。

旧版或外部插件仍可能引用独立的 `CVCommCore` / `MQTTMessageLib` 程序集身份。此时保留并交付其要求的匹配 DLL，不能仅因当前源码编译通过就删除它们，也不能把 `cvColorVision.dll` 改名来充当旧程序集。应以该插件的实际引用为依据决定兼容部署或重建；同名类型不证明二进制兼容。

## 签名与返回值不能统一推断

当前相机绑定使用 `CM_Open(IntPtr)`、`CM_SetExpTime(IntPtr, float)`、`CM_GetFrame(...)` 等声明。签名和使用方式见 `Camera/cvCameraCSLib.*.cs` 及实际设备调用方。

`cvCameraCSLib` 的原生操作返回值统一保留 `int`，包括曝光/增益、配置更新、回调、取源图、滤轮、自动对焦、图像处理及校正操作；原先只有 `0/1` 的操作也预留整数错误码，不再按非零 `bool` 判断成功。当前这些操作成功值为 `CV_ERR_SUCCESS=1`，失败保留原始 `0`、平台错误码或 SDK 错误码。`CM_GetErrorMessage` 和 `GetAllCameraIDV1` 的字符串便捷入口同样返回原始整数结果；取源图便捷入口先检查尺寸查询的失败码，失败时不分配图像缓冲。由旧 `bool` 或 `void` 声明编译的调用方需要重新编译，原生导出名称与 32 位返回 ABI 保持不变。

当前相机托管层已清理无调用且交付 DLL 不提供的 38 个旧导入及其便捷包装，并移除未使用的 `AoiParam.cs`、`C_AoiParam`、`PartiCle` 和旧角点/布点类型；这不改变服务端 AOI 流程。保留的 `CM_Reset` 必须传入原生的 `delayTimeMs`，`CM_SCGD_SDP_DarkNoise` 必须传入曝光数组，`LedCheckYaQi` 返回 `int`。C++ `bool` 参数显式使用一字节封送，Windows `BOOL` 状态返回仍使用四字节封送。

跨 DLL 的 `CVImage`（对应原生 `HImage`）、`CRECT`/`IRECT`、`AutoFocusCfg`、`ChromaInfo` 使用按声明顺序的字段及 `Pack=8`；Release/x64 大小分别为 24、16、48、44 字节，对焦配置最后一个 `double` 位于偏移 40。修改字段类型、顺序或对齐时须同步原生定义。连续帧和对焦回调的返回值为 32 位 `int`，调用约定为 `StdCall`。配置、SN 和设备模式字符串便捷入口检查原生返回码后才返回字符串；原生文本复制在容量不足时返回平台错误，避免 CRT 的无效参数处理及错误成功。

纯状态查询 `CM_IsOpen`、`CM_GetDeviceOnline`、`CM_IsFeatureAvailable`、`CM_IsBurstmodeAvailable` 仍返回 `bool`；无效句柄返回 `false`，不能被负数平台错误码误判为可用。`CM_GetSrcFrameInfo` 成功返回 `1`，尺寸、位深和通道数经引用参数输出；`CM_GetFrameMemLength` 则返回缓冲字节数，调用方应按各入口的真实语义检查结果。

本地取图、自动曝光和独立相机测试在设置曝光/增益失败时立即通过 `CM_GetErrorMessage` 构造错误，不继续采集；本地视频初始化检查采集模式、位深、曝光、增益和回调，界面手动设置和滑块调整失败也记录同一错误说明。

`CM_SwitchCaptureMode(handle, mode, bpp)` 为本地 MVS 的普通拍照/视频切换入口，成功返回 `1`。同位深保留取流，跨 8/16 位停止取流、调整原生像素格式并读回、再恢复取流；均保留相机连接、软件触发、ROI、曝光、增益及转换偏好。入口先停止并等待预览回调退出，再取得帧锁，避免回调与切换互相等待。旧后端返回 `CV_ERR_CAM_TYPE_NOT=-10030`，Engine 才使用原有关闭/重开方案；SDK 错误直接返回，不以重连掩盖失败。失败恢复先前格式，无法恢复时关闭连接。原有仅关闭时可设置的 `CM_SetTakeImageMode` / `CM_SetImageBpp` 契约不变；MVS 的 Fast/FastEx 模式仍不支持。

`CM_GetAutoExpFrame` 为测量模式下的独立自动曝光返回达标帧，参数包含调用方持有的图像指针及 `uint64_t` 字节容量、图像尺寸/位深/通道数，以及各有三个 `float` 的曝光（ms）和饱和度数组。它保留既有曝光搜索算法；成功为 `1`，按既有目标饱和度及偏差检查最终图像，未达标返回 `CV_ERR_CAM_AUTO_EXP_NOT_CONVERGED=-10075`，SDK 失败保留原始整数码。Live 模式拒绝此帧返回入口，避免把改变曝光前排队的回调图像作为最终帧；视频窗口继续使用既有自动曝光及实时显示。曝光与图像来自同一次最终采集；MVS 曝光采用设置时的 SDK 读回值，旧 burst 取图保留该合成帧的有效曝光。单次彩色/灰度采集将曝光和饱和度复制到三个槽位；CV 三通道的数组按 X/Y/Z（R/G/B）排列，图像保持现有 BGR 布局。容量不足或失败时不修改调用方图像及结果输出。Engine 在持有帧期间复制独立预览，随后释放帧；独立自动曝光不写 CVRAW、缓存文件或数据库记录。既有 `CM_GetAutoExpTime` 和正常测量保存链保持原语义。

直接 MVS 后端的 SDK 调用失败保留 SDK 原始 32 位错误码，通过 `int` 返回，例如 `MV_E_NETER=0x80000206` 的有符号值为 `-2147483130`；MVS 的成功值 `0` 在相机公共入口仍转换为平台成功值 `1`。参数校验、输出校验和非 MVS 后端保留原有平台错误码。`CM_GetErrorMessage` 识别 MVS 错误范围，按构建时从 SDK `MvErrorDefine.h` 自动生成的名称/英文说明返回解释；未知码仍输出原始十六进制值，不手工维护第二套数值定义。同线程立即查询时还可附带 SDK 操作名和相机 SN，按线程隔离且每次 MVS 操作重新清理上下文；跨线程查询仍能解释返回码本身，不承诺取到原调用的操作上下文。原生与托管日志仍沿用现有机制。

`Test/ColorVision.UI.Tests/NativeCameraErrorTests.cs` 不连接设备，保护操作接口整数返回与状态查询的边界，验证所有相机导入对应交付 DLL 的真实导出、跨 DLL 结构体大小/字段偏移、SDK 码跨线程解释、未知码回退、多个操作的无效句柄错误、取源图失败前不分配缓冲及消息缓冲边界；原生 SDK 替身验证覆盖实际失败透传与后续成功调用，不代替现场 MVS 采集验收。

接口混用 `int`、`bool`、`void`，成功值由具体入口决定。例如 `Spectrometer.GetErrorMessage` 把 `1` 作为成功而返回空字符串，不能套用“所有 native 调用返回 0 才成功”。绑定存在、构建成功或取得返回值，都不能单独证明采集、校准或输出已安全完成；验证设备动作仍需明确授权和真实状态证据。

## 先查什么

| 现象 | 第一检查点 |
| --- | --- |
| `DllNotFoundException` | native DLL 是否在 x64 输出目录，依赖 DLL 是否也在 |
| `EntryPointNotFoundException` | `EntryPoint` 名称、DLL 版本、供应商导出符号 |
| `BadImageFormatException` | x86/x64 位数混用、AnyCPU 配置 |
| `AccessViolationException` | `DllImport` 参数、数组长度、指针生命周期、释放顺序 |
| XYZ/CCT/xy/uv 数值异常 | `PoiMeasurementService` 的平面 CIE 布局、RAW 校正快照与采样区域 |
| PG/源表无响应 | 连接方式、端口/IP、Start/Stop 顺序、原生返回码日志 |

## 当前能力

| 能力 | 当前入口 | 说明 |
| --- | --- | --- |
| 相机/通用视觉 | `Camera/cvCameraCSLib.*.cs` | 相机打开关闭、预览、取帧、配置 JSON、自动曝光、ROI、采样、TIFF、对焦和多类检测函数 |
| 图卡 | `Devices/PatternGenerator/PG.cs` | PG 初始化、TCP/串口连接、Start/Stop/Reset、帧切换 |
| 源表/电源 | `Devices/PassSx/PassSx.cs` | 打开关闭、源模式、2/4 线、前后端口、电压电流、步进/扫描 |
| 极薄入口 | `Algorithms.cs` 等 | 直接暴露少量底层函数 |
| MQTT/设备 DTO | `MQTTMessageLib/`、`CVCommCore/` | 原生/设备链路相关消息和归档数据结构 |

## 检查

| 验收项 | 通过标准 |
| --- | --- |
| native DLL 就位 | `cvCamera.dll` 及其实际依赖能在 Release/x64 输出目录加载；隔离加载不需要 OLED/CUDA，实际导出覆盖当前 C# 声明 |
| 位数一致 | 主程序、插件、native DLL 都是 x64 |
| 相机链路 | 初始化、枚举/打开、取帧、关闭和释放能按真实设备流程跑通 |
| PG 链路 | 初始化、连接、Start/Stop/Reset、上下切换或指定帧切换可被设备服务调用 |
| 源表链路 | 打开、设置源模式、读电压电流、步进/扫描、关闭有明确调用顺序 |
| 错误码 | 原生返回码能进入日志或上层异常，不被吞掉 |

## cvCamera.dll 许可证构建约定

以下是外部源码仓库的交付约定，不是本仓库可单独证明的运行状态；未提供该外部源码与真实设备测试时，不应断言当前 DLL 的许可证分支已完成验证。

本仓库 `DLL/scgd_internal_dll/cvCamera.dll` 默认保存公司内部使用版本：相机和光谱仪打开链路均不执行许可证验证。外部 `scgd_internal_dll` 源码仓库仅用于本地修改和构建，不在本仓库的交付流程中提交；确认产物后，只把 DLL 复制到本仓库并在这里提交。

生成内部版本时，检查以下开关：

| 设备链路 | 源码位置 | 内部版本 | 需要许可证的交付版本 |
| --- | --- | --- | --- |
| 相机 | `cvCameraItem/cvCameraItem/cvCamera.cpp` 中的 `CM_ValiLic` | 许可证分支使用 `if (false)` | 使用 `if (true)` |
| vLight 光谱仪 | `cvCamera/cvCamera/SpectroHelper.cpp` 中的 `_ACTIVE_LICENSE_` | 定义为 `0` | 定义为 `1` |
| 高立通光谱仪 | `cvCamera/cvCamera/SpectroGaolitong.cpp` 中的许可证分支 | 使用 `if (false)` | 使用 `if (true)` |

`pCamMan->SetDeviceMode(mode)` 保存的是许可证中的 `device_mode`，供 `CM_GetDeviceMode` 返回，不是设置相机硬件工作模式。内部版本不执行许可证分支时不要单独补调用；此时设备模式保持为空，上层应继续使用配置的相机型号作为回退。

按 `Release|x64` 构建后，确认相机、vLight 光谱仪和高立通光谱仪的打开路径没有活动的 `LicenseValidate` 调用，再把产物复制到 `DLL/scgd_internal_dll/cvCamera.dll`。静态许可证/HASP 代码或导出仍可能保留在 DLL 中；内部版本的验收标准是上述运行路径不触发许可证验证。

## 变更边界

| 变更类型 | 是否改这里 |
| --- | --- |
| DLL 入口名、参数、调用约定、结构体布局变化 | 是 |
| 采集后的模板判定、OK/NG 规则 | 通常看 `ColorVision.Engine/Templates`、项目包和流程节点 |
| CVCIE/CVRAW 文件格式 | 通常看 `ColorVision.FileIO` |
| WPF 按钮、菜单、图像叠加 | 通常看 UI、ImageEditor、结果展示链 |
| 新客户项目调用已有 native 能力 | 优先复用现有声明；只有 DLL 新增入口或签名变化时扩展这里 |

## 边界

- 关键能力主要来自 native DLL，C# 负责声明、薄包装和数据类型桥接。
- `cvCameraCSLib` 由 `Camera/cvCameraCSLib.*.cs` 多个 partial 文件组成，除相机控制外还暴露图像处理、自动对焦和检测函数。
- 接口粒度不统一，不能硬写成整齐分层 API。
- 上层 Engine、设备服务和插件调用这里；这里不编排宿主窗口或业务流程。

## 关键文件

| 任务 | 先看 |
| --- | --- |
| 相机绑定面 | `Camera/cvCameraCSLib.Core.cs`、`Capture.cs`、`Configuration.cs`、`Discovery.cs`、`Calibration.cs`、`ImageProcessing.cs` |
| 图卡 | `Devices/PatternGenerator/PG.cs` |
| 源表/电源 | `Devices/PassSx/PassSx.cs` |
| 光谱仪 | `Devices/Spectrometer/` |

## 验证入口与缺口

验证缺口：未登记能替代真实供应商 DLL 与硬件的完整自动化测试；外部源码和许可证构建说明不能由本仓库静态检查直接证明，交付需核对产物和设备路径。
