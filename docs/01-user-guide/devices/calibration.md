---
knowledge_id: "operations.calibration"
knowledge_type: "topic"
status: "current"
summary: "校准服务绑定物理相机并执行本地文件或MQTT校正；输出文件、结果显示、历史落库与缓存删除是不同完成边界。"
aliases: ["校准服务","本地校正","标定资源","校准模板打不开","清理校准缓存","UseLocalCalibration","DeviceCalibration","LocalFileCalibrationService","MQTTCalibration"]
code_paths: ["Engine/ColorVision.Engine/Services/Devices/Calibration/DeviceCalibration.cs","Engine/ColorVision.Engine/Services/Devices/Calibration/ConfigCalibration.cs","Engine/ColorVision.Engine/Services/Devices/Calibration/DisplayCalibration.xaml.cs","Engine/ColorVision.Engine/Services/Devices/Calibration/LocalFileCalibrationService.cs","Engine/ColorVision.Engine/Services/Devices/Calibration/MQTTCalibration.cs","Engine/ColorVision.Engine/Services/Devices/Calibration/Views/ViewCalibration.xaml.cs","Engine/ColorVision.Engine/Services/Devices/Calibration/InfoCalibration.xaml.cs","Engine/ColorVision.Engine/Services/PhyCameras/Group/CalibrationParam.cs","Engine/ColorVision.Engine/Services/Devices/Camera/Local/LocalCalibrationCacheService.cs","Engine/ColorVision.Engine/Services/Devices/Camera/Local/LocalCalibrationCacheManagerWindow.xaml.cs","Engine/ColorVision.Engine/FlowProcessing/Nodes/LocalCalibrationNode.cs"]
test_paths: []
related: ["engine.devices","operations.device-configuration","operations.physical-camera","flow.session"]
---

# 校准服务、本地文件校正与结果持久化

`DeviceCalibration` 绑定物理相机和校准模板；`DisplayCalibration` 的文件校正默认选择 `UseLocalCalibration=true`。本地后端直接处理输入文件，MQTT 后端请求外部服务处理，两者不是同一个成功判据，也没有失败后自动切换后端的保证。

本页描述已有文件的校正与结果呈现，不把“校准服务”泛化为自动采集或生成所有标定资源。切换真实相机、标定文件和设备参数需确认资源归属及现场验证；问答或文档维护不授权取图、硬件动作、数据库写入、文件删除或缓存清理。

## 相机绑定、模板与配置归属

`ConfigCalibration.CameraCode` 通过 `PhyCameraManager` 解析物理相机。`DeviceCalibration.Save` 在公共设备保存后重新附着相机；`AttachPhyCamera` 解除旧相机事件/反向引用，订阅新相机配置变化并设置校准服务引用。不能只看服务名称判断相机归属，需核对 Code、实际 `PhyCamera` 和模板资源。

`EditCalibration` 当前只显式检查物理相机是否存在，然后打开 `TemplateCalibrationParam` 编辑器；不是“启用 MySQL 但未连接就一定进不了窗口”。数据库依赖在后续模板操作中：模板字典 ID 为 `2`，资源加载以当前相机 `SysResourceModel.Id` 为条件，`CalibrationParam.LoadResourceParams` 在 MySQL 未连接时直接返回。窗口打开、模板列表可用、模板成功保存是三个不同事实。

设备连接配置走[公共设备配置持久化](./configuration.md)；`DisplayCalibrationConfig` 则按设备 Code 保存本地显示选择，例如后端和曝光模式。物理相机与标定资源归属见[物理相机](./camera-management.md)，不要以重开编辑器替代资源/数据库核对。

## 文件、曝光与后端选择

手动执行都需要已绑定物理相机、选中 `CalibrationParam` 和输入文件名。普通曝光模式把 R 值用于 R/G/B；`IsAdvancedExposure` 才分别使用三个值。读取到有效 CVRAW/CVCIE 文件头曝光后，界面会展示头信息并限制相应编辑；这不代表输入文件已经成功校正。

| 路径 | 输入和执行条件 | 输出与完成边界 |
| --- | --- | --- |
| 本地 `LocalFileCalibrationService.Calibrate` | 需要关联的 `DeviceCamera`、可解析的校准文件；曝光必须有限且大于 0；加载后必须含 RAW | 执行本地校正，写结果文件，再直接把结果模型交给视图；数据库保存是后续可失败步骤 |
| MQTT `MQTTCalibration.Calibration` | 发送服务可访问的 `ImgFileName`、`FileType`、`TemplateParam.ID/Name`，以及 `DeviceParam.exp=[R,G,B]`、`gain=1` | `Event_GetData` 回包再按 `MasterId` 查数据库结果；回包成功不代表客户端已读到结果文件 |

本地入口支持能加载为 RAW 的 CVRAW、TIFF 或普通位图；当前文件服务对不含 RAW 的 CVCIE 输入会报错并提示切换 MQTT，不自动回退。仅有物理相机条目也不足以启用本地校正，还须有 `PhyCamera.DeviceCamera` 和有效本地校准资源。选择本地后端时，显示入口不以 MQTT 设备已打开为前提；这不免除本地文件和运行库依赖。

MQTT 请求传的是文件名/路径，不是上传文件内容；“本机存在”不能证明服务能访问。`Calibration` 方法虽然接收 `CalibrationParam item`，实际载荷使用单独传入的模板 ID/名称和曝光；定位时以发送载荷为准。

## 结果显示与历史落库不是同一件事

本地校正调用 `LocalFrameFileService.SaveCapture`，按 `Config.FileServerCfg.DataBasePath` 和校准设备 Code 保存输出，优先选择 CVCIE，其次 CVRAW；没有生成可用文件会抛错。随后创建 `MeasureResultImgModel`，记录源文件、模板、曝光和 `Backend` 等信息。

`TryPersist` 在 MySQL 未连接时直接跳过；连接后尝试关联/创建批次并保存图像结果，失败会记日志，但已生成文件仍可用。`RunLocalCalibrationAsync` 可直接 `View.ShowResult(model)`，因此“本地操作完成并显示图像”不证明历史批次和结果记录已写入数据库。需要历史重开能力时必须另核对持久化记录及文件存续。

MQTT 结果入口和 `ResultMessageBus` 的校准图像通知都由 `ViewCalibration` 转到 `ShowPersistedResult`：要求正数 `MasterId`，按 ID 读取 `MeasureResultImgModel`，有记录才显示。消息总线还按校准路由、图像类型和设备 Code 筛选。缺少记录、数据库不可用或 `FileUrl` 不可读，应按不同阶段诊断，而不是笼统归为“校准失败”。

公共 MQTT 消息追踪的 `Success` 只对应匹配请求的 `Code=0`；它不会等待数据库查询和图像加载。不要用按钮完成、消息回包或列表已有旧项互相替代本次结果验收。

## 三种“清理”必须分开

| 入口 | 实际作用与安全边界 |
| --- | --- |
| `ReleaseLocalCalibrationCacheCommand` / 本地校正缓存管理 | 面向所有相机的本地校正上下文与进程级共享文件内存缓存，不是删除磁盘标定文件；等待正在执行的校正结束，仍被其它活动上下文引用的内存不强制释放 |
| `InfoCalibration.ServiceCache_Click` → `MQTTCalibration.CacheClear` | 界面先提示永久删除，再发远端 `Event_Delete_Data`；必须按远端删除操作授权，不能当作无副作用的排障动作 |
| `ViewCalibration` 清空列表/删除结果项 | 从当前 `ViewResults` 移除，不代表删除数据库结果、输出文件或校准缓存 |

远端清理的现有界面在任意 `MsgRecordStateChanged` 时都弹出“清理完成”，没有先判断 `Success`；该提示不能证明删除成功。应核对对应请求的 Code/终态及远端实际结果。本页不宣称已经修复该行为，也不建议因怀疑旧缓存就先执行删除。

当前 `MQTTCalibration` 只封装校正请求与缓存删除，没有独立的原始文件列表/下载方法；不要从服务名称推断这些操作可用。

## Flow 本地校正是独立入口

`Engine/ColorVision.Engine/FlowProcessing/Nodes/LocalCalibrationNode.cs` 消费流程当前内存帧，或从输入结果/文件路径加载帧；不会读取手动显示的 `UseLocalCalibration` 来决定后端。可复用的 CIE 帧可以直接沿流程传递，RAW 帧按模板校正，所以手动文件入口“不含 RAW 的 CVCIE 报错”不能扩展为整个 Flow 都不支持 CIE。

该节点 `SaveFiles` 默认关闭：内存帧可继续传递不等于已生成结果文件。它的 `SaveCalibrationResult` 需要流程批次，并要求结果保存返回正数 ID，否则抛错；关闭 `SaveFiles` 不会跳过数据库保存。它不采用手动本地服务的“可跳过 MySQL 保存”策略。文件保存、完成通知与整体流程终态须分别核对，参见[Flow 执行会话](../workflow/execution.md)。

## 证据与验证缺口

本页没有声明直接覆盖 `DeviceCalibration` 本地执行、MQTT 结果关联或缓存清理终态的自动化测试。相机模板克隆和上传测试只覆盖各自资源操作，不能作为这条运行链已经通过验证的证据。

验收需在有相应环境与授权后，分别验证校准资源适配、像素结果、输出文件、历史重开和失败清理边界。
