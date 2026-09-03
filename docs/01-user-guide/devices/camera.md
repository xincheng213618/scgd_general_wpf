---
knowledge_id: "operations.camera"
knowledge_type: "topic"
status: "current"
summary: "远程取图、本地手动/流程采集与结果视图；明确SaveFiles=false文件显示限制、RAW/CIE帧租约与校正读写、命令完成和设备释放边界。"
aliases: ["相机拍图","相机服务","手动采集成功流程失败","采集超时","无文件预览","本地相机管理","本地相机取图","视频模式","相机结果查询","是否重启服务","CameraLog","DeviceCamera","MQTTCamera","DisplayCamera","ViewCamera","CameraLocalWindow","LocalCameraNode","LocalCameraSession","LocalFrameFileService","SaveFiles","AutoRefreshView","本地相机尚未打开","LocalFlowFrame","LocalFlowFrameLease","LocalFlowFrameRuntime","SetCurrentFrame","TryAcquireCurrentFrame","FlowRuntimeResources","本地帧租约","流程帧内存","SaveFiles=false","CIE重新分配"]
code_paths: ["Engine/ColorVision.Engine/Services/Devices/Camera/DeviceCamera.cs","Engine/ColorVision.Engine/Services/Devices/Camera/MQTTCamera.cs","Engine/ColorVision.Engine/Services/Devices/Camera/DisplayCamera.xaml.cs","Engine/ColorVision.Engine/Services/Devices/Camera/Views/ViewCamera.xaml.cs","Engine/ColorVision.Engine/Services/Devices/Camera/Local/LocalCameraCaptureService.cs","Engine/ColorVision.Engine/Services/Devices/Camera/Video/CameraRealtimeFramePipeline.cs","Engine/ColorVision.Engine/Services/Devices/Camera/Video/VideoFrameProcessor.cs","Engine/ColorVision.Engine/FlowProcessing/Nodes/LocalCameraNode.cs","Engine/ColorVision.Engine/Services/PhyCameras/PhyCamera.cs","Engine/ColorVision.Engine/Services/Devices/Camera/DisplayCamera.xaml","Engine/ColorVision.Engine/Services/Devices/Camera/CameraLocalWindow.xaml","Engine/ColorVision.Engine/Services/Devices/Camera/CameraLocalWindow.xaml.cs","Engine/ColorVision.Engine/Services/Devices/Camera/Local/LocalCameraSession.cs","Engine/ColorVision.Engine/Services/Devices/Camera/Local/LocalFrameFileService.cs","Engine/ColorVision.Engine/Services/Devices/Camera/Views/ViewCamera.xaml","Engine/ColorVision.Engine/Services/Devices/Camera/Views/ViewCameraConfig.cs","Engine/ColorVision.Engine/Abstractions/ViewConfigBase.cs","Engine/ColorVision.Engine/FlowProcessing/Runtime/DisplayFlow.xaml.cs","Engine/ColorVision.Engine/Services/Devices/Camera/Local/LocalFlowFrame.cs","Engine/FlowEngineLib/Base/FlowRuntimeResources.cs","Engine/FlowEngineLib/Base/CVStartCFC.cs","Engine/ColorVision.Engine/FlowProcessing/Nodes/LocalCalibrationNode.cs","Engine/ColorVision.Engine/Services/Devices/Camera/Local/LocalFrameCalibrationService.cs"]
test_paths: ["Test/ColorVision.UI.Tests/CameraViewLifecycleTests.cs","Test/ColorVision.UI.Tests/DeviceCameraAssociationTests.cs","Test/ColorVision.UI.Tests/RealtimePseudoColorServiceTests.cs","Test/ColorVision.UI.Tests/VideoProcessorResilienceTests.cs","Test/ColorVision.UI.Tests/LocalCameraSessionTests.cs","Test/ColorVision.UI.Tests/LocalFlowNodePortTests.cs","Test/ColorVision.UI.Tests/LocalFrameMirrorTests.cs"]
related: ["engine.devices","operations.device-configuration","operations.physical-camera","operations.camera-configuration","engine.camera-preview-plan"]
---

# 相机服务、采集与结果视图

本页说明如何使用远程相机、本地相机管理和流程取图节点，以及怎样判断采集完成、查找结果和处理无图问题。物理发现、许可证和资源导入见[物理相机管理](./camera-management.md)，参数来源及覆盖顺序见[相机配置](./camera-configuration.md)。

## 选择采集入口

| 入口 | 执行方式与结果 |
| --- | --- |
| 相机控制面板的“打开”“取图” | `DisplayCamera` 通过 `MQTTCamera` 向远程服务发送命令；结果由服务响应及持久化记录确认 |
| 设备右键菜单 `Local`，或本地相机节点的“相机管理” | 打开 `CameraLocalWindow` 本地相机管理；在本进程连接、测量，并直接显示内存图像 |
| 相机控制面板的“视频模式” | `DisplayCamera` 用独立句柄打开 Live/8-bit 相机，显示连续回调帧 |
| 流程节点“本地相机取图” | `LocalCameraNode` 取得本地测量帧，保存结果主记录，再交给流程下游 |
| 设备结果视图 | `ViewCamera` 展示结果记录，选择记录后按 `FileUrl` 打开图像文件 |

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

窗口使用 `Device.DisplayConfig` 的曝光、增益、平均次数和翻转参数，文件开关为 `Config.UsingFileCaching`。从节点打开时，会先把节点参数应用到窗口，后续修改再同步回该节点；这会改变设备显示配置和节点参数，不是只读查看。

关闭窗口会保存偏好、停止该窗口的预览并在 Live 模式下解绑回调，**保留共享相机会话**，供后续本地流程使用。需要断开相机时使用窗口内的“关闭”按钮。测量过程中窗口暂不能关闭；扫描相机 ID 时的关闭请求会等待扫描结束。

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

取帧后节点查找 `action.SerialNumber` 对应的流程批次，保存测量主记录，再经 `SetCurrentFrame` 交接内存帧并发布 `ResultMessageBus` 通知。找不到批次或保存主记录失败会使节点失败。**`SaveFiles=false` 只跳过图像文件保存，仍写数据库并向下游交接帧。**

### 流程帧的寿命与读写限制

`SetCurrentFrame` 把 `LocalFlowFrame` 的根引用交给 `FlowRuntimeResources`，键含 FrameId，并把当前 FrameId 写入 action.Data。更换当前帧只改变下游定位，不自动移除不同 FrameId 的旧资源；同一流程多次取图可能保留多帧，直到流程资源释放。不能把“当前帧”理解为全流程只有一个缓冲区。

复制 `CVStartCFC` 会共享 RuntimeResources；流程进入 `DoFinishingCore` 后在 finally 中释放这些资源。消费者应在根引用仍有效时调用 `Acquire()`，持有并最终 Dispose `LocalFlowFrameLease`。已取得的租约延长共享存储寿命；根对象 Dispose 后不能再从该根 Acquire，即使其它租约仍存活。租约自己的 Dispose 幂等，之后访问其指针会抛 ObjectDisposedException。

**租约不是不可变图像快照。** 下游 `LocalCalibrationNode` 可对同一帧执行 `CalibrateInPlace`：修改 RAW、重新分配 CIE、更新 Metadata，再处理方向。`ResizeCieBuffer` 会释放旧 CIE 地址，不等待其它租约归零；租约保留取得时的 Metadata/MasterId，而指针和长度读取共享存储。因此跨线程长期保留指针或同时执行预览和校正，不能仅靠 Acquire 保证数据一致或地址稳定；同步读写或生成独立快照的协议尚需由异步预览实现补齐。

`IsMirrorReady` 与缓冲区各自的 flip 状态也要一起判断：有 CIE 时最终翻转可只作用于 CIE，RAW 仍保留传感器方向；无校正模板的节点可发布尚未应用方向的 RAW。不能仅凭 FlipMode 判断显示坐标已与 POI 一致。设备视图的异步接入与验收见[内存预览设计](../../02-developer-guide/engine-development/local-camera-memory-preview.md)。

### 本地文件保存位置

本地测量的保存开关开启时，`LocalFrameFileService.SaveCapture` 按下列规则写文件：

- 根目录取 `Device.Config.FileServerCfg.DataBasePath`；为空时使用用户“文档”目录下的 `ColorVision`。
- 子目录为 `<DeviceCode>/Data/yyyy-MM-dd`，文件名为 `Local_yyyyMMdd_HHmmss_fff.cvraw`，有 CIE 数据时再保存同名 `.cvcie`。
- 文件逐个保存；后续文件失败不会撤销已写出的文件。流程节点在文件保存之后写数据库，数据库失败也可能留下已生成的图像。

因此应分别确认采集、文件和数据库结果，不能仅凭文件存在判断整个节点成功。图像转换与导出格式见[CVRAW/CVCIE 图像导出](../../04-api-reference/engine-components/cv-image-export.md)。

## 本地视频与实时伪彩

控制面板“视频模式”的打开在后台任务中执行，受句柄锁和打开中标志保护。它使用独立于 `LocalCameraSession` 的句柄，可能重新解析 `CameraID`，成功后保存新 ID 和本地显示偏好。关闭时注销回调并关闭相机；失败时应检查具体错误及设备占用。

实时画面启用 ImageEditor 伪彩后，`CameraRealtimeFramePipeline` 从当前工具状态捕获颜色表、范围和 generation，把相机帧交给 `VideoFrameProcessor` 的 latest-only 后台槽位；处理期间新帧覆盖等待帧，不在相机回调线程排队做 native 算法。伪彩输出继续应用本地视频的 FlipX/FlipY 变换，回到当前 `ImageView` 的 UI Dispatcher 后才发布。

颜色表、范围或启用状态变化会推进 generation，旧结果和停流后的结果会释放而不回写；关闭伪彩后原始实时提交恢复。第一次没有基准图像源时先显示一帧原图，建立尺寸、像素格式和缩放状态，再进入逐帧伪彩。该链只改变实时预览显示，不改写采集数据、流程测量或结果文件。

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

选择结果后的显示链是 `ViewResultImage.FileUrl → OpenImage(string?) → ImageView.OpenImage(filePath)`，空路径会清空图像。本地流程通知也先按 `MasterId` 查主记录，尚未把流程内存帧送入设备结果视图。因此 `SaveFiles=false` 的流程帧可供下游算法使用，但不能据此在 `ViewCamera` 预览或重新打开无文件历史结果；直接显示内存图像的是前述本地相机管理窗口。设备级无文件预览见[待实施设计](../../02-developer-guide/engine-development/local-camera-memory-preview.md)。

有记录但无图时，先检查选中行、`FileUrl` 和文件加载；出现其它相机记录时，核对是否执行过全表查询。设备右键菜单 `CameraLog` 从配置的主服务目录查找最新相机日志，可结合命令终态及错误消息定位远程失败。

## 设备关联与资源释放

`DeviceCamera` 是逻辑相机；`Config.Code` 标识消息中的 `DeviceCode`，`Config.CameraCode` 关联物理相机，`Config.CameraID` 用于打开硬件。

`AttachPhyCamera` 只维护对象关联和 `ConfigChanged` 订阅。`DeviceCamera.Save()` 才解析新的 `CameraCode`、释放旧关联、调用 `PhyCamera.SetDeviceCamera` 并同步物理参数，同时保留逻辑服务的 `CameraID`，最后进入[设备配置持久化与服务重启契约](./configuration.md)。`SetDeviceCamera` 会保存许可证中的 `DevCameraId`；许可证关联了校准设备时，还可能请求该校准服务重启。

`DeviceCamera.Dispose()` 幂等释放已经创建的控制面板/结果视图、物理订阅、本地会话、校正缓存和 MQTT 服务；不会为释放操作创建懒加载视图。

## 验证范围

- `DeviceCameraAssociationTests` 覆盖关联/解绑对象不改许可证中设备 ID 的断言；不覆盖 `Save()`、数据库写入和服务重启。
- `CameraViewLifecycleTests` 覆盖结果列表解绑的幂等性、事件/绑定清理；不证明完整视频或硬件生命周期。
- `LocalCameraSessionTests` 覆盖物理配置 JSON 的 14 个字段映射及全帧零 ROI，未实际打开相机。
- `LocalFlowNodePortTests.LocalFrameLivesAcrossNodeCopiesAndEndsWithFlow` 检查节点副本共享帧及流程结束后不能再 Acquire；该用例在结束前已释放租约。`LocalFrameMirrorTests` 检查 RAW/CIE 各自的方向、校正准备及幂等翻转，不覆盖异步预览与校正并发。
- `VideoProcessorResilienceTests` 覆盖后台帧处理异常后的继续运行，以及伪彩输出与实时原图一致的三个翻转方向；`RealtimePseudoColorServiceTests` 覆盖首帧基准源门禁、当前 generation 发布和旧 generation 丢弃。它们不调用真实相机或 native 伪彩 DLL。
- 已授权设备环境中的远程完成消息、校准资源、超时结果归属、句柄互斥和文件显示仍需现场验收；源码核对与文档构建不能替代这些证据。
