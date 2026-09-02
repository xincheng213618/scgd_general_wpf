---
knowledge_id: "engine.native-bindings"
knowledge_type: "reference"
status: "current"
summary: "定位供应商 native DLL 的相机、光谱、XYZ、OLED、PG 与源表绑定契约。"
aliases: ["设备SDK入口在哪里","cvColorVision","cvCameraCSLib","ConvertXYZ","CVCommCore","MQTTMessageLib","CVCommCore.dll","MQTTMessageLib.dll"]
code_paths: ["Engine/cvColorVision/README.md","Engine/cvColorVision/Camera","Engine/cvColorVision/CVCommCore","Engine/cvColorVision/MQTTMessageLib","Engine/cvColorVision/Color/ConvertXYZ.cs","Engine/cvColorVision/Devices/Display/CvOledDLL.cs","Engine/cvColorVision/Devices/Spectrometer/Spectrometer.cs","Engine/cvColorVision/cvColorVision.csproj"]
test_paths: []
related: ["engine.index","engine.native-integration","ui.core"]
---

# cvColorVision

`Engine/cvColorVision/` 是原生能力绑定层，通过 `DllImport` 暴露 `cvCamera.dll`、`cvOled.dll` 等底层接口给 C#。它不是纯托管视觉算法库，也不负责 WPF 界面、模板或工作流编排。

## 绑定与交付前提

当前工程目标是 `net10.0-windows7.0`。`cvColorVision.csproj` 从 `DLL/scgd_internal_dll/` 复制和打包供应商 DLL 及配置，不在本工程编译这些 native 实现。部分输入显式指定 `runtimes/win-x64/native` 包路径，其他设备 DLL 和配置按各自的 Pack/Copy 元数据处理，不能假设所有资产都位于同一目录。输入不止 `cvCamera.dll` / `cvoled.dll`：例如 CUDA runtime、CommLibrary、OpenCV 和设备 SDK 相关 DLL 也在清单中；具体功能还受设备驱动与部署配置约束。

Release 构建的 `ValidateGaolitongNativeDependencies` 会在 Build 前检查 `glaDevSys64.dll`、`xGUSB64.dll`、`xGCOM64.dll`、`xserial64.dll` 和 `FTD2XX.dll` 是否存在；缺少其中任一文件就报错。这只是输入存在性门禁，不校验 DLL 能否加载、导出是否匹配或真实设备能否打开。README 也作为 NuGet 包说明打包，但其仓库相对链接不保证包内含有对应知识文件。

## 命名空间与程序集

`CVCommCore/` 和 `MQTTMessageLib/` 是本项目中的 C# 源码目录，保留 `CVCommCore.*`、`MQTTMessageLib.*` 命名空间，随默认 Compile 项编入 `cvColorVision.dll`。当前 `cvColorVision.csproj` 不生成两个同名独立程序集，也没有从仓库根 `DLL/` 引用 `CVCommCore.dll` / `MQTTMessageLib.dll`；不能把 `using` 的命名空间直接当成缺失 DLL 的清单。

当前 Engine 通过项目引用消费 `cvColorVision`，依赖该 Engine 的插件沿实际程序集引用和复制规则取得所需库。部署故障应同时核对插件的依赖清单、实际 DLL 版本以及 native 输入；托管类型来源和供应商 DLL 是两种依赖。

旧版或外部插件仍可能引用独立的 `CVCommCore` / `MQTTMessageLib` 程序集身份。此时保留并交付其要求的匹配 DLL，不能仅因当前源码编译通过就删除它们，也不能把 `cvColorVision.dll` 改名来充当旧程序集。应以该插件的实际引用为依据决定兼容部署或重建；同名类型不证明二进制兼容。

## 签名与返回值不能统一推断

当前相机绑定使用 `CM_Open(IntPtr)`、`CM_SetExpTime(IntPtr, float)`、`CM_GetFrame(...)` 等实际声明，不提供旧示例中的 `CM_Init(CameraType)`、`CM_GetImage(handle, buffer)`、`CM_SetExposureTime` 或 `CM_Uninit` 这一套通用生命周期 API。签名和使用方式见 `Camera/cvCameraCSLib.*.cs` 及实际设备调用方。

`ConvertXYZ.CM_InitXYZ(IntPtr handle)` 返回 `int`，不是新建的 `IntPtr`；调用方持有并传入已有句柄。`CM_SetBufferXYZ` 的尺寸/通道参数为 `UInt32`，数组与指针重载均存在，且另有 `CM_ReleaseBuffer`。跨 native 边界须核对签名、缓冲区大小、所有权与释放顺序，不能只按方法名猜测资源生命周期。

接口混用 `int`、`bool`、`void`，成功值由具体入口决定。例如 `Spectrometer.GetErrorMessage` 把 `1` 作为成功而返回空字符串，不能套用“所有 native 调用返回 0 才成功”。绑定存在、构建成功或取得返回值，都不能单独证明采集、校准或输出已安全完成；验证设备动作仍需明确授权和真实状态证据。

## 先查什么

| 现象 | 第一检查点 |
| --- | --- |
| `DllNotFoundException` | native DLL 是否在 x64 输出目录，依赖 DLL 是否也在 |
| `EntryPointNotFoundException` | `EntryPoint` 名称、DLL 版本、供应商导出符号 |
| `BadImageFormatException` | x86/x64 位数混用、AnyCPU 配置 |
| `AccessViolationException` | `DllImport` 参数、数组长度、指针生命周期、释放顺序 |
| XYZ/CCT/xy/uv 数值异常 | `CM_SetBufferXYZ` rows/cols/bpp/channels 和采样区域 |
| PG/源表无响应 | 连接方式、端口/IP、Start/Stop 顺序、原生返回码日志 |

## 当前能力

| 能力 | 当前入口 | 说明 |
| --- | --- | --- |
| 相机/通用视觉 | `Camera/cvCameraCSLib.*.cs` | 相机打开关闭、预览、取帧、配置 JSON、自动曝光、ROI、采样、TIFF、对焦和多类检测函数 |
| 色彩采样 | `Color/ConvertXYZ.cs` | XYZ 缓冲初始化/释放，Circle/Rect/批量点位采样，xyz/uv/CCT/主波长导出 |
| OLED 算法 | `Devices/Display/CvOledDLL.cs` | `cvOled.dll` 参数加载、图片读入、像素查找、像素重建、摩尔纹滤波 |
| 图卡 | `Devices/PatternGenerator/PG.cs` | PG 初始化、TCP/串口连接、Start/Stop/Reset、帧切换 |
| 源表/电源 | `Devices/PassSx/PassSx.cs` | 打开关闭、源模式、2/4 线、前后端口、电压电流、步进/扫描 |
| 极薄入口 | `Algorithms.cs` 等 | 直接暴露少量底层函数 |
| MQTT/设备 DTO | `MQTTMessageLib/`、`CVCommCore/` | 原生/设备链路相关消息和归档数据结构 |

## 检查

| 验收项 | 通过标准 |
| --- | --- |
| native DLL 就位 | `cvCamera.dll`、`cvOled.dll` 及依赖能在 Release/x64 输出目录加载 |
| 位数一致 | 主程序、插件、native DLL 都是 x64 |
| 相机链路 | 初始化、枚举/打开、取帧、关闭和释放能按真实设备流程跑通 |
| XYZ 采样 | `CM_InitXYZ`、`CM_SetBufferXYZ`、采样、`CM_ReleaseBuffer`、`CM_UnInitXYZ` 顺序清楚 |
| OLED 链路 | `CvOledInit`、`CvLoadParam`、读图/点位/重建、`CvOledRealse` 成对验证 |
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
| XYZ 采样 | `Color/ConvertXYZ.cs` |
| OLED | `Devices/Display/CvOledDLL.cs` |
| 图卡 | `Devices/PatternGenerator/PG.cs` |
| 源表/电源 | `Devices/PassSx/PassSx.cs` |
| 光谱仪 | `Devices/Spectrometer/` |

## 验证入口与缺口

验证缺口：未登记能替代真实供应商 DLL 与硬件的完整自动化测试；外部源码和许可证构建说明不能由本仓库静态检查直接证明，交付需核对产物和设备路径。
