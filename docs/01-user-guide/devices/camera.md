---
knowledge_id: "operations.camera"
knowledge_type: "topic"
status: "current"
summary: "DeviceCamera的物理关联、远程采集完成判据与本地采集/实时预览边界；无文件设备结果预览仍未实现。"
aliases: ["相机拍图","相机服务","手动采集成功流程失败","采集超时","无文件预览","DeviceCamera","MQTTCamera","DisplayCamera","ViewCamera","LocalCameraNode","SaveFiles"]
code_paths: ["Engine/ColorVision.Engine/Services/Devices/Camera/DeviceCamera.cs","Engine/ColorVision.Engine/Services/Devices/Camera/MQTTCamera.cs","Engine/ColorVision.Engine/Services/Devices/Camera/DisplayCamera.xaml.cs","Engine/ColorVision.Engine/Services/Devices/Camera/Views/ViewCamera.xaml.cs","Engine/ColorVision.Engine/Services/Devices/Camera/Local/LocalCameraCaptureService.cs","Engine/ColorVision.Engine/Services/Devices/Camera/Video/CameraRealtimeFramePipeline.cs","Engine/ColorVision.Engine/Services/Devices/Camera/Video/VideoFrameProcessor.cs","Engine/ColorVision.Engine/FlowProcessing/Nodes/LocalCameraNode.cs","Engine/ColorVision.Engine/Services/PhyCameras/PhyCamera.cs"]
test_paths: ["Test/ColorVision.UI.Tests/CameraViewLifecycleTests.cs","Test/ColorVision.UI.Tests/DeviceCameraAssociationTests.cs","Test/ColorVision.UI.Tests/RealtimePseudoColorServiceTests.cs","Test/ColorVision.UI.Tests/VideoProcessorResilienceTests.cs"]
related: ["engine.devices","operations.device-configuration","operations.physical-camera","operations.camera-configuration","engine.camera-preview-plan"]
---

# 相机服务、采集与结果视图

`DeviceCamera` 是流程和设备树使用的逻辑相机；`Config.Code` 标识逻辑设备（消息的 `DeviceCode`），`Config.CameraCode` 关联物理相机，`Config.CameraID` 用于打开硬件。三者不是同一个字段。物理发现、许可证和资源导入见[物理相机管理](./camera-management.md)，参数来源及覆盖顺序见[相机配置](./camera-configuration.md)。

## 先分清执行路径

| 入口 | 当前职责与边界 |
| --- | --- |
| `MQTTCamera` / `DisplayCamera` 手动采集 | 组装 `Open`、`GetData`、`GetAutoExpTime`、`AutoFocus` 等远程命令；返回 `MsgRecord`，不等于硬件已完成 |
| `DisplayCamera` 本地视频 | 在本进程通过 `cvCameraCSLib` 打开 Live/8-bit 相机并接收回调，走实时显示管线；不是远程测量命令 |
| `LocalCameraNode` | 按 `DeviceCode` 查找逻辑相机，通过其 `LocalCameraSession` 和 `LocalCameraCaptureService` 同步取得本地测量帧，并把帧交给流程下游 |
| `ViewCamera` | 显示相机结果列表；远程响应和本地 `ResultMessageBus` 通知均可触发已持久化结果展示，图像仍从结果文件路径加载 |

所以“本地视频有画面”不能证明远程服务、校准或流程节点可用；手动取图也不证明流程使用相同参数。检查入口、`DeviceCode`、参数来源、模板和结果文件，不先靠重新绑定或重启掩盖差异。

## 物理关联与生命周期

`DeviceCamera.AttachPhyCamera` 只维护对象关联和 `ConfigChanged` 订阅；构造、重新关联、释放时走这里，不应因创建视图而写许可证关联。

`DeviceCamera.Save()` 才解析新的 `CameraCode`、释放旧关联、调用 `PhyCamera.SetDeviceCamera` 并应用物理配置，最后进入[设备配置持久化与服务重启契约](./configuration.md)。`SetDeviceCamera` 会保存许可证中的 `DevCameraId`；如果许可证关联了校准设备，还可能请求该校准服务重启。因此保存、刷新命令或重新绑定不是只读诊断。

`DeviceCamera.Dispose()` 幂等释放已经创建的控制面板/结果视图、物理订阅、本地会话、校正缓存和 MQTT 服务；不会为了释放而强制创建懒加载视图。

## 远程采集什么时候算完成

`MQTTCamera` 的方法调用 `PublishAsyncClient` 并返回请求记录。应关联这次请求的 `MsgRecord`、返回消息与结果记录，区分 `Success`、`Fail`、`Timeout`，不能用按钮恢复、方法返回或画面仍有旧图宣称采集成功。

`DisplayCamera.GetData_Click` 在发送前处理所选校准模板、自动曝光和 HDR；非空校准模板要求物理相机及许可证中的校准服务关联，并继续检查相应校准资源。无校准选择可回退为空模板。这些是手动按钮路径的检查，不能自动套到 `GetData()` 等其它调用入口。

手动采集发送前记录该设备最新结果 ID。命令超时后，`TryHandleCaptureTimeoutFromDatabase` 查找该设备新增结果并刷新列表：`ResultCode == 0` 时可能已有成功记录，非零则展示数据库失败信息。它按设备和新增 ID 检查，并非严格用此次 `MsgID` 匹配；多路并发时不能仅据此确认某次请求成功。没有新记录时才继续显示超时提示，勿未经确认自动重拍。

自动曝光/对焦的参数回填由 `MQTTCamera_MsgReturnChanged` 处理，要求匹配 `DeviceCode` 且 `Code == 0`；对焦的 `Code == 102` 中间响应可在 `ViewCamera` 更新位置和临时图，它不是最终完成通知。

## 本地视频、本地测量与无文件结果

本地视频的打开在后台任务中执行、受句柄锁和打开中标志保护。它可能重新解析 `CameraID` 并在成功后调用 `Device.SaveConfig()` 保存新 ID；还会保存本地显示偏好。关闭时注销回调并关闭相机。打开失败、关闭异常和设备占用必须按返回错误检查，不能把本地预览当成无副作用操作。

实时画面启用 ImageEditor 伪彩后，`CameraRealtimeFramePipeline` 从当前工具状态捕获颜色表、范围和 generation，把相机帧交给 `VideoFrameProcessor` 的 latest-only 后台槽位；处理期间新帧覆盖等待帧，不在相机回调线程排队做 native 算法。伪彩输出继续应用本地视频的 FlipX/FlipY 变换，回到当前 `ImageView` 的 UI Dispatcher 后才发布。颜色表、范围或启用状态变化会推进 generation，旧结果和停流后的结果会释放而不回写；关闭伪彩后原始实时提交立即恢复。第一次还没有基准图像源时先显示一帧原图，用于建立尺寸、像素格式和缩放状态，再进入逐帧伪彩。该链只改变实时预览显示，不改写相机采集、流程测量或结果文件。

`LocalCameraNode.AutoConnect` 在会话未打开时尝试连接，拒绝 Live 模式、空 `CameraID` 和原生打开错误；`LocalCameraCaptureService` 还会拒绝本地测量使用 Live 模式。`SaveFiles=false` 仅跳过 CVRAW/CVCIE 文件保存：节点仍保存测量主记录、发布结果消息，并通过 `SetCurrentFrame` 交接内存帧，**不等于不写数据库**。

当前 `ViewCamera` 的结果选择链是 `ViewResultImage.FileUrl → OpenImage(string?) → ImageView.OpenImage(filePath)`，空路径清空图像。本地结果通知也先按 `MasterId` 查主记录；没有把流程内存帧直接送入设备结果视图。因此 `SaveFiles=false` 时下游算法可消费内存帧，但设备结果视图不具备对应的无文件预览/历史重放能力。该扩展仍是[待实施设计](../../02-developer-guide/engine-development/local-camera-memory-preview.md)，不要与已实现的本地视频混淆。

## 副作用、排障与验证

打开/关闭相机、采集、自动曝光、ND/滤轮切换、对焦和电机移动均可能访问真实硬件，必须有当前设备和操作范围的授权。打开 `Local` 窗口还可能在许可证文件缺失时写出本地许可证。文档说明不授权自动试拍、移动、重新绑定或重启用户服务。

排障先记录具体入口、逻辑/物理 ID、命令终态与错误消息；`OpenCameraLog` 从配置的主服务目录查找最新相机日志。结果存在但无图时追查 `FileUrl` 和图像加载，不把文件显示失败等同于采集失败。

- `DeviceCameraAssociationTests` 覆盖关联/解绑对象不改许可证中设备 ID 的断言；不覆盖 `Save()`、数据库写入和服务重启。
- `CameraViewLifecycleTests` 覆盖结果列表解绑的幂等性、事件/绑定清理；不证明完整视频或硬件生命周期。
- `VideoProcessorResilienceTests` 覆盖后台帧处理异常后的继续运行，以及伪彩输出与实时原图一致的三个翻转方向；`RealtimePseudoColorServiceTests` 覆盖首帧基准源门禁、当前 generation 发布和旧 generation 丢弃。它们不调用真实相机或 native 伪彩 DLL。
- 尚需在已授权的设备环境验证远程完成消息、校准资源、超时后结果关联、句柄互斥和文件显示；本主题的源码核对不代替真机验收。
