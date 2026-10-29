---
knowledge_id: "operations.camera"
knowledge_type: "topic"
status: "current"
summary: "本地与服务相机控制、共享电机会话、自动曝光与自动对焦模板、无文件预览；明确占用、超时、结果完成及帧寿命。"
aliases: ["CamMotorNode", "本地电机", "自动聚焦模板", "相机联动", "BV/LV本地转发", "CV本地转发", "CVCameraNode", "LVCameraNode", "使用本地相机", "本地相机优先", "相机拍图", "相机服务", "手动采集成功流程失败", "采集超时", "无文件预览", "本地相机管理", "本地相机取图", "视频模式", "相机结果查询", "是否重启服务", "CameraLog", "DeviceCamera", "MQTTCamera", "DisplayCamera", "ViewCamera", "CameraLocalWindow", "LocalCameraNode", "LocalCameraSession", "LocalFrameFileService", "SaveFiles", "AutoRefreshView", "本地相机尚未打开", "LocalFlowFrame", "LocalFlowFrameLease", "LocalFlowFrameRuntime", "SetCurrentFrame", "TryAcquireCurrentFrame", "FlowRuntimeResources", "本地帧租约", "流程帧内存", "SaveFiles=false", "CIE重新分配", "CameraFocusFrameProcessor", "CameraRealtimeFramePipeline", "海康取图测试", "HikCaptureBenchmark"]
code_paths: ["Engine/ColorVision.Engine/Services/Devices/Camera/Local/LocalCameraFlowExecution.cs","Engine/ColorVision.Engine/FlowProcessing/Nodes/Compatibility/Camera/LVCameraNode.cs","Engine/ColorVision.Engine/FlowProcessing/Nodes/Compatibility/Camera/CVCameraNode.cs","Engine/ColorVision.Engine/Services/Devices/Camera/DeviceCamera.Local.cs","Engine/ColorVision.Engine/Services/Devices/Camera/DeviceCamera.Commands.cs","Engine/ColorVision.Engine/Services/Devices/Camera/Local/CameraBackendState.cs","Engine/ColorVision.Engine/Services/Devices/Camera/Local/LocalCameraNative.cs","Engine/ColorVision.Engine/Services/Devices/Camera/Local/LocalCameraAutoExposure.cs","Engine/ColorVision.Engine/Services/Devices/Camera/Local/LocalCameraPreview.cs","Engine/ColorVision.Engine/Services/Devices/Camera/Local/LocalCameraResultService.cs","Engine/ColorVision.Engine/Services/Devices/Camera/DeviceCamera.cs","Engine/ColorVision.Engine/Services/Devices/Camera/MQTTCamera.cs","Engine/ColorVision.Engine/Services/Devices/Camera/DisplayCamera.xaml.cs","Engine/ColorVision.Engine/Services/Devices/Camera/Views/ViewCamera.xaml.cs","Engine/ColorVision.Engine/Services/Devices/Camera/Local/LocalCameraCaptureService.cs","Engine/ColorVision.Engine/Services/Devices/Camera/Video/CameraRealtimeFramePipeline.cs","Engine/ColorVision.Engine/Services/Devices/Camera/Video/CameraFocusFrameProcessor.cs","Engine/ColorVision.Engine/FlowProcessing/Nodes/LocalCameraNode.cs","Engine/ColorVision.Engine/Services/PhyCameras/PhyCamera.cs","Engine/ColorVision.Engine/Services/Devices/Camera/DisplayCamera.xaml","Engine/ColorVision.Engine/Services/Devices/Camera/CameraLocalWindow.xaml","Engine/ColorVision.Engine/Services/Devices/Camera/CameraLocalWindow.xaml.cs","Engine/ColorVision.Engine/Services/Devices/Camera/Local/LocalCameraSession.cs","Engine/ColorVision.Engine/Services/Devices/Camera/Local/LocalFrameFileService.cs","Engine/ColorVision.Engine/Services/Devices/Camera/Views/ViewCamera.xaml","Engine/ColorVision.Engine/Services/Devices/Camera/Views/ViewCameraConfig.cs","Engine/ColorVision.Engine/Abstractions/ViewConfigBase.cs","Engine/ColorVision.Engine/FlowProcessing/Runtime/DisplayFlow.xaml.cs","Engine/ColorVision.Engine/Services/Devices/Camera/Local/LocalFlowFrame.cs","Engine/FlowEngineLib/Base/FlowRuntimeResources.cs","Engine/FlowEngineLib/Base/CVStartCFC.cs","Engine/ColorVision.Engine/FlowProcessing/Nodes/LocalCalibrationNode.cs","Engine/ColorVision.Engine/Services/Devices/Camera/Local/LocalFrameCalibrationService.cs","UI/ColorVision.ImageEditor/Realtime/RealtimeFramePresenter.cs","UI/ColorVision.ImageEditor/Presentation/ImageStreamPresentation.cs","Engine/ColorVision.Engine/Services/Devices/Camera/Diagnostics","Engine/ColorVision.Engine/Services/Devices/Camera/Local/LocalCameraMotorSession.cs","Engine/ColorVision.Engine/Services/Devices/Camera/Local/LocalAutoFocusSearch.cs","Engine/ColorVision.Engine/Services/Devices/Camera/Local/LocalCameraMotorExecution.cs","Engine/ColorVision.Engine/FlowProcessing/Nodes/Compatibility/Camera/CamMotorNode.cs"]
test_paths: ["Test/ColorVision.UI.Tests/CameraIdentityOpenPersistenceTests.cs","Test/ColorVision.UI.Tests/LvCameraLocalForwardingTests.cs","Test/ColorVision.UI.Tests/CameraBackendRoutingTests.cs","Test/ColorVision.UI.Tests/LocalCameraOwnershipTests.cs","Test/ColorVision.UI.Tests/LocalCameraResultTests.cs","Test/ColorVision.UI.Tests/CameraViewLifecycleTests.cs","Test/ColorVision.UI.Tests/DeviceCameraAssociationTests.cs","Test/ColorVision.UI.Tests/ImageDisplayEffectsTests.cs","Test/ColorVision.UI.Tests/VideoProcessorResilienceTests.cs","Test/ColorVision.UI.Tests/LocalCameraSessionTests.cs","Test/ColorVision.UI.Tests/LocalFrameMirrorTests.cs","Test/ColorVision.UI.Tests/HikCaptureBenchmarkTests.cs","Test/ColorVision.UI.Tests/LocalCameraMotorTests.cs","Test/ColorVision.UI.Tests/LocalAutoFocusSearchTests.cs","Test/ColorVision.UI.Tests/LocalCameraMotorFlowTests.cs"]
related: ["engine.devices", "operations.device-configuration", "operations.physical-camera", "operations.camera-configuration", "engine.camera-preview-plan", "ui.image-editor-context"]
---

# 相机服务、采集与结果视图

本页说明如何使用远程相机、本地相机管理和流程取图节点，以及怎样判断采集完成、查找结果和处理无图问题。物理发现、许可证和资源导入见[物理相机管理](./camera-management.md)，参数来源及覆盖顺序见[相机配置](./camera-configuration.md)。字段默认值、调用顺序和异常分支以映射源码为准，本页只保留操作语义、兼容承诺和排障判断。

这些采集入口会访问硬件。自动曝光、ND/滤轮切换、对焦和电机移动还会改变设备状态，应在已授权的设备与操作范围内使用。

## 选择采集入口

| 入口 | 执行方式与结果 |
| --- | --- |
| 相机控制面板的“连接”“取图” | 显示配置决定下次打开的后端；取图跟随当前实际打开的会话，本地取图直接预览内存图，默认保存图像和结果记录 |
| 设备右键菜单 `Local`，或本地相机节点的“相机管理” | 打开 `CameraLocalWindow` 本地相机管理；在本进程连接、测量，并直接显示内存图像 |
| 相机控制面板的“开始预览” | 本地后端复用 `LocalCameraSession` 打开 Live/8-bit 预览；服务后端须先断开服务相机，再使用独立本地视频句柄 |
| 流程节点“本地相机取图” | `LocalCameraNode` 取得本地测量帧，保存结果主记录，再交给流程下游 |
| 流程节点“L/BV相机”与“CV相机” | `LVCameraNode` / `CVCameraNode` 复用当前打开的本地会话；相机关闭且启用“使用本地相机”时先自动打开本地测量会话，再取图；服务已占用或未启用本地偏好时保留服务请求 |
| 相机属性 → 校准与校正 → 用户校正 | 带入当前相机，选择最近拍摄图像、导入文件或取图后进行单点 / RGBW 修正；见[用户校正](./calibration.md#四色校正采集) |
| 设备结果视图 | `ViewCamera` 展示结果记录；已生成预览快照的最新本地结果优先使用该快照，其它记录按 `FileUrl` 打开文件 |

## 本地与服务后端

### 后端选择与占用

“采集与显示 → 显示配置”中的“使用本地相机”（`DisplayCameraConfig.UseLocalCamera`）默认开启，新配置和缺少此字段的旧配置均使用本地相机，已保存的选择保留。它只决定**下次打开**的后端：已打开、取图或关闭中的会话继续跟随实际后端，须关闭后重新打开才应用；它也不改服务端自动打开设置。主面板“连接”进入测量模式，视频由视频按钮开启。

本地会话不发布 MQTT 相机指令，仍保留 `MsgRecord` 成功/失败通知。服务已打开或正在操作时不能创建本地会话；曾观察到服务占用后，即使服务离线/未知也须收到 `Closed` 才视为释放。可以先选好本地偏好，用“断开”关闭服务会话，再重新打开。应用内同 CameraCode/CameraID 的其它逻辑相机和独立视频也参与占用检查；外部进程的并发打开仍依赖 SDK 独占保护。

本地实际打开后，`DService.DeviceStatus` 显示本地状态，服务心跳不能把本地会话覆盖成离线；关闭后恢复逻辑服务状态。通过 Local 窗口或节点显式打开的会话即使开关关闭也归当前设备所有，主面板可取图、自动曝光并关闭它。

本地打开时比较关联物理相机许可证的 `ExpiryDate` 与本机时间（不解码载荷、不新增签名校验）；缺失或已到期则提示许可证问题并关闭本地相机。“实验功能”（`ExperimentalFeaturesConfig.IsEnabled`，默认关闭）开启时跳过这项应用侧检查，原生 SDK 自身的许可校验不受影响。只在新打开本地相机时判断，复用会话和模式切换不检查；服务端打开路径不变。

### 相机身份

已绑定设备以物理配置的 `CameraID` 为准。旧配置只把 ID 存在逻辑设备时，关联阶段先校验完整 `MD5(CameraID)` 与物理 Code，匹配才补齐物理 ID；已有正确物理 ID 不被逻辑字段覆盖。没有可迁移 ID 时，本地连接按型号搜索，只选唯一完整 MD5 匹配项；无绑定且空 ID 只发现一台时自动选择；未发现或无法唯一匹配时明确提示，**不连接其他相机**。服务连接不会代替远程主机搜索本机硬件。

每次新建本地连接成功或发送服务打开请求前，按最新配置保存物理 ID 并同步逻辑镜像，只写身份字段，不重置其它参数或重启服务；保存失败记录日志并在下次连接重试。当前连接与物理身份不一致时，须先关闭再连接。

### 打开失败的诊断

打开失败会在现有提示中返回目标 ID、失败步骤和原始错误码。`CM_Open` 失败后按当前型号重新检测一次，区分：未发现设备（含 `CV_ERR_CAM_SCAN (-10005)`，提示先连接相机）、指定 ID 不在检测列表、目标仍可检测到、重新检测也失败。检测结果只反映失败后的状态，不自动换绑或重试其它相机；目标仍可检测到时应结合底层错误排查连接和其它程序/服务占用，不能直接认定为占用。

### 海康 MVS 后端

对 `HK_USB` / `HK_CARD` 的 BV/LV/LVTOBV 本地会话，“海康使用新取图”（`DisplayCameraConfig.UseHikMvs`，默认开启，缺字段视为开启）选择 MVS，关闭则使用旧 SCGDCamLayer；CV 滤轮模式和其它型号保持原路径。相关偏好都只在**下一次实际打开前**下发，不影响当前连接：

| 设置 | 默认 | 含义 |
| --- | --- | --- |
| `HikBayerQuality` | 3（缺字段同样为 3） | MVS 彩色拍照插值质量 0–3；视频固定使用 2 |
| `HikOutputBgr` | 开启（缺字段同样开启） | 交换红蓝通道以匹配旧后端 BGR 输出和现有校正；关闭仅供对照，此时 BGR 校正模板的结果不能作为正常生产测量 |

MVS 拍照使用单帧软件触发、视频使用连续采集，打开时设置并读回，关闭或失败时恢复原参数，无需先在海康客户端设置 Trigger。MVS 只提供普通拍照和视频，快速/快速扩展选项禁用。

海康彩色相机可用 LV：优先原生 Mono8/Mono16，没有时使用 Bayer8/Bayer16 直接复制原始单通道数据（不插值、不合成灰度）。Bayer 单通道响应不同于黑白传感器，须配套该采集模式的校正数据。BV 只在 Bayer 相位可确认时选择格式。请求位深没有匹配格式时明确失败，不静默改位深或把 12 位冒充 16 位。

新旧后端对照时保持曝光、增益、ROI、平均次数、校正、翻转及保存设置一致，分别连接采集；本地相机管理的 `[CameraCaptureCompare]` 日志记录本次连接实际使用的 `HikUseMvs`、`HikBayerQuality`、`HikOutputBgr`，原生日志 `HikMvsOpen` / `HikMvsMode` / `HikMvsCapture` 记录实际像素格式、通道数和质量。

### 通道数与滤光轮

测量输出通道数由模式决定：LV 为 1，BV、CV 和 LVTOBV 为 3；旧配置中的不匹配值按模式解释，界面不再提供独立选择。CFW 页仅在 CV 模式显示。

CV 本地视频只预览当前光路单通道，不初始化滤光轮，视频中的自动曝光也只计算当前通道。首次进入拍照时按物理相机（无则逻辑配置）的 `CFW` 准备滤光轮，仅 `IsUseCFW=true` 且 `IsCOM=true` 时启用串口。准备或复位到位失败会终止打开并返回原始错误；在视频中切拍照失败时原视频会话保留，可再次拍照重试。滤轮端口或波特率修改后须关闭再连接。拍照与视频切换保留滤轮串口连接，需要配套更新后的 `cvCamera.dll`，Fast/FastEx 模式仍可能完整关闭重开。**仅有视频打开成功不能证明滤光轮通信成功**，三通道测量须实机验证。

## 自动曝光

主面板有两个可选模板：上方模板配合“自动曝光”按钮，下方模板配合“取图”（选有效模板即先自动曝光再取图）。两者默认选择“不使用”；本地和服务遵循同一规则，不另设拍前曝光开关。没有可选模板时，本地显示“本机参数”，通过齿轮编辑并保存到显示配置。模板参数执行前复制，V1 保留原字段单位，V2 保留 `type`、ROI 等 JSON 参数；模板无效时报告失败，不沿用上次配置。

本地独立“自动曝光”在测量模式下回填曝光、饱和度和显示配置，并直接显示达标帧；**不保存图像、不写结果记录**，不受“本地取图保存文件”控制。失败或到达曝光边界仍未达标时提示具体错误，不替换当前图。CV 自动曝光按 X/Y/Z 返回并使用与正式取图相同的孔位/通道配置，拒绝重复或缺少 X/Y/Z 的组合。

不支持的组合会明确报错：本地使用 `IsAutoExpWithND=true` 或非空服务 HDR 模板。主面板本地模式隐藏 ND 手动控制和服务 HDR 模板。校正模板关联的校正组仍覆盖增益（见[本地视频与实时伪彩](#本地视频与实时伪彩)中的增益优先级）。

## 电机与自动对焦

物理配置 `MotorConfig.IsUseMotor=true` 时，本地相机连接同时创建独立电机句柄（无物理对象时用逻辑配置）。相机打开期间改电机配置须关闭再重连；同一串口只允许一个本地会话持有。相机联动 `IsCameraLinkage` 只控制失败策略：开启时电机失败会回滚相机打开，关闭时相机仍可取图、电机操作报告原始错误；它**不表示自动跟随或自动对焦**。

主面板在启用电机且停止预览后提供移动、回零、位置读取、光圈及自动对焦，电机跟随相机当前后端，本地失败不补发服务请求。本地层不按 `MinPosition` / `MaxPosition` 拦截移动，限位由控制器处理。`VID.IsUseVID=true` 时按映射文件把位置换算为 VID，公式与服务端 `CalDistanceExV4` 一致；文件无效或结果非有限值时报告失败。

本地自动对焦复用 `Camera,AutoFocus` 模板的评价算法、绝对搜索范围、步长、缩减系数、最低评价值和超时，搜索方式来自物理电机配置：

- 搜索范围直接使用模板的绝对坐标，默认 0–7800 不是 VID 等脉冲控制器的通用行程，须按设备修改。
- “最大差异”粗扫全范围后插值细搜；“二分”从当前位置开始局部找峰，适合已在焦点附近，不保证找到全范围最大峰；“最大值搜索”逐步缩小邻域。本地与服务端的采样顺序和拟合结果不保证逐位相同，需要实机对照。
- `nTimeout` 单位为毫秒，限制整次搜索总耗时（含移动、曝光、评价）；长行程、小步长或长曝光要增大，程序不会自动放宽。超时或停止会阻止后续动作，但不能中断正在进行的 SDK 调用。
- 使用当前手动曝光和增益，不自动执行曝光模板。模板保存后下一次对焦即生效；电机速度、搜索方式和 VID 映射修改须重连相机。开始日志记录实际生效的模板参数、曝光和增益。
- 主面板对焦过程中显示每个采样点的图像和评价值，不保存中间图、不写测量结果；完成后在最佳位置取一帧显示。

流程“电机[相机]”节点 `CamMotorNode` 同样跟随相机后端，兼容已有存档；本地分支复用相机会话、相机关闭且启用本地偏好时自动连接，预览期间须先停止预览。模板缺失、参数无效或电机失败使节点失败，不补发服务请求；本地分支不使用服务最大超时。节点只返回位置或评价结果，不拍预览、不生成测量记录、不替换当前流程图像。TSM/SV6100 等控制器的通信、方向和现场精度仍需实机验收。

## 使用远程相机取图

1. 在相机控制面板确认设备配置，点击“连接”。成功响应后才切换到已打开状态。
2. 设置曝光等参数，选择校准、自动曝光和 HDR 模板。空模板是有效选择，不等于启用对应功能；缺少选择时按钮直接返回、不发送请求。非空校准模板还要求物理相机、许可证中的校准服务关联和相应资源。
3. 点击“取图”，核对本次命令终态及新增结果。`MQTTCamera` 返回的 `MsgRecord` 只是请求记录，方法返回、按钮恢复或仍显示上一张图都不能作为采集成功依据。

这些发送前检查属于手动按钮路径，其它调用入口应核对自己的检查。自动曝光/对焦参数回填要求返回消息匹配 `DeviceCode` 且 `Code == 0`；对焦 `Code == 102` 是中间响应。

### 失败、超时与重启提示

命令超时后，`TryHandleCaptureTimeoutFromDatabase` 查找该设备在发送后新增的结果：`ResultCode == 0` 表示数据库已有成功记录，非零展示失败信息，没有新记录才显示超时。该回查**未严格关联本次 `MsgID`**，多路并发时需核对结果归属，避免重复采集。

取图失败后的“是否重启服务？”调用 `DisplayFlow.RestartColorVisionServicesAsync`，依次停止再启动 `RegistrationCenterService`、`CVMainService_x64`、`CVMainService_dev`，会影响共用这些服务的其它设备。某步失败会中断后续步骤，没有整体回滚，结束也不保证注册中心已重连；确认后仍应检查服务与设备状态，该操作不会自动重新取图。

## 使用本地相机管理

1. 在逻辑相机右键菜单选择 `Local`，或在“本地相机取图”节点选择“相机管理”。
2. 在未连接状态选择采集模式和位深，点击“连接”。相机 ID 为空时按[相机身份](#相机身份)规则自动选择；多台或绑定不匹配时先刷新并手动选择。窗口与同一设备的流程节点共用 `LocalCameraSession`。连接后可直接切换采集模式；相机 ID、型号和拍照位深修改须先关闭。
3. 设置曝光、增益、平均次数和校正模板，按需勾选“保存文件”，点击“测量”。窗口直接显示内存 RAW（有 CIE 时一并挂载）；该路径不写流程测量主记录，也不发布流程结果通知。

窗口使用 `Device.DisplayConfig` 的参数。从节点打开时会先把节点参数应用到窗口，后续修改再同步回节点；这会改变设备显示配置和节点参数，不是只读查看。关闭窗口保存偏好、停止本窗口拥有的预览，但**保留共享相机会话**供后续流程使用；需要断开时使用窗口内的“关闭”。测量过程中窗口暂不能关闭。

本地取图按校正模板保存的文件引用解析资源，引用丢失或文件未同步会失败，不回退为校正组默认文件。

### 海康取图测试

“海康取图测试”（`HK_USB` / `HK_CARD`）是只用于定位耗时的独立诊断进程，不加载插件、设备服务或数据库，需要海康 x64 MVS 运行库及匹配驱动。使用前关闭 cvCamera、服务或视频占用的相机并停止流程。每点一次“取一张”执行一次软件触发；不校正、翻转、存图或写数据库。“取一张并保存原始帧”把原始 Mono16/Bayer16 和 `metadata.json` 存为 ZIP，需手动带回。测试日志写入 `%APPDATA%\ColorVision\Log\HikCaptureTest_*.log`，近期文件会被反馈收集器带回。

测试成功只证明该独立链路有效，不能认定主程序的 SDK、占用状态或防火墙一致；原 `SCGDCamLayer.dll` 的插值和白平衡不在本仓库源码中，因此**不能证明测量结果等价**。报告字段和计时口径见 `Engine/ColorVision.Engine/Services/Devices/Camera/Diagnostics`。该入口不替换生产采集路径，诊断完成后可连同该目录及专用 SDK 引用移除。

## 主面板本地取图

主面板取图创建独立批次（不归档）和测量图像记录，**数据库保存成功才报告命令成功**；已取到图但落库失败时显示当前图并报告失败。“本地取图保存文件”（`SaveLocalCaptureFiles`）默认开启（缺字段同样开启），开启时保存含色度校正参数的 CVRAW；关闭仍预览内存图并写数据库。该选项用于主面板、POI、定时本地取图和本地相机管理窗口；流程节点使用节点自己的 `SaveFiles`。

相机卡片和结果详情的初始化边界见[设备详情视图按需初始化](../../04-api-reference/engine-components/device-service-chain.md#设备详情视图按需初始化)。“本地视频有画面”只说明预览路径可用；排查手动取图与流程结果不一致时，先核对实际入口、设备、曝光参数、校准模板和结果文件。

## 流程中的相机节点

### 普通 L/BV 与 CV 节点的本地转发

`LVCameraNode` / `CVCameraNode` 保留 FlowEngineLib 保存标识以兼容旧流程，每次执行按实际会话归属选择后端：已有本地测量会话则复用；相机关闭、启用“使用本地相机”且服务未占用时自动打开本地会话；服务已打开时仍走服务，修改偏好不切换已打开的后端。本地 Live 会话会先切为测量并保留拍照模式。打开或取图失败按节点原有失败策略处理，**不补发服务请求**。循环 `.For` 和通用相机节点仍用原执行方式。

本地分支使用节点的曝光、增益、平均次数、校正模板和翻转；CV 节点保留独立 R/G/B 曝光并对应 X/Y/Z，要求设备为 CV 三通道；本地分支暂不支持 `CV2LVChannel` 非 `None`（明确失败），服务分支仍传递该参数。校正组增益仍覆盖节点增益。

相机节点只负责取图及所选校正，本地和服务请求均不带 `POIParam`；旧流程中的 `POITempName`、`POIFilterTempName`、`POIReviseTempName` 被忽略，重新保存后清除。需要测量时在下游连接独立 POI 节点或本地“校正+实时 POI”。

节点配置面板仅在所选设备走本地取图时显示“本地相机”分组（保存文件），设备状态变化后重新打开面板刷新。结果归入原流程 `SerialNumber` 批次，以 `MasterResultType=100` 交接并经 `SetCurrentFrame` 交给下游。旧画布缺少保存字段时默认保存；原来通过设备显示配置关闭流程存图的配置，升级后需在节点关闭“保存文件”。

本地分支不启用节点超时，画布“最大超时”只对服务分支生效：原生采集不能即时中断，提前超时会让重启后的流程撞上仍在占用的相机。主动停止流程仍生效，晚到帧会释放、不写结果。转发一个相机节点不代表整个流程可脱离服务运行。

### 本地取图节点

`LocalCameraNode` 按 `DeviceCode` 查找已加载的逻辑相机，使用节点自身参数：

| 参数 | 默认值与约束 |
| --- | --- |
| `ExpTime` | 100 ms，大于 0；三通道共用一个曝光值 |
| `Gain` / `AvgCount` | 0 / 1；增益非负，平均次数至少为 1 |
| `CalibTempName` | 空；非空时须找到该物理相机的校正模板 |
| `AutoConnect` | `true`；未打开时按设备配置连接并按身份规则选择相机，已有 Live 会话自动切换为拍照 |
| `IsAutoExp` / `SaveFiles` | `false` / `true`；关闭保存时只写 CVRAW 缓存，数据库记录仍保留 |
| `FlipMode` | `None` |

取图走 RAW 按需色度路径，不生成整幅 CIE/XYZ 内存；需要完整 CIE 的下游接普通本地校正节点并关闭其加速。旧流程中的 `AllowAcceleration`、`SaveAsynchronously` 被忽略。关闭自动连接时须先建立本地会话，否则提示“本地相机尚未打开”。所有本地测量请求由进程级 `CaptureLock` 串行化，**不同设备也不会并行测量**。

节点先写 CVRAW 与色度参数，再保存测量主记录、交接内存帧并发布一次 `ResultMessageBus` 通知。图像或参数保存失败不写主记录；找不到批次或主记录保存失败使节点失败，已写图像保留。`SaveFiles=false` 依赖全局 CVRAW 缓存开启；调用只识别磁盘文件的旧服务时，须开启“保存文件”。仅缓存图像被淘汰后按缺失文件处理。

### 流程帧的寿命与读写限制

`SetCurrentFrame` 把 `LocalFlowFrame` 交给 `FlowRuntimeResources`，更换当前帧不释放旧帧；同一流程多次取图可能同时保留多帧，直到流程在 `DoFinishingCore` 释放资源。不能把“当前帧”理解为全流程只有一个缓冲区。消费者在根引用有效时 `Acquire()`，持有并最终 Dispose `LocalFlowFrameLease`。

**租约不是不可变图像快照。** 下游 `LocalCalibrationNode` 可对同一帧 `CalibrateInPlace`，修改 RAW 并重新分配 CIE（旧地址立即释放）。跨线程长期保留指针或并发预览与校正时，异步消费者必须自行复制稳定快照；UI 不持有原生指针或流程帧租约。`IsMirrorReady` 须与缓冲区各自的翻转状态一起判断：有 CIE 时 RAW 可能仍是传感器方向，不能仅凭 `FlipMode` 判断显示坐标已与 POI 一致。内存快照设计见[内存预览设计](../../02-developer-guide/engine-development/local-camera-memory-preview.md)。

“本地缓存管理 → 相机取图缓冲”的“启用相机取图缓冲”（`CameraRawBufferCacheConfig.IsEnabled`，默认关闭）让每台相机复用最多一块 RAW 工作内存，例如 9568×6380 三通道 16-bit 约 349 MiB；这不是流程在用图像的总内存上限。页面可按相机释放空闲缓冲，关闭开关立即释放空闲部分；连续运行时清理后不保证一直为零。节点阶段耗时中的 `AllocateRawBuffer` / `ReuseRawBuffer` 区分实际申请与复用，`ProcessGcPauseMs` 是整个进程的 GC 暂停增量（嵌套阶段不能相加），`ThreadCpuMs` 不含相机 SDK 线程。

### 本地文件保存位置

- 根目录取 `Device.Config.FileServerCfg.DataBasePath`，为空时使用“文档”下的 `ColorVision`。
- 路径为 `<DeviceCode>/Data/yyyy-MM-dd/yyyyMMdd_HHmmss_fff.cvraw`；执行了校正时在扩展名前追加 `_<校正模板名>`，同名才追加 `_2`、`_3`。色度参数保存在 CVRAW 尾部，旧文件名和旧服务 `.cvcie` 保留读取兼容。
- `LocalCameraNode` 先保存文件再写数据库；L/BV 本地转发先写数据库再保存图像，保存失败不撤销已写记录。

因此应分别确认采集、文件和数据库结果，不能仅凭文件存在判断节点成功。格式见[CVRAW/CVCIE 图像导出](../../04-api-reference/engine-components/cv-image-export.md)。

## 本地视频与实时伪彩

相机关闭时顶部显示“打开”和“视频模式”两个入口。本地相机打开后，“关闭视频”保留会话并恢复取图，“关闭”结束整个会话；服务模式下打开任一模式后只显示“关闭”。预览期间隐藏取图、平均、校正、曝光模板、HDR 和电机；“区域”按钮展开 ROI 与参考线参数。

预览曝光和增益在后台串行写入，界面不等待 SDK；界面响应快不代表相机已应用新曝光。MVS 视频的 `fps` 统计进入实时管线的回调频率，与 MVS 工具的相机帧率含义不同；排查显示速度时对照 `HikMvsCapture` 的 `CaptureMode`、`WaitMs` 和 `ConvertMs`，不能仅凭曝光推定。

拍照与预览共用 `FlipMode`：X 对应画面上下翻转，Y 对应左右翻转。旧配置同时保存测量翻转与 `LocalVideoTransform` 时以测量翻转为准。

增益优先级：面板增益 < 服务 HDR 模板 < 校正组。校正组固定增益时面板显示组增益并禁止修改，选择“不使用”后恢复编辑。预览增益不改校正组配置。服务独立自动曝光不带增益，沿用相机当前值；服务拍前自曝在设置本次取图增益后执行。

CV 测量显示 R/G/B 三路曝光，视频显示单路曝光；停止预览后的手动、POI 和定时取图按 R/G/B 组装参数，**预览曝光不会覆盖 R 通道**。

“停止预览”保留连接，仅“断开”释放连接。服务模式的独立视频可能重新解析并保存 `CameraID`，停止时关闭独立视频相机后才能连接服务拍照。停止失败不会假装恢复画面。

实时帧经 `CameraRealtimeFramePipeline` → `RealtimeFramePresenter` 在后台生成冻结快照，保留完整分辨率、位深和通道，新帧覆盖等待帧；视频启动会清理上一文件的校正/POI 状态，晚到的旧文件读取不能覆盖视频。显示契约见[连续帧显示](../../04-api-reference/ui-components/image-editor-context.md#连续帧显示)；这只更新预览文档，不改写采集缓冲、测量数据或结果文件。对焦清晰度由 `CameraFocusFrameProcessor`、十字参考线由 `VideoCrossGuideProcessor` 独立处理，单次异常记录日志后继续。后台化不保证超大图像达到满帧，仍需实机验证；不能以进程内存立即下降作为缓冲释放完成的判据。

## 查询和显示相机结果

| 操作或设置 | 当前行为 |
| --- | --- |
| 收到新的远程或本地结果通知 | 按当前 `DeviceCode` 筛选后加入列表，默认插在开头 |
| `AutoRefreshView` | 默认 `true`，新结果自动选中并显示；关闭后仍加入记录，但不切换图像（主面板手动取图仍强制显示） |
| “查询” | 清空列表后查询整个相机结果表，**没有自动限定当前设备**；默认按 ID 降序取 50 条，`Count <= 0` 不限 |
| “高级查询” | 使用用户条件和数量，同样不自动附加当前相机条件 |
| `MaxHistoryCount`（实时结果保留上限） | 默认 500，按最小主记录 ID 淘汰；搜索加载可超过上限，之后有实时记录加入时收回到上限 |
| 结果视图设置 | 齿轮打开 `ViewCameraConfig`，相机视图共用该配置 |
| CSV 导出 | 只导出选中的一条记录，自动追加 `.csv` |
| 清空列表或删除选中行 | 只移除视图中的行，不删除数据库记录或图像文件 |

流程和本地取图结果按 `FileUrl` 经 `ImageView.OpenImage` 打开 CVRAW；`SaveFiles=false` 只能从缓存读取，缓存和磁盘都没有时无法打开。没有 CVRAW 路径的手动预览（如独立自动曝光）使用内存快照。RAW 预览只在显示副本中转换格式和方向，不修改源缓冲。

有记录但无图时，先检查选中行、`FileUrl` 和文件加载；出现其它相机记录时，核对是否执行过全表查询。设备右键菜单的“相机日志”（`CameraLog`）按实际后端选择：本地会话打开本进程 `cvCamera.dll` 原生日志（目录依次为 `COLORVISION_NATIVE_LOG_DIRECTORY`、主程序日志目录、工作目录 `log`，取最新的 `*_Main_*.log`）；服务模式打开主服务目录的 `CVMainWindowsService_x64_camera*.log`。没有本地日志时不会改开服务日志。

## 设备关联与资源释放

`DeviceCamera` 是逻辑相机：`Config.Code` 是消息中的 `DeviceCode`，`Config.CameraCode` 关联物理相机，`Config.CameraID` 用于打开硬件。`DeviceCamera.Save()` 会重新绑定物理相机、同步参数和 ID，并进入[设备配置持久化与服务重启契约](./configuration.md)；`PhyCamera.SetDeviceCamera` 保存许可证中的 `DevCameraId`，许可证关联了校准设备时还可能请求该校准服务重启。`DeviceCamera.Dispose()` 幂等释放视图、订阅、本地会话、校正缓存和 MQTT 服务。

## 验证范围

`test_paths` 中的测试均使用原生/采集替身、软件构造帧或临时数据库，覆盖身份迁移与持久化、后端路由与占用、会话复用与关闭重开、结果保存与显示副本、L/BV 转发、电机与自动对焦搜索、流程帧租约和视频处理器容错；它们**不连接真实相机、MQTT 服务或业务数据库**。远程完成消息、校准资源、超时结果归属、句柄互斥、滤光轮与电机通信、驱动回调时序、长时间运行和文件显示仍需在已授权设备环境现场验收；源码核对与文档构建不能替代这些证据。
