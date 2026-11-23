---
knowledge_id: "operations.camera"
knowledge_type: "topic"
status: "current"
summary: "本地与服务相机控制、共享电机会话、自动曝光与自动对焦模板、无文件预览；明确占用、超时、结果完成及帧寿命。"
aliases: ["CamMotorNode", "本地电机", "自动聚焦模板", "相机联动", "BV/LV本地转发", "CV本地转发", "CVCameraNode", "LVCameraNode", "使用本地相机", "本地相机优先", "相机拍图", "相机服务", "手动采集成功流程失败", "采集超时", "无文件预览", "本地相机管理", "本地相机取图", "视频模式", "相机结果查询", "是否重启服务", "CameraLog", "DeviceCamera", "MQTTCamera", "DisplayCamera", "ViewCamera", "CameraLocalWindow", "LocalCameraNode", "LocalCameraSession", "LocalFrameFileService", "SaveFiles", "AutoRefreshView", "本地相机尚未打开", "LocalFlowFrame", "LocalFlowFrameLease", "LocalFlowFrameRuntime", "SetCurrentFrame", "TryAcquireCurrentFrame", "FlowRuntimeResources", "本地帧租约", "流程帧内存", "SaveFiles=false", "CIE重新分配", "CameraFocusFrameProcessor", "CameraRealtimeFramePipeline", "海康取图测试", "HikCaptureBenchmark"]
code_paths: ["Engine/ColorVision.Engine/Services/Devices/Camera/Local","Engine/ColorVision.Engine/FlowProcessing/Nodes/Compatibility/Camera/LVCameraNode.cs","Engine/ColorVision.Engine/FlowProcessing/Nodes/Compatibility/Camera/CVCameraNode.cs","Engine/ColorVision.Engine/Services/Devices/Camera/DeviceCamera.Local.cs","Engine/ColorVision.Engine/Services/Devices/Camera/DeviceCamera.Commands.cs","Engine/ColorVision.Engine/Services/Devices/Camera/DeviceCamera.cs","Engine/ColorVision.Engine/Services/Devices/Camera/MQTTCamera.cs","Engine/ColorVision.Engine/Services/Devices/Camera/DisplayCamera.xaml.cs","Engine/ColorVision.Engine/Services/Devices/Camera/Views/ViewCamera.xaml.cs","Engine/ColorVision.Engine/Services/Devices/Camera/Video/CameraRealtimeFramePipeline.cs","Engine/ColorVision.Engine/Services/Devices/Camera/Video/CameraFocusFrameProcessor.cs","Engine/ColorVision.Engine/FlowProcessing/Nodes/LocalCameraNode.cs","Engine/ColorVision.Engine/Services/PhyCameras/PhyCamera.cs","Engine/ColorVision.Engine/Services/Devices/Camera/DisplayCamera.xaml","Engine/ColorVision.Engine/Services/Devices/Camera/CameraLocalWindow.xaml","Engine/ColorVision.Engine/Services/Devices/Camera/CameraLocalWindow.xaml.cs","Engine/ColorVision.Engine/Services/Devices/Camera/Views/ViewCamera.xaml","Engine/ColorVision.Engine/Services/Devices/Camera/Views/ViewCameraConfig.cs","Engine/ColorVision.Engine/Abstractions/ViewConfigBase.cs","Engine/ColorVision.Engine/FlowProcessing/Runtime/DisplayFlow.xaml.cs","Engine/FlowEngineLib/Base/FlowRuntimeResources.cs","Engine/FlowEngineLib/Base/CVStartCFC.cs","Engine/ColorVision.Engine/FlowProcessing/Nodes/LocalCalibrationNode.cs","UI/ColorVision.ImageEditor/Realtime/RealtimeFramePresenter.cs","UI/ColorVision.ImageEditor/Presentation/ImageStreamPresentation.cs","Engine/ColorVision.Engine/Services/Devices/Camera/Diagnostics","Engine/ColorVision.Engine/FlowProcessing/Nodes/Compatibility/Camera/CamMotorNode.cs"]
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

相机的“使用本地相机”（`DisplayCameraConfig.UseLocalCamera`）可在“修改配置 → 设备与连接”或“编辑显示配置 → 采集与显示”中设置，两处共用本机显示配置。“修改配置”窗口按设备连接与文件保存分组，点击确定后保存。新配置和缺少此字段的旧配置默认使用本地相机，已保存的选择保留。只修改本地偏好无需重启服务，偏好决定**下次打开**的后端。已打开、取图或关闭中的会话继续使用实际后端。主面板“连接”进入测量模式，视频由视频按钮开启。

修改相机型号或成像模式（例如海康相机的 BV 与 LV）后，关闭并重新连接本地相机，新配置才可用于测量或预览。

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
| `HikOutputBgr` | 开启（缺字段同样开启） | 按 BGR 顺序输出以匹配旧后端和现有校正；关闭仅供对照，此时 BGR 校正模板的结果不能作为正常生产测量 |

MVS 拍照使用单帧软件触发、视频使用连续采集，打开时设置并读回，关闭或失败时恢复原参数，无需先在海康客户端设置 Trigger。MVS 只提供普通拍照和视频，快速/快速扩展选项禁用。

LV 优先选用原生 Mono8/Mono16。单色相机缺少 Mono16 时，可将 Mono10/12/14（含支持的 Packed 格式）解包并左对齐到 16 位输出，保留有效位数。彩色相机可用 Bayer8/Bayer16 原始单通道数据，须使用对应校正。BV 仅在 Bayer 相位可确认时插值。没有可用格式时，连接返回错误。

9568 × 6380 全幅 BayerRG16 普通拍照使用质量档 3 并启用 BGR 输出时，当前连接可自动校验加速转换。首张符合条件的实拍 RAW 先用 SDK 转成 RGB16 再交换红蓝通道，输出这份 BGR16，并以它为基准逐值校验同一帧的 SDK 直出。GigE 型号 `MV-CH610-10TC-TF` 满足零偏移、紧凑帧布局、相机参数读回及合成探针校验时，再独立比对本地 RGGB 转换。SDK 版本、曝光和增益使用当前会话的实际值，由像素校验结果决定加速路径。

后续帧优先使用已通过校验的本地转换（`Route=CustomBGR16`），其次使用已通过校验的 SDK 直出（`Route=DirectBGR16`），否则使用 RGB16 加换色（`Route=RGB+Swap`）。已启用的路径出错时，按同一顺序用本帧原始数据回退。现场对照时保持曝光、增益、ROI、平均次数、校正、翻转及保存设置一致。用 `[CameraCaptureCompare]` 核对相机实际配置，在 `HikMvsCapture` 日志中单列首帧校验时间 `QualificationMs`，再比较后续帧的 `Route`、`ConvertMs` 和结果表用时。

### 通道数与滤光轮

测量输出通道数由模式决定：LV 为 1，BV、CV 和 LVTOBV 为 3；旧配置中的不匹配值按模式解释，界面不再提供独立选择。CFW 页仅在 CV 模式显示。

CV 本地视频只预览当前光路单通道，不初始化滤光轮，视频中的自动曝光也只计算当前通道。首次进入拍照时按物理相机（无则逻辑配置）的 `CFW` 准备滤光轮，仅 `IsUseCFW=true` 且 `IsCOM=true` 时启用串口。准备或复位到位失败会终止打开并返回原始错误；在视频中切拍照失败时原视频会话保留，可再次拍照重试。滤轮端口或波特率修改后须关闭再连接。拍照与视频切换保留滤轮串口连接，需要配套更新后的 `cvCamera.dll`，Fast/FastEx 模式仍可能完整关闭重开。**仅有视频打开成功不能证明滤光轮通信成功**，三通道测量须实机验证。

若相机断开后滤光轮串口释放失败，重试关闭，待资源释放后再连接。

## 自动曝光

主面板有两个可选模板：上方模板配合“自动曝光”按钮，下方模板配合“取图”（选有效模板即先自动曝光再取图）。两者默认选择“不使用”；本地和服务遵循同一规则，不另设拍前曝光开关。没有可选模板时，本地显示“本机参数”，通过齿轮编辑并保存到显示配置。模板参数执行前复制，V1 保留原字段单位，V2 保留 `type`、ROI 等 JSON 参数；模板无效时报告失败，不沿用上次配置。

本地自动曝光停用同步时将同步频率设为 `-1`。启用同步时，一个周期须小于或等于最大曝光时间。例如 50 Hz 对应 20 ms，最大曝光至少设为 20 ms。

本地独立“自动曝光”在测量模式下回填曝光、饱和度和显示配置，并显示达标帧，且**不保存图像、不写结果记录**，不受“本地取图保存文件”控制。失败或到达曝光边界未达标时提示具体错误，不替换当前图。CV 自动曝光按 X/Y/Z 返回。本地 CV 取图与自动曝光共用滤轮孔位配置，缺少或重复任一 X、Y、Z 通道时拒绝采集。

主面板本地取图会拒绝自动曝光联动 ND，并隐藏 ND 手动控制。校正模板关联的校正组仍覆盖增益（见[本地视频与实时伪彩](#本地视频与实时伪彩)中的增益优先级）。

## 电机与自动对焦

物理配置 `MotorConfig.IsUseMotor=true` 时，本地相机连接同时创建独立电机句柄（无物理对象时用逻辑配置）。相机打开期间改电机配置须关闭再重连；同一串口只允许一个本地会话持有。相机联动 `IsCameraLinkage` 只控制失败策略：开启时电机失败会回滚相机打开，关闭时相机仍可取图、电机操作报告原始错误；它**不表示自动跟随或自动对焦**。联动打开失败且资源未释放时，须先关闭相机并重新连接，才能再次取图。

主面板在启用电机且停止预览后提供移动、回零、位置读取、光圈及自动对焦。用“步长”输入正整数，点左箭头按负方向移动、点右箭头按正方向移动，每次移动一个步长。电机跟随相机当前后端，本地失败不补发服务请求。直接按坐标移动的限位由控制器处理。`VID.IsUseVID=true` 时按映射文件把位置换算为 VID，相机画面浮窗同时显示电机位置和 VID。换算公式与服务端 `CalDistanceExV4` 一致，文件无效或结果非有限值时报告失败。

本地相机通过“移动”按钮或左右步进移动电机后，会读回实际位置并取一帧显示。取图失败时保留已读回的位置。

打开已启用 VID 的本地相机，在电机区输入带符号的“目标 VID (mm)”并点击“按 VID 移动”。程序用映射文件在电机配置的 `MinPosition` / `MaxPosition` 内反算绝对坐标。多段都能命中时取坐标最小的有效位置。移动后读回实际位置和 VID，再用当前曝光、增益取一帧显示。无可达位置时提示本次采用的电机移动区间。

本地自动对焦（含 VID 微聚焦）遇到视频模式时会先切回拍照模式，再继续对焦。对焦参数来自 `Camera,AutoFocus` 模板，搜索方式来自物理电机配置：

- 搜索范围直接使用模板的绝对坐标，默认 0–7800 不是 VID 等脉冲控制器的通用行程，须按设备修改。
- “最大差异”粗扫全范围后插值细搜；“二分”从当前位置开始局部找峰，适合已在焦点附近，不保证找到全范围最大峰；“最大值搜索”逐步缩小邻域。本地与服务端的采样顺序和拟合结果不保证逐位相同，需要实机对照。
- 本地扫描的采样网格由范围下限和步长决定，但会从离电机当前位置更近的一端开始扫，减少空程。
- 电机配置的“粗扫提前停止比例”默认 0（关闭）。设为 0.3–0.5 后，本地全范围粗扫越过峰值，且出现“粗扫提前停止连续低点数”（默认 2）个连续低于“峰值 × 比例”的点时停止。峰值前也必须出现过低于该比例的点；峰值在范围端点时仍扫完全程。可能出现多个清晰峰的光路应保持关闭，否则可能停在先遇到的次峰。
- “微对焦”从当前位置试探邻点，沿评价值上升的方向移动，两侧都没有改善时缩小步长。搜索限于当前位置左右各一个模板初始步长和模板搜索范围的交集，适合已经接近焦点时使用。
- `nTimeout` 单位为毫秒，限制整次搜索总耗时（含移动、曝光、评价）；长行程、小步长或长曝光要增大。本地对焦时，启动用的“自动聚焦”或“微对焦”按钮原位变为“停止对焦”。程序等待当前 SDK 调用返回，取消后续移动和取图。
- 本地自动对焦时，VID 串口电机移动返回通用失败码 `0`，程序在模板剩余时间内向同一绝对位置重试一次，并记录目标和串口。重试仍失败时结束对焦。
- 使用当前手动曝光和增益，不自动执行曝光模板。每次对焦使用开始时的模板和 VID 文件内容。更改电机速度、搜索方式或 VID 文件路径须重连相机。
- 本地自动对焦时，相机画面右上角显示电机位置与清晰度评价值曲线。曲线标记最佳采样点。点“×”可隐藏，点“曲线”恢复，不影响采样。显示普通取图结果或进入视频时收起对焦曲线。对焦不保存中间图或写测量记录，完成后取一帧显示。
- 在相机“编辑显示配置 → 采集与显示”中，可切换“VID 使用电机到位读数”。默认开启，最终移动成功后读取电机实际位置，用读数换算 VID，读取失败时本次 VID 测量失败。关闭时用对焦选中的目标位置换算。设置按相机保存，下次对焦生效。完成日志的 `Position` 是选中的目标位置，开启读回时另记 `ActualPosition`。`VidMm` 是原始 VID 毫米值，`VidSource` 指明本次采用的坐标来源。
- 在相机“显示配置”中启用“自动保存对焦结果”后，手动点击“自动聚焦”或“微对焦”成功时，按设备、日期和对焦时间归档 `FocusImage.png`、`FocusCurve.png` 和 `FocusSamples.csv`。两张图标注电机位置、最佳采样点的清晰度和已启用映射的 VID。CSV 按采样顺序记录目标位置 `MotorPosition` 和评价值 `Sharpness`。隐藏曲线也会保存。“对焦结果保存目录”沿用原配置，留空使用“文档/ColorVision/AutoFocus”。

流程“电机[相机]”节点 `CamMotorNode` 跟随相机后端，兼容已有存档。本地分支复用相机会话，相机关闭且启用本地偏好时自动连接。焦距和光圈操作须先停止预览。模板缺失、参数无效或电机失败使节点失败，不补发服务请求。本地分支不使用服务最大超时。节点只返回位置或评价结果，不拍预览、不生成测量记录、不替换当前流程图像。控制器通信、方向和现场精度仍需实机验收。

## 使用远程相机取图

1. 在相机控制面板确认设备配置，点击“连接”。成功响应后才切换到已打开状态。
2. 设置曝光等参数，选择校准和自动曝光模板。空模板是有效选择，不等于启用对应功能；缺少选择时按钮直接返回、不发送请求。非空校准模板还要求物理相机、许可证中的校准服务关联和相应资源。
3. 点击“取图”，核对本次命令终态及新增结果。`MQTTCamera` 返回的 `MsgRecord` 只是请求记录，方法返回、按钮恢复或仍显示上一张图都不能作为采集成功依据。

这些发送前检查属于手动按钮路径，其它调用入口应核对自己的检查。自动曝光/对焦参数回填要求返回消息匹配 `DeviceCode` 且 `Code == 0`。收到对焦进度 `Code == 102` 后，请求继续等候最终响应，仍按发送时设定的超时期限计时。

自动曝光兼容旧版数组回包及含 `ExpTime`、可选 `NDPort` 的对象回包。界面提示按相机依次显示，曝光、饱和度和 ND 孔位在成功提示前回填。MQTT 继续处理后续回包。设备释放或切入本地控制后，已排队的远程自动曝光结果失效。

### 失败、超时与重启提示

远程连接、断开、取图和读取 ND 的失败提示与自动曝光提示按相机依次显示。提示打开时，MQTT 可以继续处理后续回包。取图失败后的历史和超时回查在后台读取，仍沿用历史列表的结果数和排序设置。历史回填仅应用于仍存活且允许显示的当前视图，读取期间选中其他结果或列表已更新时保留当前画面和列表。

手动取图和旧 Flow 的普通 `GetData` 收到与原请求匹配的成功回执后，才会按正数 `MasterId` 通知相机视图。视图使用请求时的设备、服务和已验证数据库连接，按通知顺序读取结果。连接切换后，旧请求的迟到结果和自动历史回填失效。新库的有效结果可显示，即使 ID 和文件路径与旧结果相同。本地直接交付的图像和已绑定数据库的本地结果也可显示。

POI 和键盘 POI 编辑器的“拍照”使用原请求的数据库读取背景图。切库、关闭编辑器、手动换图或发起下一次拍照后，旧请求停止更新背景。

命令超时会显示“尚未确认本次取图结果”。视图可从原数据库刷新历史，列表中的其他采集记录不参与本次取图的成败判断。收到匹配的失败回执时，面板显示服务错误，并提供需确认的重启服务入口。

取图失败后的“是否重启服务？”调用 `DisplayFlow.RestartColorVisionServicesAsync`，依次停止再启动 `RegistrationCenterService`、`CVMainService_x64`、`CVMainService_dev`，会影响共用这些服务的其它设备。某步失败会中断后续步骤，没有整体回滚，结束也不保证注册中心已重连；确认后仍应检查服务与设备状态，该操作不会自动重新取图。

## 使用本地相机管理

1. 在逻辑相机右键菜单选择 `Local`，或在“本地相机取图”节点选择“相机管理”。
2. 在未连接状态选择采集模式和位深，点击“连接”。相机 ID 为空时按[相机身份](#相机身份)规则自动选择；多台或绑定不匹配时先刷新并手动选择。窗口与同一设备的流程节点共用 `LocalCameraSession`。连接后可直接切换采集模式；相机 ID、型号和拍照位深修改须先关闭。
3. 设置曝光、增益、平均次数和校正模板，按需勾选“保存文件”，点击“测量”。窗口直接显示内存 RAW（有 CIE 时一并挂载）；该路径不写流程测量主记录，也不发布流程结果通知。

窗口使用 `Device.DisplayConfig` 的参数。从节点打开时会先把节点参数应用到窗口，后续修改再同步回节点；这会改变设备显示配置和节点参数，不是只读查看。关闭窗口保存偏好、停止本窗口拥有的预览，但**保留共享相机会话**供后续流程使用；需要断开时使用窗口内的“关闭”。测量过程中窗口暂不能关闭。注销预览回调失败时，窗口保留并显示错误，可再次关闭以重试。

MVS 相机切换采集模式失败并回到原视频模式时，应用会尝试恢复原预览。相机断开或回调恢复失败时结束预览。

本地取图按校正模板保存的文件引用解析资源，引用丢失或文件未同步会失败，不回退为校正组默认文件。

### 海康取图测试

“海康取图测试”（`HK_USB` / `HK_CARD`）是只用于定位耗时的独立诊断进程，不加载插件、设备服务或数据库，需要海康 x64 MVS 运行库及匹配驱动。使用前关闭 cvCamera、服务或视频占用的相机并停止流程。每点一次“取一张”执行一次软件触发；不校正、翻转、存图或写数据库。要核对真实 Bayer 帧，关闭主程序中该相机的连接，进入本地相机的“海康取图测试”，核对序列号、曝光、增益和质量档后连接。点击“取一张并保存原始帧”，在保存窗口选好位置，再将完整的 `HikRaw_*.zip` 作为反馈附件。彩色 ZIP 包含插值前的 Bayer16 平面，`metadata.json` 中的 `SdkReference` 记录同一帧在本次连接上的 SDK RGB16 输出校验值。导出完成后断开测试连接，可回主程序重新打开相机。单色取样保留 Mono16 和帧参数。测试日志写入 `%APPDATA%\ColorVision\Log\HikCaptureTest_*.log`，近期文件会被反馈收集器带回。

测试成功只证明该独立链路有效，不能认定主程序的 SDK、占用状态或防火墙一致；原 `SCGDCamLayer.dll` 的插值和白平衡不在本仓库源码中，因此**不能证明测量结果等价**。报告字段和计时口径见 `Engine/ColorVision.Engine/Services/Devices/Camera/Diagnostics`。该入口不替换生产采集路径，诊断完成后可连同该目录及专用 SDK 引用移除。

## 主面板本地取图

主面板在命令排队前捕获设备所属的已验证数据库。独立批次（不归档）和测量图像在原库的一次事务中提交，提交后才发布带来源的结果。若取图期间切库，新库的历史列表不会接纳旧库结果。已取到图但落库失败时，回滚批次和图像并报告命令失败，当前帧仍可预览。离线取图可以仅预览，返回的历史 ID 为 `0`。

“本地取图保存文件”（`SaveLocalCaptureFiles`）默认开启（缺字段同样开启），开启时保存含色度校正参数的 CVRAW。关闭时仍可预览内存图，已绑定数据库时仍保存结果记录。该选项用于主面板、POI、定时本地取图和本地相机管理窗口。流程节点使用节点自己的 `SaveFiles`。

相机卡片和结果详情的初始化边界见[设备详情视图按需初始化](../../04-api-reference/engine-components/device-service-chain.md#设备详情视图按需初始化)。“本地视频有画面”只说明预览路径可用；排查手动取图与流程结果不一致时，先核对实际入口、设备、曝光参数、校准模板和结果文件。

## 流程中的相机节点

### 相机服务节点的本地转发

L/BV、CV 和通用相机保留原流程保存标识，并按会话归属选择后端。已有本地会话时复用，关闭且启用“使用本地相机”时尝试打开本地会话，服务占用时沿用服务。修改偏好在下次打开时生效。本地 Live 先切回拍照；本地失败按节点策略处理，**不补发服务请求**。循环 `.For` 沿用原执行方式。

L/BV 和 CV 本地分支使用节点填写的曝光、增益和平均次数。CV 节点要求设备为 CV 三通道，独立 R/G/B 曝光对应 X/Y/Z，`CV2LVChannel` 须设为 `None`。校正组增益覆盖取图增益。

通用相机本地分支同步保存 CVRAW，曝光、增益和平均次数取自普通相机模板。模板留空时，使用设备当前的采集参数。校正、翻转和所选自动曝光模板仍参与取图。自动曝光联动 ND 以及普通模板内的焦距和光圈设置在本地分支会明确报错，后者可由独立相机电机节点执行。服务分支继续支持自动曝光联动 ND。

相机节点只负责取图及所选校正，本地和服务请求均不带 `POIParam`；旧流程中的 `POITempName`、`POIFilterTempName`、`POIReviseTempName` 被忽略，重新保存后清除。需要测量时在下游连接独立 POI 节点或本地“校正+实时 POI”。

L/BV 和 CV 节点在所选设备走本地取图时显示“保存文件”，设备状态变化后重新打开面板刷新。旧画布缺少该字段时默认保存。本地 CVRAW 的像素与采集、校正信息一同写入文件或缓存。各本地转发节点在写入完成后保存当前流程批次的测量记录并交接内存帧。图像保存失败时不写测量记录，后续落库失败时保留已写图像。

本地分支不启用节点超时，画布“最大超时”只对服务分支生效：原生采集不能即时中断，提前超时会让重启后的流程撞上仍在占用的相机。主动停止流程会取消排队中的取图。若原生采集已经开始，程序等待返回并释放晚到帧，不写结果。转发一个相机节点不代表整个流程可脱离服务运行。

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

“相机取图”开启自动曝光后，点击“自动曝光参数”旁的“编辑…”查看设备参数，修改并应用后随画布保存。可切换到 JSON 查看或编辑 SDK 的 `expTimeCfg` 配置，点击“恢复设备参数”并应用可重新跟随设备。旧画布的 `AutoExposureJson` 继续可用，`AutoExpTempName` 会被忽略。无效配置会停止取图。通用相机仍使用 V1 或 V2 曝光模板，同名时本地分支优先使用 V2。

启用了电机的相机可在节点设置“取图前聚焦”，旧流程默认关闭。同型号换件时可选“微聚焦”，从当前电机位置附近找焦。换型号后可选“全范围聚焦”，使用对焦模板范围和设备搜索方式。对焦模板留空时使用相机面板当前选择。启用自动曝光时先测曝光，再聚焦和取图。聚焦失败时节点报错，停止流程后不再继续取图。

取图走 RAW 按需色度路径，不生成整幅 CIE/XYZ 内存；需要完整 CIE 的下游接普通本地校正节点并关闭其加速。旧流程中的 `AllowAcceleration`、`SaveAsynchronously` 被忽略。关闭自动连接时须先建立本地会话，否则提示“本地相机尚未打开”。所有本地测量请求由进程级 `CaptureLock` 串行化，**不同设备也不会并行测量**。

节点先写 CVRAW 与色度参数，再保存测量主记录、交接内存帧并发布一次 `ResultMessageBus` 通知。流程停止会取消排队中的取图。原生采集已开始时，程序在返回后释放晚到帧，跳过文件、缓存和测量结果保存。图像或参数保存失败不写主记录；找不到批次或主记录保存失败使节点失败，已写图像保留。`SaveFiles=false` 依赖全局 CVRAW 缓存开启；调用只识别磁盘文件的旧服务时，须开启“保存文件”。仅缓存图像被淘汰后按缺失文件处理。

### 流程帧的寿命与读写限制

`SetCurrentFrame` 把 `LocalFlowFrame` 交给 `FlowRuntimeResources`，更换当前帧不释放旧帧；同一流程多次取图可能同时保留多帧，直到流程在 `DoFinishingCore` 释放资源。不能把“当前帧”理解为全流程只有一个缓冲区。消费者在根引用有效时 `Acquire()`，持有并最终 Dispose `LocalFlowFrameLease`。

**租约不是不可变图像快照。** 下游 `LocalCalibrationNode` 可对同一帧 `CalibrateInPlace`，修改 RAW 并重新分配 CIE（旧地址立即释放）。跨线程长期保留指针或并发预览与校正时，异步消费者必须自行复制稳定快照；UI 不持有原生指针或流程帧租约。`IsMirrorReady` 须与缓冲区各自的翻转状态一起判断：有 CIE 时 RAW 可能仍是传感器方向，不能仅凭 `FlipMode` 判断显示坐标已与 POI 一致。内存快照设计见[内存预览设计](../../02-developer-guide/engine-development/local-camera-memory-preview.md)。

“本地缓存管理 → 相机取图缓冲”的“启用相机取图缓冲”（`CameraRawBufferCacheConfig.IsEnabled`，默认关闭）让每台相机复用最多一块 RAW 工作内存，例如 9568×6380 三通道 16-bit 约 349 MiB；这不是流程在用图像的总内存上限。页面可按相机释放空闲缓冲，关闭开关立即释放空闲部分；连续运行时清理后不保证一直为零。节点阶段耗时中的 `AllocateRawBuffer` / `ReuseRawBuffer` 区分实际申请与复用，`ProcessGcPauseMs` 是整个进程的 GC 暂停增量（嵌套阶段不能相加），`ThreadCpuMs` 不含相机 SDK 线程。

### 本地文件保存位置

- 根目录取 `Device.Config.FileServerCfg.DataBasePath`，为空时使用“文档”下的 `ColorVision`。
- 路径为 `<DeviceCode>/Data/yyyy-MM-dd/yyyyMMdd_HHmmss_fff.cvraw`；执行了校正时在扩展名前追加 `_<校正模板名>`，同名才追加 `_2`、`_3`。色度参数保存在 CVRAW 尾部，旧文件名和旧服务 `.cvcie` 保留读取兼容。
- 本地取图节点和 LV、CV、通用相机节点的本地转发先按配置保存 CVRAW，再把图像结果写入本次流程批次所在的数据库。重连后，已开始的流程仍使用原数据库目标，设备视图丢弃来自已退役连接的结果通知和待显示预览。
- 流程关闭结果保存时，图像仍按节点设置写文件或保留在缓存，内存帧继续交给下游节点。

因此应分别确认采集、文件和数据库结果，不能仅凭文件存在判断节点成功。格式见[CVRAW/CVCIE 图像导出](../../04-api-reference/engine-components/cv-image-export.md)。

## 本地视频与实时伪彩

相机关闭时顶部显示“打开”和“视频模式”两个入口。本地相机打开后，“关闭视频”保留会话并恢复取图，“关闭”结束整个会话；服务模式下打开任一模式后只显示“关闭”。预览期间隐藏取图、平均、校正、曝光模板和电机；“区域”按钮展开 ROI 与参考线参数。

本地单张取图期间可点“关闭”。相机会等待当前取图返回后关闭，等待期间拒绝新取图。切换视频须等当前操作结束。

海康 MVS 视频会丢弃坏帧并继续等待有效帧。相机控制面板使用本地会话预览时，长时间收不到新帧会自动重启一次取帧。等待时间随曝光延长。恢复后画面继续更新，仍无新帧时停止预览并在面板提示，可手动重新打开视频。

海康 MVS 三通道视频按会话打开时的 RGB/BGR 设置解释像素。预览和伪彩使用对应的颜色与亮度，清晰度和十字引导也按同一通道顺序读取。

视频浮窗显示最近 30 秒的清晰度趋势，底部显示 FPS 和累计帧数。启用十字引导后还显示中心偏移、距离、角度和 PASS/NG。关闭清晰度仍保留帧统计，隐藏浮窗或自动恢复视频继续累计，手动重开从零开始。

停止或重开会丢弃上一会话的帧和分析结果。切换清晰度算法只丢弃旧清晰度结果。重新预览可能先显示画面，再等待旧计算释放后恢复清晰度。视图保留最后一帧时也不会接收迟到结果。SDK 注销或关闭失败时可重试清理。

预览使用当前 RGB/BGR 通道解释。有效 ROI 的清晰度量测限定在该区域，边缘算子沿用整帧邻域。视频中的测量结果进入历史列表，预览继续播放。

预览曝光和增益在后台串行写入，界面不等待 SDK；界面响应快不代表相机已应用新曝光。MVS 视频的 `fps` 统计进入实时管线的回调频率，与 MVS 工具的相机帧率含义不同；排查显示速度时对照 `HikMvsCapture` 的 `CaptureMode`、`WaitMs` 和 `ConvertMs`，不能仅凭曝光推定。

拍照与预览共用 `FlipMode`：X 对应画面上下翻转，Y 对应左右翻转。旧配置同时保存测量翻转与 `LocalVideoTransform` 时以测量翻转为准。

校正组的固定增益优先于面板增益。校正组固定增益时面板显示组增益并禁止修改，选择“不使用”后恢复编辑。预览增益不改校正组配置。服务独立自动曝光不带增益，沿用相机当前值；服务拍前自曝在设置本次取图增益后执行。

CV 测量显示 R/G/B 三路曝光，视频显示单路曝光；停止预览后的手动、POI 和定时取图按 R/G/B 组装参数，**预览曝光不会覆盖 R 通道**。

“停止预览”保留连接，仅“断开”释放连接。服务模式的独立视频可能重新解析并保存 `CameraID`，停止时关闭独立视频相机后才能连接服务拍照。停止失败不会假装恢复画面。

视频更新预览文档，保留原始帧位深和分辨率。它不会改写测量、采集缓冲或文件。启动视频后清理上一文件的校正和 POI 状态，旧文件的迟到结果不能覆盖新画面。清晰度与十字引导独立处理，单次异常记入日志。实际显示吞吐和缓冲释放按设备与观察窗口判断。 [连续帧显示](../../04-api-reference/ui-components/image-editor-context.md#连续帧显示)

## 查询和显示相机结果

| 操作或设置 | 当前行为 |
| --- | --- |
| 收到新的远程或本地结果通知 | 核对设备和结果来源后加入列表，默认插在开头 |
| `AutoRefreshView` | 默认 `true`，新结果自动选中并显示；关闭后仍加入记录，但不切换图像（主面板手动取图仍强制显示） |
| “查询” | 查询当前已验证连接的相机结果表并替换列表。默认按 ID 降序取 50 条，`Count <= 0` 不限 |
| “高级查询” | 固定打开时的数据库连接，使用用户条件和数量 |
| `MaxHistoryCount`（实时结果保留上限） | 默认 500，按最小主记录 ID 淘汰；搜索加载可超过上限，之后有实时记录加入时收回到上限 |
| 结果视图设置 | 齿轮打开 `ViewCameraConfig`，相机视图共用该配置 |
| CSV 导出 | 只导出选中的一条记录，自动追加 `.csv` |
| 清空列表或删除选中行 | 只移除视图中的行，不删除数据库记录或图像文件 |

相机详情的结果列表省略 `DeviceCode` 列。实时结果按设备标识筛选，两种历史查询都覆盖该库中的所有相机，CSV 导出保留原有字段。

流程和本地取图结果按 `FileUrl` 打开 CVRAW，`SaveFiles=false` 从缓存读取。选中记录的图像缺失、损坏或格式不支持时，结果视图清除先前图像并显示本条记录的 ID。迟到的打开错误仅处理仍由该请求占用的视图。没有 CVRAW 路径的手动预览（如独立自动曝光）使用内存快照。RAW 预览只在显示副本中转换格式和方向，不修改源缓冲。

有记录但无图时，先检查选中行、`FileUrl` 和文件加载；出现其它相机记录时，核对是否执行过全表查询。设备右键菜单的“相机日志”（`CameraLog`）按实际后端选择：本地会话打开本进程 `cvCamera.dll` 原生日志（目录依次为 `COLORVISION_NATIVE_LOG_DIRECTORY`、主程序日志目录、工作目录 `log`，取最新的 `*_Main_*.log`）；服务模式打开主服务目录的 `CVMainWindowsService_x64_camera*.log`。没有本地日志时不会改开服务日志。

## 设备关联与资源释放

`DeviceCamera` 是逻辑相机：`Config.Code` 是消息中的 `DeviceCode`，`Config.CameraCode` 关联物理相机，`Config.CameraID` 用于打开硬件。`DeviceCamera.Save()` 会重新绑定物理相机、同步参数和 ID，并进入[设备配置持久化与服务重启契约](./configuration.md)；`PhyCamera.SetDeviceCamera` 保存许可证中的 `DevCameraId`，许可证关联了校准设备时还可能请求该校准服务重启。`DeviceCamera.Dispose()` 幂等释放视图、订阅、本地会话、校正缓存和 MQTT 服务。

## 验证范围

在已授权的设备环境核对远程完成消息、校准资源、超时结果归属、句柄互斥、滤光轮和电机通信、驱动回调时序、长时间运行及文件显示。
