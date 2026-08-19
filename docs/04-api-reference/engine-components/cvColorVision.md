# cvColorVision

`Engine/cvColorVision/` 是原生能力绑定层，通过 `DllImport` 暴露 `cvCamera.dll`、`cvOled.dll` 等底层接口给 C#。它不是纯托管视觉算法库，也不负责 WPF 界面、模板或工作流编排。

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
| 相机/通用视觉 | `cvCameraCSLib.cs` | 相机打开关闭、预览、取帧、配置 JSON、自动曝光、ROI、采样、TIFF、对焦和多类检测函数 |
| 色彩采样 | `ConvertXYZ.cs` | XYZ 缓冲初始化/释放，Circle/Rect/批量点位采样，xyz/uv/CCT/主波长导出 |
| OLED 算法 | `CvOledDLL.cs` | `cvOled.dll` 参数加载、图片读入、像素查找、像素重建、摩尔纹滤波 |
| 图卡 | `PG.cs` | PG 初始化、TCP/串口连接、Start/Stop/Reset、帧切换 |
| 源表/电源 | `PassSx.cs` | 打开关闭、源模式、2/4 线、前后端口、电压电流、步进/扫描 |
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
- `cvCameraCSLib.cs` 名字像相机库，但实际还暴露色彩采样、图像处理、自动对焦和检测函数。
- 接口粒度不统一，不能硬写成整齐分层 API。
- 上层 Engine、设备服务和插件调用这里；这里不编排宿主窗口或业务流程。

## 关键文件

| 任务 | 先看 |
| --- | --- |
| 总绑定面 | `cvCameraCSLib.cs` |
| XYZ 采样 | `ConvertXYZ.cs` |
| OLED | `CvOledDLL.cs` |
| 图卡 | `PG.cs` |
| 源表/电源 | `PassSx.cs` |
| 光谱仪 | `Devices/Spectrometer/` |
