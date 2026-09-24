---
knowledge_id: "operations.camera"
knowledge_type: "topic"
status: "current"
summary: "本地优先与服务兼容的相机控制、共享会话、无文件内存预览；明确后端占用、自动曝光边界、文件/数据库完成及帧寿命。"
aliases: ["BV/LV本地转发", "LVCameraNode", "使用本地相机", "本地相机优先", "相机拍图", "相机服务", "手动采集成功流程失败", "采集超时", "无文件预览", "本地相机管理", "本地相机取图", "视频模式", "相机结果查询", "是否重启服务", "CameraLog", "DeviceCamera", "MQTTCamera", "DisplayCamera", "ViewCamera", "CameraLocalWindow", "LocalCameraNode", "LocalCameraSession", "LocalFrameFileService", "SaveFiles", "AutoRefreshView", "本地相机尚未打开", "LocalFlowFrame", "LocalFlowFrameLease", "LocalFlowFrameRuntime", "SetCurrentFrame", "TryAcquireCurrentFrame", "FlowRuntimeResources", "本地帧租约", "流程帧内存", "SaveFiles=false", "CIE重新分配", "CameraFocusFrameProcessor", "CameraRealtimeFramePipeline"]
code_paths: ["Engine/ColorVision.Engine/Services/Devices/Camera/Local/LocalLvCameraExecution.cs", "Engine/ColorVision.Engine/FlowProcessing/Nodes/Compatibility/Camera/LVCameraNode.cs", "Engine/ColorVision.Engine/Services/Devices/Camera/DeviceCamera.Local.cs", "Engine/ColorVision.Engine/Services/Devices/Camera/DeviceCamera.Commands.cs", "Engine/ColorVision.Engine/Services/Devices/Camera/Local/CameraBackendState.cs", "Engine/ColorVision.Engine/Services/Devices/Camera/Local/LocalCameraNative.cs", "Engine/ColorVision.Engine/Services/Devices/Camera/Local/LocalCameraAutoExposure.cs", "Engine/ColorVision.Engine/Services/Devices/Camera/Local/LocalCameraPreview.cs", "Engine/ColorVision.Engine/Services/Devices/Camera/Local/LocalCameraResultService.cs", "Engine/ColorVision.Engine/Services/Devices/Camera/DeviceCamera.cs", "Engine/ColorVision.Engine/Services/Devices/Camera/MQTTCamera.cs", "Engine/ColorVision.Engine/Services/Devices/Camera/DisplayCamera.xaml.cs", "Engine/ColorVision.Engine/Services/Devices/Camera/Views/ViewCamera.xaml.cs", "Engine/ColorVision.Engine/Services/Devices/Camera/Local/LocalCameraCaptureService.cs", "Engine/ColorVision.Engine/Services/Devices/Camera/Video/CameraRealtimeFramePipeline.cs", "Engine/ColorVision.Engine/Services/Devices/Camera/Video/CameraFocusFrameProcessor.cs", "Engine/ColorVision.Engine/FlowProcessing/Nodes/LocalCameraNode.cs", "Engine/ColorVision.Engine/Services/PhyCameras/PhyCamera.cs", "Engine/ColorVision.Engine/Services/Devices/Camera/DisplayCamera.xaml", "Engine/ColorVision.Engine/Services/Devices/Camera/CameraLocalWindow.xaml", "Engine/ColorVision.Engine/Services/Devices/Camera/CameraLocalWindow.xaml.cs", "Engine/ColorVision.Engine/Services/Devices/Camera/Local/LocalCameraSession.cs", "Engine/ColorVision.Engine/Services/Devices/Camera/Local/LocalFrameFileService.cs", "Engine/ColorVision.Engine/Services/Devices/Camera/Views/ViewCamera.xaml", "Engine/ColorVision.Engine/Services/Devices/Camera/Views/ViewCameraConfig.cs", "Engine/ColorVision.Engine/Abstractions/ViewConfigBase.cs", "Engine/ColorVision.Engine/FlowProcessing/Runtime/DisplayFlow.xaml.cs", "Engine/ColorVision.Engine/Services/Devices/Camera/Local/LocalFlowFrame.cs", "Engine/FlowEngineLib/Base/FlowRuntimeResources.cs", "Engine/FlowEngineLib/Base/CVStartCFC.cs", "Engine/ColorVision.Engine/FlowProcessing/Nodes/LocalCalibrationNode.cs", "Engine/ColorVision.Engine/Services/Devices/Camera/Local/LocalFrameCalibrationService.cs", "UI/ColorVision.ImageEditor/Realtime/RealtimeFramePresenter.cs", "UI/ColorVision.ImageEditor/Presentation/ImageStreamPresentation.cs"]
test_paths: ["Test/ColorVision.UI.Tests/LvCameraLocalForwardingTests.cs", "Test/ColorVision.UI.Tests/DeferredDeviceViewTests.cs", "Test/ColorVision.UI.Tests/CameraBackendRoutingTests.cs", "Test/ColorVision.UI.Tests/LocalCameraOwnershipTests.cs", "Test/ColorVision.UI.Tests/LocalCameraResultTests.cs", "Test/ColorVision.UI.Tests/CameraViewLifecycleTests.cs", "Test/ColorVision.UI.Tests/DeviceCameraAssociationTests.cs", "Test/ColorVision.UI.Tests/ImageDisplayEffectsTests.cs", "Test/ColorVision.UI.Tests/VideoProcessorResilienceTests.cs", "Test/ColorVision.UI.Tests/LocalCameraSessionTests.cs", "Test/ColorVision.UI.Tests/LocalFlowNodePortTests.cs", "Test/ColorVision.UI.Tests/LocalFrameMirrorTests.cs", "Test/ColorVision.UI.Tests/ImageStreamPresentationTests.cs"]
related: ["engine.devices", "operations.device-configuration", "operations.physical-camera", "operations.camera-configuration", "engine.camera-preview-plan", "ui.image-editor-context"]
---

# 相机服务、采集与结果视图

本页说明如何使用远程相机、本地相机管理和流程取图节点，以及怎样判断采集完成、查找结果和处理无图问题。物理发现、许可证和资源导入见[物理相机管理](./camera-management.md)，参数来源及覆盖顺序见[相机配置](./camera-configuration.md)。

## 选择采集入口

| 入口 | 执行方式与结果 |
| --- | --- |
| 相机控制面板的“打开”“取图” | 显示配置决定下次打开的后端；取图跟随当前实际打开的会话，本地取图直接预览内存图，默认保存图像和结果记录 |
| 设备右键菜单 `Local`，或本地相机节点的“相机管理” | 打开 `CameraLocalWindow` 本地相机管理；在本进程连接、测量，并直接显示内存图像 |
| 相机控制面板的“视频模式” | `DisplayCamera` 用独立句柄打开 Live/8-bit 相机，显示连续回调帧 |
| 流程节点“本地相机取图” | `LocalCameraNode` 取得本地测量帧，保存结果主记录，再交给流程下游 |
| 流程节点“L/BV相机” | `LVCameraNode` 复用当前打开的本地会话；相机关闭且启用“使用本地相机”时先自动打开本地测量会话，再取图；服务已占用或未启用本地偏好时保留服务请求。本地分支忽略 POI、过滤和修正 |
| 相机属性 → 校准与校正 → 用户校正 | 带入当前相机，选择最近拍摄图像、导入文件或取图后进行单点 / RGBW 修正；见[用户校正](./calibration.md#四色校正采集) |
| 设备结果视图 | `ViewCamera` 展示结果记录；已生成预览快照的最新本地结果优先使用该快照，其它记录按 `FileUrl` 打开文件 |

在相机属性的“采集与显示 → 显示配置”中编辑“使用本地相机”。该软开关持久化在本机 `DisplayCameraConfig.UseLocalCamera`，默认关闭，只决定下次打开的后端，不更改服务端自动打开设置。可以在已打开、取图或关闭过程中修改，当前连接、取图、自动曝光和关闭仍跟随实际会话；须先关闭当前连接，再打开才应用新偏好。主面板不显示此开关或后端说明文字。服务不可用且未观察到占用时，选择本地后仍提供“打开”入口，但不会把逻辑设备状态改成已打开。

本地会话通过 `DeviceCamera` 调用 `LocalCameraSession` 和 `LocalCameraCaptureService`，保留 `MsgRecord` 成功/失败通知，不发布 MQTT 相机指令。主面板取图和自动曝光要求会话已打开，不通过取图应用新偏好或隐式重连；流程节点的 `AutoConnect` 是独立的显式本地入口。服务已打开或正在操作时，不能再创建本地会话；曾观察到服务占用后，即使服务离线/未知也不视为已释放，须收到 `Closed`。可先选好本地偏好，再用“关闭”关闭当前服务会话，随后重新打开。启动时尚未观察到服务占用可尝试本地打开，最终仍以原生 SDK 的独占打开结果为准。应用内同 CameraCode/CameraID 的其它逻辑相机以及独立视频也参与打开前的占用检查；外部进程的并发打开仍依赖 SDK 独占保护。

本地会话与原始服务状态分别保存；`DService.DeviceStatus` 在本地实际打开后显示本地状态，覆盖逻辑相机自身状态。服务心跳只更新服务侧记录，不能把本地会话覆盖成离线或阻止该会话取图；修改偏好也不改变当前状态或路由。即使开关关闭，通过 Local 窗口/节点显式打开的会话也取得当前设备的本地归属，主面板可取图、自动曝光并关闭该会话。关闭后恢复逻辑服务状态，下次打开重新读取偏好。

本地自动曝光沿用原生 `CM_GetAutoExpTime`，回填曝光、饱和度和显示配置；取图自动曝光在生成帧元数据之前完成。自动曝光下拉框在本地模式仅选择是否启用原生曝光，不应用服务 V1/V2 模板参数。`IsAutoExpWithND=true` 和非空 HDR 模板会明确报不支持；ND 手动控制、对焦、电机操作在主面板本地模式下禁用。校正模板仍由校正组覆盖增益，资源按模板文件引用解析，不要求服务校准设备在线。

本地主面板取图按原有手动取图含义创建独立批次（不归档）和测量图像记录，数据库保存成功才报告命令成功。显示配置中的“本地取图保存文件”（`SaveLocalCaptureFiles`）默认开启，包括已有配置缺少此字段的情况，按本地文件规则保存 CVRAW；同时遵循 `IsCVCIEFileSave`（关闭时仅写 RAW，内存 CIE 仍可使用）。关闭文件保存仍预览内存图并写数据库。此选项用于主面板/POI/定时本地取图和普通 L/BV 节点的本地转发，本地管理窗口和独立 `LocalCameraNode` 使用各自的保存设置。已取到图但数据库保存失败时显示当前图并报告失败，不把预览成功当作落库成功。

相机卡片和结果详情的登记、首次显示、首结果及提前释放边界见[设备详情视图按需初始化](../../04-api-reference/engine-components/device-service-chain.md#设备详情视图按需初始化)。

“本地视频有画面”只说明该预览路径可用。排查手动取图与流程结果不一致时，先核对实际入口、设备、曝光参数、校准模板和结果文件。

这些采集入口会访问硬件。自动曝光、ND/滤轮切换、对焦和电机移动还会改变设备状态，应在已授权的设备与操作范围内使用。

## 使用远程相机取图

1. 在相机控制面板确认设备配置，点击“打开”。`Open_Click` 发送当前 `CameraID`、采集模式和位深；成功响应后才切换到已打开的控制状态。
2. 设置曝光等参数，选择校准、自动曝光和 HDR 模板。`GetData_Click` 要求自动曝光及 HDR 下拉框各有一个有效的 `ParamBase` 选择；缺少选择时直接返回，不发送请求。空模板是有效选择，不等于启用对应功能。非空校准模板还要求物理相机、许可证中的校准服务关联和相应校准资源；无校准选择时回退为空模板。
3. 点击“取图”，核对本次命令终态及新增结果。`MQTTCamera` 返回的 `MsgRecord` 只是请求记录，方法返回、按钮恢复或仍显示上一张图都不能作为采集成功依据。

这些发送前检查属于手动按钮路径；其它调用入口应核对自己的检查及参数来源。自动曝光/对焦的参数回填要求返回消息匹配 `DeviceCode` 且 `Code == 0`；对焦的 `Code == 102` 是中间响应，可更新位置和临时图。

### 失败、超时与重启提示

手动取图发送前记录该设备最新结果 ID。命令超时后，`TryHandleCaptureTimeoutFromDatabase` 查找该设备新增结果并刷新列表：`ResultCode == 0` 表示数据库已有成功记录，非零则展示数据库失败信息；没有新记录才继续显示超时提示。该回查按设备和新增 ID 匹配，未严格关联此次 `MsgID`，多路并发时仍需核对结果归属，避免重复采集。

取图失败后的“是否重启服务？”会调用 `DisplayFlow.RestartColorVisionServicesAsync`：依次停止 `RegistrationCenterService`、`CVMainService_x64`、`CVMainService_dev`，再按同一顺序启动，并尝试重新连接注册中心。它会影响共用这些服务的其它设备。某一步失败会中断后续步骤，没有整体回滚；方法结束也不保证注册中心已重连。确认重启后仍应检查服务与设备状态，该操作不会自动重新取图。

## 使用本地相机管理

1. 在逻辑相机右键菜单选择 `Local`，或在“本地相机取图”节点选择“相机管理”。窗口加载会初始化本地 SDK；许可证文件缺失时，入口还可能写出本地许可证。
2. 在未连接状态选择相机 ID、采集模式和位深，点击“连接”。测量使用 `Measure_Normal` 等测量模式；`Live` 用于实时画面。窗口与同一设备的流程节点共用 `LocalCameraSession`，已打开会话会被复用。要改变打开参数，应先点击窗口内的“关闭”，再设置并重新连接。
3. 设置曝光、增益、平均次数和校正模板，按需要勾选“保存文件”，点击“测量”。成功后本窗口直接显示内存中的 RAW 图像，并在存在 CIE 数据时挂载相应数据；关闭“保存文件”仍可显示当前测量图像。该手动窗口路径不写入流程测量主记录，也不发布流程结果通知。

本地取图按校正模板中保存的文件引用与校正类型解析资源，引用丢失或文件未同步会失败，不自动回退为校正组的默认文件。四色校正窗口自动带入文件共用此资源解析。

窗口使用 `Device.DisplayConfig` 的曝光、增益、平均次数和翻转参数，文件开关为 `Config.UsingFileCaching`。从节点打开时，会先把节点参数应用到窗口，后续修改再同步回该节点；这会改变设备显示配置和节点参数，不是只读查看。

关闭窗口会保存偏好、停止该窗口的预览并在 Live 模式下解绑回调，**保留共享相机会话**，供后续本地流程使用。需要断开相机时使用窗口内的“关闭”按钮。测量过程中窗口暂不能关闭；扫描相机 ID 时的关闭请求会等待扫描结束。

### 普通 L/BV 节点的本地转发

`FlowEngineLib.LVCameraNode` 在 Engine 内实现，通过稳定保存标识兼容旧流程；执行时直接按 `DeviceCode` 查找已加载的逻辑相机，每次执行优先按实际会话归属选择后端。对应设备已打开本地测量会话时复用该会话；相机关闭、启用“使用本地相机”且服务未占用时，使用设备的 Camera ID、采集模式和位深自动打开本地测量会话，再调用共用采集链。打开成功后保留共享会话并显示本地打开状态；再次取图复用会话。服务已经打开时仍使用服务，修改偏好不切换已打开的后端；服务占用尚未确认释放、本地视频占用或 Live 模式不能据此强行打开测量会话。打开或取图失败时按节点原有失败策略处理，不补发服务请求。CV、循环 `.For` 和通用相机节点仍使用原有执行方式。旧流程继续使用原节点身份。临时保存为 Engine 类型的 BV/LV 节点通过名称回退加载，保存或执行数据库节点更新时写回 FlowEngineLib 标识；参数和连线保留。

本地分支使用节点的曝光、增益、平均次数、校正模板和翻转；校正组存在增益配置时仍覆盖节点增益。POI、POI Filter 和 POI Revise 不解析、不执行，保存在节点中的这些模板配置不变，服务分支仍完整传递它们。此处忽略的是 POI 修正，取图校正模板仍生效。

结果归入原流程 `SerialNumber` 对应的批次和节点 `ZIndex`，保存图像主记录，以 `MasterResultType=100` 和 `MasterId` 交接结果，通过 `SetCurrentFrame` 交给下游并发布结果通知。相机结果视图的自动刷新开启时创建并提交设备预览；关闭时跳过预览快照，不复制 RAW/CIE 或转换显示图。文件保存遵循显示配置 `SaveLocalCaptureFiles`（默认开启）及 `IsCVCIEFileSave`；不会另建手动取图批次。

转发沿用原节点消息 ID、超时和停止处理。采集返回前命令已超时或流程已停止时，晚到帧会释放，不写结果记录、不继续下游；原生采集不能即时中断，采集链已经生成的文件可能保留。流程启动及其它服务节点的 MQTT/服务配置前提不变，转发一个相机节点不代表整个流程可脱离服务运行。

### 在流程中使用本地取图

`LocalCameraNode` 按 `DeviceCode` 精确查找已加载的逻辑相机，使用节点自身的取图参数：

| 参数 | 默认值与约束 |
| --- | --- |
| `ExpTime` | 100 ms，必须有限且大于 0；三通道使用同一个曝光值 |
| `Gain` / `AvgCount` | 0 / 1；增益必须有限且非负，平均次数至少为 1 |
| `CalibTempName` | 空；非空时须按名称找到该物理相机的校正模板 |
| `AutoConnect` | `true`；会话未打开时按设备配置连接，拒绝 Live 模式、空 `CameraID` 和原生打开错误 |
| `IsAutoExp` / `SaveFiles` | 均为 `false`；分别控制本地自动曝光和文件保存 |
| `FlipMode` | `None`；方向及校正顺序由本地帧处理链执行 |

关闭自动连接后，须先通过本地相机管理建立测量会话，否则提示“本地相机尚未打开”。`LocalCameraCaptureService` 也会拒绝 Live 模式测量。此服务的进程级 `CaptureLock` 串行化所有本地测量请求，同时通过设备会话锁访问句柄；设备不同也不表示这些测量会并行执行。

取帧后节点查找 `action.SerialNumber` 对应的流程批次，保存测量主记录，再经 `SetCurrentFrame` 交接内存帧并发布 `ResultMessageBus` 通知。找不到批次或保存主记录失败会使节点失败。**`SaveFiles=false` 只跳过图像文件保存，仍写数据库并向下游交接帧。** 相机结果视图的自动刷新开启时，节点复制内存预览，按设备只保留最新待显示快照；关闭时跳过快照创建及显示转换，仍保存结果记录并向下游传递原内存帧。主面板手动取图的显式显示请求仍创建快照。预览转换或显示错误记录到日志，不改变已有采集和数据库成功结果。已生成的最新快照可随对应记录重选；同时关闭存图和自动刷新时，本次结果没有供历史重选的图像，重新开启自动刷新只影响后续取图。

### 流程帧的寿命与读写限制

`SetCurrentFrame` 把 `LocalFlowFrame` 的根引用交给 `FlowRuntimeResources`，键含 FrameId，并把当前 FrameId 写入 action.Data。更换当前帧只改变下游定位，不自动移除不同 FrameId 的旧资源；同一流程多次取图可能保留多帧，直到流程资源释放。不能把“当前帧”理解为全流程只有一个缓冲区。

复制 `CVStartCFC` 会共享 RuntimeResources；流程进入 `DoFinishingCore` 后在 finally 中释放这些资源。消费者应在根引用仍有效时调用 `Acquire()`，持有并最终 Dispose `LocalFlowFrameLease`。已取得的租约延长共享存储寿命；根对象 Dispose 后不能再从该根 Acquire，即使其它租约仍存活。租约自己的 Dispose 幂等，之后访问其指针会抛 ObjectDisposedException。

**租约不是不可变图像快照。** 下游 `LocalCalibrationNode` 可对同一帧执行 `CalibrateInPlace`：修改 RAW、重新分配 CIE、更新 Metadata，再处理方向。`ResizeCieBuffer` 会释放旧 CIE 地址，不等待其它租约归零；租约保留取得时的 Metadata/MasterId，而指针和长度读取共享存储。因此跨线程长期保留指针或同时执行预览和校正，不能仅靠 Acquire 保证数据一致或地址稳定；同步读写或生成独立快照的协议尚需由异步消费者补齐；当前相机设备预览在交接下游前复制 RAW/CIE，UI 不持有原生指针或流程帧租约。

`IsMirrorReady` 与缓冲区各自的 flip 状态也要一起判断：有 CIE 时最终翻转可只作用于 CIE，RAW 仍保留传感器方向；无校正模板的节点可发布尚未应用方向的 RAW。不能仅凭 FlipMode 判断显示坐标已与 POI 一致。设备视图快照的范围及后续优化见[内存预览设计](../../02-developer-guide/engine-development/local-camera-memory-preview.md)。

### 本地文件保存位置

本地测量的保存开关开启时，`LocalFrameFileService.SaveCapture` 按下列规则写文件：

- 根目录取 `Device.Config.FileServerCfg.DataBasePath`；为空时使用用户“文档”目录下的 `ColorVision`。
- 子目录为 `<DeviceCode>/Data/yyyy-MM-dd`，文件名为 `Local_yyyyMMdd_HHmmss_fff.cvraw`，有 CIE 数据时再保存同名 `.cvcie`。
- 文件逐个保存；后续文件失败不会撤销已写出的文件。流程节点在文件保存之后写数据库，数据库失败也可能留下已生成的图像。

因此应分别确认采集、文件和数据库结果，不能仅凭文件存在判断整个节点成功。图像转换与导出格式见[CVRAW/CVCIE 图像导出](../../04-api-reference/engine-components/cv-image-export.md)。

## 本地视频与实时伪彩

控制面板“视频模式”的打开在后台任务中执行，受句柄锁和打开中标志保护。它使用独立于 `LocalCameraSession` 的句柄，可能重新解析 `CameraID`，成功后保存新 ID 和本地显示偏好。关闭时注销回调并关闭相机；失败时应检查具体错误及设备占用。

`CameraRealtimeFramePipeline` 将原始回调帧交给 ImageEditor 的 `RealtimeFramePresenter`。presenter 在 UI Dispatcher 应用 FlipX/FlipY，并把帧交给共用 `ImageStreamPresentation`；颜色表与范围来自每视图 `ImageDisplayEffects`，不从工具栏发现状态。可变源复制并冻结后才进入后台伪彩，最多一帧执行、一帧等待，新输入覆盖等待帧；参数变化、换图、流重置和释放后的结果必须通过有效期检查才能发布。

视频启动先清理上一文件的路径、打开器工具、图层和测量状态，再重置实时帧队列。旧 CVRAW 后台读取即使晚到也不能覆盖视频，旧文件的校正/POI 数据不能继续作用于实时帧；重新启动视频会丢弃上一轮尚未显示的帧。实时输入继续使用 `DispatcherPriority.Background` 调度，给鼠标/键盘输入留出机会。此入口不受结果列表的 `AutoRefreshView` 控制。

帧发布以 `CommitSourcePixels` 登记与显示结果对应的同一帧原图，再经 `ImagePresentation` 发布处理显示。关闭伪彩或处理失败时恢复该帧原图；第一次没有基准源时也先发布原图，随后建立尺寸、像素格式和缩放状态。冻结快照、队列和重入保护的完整契约见[连续帧显示](../../04-api-reference/ui-components/image-editor-context.md#连续帧显示)。这会更新预览文档的源与 revision，不改写相机采集缓冲、流程测量数据或结果文件。

对焦清晰度指标独立由 `CameraFocusFrameProcessor` 计算，输入是原始帧的拥有型副本及 ROI/算法请求；该处理器不生成伪彩图。其双缓冲保留一帧工作和一帧等待，单次处理异常记录日志后继续接收，关闭时等待 worker 退出再释放缓冲。`CameraRealtimeFramePipeline` 以自身 generation 拒绝停流后指标，指标叠加与图像显示分别更新。十字参考线使用独立 `VideoCrossGuideProcessor`，不能把相机指标生命周期等同于视频文件播放的 `VideoPlaybackSession`。

## 查询和显示相机结果

| 操作或设置 | 当前行为 |
| --- | --- |
| 收到新的远程或本地结果通知 | 按当前 `DeviceCode` 筛选，再按 `MasterId` 读取主记录并加入列表；默认插在开头 |
| `AutoRefreshView` | 默认 `true`，收到新结果时自动选择并滚动到该行；关闭后仍加入记录，但不自动切换选中图像 |
| “查询” | 先清空列表，再查询整个相机结果表；**没有自动限定当前设备**。默认按 ID 降序取 50 条；`Count <= 0` 时不限制条数 |
| “高级查询” | 使用用户设置的查询条件，同样不自动附加当前相机条件 |
| 结果视图设置 | 齿轮打开 `ViewCameraConfig`，相机视图共用该配置，可设置数量、排序和自动选择行为 |
| CSV 导出 | 先选择一条记录；只导出该记录。保存对话框确认后代码会追加 `.csv`，文件名无需再次填写此后缀 |
| 清空列表或删除选中行 | 只移除当前视图集合中的行，不删除数据库记录或图像文件 |

最新本地结果选择时使用 `LocalCameraPreview` 的独立快照；其它结果的显示链仍是 `ViewResultImage.FileUrl → OpenImage(string?) → ImageView.OpenImage(filePath)`，空路径清空图像。`SaveFiles=false` 的本地流程结果可立即预览，也继续供下游使用；新的本地结果替换旧快照后，旧的无文件记录无法重新打开。主面板和本地管理窗口的 RAW 预览均使用 CVRAW 解码链的 BGR 显示约定和源行步长；16 位三通道只在显示副本中转为 WPF 的 RGB48，不交换绿蓝通道、不缩放采样值、不修改源缓冲；校正处理和可选 CIE 真彩显示是独立步骤。预览方向调整只作用于显示副本；RAW 与 CIE 色彩/坐标的实机验证和进一步性能优化见[内存预览设计](../../02-developer-guide/engine-development/local-camera-memory-preview.md)。

自动流程在 `AutoRefreshView=false` 时跳过内存预览复制，结果仍可加入列表；主面板手动取图的强制显示请求不受此开关阻止。直接快照显示先清理文件状态，带完整 CIE 时继续挂载内存测量数据。实时帧、取图快照和 CVRAW 文件槽位分别拥有自己的像素：覆盖或统一释放文件/校正缓存不会释放已经显示的独立帧。文件读取缓冲复用也不会消除实时流和取图快照现有的复制与冻结成本。

有记录但无图时，先检查选中行、`FileUrl` 和文件加载；出现其它相机记录时，核对是否执行过全表查询。设备右键菜单 `CameraLog` 从配置的主服务目录查找最新相机日志，可结合命令终态及错误消息定位远程失败。

## 设备关联与资源释放

`DeviceCamera` 是逻辑相机；`Config.Code` 标识消息中的 `DeviceCode`，`Config.CameraCode` 关联物理相机，`Config.CameraID` 用于打开硬件。

`AttachPhyCamera` 只维护对象关联和 `ConfigChanged` 订阅。`DeviceCamera.Save()` 才解析新的 `CameraCode`、释放旧关联、调用 `PhyCamera.SetDeviceCamera` 并同步物理参数，同时保留逻辑服务的 `CameraID`，最后进入[设备配置持久化与服务重启契约](./configuration.md)。`SetDeviceCamera` 会保存许可证中的 `DevCameraId`；许可证关联了校准设备时，还可能请求该校准服务重启。

`DeviceCamera.Dispose()` 幂等释放已经创建的控制面板/结果视图、物理订阅、本地会话、校正缓存和 MQTT 服务；不会为释放操作创建懒加载视图。

## 验证范围

- `DeviceCameraAssociationTests` 覆盖关联/解绑对象不改许可证中设备 ID 的断言；不覆盖 `Save()`、数据库写入和服务重启。
- `CameraViewLifecycleTests` 覆盖结果列表解绑的幂等性、事件/绑定清理；不证明完整视频或硬件生命周期。
- `CameraPreviewFileHandoffTests` 使用真实 WPF 视图和软件构造的帧检查文件→视频的过期读取拒绝、旧校正/POI 清理、预览重启丢弃待显示帧、8/16 位单/三通道副本独立、统一释放缓存、视频→文件及 RAW/CIE 直接快照→文件切换；`DeferredDeviceViewTests` 检查自动刷新开关及手动强制显示。这些测试不连接真实相机，不覆盖驱动回调时序与现场长时间运行。
- `LocalCameraSessionTests` 覆盖物理配置 JSON 的 14 个字段映射及全帧零 ROI。`LocalCameraOwnershipTests` 用原生替身检查复用、失败状态、参数冲突及关闭后重开；`CameraBackendRoutingTests` 覆盖软开关、当前会话路由、占用和服务心跳隔离；`LocalCameraResultTests` 覆盖默认保存及关闭保存、独立预览副本、方向、非对齐行、与 CVRAW 解码像素的一致性、结果文件字段和曝光回填。它们不打开真实硬件、不写实际业务数据库。
- `LocalFlowNodePortTests.LocalFrameLivesAcrossNodeCopiesAndEndsWithFlow` 检查节点副本共享帧及流程结束后不能再 Acquire；该用例在结束前已释放租约。`LocalFrameMirrorTests` 检查 RAW/CIE 各自的方向、校正准备及幂等翻转，不覆盖异步预览与校正并发。
- `LvCameraLocalForwardingTests` 用真实流程节点及采集/保存替身检查连续 L/BV 转发、参数和批次交接、POI 忽略、服务请求保留、CV 范围隔离、失败不回退和超时/停止后的资源清理；不连接硬件或业务数据库。
- `VideoProcessorResilienceTests` 覆盖对焦与十字参考线后台处理异常后继续运行；`ImageDisplayEffectsTests` 覆盖参数捕获的基准源、启用与存活门禁，以及不可变参数和无发布副作用。`ImageStreamPresentationTests` 检查冻结源、有界等待帧、过期拒绝和失败原图回退。它们不调用真实相机或 native 伪彩 DLL；实际 FlipX/FlipY、缩放和指标位置仍需按输入与设备验证。
- 已授权设备环境中的远程完成消息、校准资源、超时结果归属、句柄互斥和文件显示仍需现场验收；源码核对与文档构建不能替代这些证据。
