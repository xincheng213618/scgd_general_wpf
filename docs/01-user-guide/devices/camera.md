---
knowledge_id: "operations.camera"
knowledge_type: "topic"
status: "current"
summary: "本地优先与服务兼容的相机控制、共享会话、无文件内存预览；明确后端占用、自动曝光边界、文件/数据库完成及帧寿命。"
aliases: ["BV/LV本地转发", "LVCameraNode", "使用本地相机", "本地相机优先", "相机拍图", "相机服务", "手动采集成功流程失败", "采集超时", "无文件预览", "本地相机管理", "本地相机取图", "视频模式", "相机结果查询", "是否重启服务", "CameraLog", "DeviceCamera", "MQTTCamera", "DisplayCamera", "ViewCamera", "CameraLocalWindow", "LocalCameraNode", "LocalCameraSession", "LocalFrameFileService", "SaveFiles", "AutoRefreshView", "本地相机尚未打开", "LocalFlowFrame", "LocalFlowFrameLease", "LocalFlowFrameRuntime", "SetCurrentFrame", "TryAcquireCurrentFrame", "FlowRuntimeResources", "本地帧租约", "流程帧内存", "SaveFiles=false", "CIE重新分配", "CameraFocusFrameProcessor", "CameraRealtimeFramePipeline"]
code_paths: ["Engine/ColorVision.Engine/Services/Devices/Camera/Local/LocalLvCameraExecution.cs", "Engine/ColorVision.Engine/FlowProcessing/Nodes/Compatibility/Camera/LVCameraNode.cs", "Engine/ColorVision.Engine/Services/Devices/Camera/DeviceCamera.Local.cs", "Engine/ColorVision.Engine/Services/Devices/Camera/DeviceCamera.Commands.cs", "Engine/ColorVision.Engine/Services/Devices/Camera/Local/CameraBackendState.cs", "Engine/ColorVision.Engine/Services/Devices/Camera/Local/LocalCameraNative.cs", "Engine/ColorVision.Engine/Services/Devices/Camera/Local/LocalCameraAutoExposure.cs", "Engine/ColorVision.Engine/Services/Devices/Camera/Local/LocalCameraPreview.cs", "Engine/ColorVision.Engine/Services/Devices/Camera/Local/LocalCameraResultService.cs", "Engine/ColorVision.Engine/Services/Devices/Camera/DeviceCamera.cs", "Engine/ColorVision.Engine/Services/Devices/Camera/MQTTCamera.cs", "Engine/ColorVision.Engine/Services/Devices/Camera/DisplayCamera.xaml.cs", "Engine/ColorVision.Engine/Services/Devices/Camera/Views/ViewCamera.xaml.cs", "Engine/ColorVision.Engine/Services/Devices/Camera/Local/LocalCameraCaptureService.cs", "Engine/ColorVision.Engine/Services/Devices/Camera/Video/CameraRealtimeFramePipeline.cs", "Engine/ColorVision.Engine/Services/Devices/Camera/Video/CameraFocusFrameProcessor.cs", "Engine/ColorVision.Engine/FlowProcessing/Nodes/LocalCameraNode.cs", "Engine/ColorVision.Engine/Services/PhyCameras/PhyCamera.cs", "Engine/ColorVision.Engine/Services/Devices/Camera/DisplayCamera.xaml", "Engine/ColorVision.Engine/Services/Devices/Camera/CameraLocalWindow.xaml", "Engine/ColorVision.Engine/Services/Devices/Camera/CameraLocalWindow.xaml.cs", "Engine/ColorVision.Engine/Services/Devices/Camera/Local/LocalCameraSession.cs", "Engine/ColorVision.Engine/Services/Devices/Camera/Local/LocalFrameFileService.cs", "Engine/ColorVision.Engine/Services/Devices/Camera/Views/ViewCamera.xaml", "Engine/ColorVision.Engine/Services/Devices/Camera/Views/ViewCameraConfig.cs", "Engine/ColorVision.Engine/Abstractions/ViewConfigBase.cs", "Engine/ColorVision.Engine/FlowProcessing/Runtime/DisplayFlow.xaml.cs", "Engine/ColorVision.Engine/Services/Devices/Camera/Local/LocalFlowFrame.cs", "Engine/FlowEngineLib/Base/FlowRuntimeResources.cs", "Engine/FlowEngineLib/Base/CVStartCFC.cs", "Engine/ColorVision.Engine/FlowProcessing/Nodes/LocalCalibrationNode.cs", "Engine/ColorVision.Engine/Services/Devices/Camera/Local/LocalFrameCalibrationService.cs", "UI/ColorVision.ImageEditor/Realtime/RealtimeFramePresenter.cs", "UI/ColorVision.ImageEditor/Presentation/ImageStreamPresentation.cs"]
test_paths: ["Test/ColorVision.UI.Tests/LvCameraLocalForwardingTests.cs","Test/ColorVision.UI.Tests/CameraBackendRoutingTests.cs","Test/ColorVision.UI.Tests/LocalCameraOwnershipTests.cs","Test/ColorVision.UI.Tests/LocalCameraResultTests.cs","Test/ColorVision.UI.Tests/CameraViewLifecycleTests.cs","Test/ColorVision.UI.Tests/DeviceCameraAssociationTests.cs","Test/ColorVision.UI.Tests/ImageDisplayEffectsTests.cs","Test/ColorVision.UI.Tests/VideoProcessorResilienceTests.cs","Test/ColorVision.UI.Tests/LocalCameraSessionTests.cs","Test/ColorVision.UI.Tests/LocalFrameMirrorTests.cs"]
related: ["engine.devices", "operations.device-configuration", "operations.physical-camera", "operations.camera-configuration", "engine.camera-preview-plan", "ui.image-editor-context"]
---

# 相机服务、采集与结果视图

本页说明如何使用远程相机、本地相机管理和流程取图节点，以及怎样判断采集完成、查找结果和处理无图问题。物理发现、许可证和资源导入见[物理相机管理](./camera-management.md)，参数来源及覆盖顺序见[相机配置](./camera-configuration.md)。

## 选择采集入口

| 入口 | 执行方式与结果 |
| --- | --- |
| 相机控制面板的“连接”“取图” | 显示配置决定下次打开的后端；取图跟随当前实际打开的会话，本地取图直接预览内存图，默认保存图像和结果记录 |
| 设备右键菜单 `Local`，或本地相机节点的“相机管理” | 打开 `CameraLocalWindow` 本地相机管理；在本进程连接、测量，并直接显示内存图像 |
| 相机控制面板的“开始预览” | 本地后端复用 `LocalCameraSession` 打开 Live/8-bit 预览；服务后端须先断开服务相机，再使用独立本地视频句柄 |
| 流程节点“本地相机取图” | `LocalCameraNode` 取得本地测量帧，保存结果主记录，再交给流程下游 |
| 流程节点“L/BV相机” | `LVCameraNode` 复用当前打开的本地会话；相机关闭且启用“使用本地相机”时先自动打开本地测量会话，再取图；服务已占用或未启用本地偏好时保留服务请求。本地分支忽略 POI、过滤和修正 |
| 相机属性 → 校准与校正 → 用户校正 | 带入当前相机，选择最近拍摄图像、导入文件或取图后进行单点 / RGBW 修正；见[用户校正](./calibration.md#四色校正采集) |
| 设备结果视图 | `ViewCamera` 展示结果记录；已生成预览快照的最新本地结果优先使用该快照，其它记录按 `FileUrl` 打开文件 |

在相机属性的“采集与显示 → 显示配置”中编辑“使用本地相机”。该软开关持久化在本机 `DisplayCameraConfig.UseLocalCamera`，默认关闭，只决定下次打开的后端，不更改服务端自动打开设置。主面板“连接”使用测量模式；上次配置为 Live 时进入普通拍照，视频由视频按钮开启。可以在已打开、取图或关闭过程中修改，当前连接、取图、自动曝光和关闭仍跟随实际会话；须先关闭当前连接，再打开才应用新偏好。主面板不显示此开关或后端说明文字。服务不可用且未观察到占用时，选择本地后仍提供“连接”入口，但不会把逻辑设备状态改成已打开。

“海康使用新取图”（`DisplayCameraConfig.UseHikMvs`）也可在本地相机管理中勾选，默认开启，已有本机配置缺少此字段时同样开启。对 `HK_USB` 的 BV/LV/LVTOBV 本地会话，开启使用 MVS，关闭使用旧 SCGDCamLayer；CV 滤轮模式和其它相机型号保持原路径。修改只保存本机偏好，不关闭相机、不改变当前连接；仅在下一次实际打开前设置 C++ 后端。“MVS 插值质量”（`DisplayCameraConfig.HikBayerQuality`）可选 0 快速、1 均衡、2 最优、3 最优+，默认 3；旧配置缺少此字段时同样为 3。只影响 MVS 彩色转换，旧后端和灰度取图忽略该质量项；和后端偏好一样，仅在下次实际打开前下发，当前连接继续使用打开时的质量。“MVS 输出 BGR（兼容旧流程）”（`DisplayCameraConfig.HikOutputBgr`）默认开启，旧配置缺少此字段时也开启：将 MVS 的 RGB 输出交换红蓝通道，以匹配旧 SCGDCamLayer 的 BGR 输出和现有校正输入。关闭保留 RGB 供对照；此时现有 BGR 校正模板的结果不能作为正常生产测量。该选项同样只在下次打开前下发，不影响旧后端及灰度取图。本地主面板、视频、管理窗口和流程复用同一会话；切换视频/拍照保留当前连接的后端、插值质量及通道顺序偏好。MVS 打开时自动设置单帧软件触发并验证读回，取图期间沿用该设置，关闭或打开失败时恢复原相机参数；无需先在海康客户端设置 Trigger。对照时在相同曝光、增益、ROI、平均次数、校正、翻转及保存设置下分别连接采集；本地相机管理的 `[CameraCaptureCompare]` 日志中 `HikUseMvs`、`HikBayerQuality` 和 `HikOutputBgr` 记录本次连接实际设置的后端、质量与通道顺序偏好，不随当前配置变化。

本地会话通过 `DeviceCamera` 调用 `LocalCameraSession` 和 `LocalCameraCaptureService`，保留 `MsgRecord` 成功/失败通知，不发布 MQTT 相机指令。主面板取图和自动曝光要求会话已打开；视频中取图会先切换到拍照模式，仍使用本地后端，不应用新后端偏好；流程节点的 `AutoConnect` 是独立的显式本地入口。服务已打开或正在操作时，不能再创建本地会话；曾观察到服务占用后，即使服务离线/未知也不视为已释放，须收到 `Closed`。可先选好本地偏好，再用“断开”关闭当前服务会话，随后重新打开。启动时尚未观察到服务占用可尝试本地打开，最终仍以原生 SDK 的独占打开结果为准。应用内同 CameraCode/CameraID 的其它逻辑相机以及独立视频也参与打开前的占用检查；外部进程的并发打开仍依赖 SDK 独占保护。

本地会话与原始服务状态分别保存；`DService.DeviceStatus` 在本地实际打开后显示本地状态，覆盖逻辑相机自身状态。服务心跳只更新服务侧记录，不能把本地会话覆盖成离线或阻止该会话取图；修改偏好也不改变当前状态或路由。即使开关关闭，通过 Local 窗口/节点显式打开的会话也取得当前设备的本地归属，主面板可取图、自动曝光并关闭该会话。关闭后恢复逻辑服务状态，下次打开重新读取偏好。

本地视频和拍照切换先停止当前预览队列并注销原生回调，避免旧视频帧覆盖测量图。MVS 普通拍照与视频共用软件触发配置：位深相同时清空 SDK 缓冲并切模式；8/16 位不同时停止取流、设置并读回原生像素格式，再恢复取流，保持相机连接、ROI、曝光、增益和打开时的转换偏好。旧本地后端仍关闭并重开同一管理器及相机 ID，全程保留本地归属；任何失败都终止当前请求，不补发服务请求。MVS 切换失败尝试恢复先前像素格式和取流状态；恢复失败时关闭连接并保留原始错误。该优化仅作用于实际本地会话，服务及其独立视频路径保持原有行为。

共享本地会话的连接入口在 `CameraID` 为空时按当前 `CameraModel` 搜索相机：有 `CameraCode` 时仅选择唯一匹配其 MD5 标识的相机；没有绑定且只发现一台时自动选择。未发现相机、绑定无法唯一匹配或存在多台未绑定相机时明确提示，在本地相机管理中刷新并手动选择 ID 后再连接，不自动选择列表中的第一台。显式选择或已有非空 ID 直接用于连接；自动获取的 ID 仅在连接成功后保存。已打开的共享会话继续复用，不重新扫描。

本地主面板及本地相机管理窗口在测量模式下，独立“自动曝光”按钮通过 `CM_GetAutoExpFrame` 回填曝光、饱和度和显示配置，并将达标的最终帧直接显示在图像区；不保存图像文件、不写测量结果记录，也不受“本地取图保存文件”开关控制。通常复用曝光搜索的最后一帧；同步频率处理选择了之前的曝光值时重新取该曝光的帧并验证达标。失败或达到曝光边界但未达标时提示具体错误，不替换当前显示图。管理窗口在视频模式下沿用曝光更新及实时回调显示；主面板独立自动曝光会先进入测量模式，再显示达标帧。本地取图中的自动曝光仍使用 `CM_GetAutoExpTime`，随后按取图参数执行平均及校正，在生成帧元数据之前完成曝光更新。主面板的独立自动曝光和取图前自动曝光分别使用各自选中的 V1/V2 模板：上方模板配合“自动曝光”按钮执行，下方模板配合“取图”执行；下方选择 Empty（不使用）则直接取图，选择有效模板则先自动曝光。本地和服务遵循同一选择规则，不另设拍前曝光开关。执行前复制模板参数，并通过 `Cfg_ExpTime` 下发：V1 保留原字段单位，V2 保留 `type` 和 ROI 等 JSON 参数；模板无效时报告失败，不沿用上一次配置。没有可选模板时，本地显示“本机参数”，通过齿轮编辑并保存到显示配置。服务模式保留独立自曝和拍前自曝各自的模板；本地不显示服务 HDR 模板。其它本地调用传入非空 HDR 模板仍明确失败。`IsAutoExpWithND=true` 和非空 HDR 模板会明确报不支持；ND 手动控制、对焦、电机操作在主面板本地模式下隐藏。校正模板仍由校正组覆盖增益，资源按模板文件引用解析，不要求服务校准设备在线。

本地主面板取图按原有手动取图含义创建独立批次（不归档）和测量图像记录，数据库保存成功才报告命令成功。显示配置中的“本地取图保存文件”（`SaveLocalCaptureFiles`）默认开启，包括已有配置缺少此字段的情况；开启时按本地文件规则保存 CVRAW，并包含已执行的色度校正参数。关闭文件保存仍预览内存图并写数据库。此选项用于主面板/POI/定时本地取图和本地相机管理窗口；流程中的 `LVCameraNode` 本地转发和 `LocalCameraNode` 使用节点自己的保存设置。已取到图但数据库保存失败时显示当前图并报告失败，不把预览成功当作落库成功。

相机卡片和结果详情的登记、首次显示、首结果及提前释放边界见[设备详情视图按需初始化](../../04-api-reference/engine-components/device-service-chain.md#设备详情视图按需初始化)。

“本地视频有画面”只说明该预览路径可用。排查手动取图与流程结果不一致时，先核对实际入口、设备、曝光参数、校准模板和结果文件。

这些采集入口会访问硬件。自动曝光、ND/滤轮切换、对焦和电机移动还会改变设备状态，应在已授权的设备与操作范围内使用。

## 使用远程相机取图

1. 在相机控制面板确认设备配置，点击“连接”。`Open_Click` 发送当前 `CameraID`、采集模式和位深；成功响应后才切换到已打开的控制状态。
2. 设置曝光等参数，选择校准、自动曝光和 HDR 模板。`GetData_Click` 要求自动曝光及 HDR 下拉框各有一个有效的 `ParamBase` 选择；缺少选择时直接返回，不发送请求。空模板是有效选择，不等于启用对应功能。非空校准模板还要求物理相机、许可证中的校准服务关联和相应校准资源；无校准选择时回退为空模板。
3. 点击“取图”，核对本次命令终态及新增结果。`MQTTCamera` 返回的 `MsgRecord` 只是请求记录，方法返回、按钮恢复或仍显示上一张图都不能作为采集成功依据。

这些发送前检查属于手动按钮路径；其它调用入口应核对自己的检查及参数来源。自动曝光/对焦的参数回填要求返回消息匹配 `DeviceCode` 且 `Code == 0`；对焦的 `Code == 102` 是中间响应，可更新位置和临时图。

### 失败、超时与重启提示

手动取图发送前记录该设备最新结果 ID。命令超时后，`TryHandleCaptureTimeoutFromDatabase` 查找该设备新增结果并刷新列表：`ResultCode == 0` 表示数据库已有成功记录，非零则展示数据库失败信息；没有新记录才继续显示超时提示。该回查按设备和新增 ID 匹配，未严格关联此次 `MsgID`，多路并发时仍需核对结果归属，避免重复采集。

取图失败后的“是否重启服务？”会调用 `DisplayFlow.RestartColorVisionServicesAsync`：依次停止 `RegistrationCenterService`、`CVMainService_x64`、`CVMainService_dev`，再按同一顺序启动，并尝试重新连接注册中心。它会影响共用这些服务的其它设备。某一步失败会中断后续步骤，没有整体回滚；方法结束也不保证注册中心已重连。确认重启后仍应检查服务与设备状态，该操作不会自动重新取图。

## 使用本地相机管理

1. 在逻辑相机右键菜单选择 `Local`，或在“本地相机取图”节点选择“相机管理”。窗口加载会初始化本地 SDK；许可证文件缺失时，入口还可能写出本地许可证。
2. 在未连接状态选择采集模式和位深，点击“连接”。相机 ID 为空时按共享会话的规则自动搜索和选择；多台或绑定不匹配时先刷新并手动选择 ID。测量使用 `Measure_Normal` 等测量模式；`Live` 用于实时画面。窗口与同一设备的流程节点共用 `LocalCameraSession`，已打开会话会被复用。连接后可直接切换采集模式，视频中点击“测量”也会自动进入拍照模式。相机 ID、型号和拍照位深的配置修改仍需先关闭。MVS 只提供普通拍照和视频，快速/快速扩展选项禁用。
3. 设置曝光、增益、平均次数和校正模板，按需要勾选“保存文件”，点击“测量”。成功后本窗口直接显示内存中的 RAW 图像，并在存在 CIE 数据时挂载相应数据；关闭“保存文件”仍可显示当前测量图像。该手动窗口路径不写入流程测量主记录，也不发布流程结果通知。

“海康取图测试”是临时诊断入口：先关闭 cvCamera、服务或视频占用的相机并停止流程，再从这里打开独立测试窗口。窗口复用 Engine 可执行程序的诊断启动参数，独立进程不加载插件、设备服务或数据库；需要已安装海康 x64 MVS 运行库和驱动。启动时读取本机的 MVS 插值质量，默认 3；测试窗口也可独立选择 0–3，连接后按本次打开的质量取图。点击“连接相机”后，每点一次“取一张”才执行一次软件触发，不自动连拍；对所选 GigE / USB3 相机将 Bayer16 转成 RGB48，连接及输出缓冲跨次复用，点击“关闭相机”或退出窗口后释放测试会话；不校正、翻转、自动存图或写入数据库。点击“取一张并保存原始帧”会额外触发一帧，将插值前的 Bayer16 数据及 JSON 参数保存为一个 ZIP（不压缩原始数据）；旧帧不保留。ZIP 内 `frame.bayer16.raw` 原样保存 SDK 输入缓冲，`metadata.json` 记录尺寸、像素格式及 Bayer 排列、字节序、有效图像与完整载荷长度、ROI、曝光、增益、帧号、SDK 版本和相机参数。载荷不是紧密排列时行跨度标为 null，需要先核对 Chunk/载荷布局。保存路径、帧号与保存耗时写入日志，文件需手动带回，不会随反馈自动上传。导出沿用当前采集线程，在归还 SDK 缓冲前分块写入；保存期间不能再次取图，取消时删除本次未完成文件，已存在的目标文件只在完整写入后替换。普通取图不增加原始帧复制或缓存；保存耗时从取图及线程 CPU 计时中排除。首次帧单独列出，后续统计拆分原始帧等待、格式转换和调用线程 CPU 时间，设参时间另列，单帧总计包含设参和取图，手动点击之间的等待不计入耗时，报告可复制。原本地测量与 MVS 测试都会记录带序号的 `[CameraCaptureCompare]` 日志；测试日志写入 `%APPDATA%\ColorVision\Log\HikCaptureTest_*.log`，近期文件会被现有反馈收集器自动带回。曝光按 `CameraManager::SetExpTime` 截断为整数微秒，曝光/增益的读回容差和超时沿用原上层规则；增益输入为 MVS 原值。测试连接时自动配置单帧软件触发，采用与生产 MVS 相同的触发选择、连续采集模式及单帧突发规则，关闭其它触发选择器并读回确认；点击“取一张”只发送一次 `TriggerSoftware`。关闭相机或连接失败时恢复原参数，不写入设备 UserSet；无需先到海康客户端修改 Trigger 配置。相机当前 ROI、ADC 位深、触发帧数及其它读回参数会写入报告；原 `SCGDCamLayer.dll` 的插值算法、白平衡映射和内部启停行为不在本仓库可核对的源代码中，因此此工具只用于定位耗时，不能证明测量结果等价。原始 SDK 缓冲在转换及可选导出后归还；退出等待正在执行的原生调用返回，之后释放测试资源。该入口不替换生产采集路径，诊断完成后可连同 `Services/Devices/Camera/Diagnostics/` 及专用 SDK 引用移除。

本地取图按校正模板中保存的文件引用与校正类型解析资源，引用丢失或文件未同步会失败，不自动回退为校正组的默认文件。四色校正窗口自动带入文件共用此资源解析。

窗口使用 `Device.DisplayConfig` 的曝光、增益、平均次数、翻转和“本地取图保存文件”参数。从节点打开时，会先把节点参数应用到窗口，后续修改再同步回该节点；这会改变设备显示配置和节点参数，不是只读查看。

关闭窗口会保存偏好并停止该窗口拥有的预览；另一窗口已接管回调时不会解绑对方的预览，**保留共享相机会话**，供后续本地流程使用。需要断开相机时使用窗口内的“关闭”按钮。测量过程中窗口暂不能关闭；扫描相机 ID 时的关闭请求会等待扫描结束。

### 普通 L/BV 节点的本地转发

`FlowEngineLib.LVCameraNode` 在 Engine 内实现，通过稳定保存标识兼容旧流程；执行时直接按 `DeviceCode` 查找已加载的逻辑相机，每次执行优先按实际会话归属选择后端。对应设备已打开本地测量会话时复用该会话；相机关闭、启用“使用本地相机”且服务未占用时，使用设备的 Camera ID、采集模式和位深自动打开本地测量会话，再调用共用采集链。打开成功后保留共享会话并显示本地打开状态；再次取图复用会话。服务已经打开时仍使用服务，修改偏好不切换已打开的后端；当前本地 Live 会话会先停止预览并切换为测量，完成后保留拍照模式。服务占用尚未确认释放，或服务路径的独立视频仍占用时，不能据此强行打开本地会话。打开或取图失败时按节点原有失败策略处理，不补发服务请求。CV、循环 `.For` 和通用相机节点仍使用原有执行方式。旧流程继续使用原节点身份。临时保存为 Engine 类型的 BV/LV 节点通过名称回退加载，保存或执行数据库节点更新时写回 FlowEngineLib 标识；参数和连线保留。

本地分支使用节点的曝光、增益、平均次数、校正模板和翻转；校正组存在增益配置时仍覆盖节点增益。POI、POI Filter 和 POI Revise 不解析、不执行，保存在节点中的这些模板配置不变，服务分支仍完整传递它们。此处忽略的是 POI 修正，取图校正模板仍生效。

节点配置面板每次显示时，沿用节点执行时的后端选择规则，判断是否显示“本地相机”分组（保存文件）：所选设备走本地取图时显示，走旧服务时隐藏。该条件通过节点上的 `PropertyVisibility` 声明，属性面板复用字段显隐结果隐藏空分组；设备状态变化后重新打开配置面板即可刷新。

结果归入原流程 `SerialNumber` 对应的批次和节点 `ZIndex`，保存图像主记录，以 `MasterResultType=100` 和 `MasterId` 交接结果，通过 `SetCurrentFrame` 交给下游并发布结果通知。相机结果视图的自动刷新开启时创建并提交设备预览；关闭时跳过预览快照，不复制 RAW/CIE 或转换显示图。先分配 CVRAW 路径并同步保存数据库主记录，再按节点的 `SaveFiles` 保存图像，后续色度参数追加继承相同模式，不会另建手动取图批次。旧画布缺少保存字段时默认保存；原来通过设备显示配置关闭 L/BV 流程存图的配置，升级后需在节点关闭“保存文件”。服务分支的请求协议保持不变，此选项仅控制本地分支。

转发沿用原节点消息 ID 和停止处理。本地分支与本地取图节点一样不启用节点超时，画布中的“最大超时”仅对服务分支生效，旧画布不需修改该值。原生采集不能即时中断，因此不能用节点超时提前结束本地命令，否则重启流程时上一条采集仍可能占用相机。主动停止流程仍生效；采集返回前流程已停止时，晚到帧会释放，不写结果记录、不继续下游，相机命令占用在采集链返回后释放。流程启动及其它服务节点的 MQTT/服务配置前提不变，转发一个相机节点不代表整个流程可脱离服务运行。

### 在流程中使用本地取图

`LocalCameraNode` 按 `DeviceCode` 精确查找已加载的逻辑相机，使用节点自身的取图参数：

| 参数 | 默认值与约束 |
| --- | --- |
| `ExpTime` | 100 ms，必须有限且大于 0；三通道使用同一个曝光值 |
| `Gain` / `AvgCount` | 0 / 1；增益必须有限且非负，平均次数至少为 1 |
| `CalibTempName` | 空；非空时须按名称找到该物理相机的校正模板 |
| `AutoConnect` | `true`；会话未打开时按设备配置连接，空 `CameraID` 自动搜索并按绑定或单相机规则选择；已有本地 Live 会话自动切换为拍照；无法唯一选择相机和原生打开错误仍失败 |
| `IsAutoExp` / `SaveFiles` | 自动曝光默认 `false`，保存文件默认 `true`。保存开启时写完后继续；关闭时仅缓存，数据库记录仍保留 |
| `FlipMode` | `None`；方向及校正顺序由本地帧处理链执行 |

关闭自动连接后，须先建立本地会话；已有视频会话也能自动切换，未连接时提示“本地相机尚未打开”。切换和取图在同一设备会话锁内完成，避免其它本地入口在采集前改回视频。此服务的进程级 `CaptureLock` 串行化所有本地测量请求，同时通过设备会话锁访问句柄；设备不同也不表示这些测量会并行执行。

取帧后节点先生成唯一 CVRAW 路径，查找 `action.SerialNumber` 对应的流程批次并保存测量主记录，然后按节点配置写图像缓存/磁盘，最后经 `SetCurrentFrame` 交接内存帧并发布 `ResultMessageBus` 通知。找不到批次或保存主记录失败会使节点失败，并且不会开始图像保存。`SaveFiles=true` 在图像和参数写盘完成后返回；`SaveFiles=false` 只写缓存。后续参数追加继承缓存条目的模式。旧流程中的 `SaveAsynchronously` 字段忽略，已有 `SaveFiles` 值继续保留；缺少保存字段的旧流程和新建节点均默认保存。

数据库路径在保存和仅缓存两种模式下均可用于缓存查找；仅缓存要求全局 CVRAW 缓存开启。调用只识别磁盘文件的旧服务时，由流程配置者开启“保存文件”，不会为仅缓存图像自动补写。数据库中的取图耗时记录写库前已完成的采集/校正，节点结果另记录保存调用耗时；保存开启时包含等待写盘完成的耗时。整图保存和参数追加在调用线程串行完成，不积压后台整图任务；数据库记录仍先同步写入。

相机结果视图的自动刷新开启时，节点复制内存预览，按设备只保留最新待显示快照；关闭时跳过预览转换，仍保存结果并传递原内存帧。主面板手动取图仍强制显示。历史记录可从仍驻留的 CVRAW 缓存重新打开；仅缓存图像被淘汰或清理后，按普通缺失文件处理。预览转换失败写日志，不改变已有采集和数据库成功结果。

### 流程帧的寿命与读写限制

`SetCurrentFrame` 把 `LocalFlowFrame` 的根引用交给 `FlowRuntimeResources`，键含 FrameId，并把当前 FrameId 写入 action.Data。更换当前帧只改变下游定位，不自动移除不同 FrameId 的旧资源；同一流程多次取图可能保留多帧，直到流程资源释放。不能把“当前帧”理解为全流程只有一个缓冲区。

复制 `CVStartCFC` 会共享 RuntimeResources；流程进入 `DoFinishingCore` 后在 finally 中释放这些资源。消费者应在根引用仍有效时调用 `Acquire()`，持有并最终 Dispose `LocalFlowFrameLease`。已取得的租约延长共享存储寿命；根对象 Dispose 后不能再从该根 Acquire，即使其它租约仍存活。租约自己的 Dispose 幂等，之后访问其指针会抛 ObjectDisposedException。

本地测量取图的 RAW 内存由相机会话持有的 `LocalCameraRawBufferPool` 复用，最多保留一块与最近申请字节数匹配的空闲缓冲。池为空时直接申请，同一流程可同时持有多张图，不等待其它帧归还；最后一个使用者释放后，空闲槽已满或大小不匹配的缓冲直接释放。帧标识、曝光、校正与翻转状态每次独立创建，CIE 内存及文件加载帧沿用直接申请释放。每台相机闲置占用最多为当前一张 RAW 的大小，例如 9568×6380、三通道 16-bit 约 349 MiB；这不是流程在用图像的总内存上限。相机确认关闭或设备释放时清空池，重新打开使用新池，旧帧的晚到归还直接释放。“本地缓存管理 → 相机取图缓冲”按相机列出空闲缓冲占用，支持“释放选中”和“释放全部”，无需关闭相机。手动释放只移除当时空闲的缓冲，不等待或释放仍在用的帧；在途帧之后归还或继续取图时仍可重新形成空闲缓存，连续运行时清理后不保证一直为零。池只复用工作内存，不承担 CVRAW 历史图像缓存或文件保存；验证入口为 `LocalCameraSessionTests` 的多帧租约、尺寸变化、状态重置与关闭重开测试。

节点阶段耗时在 `AllocateFrame` 下记录 `AllocateRawBuffer` 或 `ReuseRawBuffer`，据此区分实际申请与复用。各已完成阶段附带 `ProcessGcPauseMs` 和 `ThreadCpuMs`：前者是阶段期间整个进程的 GC 暂停增量，嵌套阶段不能相加；后者只统计执行线程，不包含相机 SDK 内部工作线程，阶段跨线程完成或计数不可用时为空。CPU 时间粒度较粗，短阶段为零不代表没有计算，墙钟与线程 CPU 的差额也不能全部归因于磁盘等待。慢数据库命令的 `DatabaseCommandTiming` 同样记录进程 GC 暂停增量，便于与卡顿时刻对照。

**租约不是不可变图像快照。** 下游 `LocalCalibrationNode` 可对同一帧执行 `CalibrateInPlace`：修改 RAW、重新分配 CIE、更新 Metadata，再处理方向。`ResizeCieBuffer` 会释放旧 CIE 地址，不等待其它租约归零；租约保留取得时的 Metadata/MasterId，而指针和长度读取共享存储。因此跨线程长期保留指针或同时执行预览和校正，不能仅靠 Acquire 保证数据一致或地址稳定；异步消费者必须自行复制稳定快照。CVRAW 保存返回前完成像素写入或缓存复制，参数尾块独立替换；当前相机设备预览在交接下游前复制 RAW/CIE，UI 不持有原生指针或流程帧租约。

`IsMirrorReady` 与缓冲区各自的 flip 状态也要一起判断：有 CIE 时最终翻转可只作用于 CIE，RAW 仍保留传感器方向；无校正模板的节点可发布尚未应用方向的 RAW。不能仅凭 FlipMode 判断显示坐标已与 POI 一致。设备视图快照的范围及后续优化见[内存预览设计](../../02-developer-guide/engine-development/local-camera-memory-preview.md)。

### 本地文件保存位置

本地测量的保存开关开启时，`LocalFrameFileService.SaveCapture` 按下列规则写文件：

- 根目录取 `Device.Config.FileServerCfg.DataBasePath`；为空时使用用户“文档”目录下的 `ColorVision`。
- 子目录为 `<DeviceCode>/Data/yyyy-MM-dd`，文件名为 `Local_yyyyMMdd_HHmmss_fff_<唯一标识>.cvraw`；色度校正参数保存在 CVRAW 尾部，旧服务生成的 `.cvcie` 保留读取兼容。
- 流程节点先写数据库再保存图像；RAW 与参数保存失败在调用时报告，不会撤销数据库或已写出的内容。主面板和独立本地校正保持原来的同步保存流程。

因此应分别确认采集、文件和数据库结果，不能仅凭文件存在判断整个节点成功。图像转换与导出格式见[CVRAW/CVCIE 图像导出](../../04-api-reference/engine-components/cv-image-export.md)。

## 本地视频与实时伪彩

控制面板按连接操作、采集参数、预览工具三个子控件组织，由外层统一处理状态和操作；子控件不创建相机会话。相机关闭时，“打开”和“视频模式”两个入口铺满顶部一行，可直接选择拍照或视频。本地相机打开后保留“关闭”和视频切换按钮；“关闭视频”保留相机会话并恢复取图，“关闭”结束整个会话。服务模式保持互斥：打开拍照或视频后，顶部只显示“关闭”，释放当前模式后才重新显示两个入口。取图按钮位于取图曝光模板与校正模板右侧，跨两行显示；预览期间隐藏取图，先关闭视频再手动拍照。不显示常驻连接状态文字，不适用于当前后端的操作隐藏。拍照参数使用短标签直接排列，增益、平均和翻转共用一行，位于曝光下方；校正模板前不加标签。自动曝光及 HDR 模板按后端显示；服务模式且配置启用电机时直接显示电机操作。预览时隐藏平均、校正、曝光模板、HDR 和电机，只显示当前预览参数；“区域”按钮展开 ROI 与参考线参数，齿轮保留预览设置入口。

拍照与预览共用 `FlipMode`。预览转换显式映射 OpenCV 方向：X 对应画面上下翻转，Y 对应左右翻转。旧配置同时保存测量翻转与 `LocalVideoTransform` 时以测量翻转为准；仅有旧预览字段时迁移为对应方向，新配置只保存一个翻转值。

普通取图使用面板增益；校正模板关联的组增益优先，服务 HDR 模板也可覆盖手动增益，校正组最终优先。这些测量增益固定时隐藏手动输入。预览仍显示可调整的预览增益，不改校正组配置；本地主面板独立自动曝光也按所选校正组解析测量增益。服务独立自动曝光请求不带增益，沿用相机当前值；服务拍前自曝则在设置本次取图增益后执行，按钮提示区分这两种行为。

CV 测量显示 R/G/B 三路独立曝光，视频预览显示单路曝光。本地 CV 即使仍保留 Live 连接，停止预览后也显示三路测量参数；手动、POI 和定时取图在切回测量模式前按 R/G/B 组装参数，不能用预览曝光覆盖 R 通道。MVS 彩色图像的三个像素通道共用单路曝光；CV 滤轮模式仍由原相机后端实现，不进入 MVS 快速切换路径。

“开始预览”在后台任务中执行，受句柄锁和操作中状态保护。本地后端复用 `LocalCameraSession`；“停止预览”注销回调并停止显示队列，保留连接，之后可再次预览或取图。仅“断开”释放连接。服务模式的独立视频可能重新解析 `CameraID`，成功后保存新 ID 和显示偏好；停止时仍关闭独立视频相机，之后才可连接服务拍照。停止失败不会假装恢复连续画面；保留占用时可再次断开。操作期间连接、取图、预览与参数输入禁用，终态后根据实际归属恢复，不切换后端。

`CameraRealtimeFramePipeline` 将原始回调帧交给 ImageEditor 的 `RealtimeFramePresenter`。presenter 在 UI Dispatcher 应用 FlipX/FlipY，并把帧交给共用 `ImageStreamPresentation`；颜色表与范围来自每视图 `ImageDisplayEffects`，不从工具栏发现状态。可变源复制并冻结后才进入后台伪彩，最多一帧执行、一帧等待，新输入覆盖等待帧；参数变化、换图、流重置和释放后的结果必须通过有效期检查才能发布。

视频启动先清理上一文件的路径、打开器工具、图层和测量状态，再重置实时帧队列。旧 CVRAW 后台读取即使晚到也不能覆盖视频，旧文件的校正/POI 数据不能继续作用于实时帧；重新启动视频会丢弃上一轮尚未显示的帧。实时输入继续使用 `DispatcherPriority.Background` 调度，给鼠标/键盘输入留出机会。此入口不受结果列表的 `AutoRefreshView` 控制。

帧发布以 `CommitSourcePixels` 登记与显示结果对应的同一帧原图，再经 `ImagePresentation` 发布处理显示。关闭伪彩或处理失败时恢复该帧原图；第一次没有基准源时也先发布原图，随后建立尺寸、像素格式和缩放状态。冻结快照、队列和重入保护的完整契约见[连续帧显示](../../04-api-reference/ui-components/image-editor-context.md#连续帧显示)。这会更新预览文档的源与 revision，不改写相机采集缓冲、流程测量数据或结果文件。

对焦清晰度指标独立由 `CameraFocusFrameProcessor` 计算，输入是原始帧的拥有型副本及 ROI/算法请求；该处理器不生成伪彩图。其双缓冲保留一帧工作和一帧等待，单次处理异常记录日志后继续接收，关闭时等待 worker 退出再释放缓冲。`CameraRealtimeFramePipeline` 以自身 generation 拒绝停流后指标，指标叠加与图像显示分别更新。十字参考线使用独立 `VideoCrossGuideProcessor`，不能把相机指标生命周期等同于视频文件播放的 `VideoPlaybackSession`。

## 查询和显示相机结果

| 操作或设置 | 当前行为 |
| --- | --- |
| 收到新的远程或本地结果通知 | 按当前 `DeviceCode` 筛选，再按 `MasterId` 读取主记录并加入列表；默认插在开头 |
| `AutoRefreshView` | 默认 `true`，收到新结果时自动选择并滚动到该行；关闭后仍加入记录，但不自动切换选中图像 |
| “查询” | 先清空列表，再查询整个相机结果表；**没有自动限定当前设备**。默认按 ID 降序取 50 条；按 `Count` 完整加载，`Count <= 0` 不限制查询条数 |
| “高级查询” | 使用用户设置的查询条件和查询数量，同样不自动附加当前相机条件；搜索结果不受实时保留上限限制 |
| `MaxHistoryCount`（实时结果保留上限） | 默认 500，至少 1 条；实时新增结果时按此上限裁剪当前列表，修改后也立即裁剪当前列表；搜索加载时不裁剪 |
| 结果视图设置 | 齿轮打开 `ViewCameraConfig`，相机视图共用该配置，可设置数量、排序和自动选择行为 |
| CSV 导出 | 先选择一条记录；只导出该记录。保存对话框确认后代码会追加 `.csv`，文件名无需再次填写此后缀 |
| 清空列表或删除选中行 | 只移除当前视图集合中的行，不删除数据库记录或图像文件 |

结果列表按主记录 ID 去重，默认实时保留上限为 500 条。搜索时按查询设置完整加载，可以超过此上限；之后有新实时记录加入，就将同一个列表收回到上限，旧搜索记录也会被淘汰。裁剪按最小主记录 ID 淘汰，表头排序和插入方向不改变规则，晚到的旧通知不会挤掉上限内已保留的较新结果。选中行被淘汰时，选择和显示随之清空，开启自动刷新时再选择新结果。裁剪只释放视图内存，不删除数据库记录或原文件。每条结果的右键菜单及相关命令在使用时创建，视图释放时清空结果集合与去重索引。

最新本地结果选择时使用 `LocalCameraPreview` 的独立快照；其它结果的显示链仍是 `ViewResultImage.FileUrl → OpenImage(string?) → ImageView.OpenImage(filePath)`，空路径清空图像。`SaveFiles=false` 的本地流程结果可立即预览，也继续供下游使用；新的本地结果替换旧快照后，流程记录仍可从驻留文件缓存打开；缓存和磁盘都没有时无法重新打开。主面板和本地管理窗口的 RAW 预览均使用 CVRAW 解码链的 BGR 显示约定和源行步长；16 位三通道只在显示副本中转为 WPF 的 RGB48，不交换绿蓝通道、不缩放采样值、不修改源缓冲；校正处理和可选 CIE 真彩显示是独立步骤。预览方向调整只作用于显示副本；RAW 与 CIE 色彩/坐标的实机验证和进一步性能优化见[内存预览设计](../../02-developer-guide/engine-development/local-camera-memory-preview.md)。

自动流程在 `AutoRefreshView=false` 时跳过内存预览复制，结果仍可加入列表；主面板手动取图的强制显示请求不受此开关阻止。直接快照显示先清理文件状态，带完整 CIE 时继续挂载内存测量数据。实时帧、取图快照和 CVRAW 文件槽位分别拥有自己的像素：覆盖或统一释放文件/校正缓存不会释放已经显示的独立帧。文件读取缓冲复用也不会消除实时流和取图快照现有的复制与冻结成本。

有记录但无图时，先检查选中行、`FileUrl` 和文件加载；出现其它相机记录时，核对是否执行过全表查询。设备右键菜单 `CameraLog` 从配置的主服务目录查找最新相机日志，可结合命令终态及错误消息定位远程失败。

## 设备关联与资源释放

`DeviceCamera` 是逻辑相机；`Config.Code` 标识消息中的 `DeviceCode`，`Config.CameraCode` 关联物理相机，`Config.CameraID` 用于打开硬件。

`AttachPhyCamera` 只维护对象关联和 `ConfigChanged` 订阅。`DeviceCamera.Save()` 才解析新的 `CameraCode`、释放旧关联、调用 `PhyCamera.SetDeviceCamera` 并同步物理参数，同时保留逻辑服务的 `CameraID`，最后进入[设备配置持久化与服务重启契约](./configuration.md)。`SetDeviceCamera` 会保存许可证中的 `DevCameraId`；许可证关联了校准设备时，还可能请求该校准服务重启。

`DeviceCamera.Dispose()` 幂等释放已经创建的控制面板/结果视图、物理订阅、本地会话、校正缓存和 MQTT 服务；不会为释放操作创建懒加载视图。

## 验证范围

- `DeviceCameraAssociationTests` 覆盖关联/解绑对象不改许可证中设备 ID 的断言；不覆盖 `Save()`、数据库写入和服务重启。
- `CameraViewLifecycleTests` 覆盖结果列表解绑的幂等性、事件/绑定清理；不证明完整视频或硬件生命周期。
- `CameraPreviewFileHandoffTests` 使用真实 WPF 视图和软件构造的帧检查文件→视频的过期读取拒绝、旧校正/POI 清理、预览重启丢弃待显示帧、8/16 位单/三通道副本独立、统一释放缓存、视频→文件及 RAW/CIE 直接快照→文件切换；这些测试不连接真实相机，不覆盖驱动回调时序与现场长时间运行。
- `LocalCameraSessionTests` 覆盖物理配置 JSON 的 14 个字段映射及全帧零 ROI。`LocalCameraOwnershipTests` 用原生替身检查复用、失败状态、参数冲突及关闭后重开；`CameraBackendRoutingTests` 覆盖软开关、当前会话路由、占用和服务心跳隔离；`LocalCameraResultTests` 覆盖默认保存及关闭保存、独立预览副本、方向、非对齐行、与 CVRAW 解码像素的一致性、结果文件字段和曝光回填。它们不打开真实硬件、不写实际业务数据库。
- 该用例在结束前已释放租约。`LocalFrameMirrorTests` 检查 RAW/CIE 各自的方向、校正准备及幂等翻转，不覆盖异步预览与校正并发。
- `LvCameraLocalForwardingTests` 用真实流程节点及采集/保存替身检查连续 L/BV 转发、参数和批次交接、POI 忽略、服务请求保留、CV 范围隔离、失败不回退和超时/停止后的资源清理；不连接硬件或业务数据库。
- `VideoProcessorResilienceTests` 覆盖对焦与十字参考线后台处理异常后继续运行；`ImageDisplayEffectsTests` 覆盖参数捕获的基准源、启用与存活门禁，以及不可变参数和无发布副作用。它们不调用真实相机或 native 伪彩 DLL；实际 FlipX/FlipY、缩放和指标位置仍需按输入与设备验证。
- 已授权设备环境中的远程完成消息、校准资源、超时结果归属、句柄互斥和文件显示仍需现场验收；源码核对与文档构建不能替代这些证据。
