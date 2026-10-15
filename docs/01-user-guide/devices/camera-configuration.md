---
knowledge_id: "operations.camera-configuration"
knowledge_type: "topic"
status: "current"
summary: "相机参数的编辑入口、同步覆盖与保存；CameraID统一以物理配置为准，搜索保存身份并同步逻辑镜像，路径移动失败或被拒绝不等于取消路径变更。"
aliases: ["曝光","增益","相机参数","相机配置被覆盖","ROI","三通道曝光","ConfigCamera","ConfigPhyCamera","DisplayCameraConfig","CameraRunParam","ApplyTo","IsExpThree","LocalVideoRoi","includeCameraId","本地相机ID","应用设置","相机数据路径"]
code_paths: ["Engine/ColorVision.Engine/Services/Devices/Camera/Configs/ConfigCamera.cs","Engine/ColorVision.Engine/Services/Devices/Camera/DeviceCamera.cs","Engine/ColorVision.Engine/Services/Devices/Camera/DisplayCamera.xaml.cs","Engine/ColorVision.Engine/Services/Devices/Camera/EditCamera.xaml","Engine/ColorVision.Engine/Services/Devices/Camera/EditCamera.xaml.cs","Engine/ColorVision.Engine/Services/Devices/Camera/Templates/CameraRunParam/CameraRunParam.cs","Engine/ColorVision.Engine/Services/PhyCameras/Configs/ConfigPhyCamera.cs","Engine/ColorVision.Engine/Services/PhyCameras/Configs/PhyCameraCfg.cs","Engine/ColorVision.Engine/Services/PhyCameras/EditConfigPhyCamera.xaml.cs","Engine/ColorVision.Engine/Services/PhyCameras/InfoPhyCamera.xaml","Engine/ColorVision.Engine/Services/PhyCameras/PhyCamera.cs","Engine/ColorVision.Engine/FlowProcessing/Nodes/LocalCameraNode.cs","Engine/ColorVision.Engine/FlowProcessing/Editor/FlowNodeContextMenuService.cs","Engine/ColorVision.Engine/FlowProcessing/Editor/CameraNodeCreationDefaults.cs"]
test_paths: ["Test/ColorVision.UI.Tests/DeviceCameraAssociationTests.cs","Test/ColorVision.UI.Tests/LocalCameraSessionTests.cs","Test/ColorVision.UI.Tests/CameraRunParamTests.cs","Test/ColorVision.UI.Tests/ConfigPhyCameraApplyTests.cs","Test/ColorVision.UI.Tests/CameraNodeCreationDefaultsTests.cs","Test/ColorVision.UI.Tests/MotorConfigurationEditorTests.cs"]
related: ["operations.camera","operations.physical-camera","operations.device-configuration"]
---

# 相机参数来源、同步与保存

相机参数分别保存在物理配置、逻辑服务配置、显示参数和流程节点/模板中。本页说明从哪里修改、何时同步，以及保存的影响范围。先确认执行的是远程手动采集、本地视频还是流程节点，再核对下表中的参数来源；执行与完成判据见[相机服务](./camera.md)。

## 参数放在哪一层

| 对象 / 源码入口 | 负责的状态 |
| --- | --- |
| `ConfigPhyCamera` | 物理型号、模式、通道/位深、CFW、电机、曝光/增益默认值与范围；`CameraCfg` 另含传感器 ROI、温控等物理参数 |
| `ConfigCamera` | 逻辑服务的 `CameraCode` / `CameraID`、采集模式、通道/位深、自动曝光开关与范围、ND/CFW 和电机/对焦 |
| `DisplayCameraConfig`（`DisplayCamera.xaml.cs`） | 按逻辑设备 `Config.Code` 获取；手动采集的曝光、增益、平均次数、翻转、模板选择和“本地取图保存文件”，以及本地视频 ROI/显示偏好；饱和度字段也在这里，不在 `ConfigCamera` |
| `CameraRunParam` | 参数模板里的曝光、增益、平均次数、焦点/光圈等；不是显示参数的别名 |
| `LocalCameraNode` | 用节点自身的 `ExpTime`、`Gain`、`AvgCount` 构造 `CameraRunParam`；右键新建时从相机显示参数取一次初值，此后仍使用节点自身保存的值 |
| `DeviceCamera.RealtimeCameraConfig` | 返回共享的 `DefaultRealtimeCameraConfig.Current`；不是每台相机独立克隆的配置 |

## 编辑入口与物理配置同步

编辑物理参数时，在“物理相机管理”选择目标相机，点击详情区的“修改配置”；编辑逻辑服务时使用该逻辑相机的 `EditCommand`，此入口要求管理员权限。逻辑配置窗口的“应用设置”把所选物理参数带入编辑副本，“提交”才把副本复制回服务并保存。两者都可能影响绑定服务，确认前先核对逻辑 `Code` 与物理 `CameraCode`。

`ConfigPhyCamera.ApplyTo(ConfigCamera)` 复制通道、CFW、电机、模式、型号、采集模式、位深和曝光上下限。`includeCameraId` 默认 `true`，逻辑相机的 `CameraID` 是物理配置的兼容镜像；`includeCameraType` 默认 `true`。它不复制物理 `CameraCfg`、文件服务配置或显示端当前曝光/增益。

电机配置的“启用电机”排在最前面，关闭时隐藏电机的详细参数及 VID；开启后恢复原有参数，不清空串口、行程或映射文件。VID 同样先显示“启用 VID”，关闭时隐藏映射文件和拟合参数。显示条件使用共享属性编辑器的元数据，物理配置窗口和通用属性编辑入口保持一致；独立电机设备使用的基础配置仍直接显示其参数。相机联动 `IsCameraLinkage` 默认 `false`，不在可编辑界面显示，保留已有配置值的读取与保存兼容；它控制电机连接失败是否使相机打开失败，不是自动对焦开关。

| 调用位置 | `CameraID` | `CameraType` |
| --- | --- | --- |
| 逻辑配置窗口选择物理相机或“应用设置” | 同步所选物理相机 ID，同时更新 `CameraCode` | 保留，显式传 `false` |
| `DeviceCamera.Save()` | 同步所绑定物理相机的 ID，同一绑定也修复旧逻辑 ID | 从物理配置同步 |
| 物理配置保存触发 `PhyCameraConfigChanged` | 从物理配置同步 | 从物理配置同步 |
| 其它调用显式传 `includeCameraId: true` | 从物理配置同步 | 取决于 `includeCameraType` |

`CameraCode`/MD5 是不变的物理绑定，`CameraID` 由该物理相机持有。构造、重新关联、物理 ID 更新和设备保存均同步逻辑镜像；打开本地或服务相机时也重新读取物理 ID，避免使用旧调用参数或旧逻辑 ID。完整 `MD5(CameraID)` 必须与物理 Code 相符，不能以部分匹配或列表第一台替换绑定。已绑定设备在本地相机管理中不能独立修改 ID；未绑定的独立本地相机保留手动选择能力。

显式搜索在当前配置库保存已匹配物理相机的 ID，再同步相同存储中绑定它的相机与校准设备；只更新身份字段，不调用设备 `Save()`、物理参数 `ConfigChanged` 或服务重启，也不重置曝光、增益或校正配置。逻辑配置保留 `CameraID` 字段以兼容旧协议，不能作为独立身份来源。搜索不创建尚未配置的相机、不清空未发现相机的 ID，失败诊断中的重新检测也不写配置。

提交按钮先检查是否允许切换物理绑定，再把 `EditCamera` 的配置克隆复制回服务，调用一次 `DeviceCamera.Save()`，按 `CameraCode` 重新关联并以表中规则同步，随后执行[数据库保存与服务重启](./configuration.md)。保存完成才关闭窗口；失败时显示原始错误并记录日志。当前相机仍由本地会话、视频或服务占用时，切换会明确提示先关闭相机，编辑副本不写回当前配置；保存同一物理绑定不受这项限制。窗口选择列表仅包含尚未关联逻辑相机，或关联相机 `Name` 与当前设备相同的物理对象；这个界面过滤不能视为全局唯一绑定校验。

当物理相机执行 `SaveConfig()` 时，`ConfigChanged` 会触发绑定设备的 `DeviceCamera.PhyCameraConfigChanged`：再次 `ApplyTo`，将显示端 `Gain` 重置为 `GainDefault`，将 `ExpTime` 和 R/G/B 三项都重置为 `ExpDefalut`，然后 `Save()`。所以“刚调好的曝光被改回”应先检查物理配置保存和绑定事件，不应立即判成 UI 没保存。

## 曝光、通道与模板的区别

`ConfigCamera.IsExpThree` 的条件是 `TakeImageMode != Live && CameraMode == CV_MODE`；`IsChannelThree` 则只检查 `Channel == Three`。三通道图像不自动意味着发送三份曝光，Live 模式也不走这一三曝光分支。

远程手动采集根据 `IsExpThree` 发送显示端 `[ExpTimeR, ExpTimeG, ExpTimeB]` 或 `[ExpTime]`，并读取所选校准、自动曝光、HDR 模板。选择有效校准模板后，面板会立即把增益同步为该模板引用的校正组增益并禁止编辑，与远程服务端的覆盖顺序保持一致；选择 `Empty` 后恢复增益编辑。自动曝光返回还可更新显示曝光/饱和度和 `Config.NDPort`，因此这些值可能被成功响应改写。

流程属性面板中的直接增益相机节点（包括 CV、L/BV 和“本地相机取图”）使用同一规则：选择校准模板后同步并锁定校正组增益，清除模板后恢复编辑。节点下拉框不增加 `Empty` 项，使用已有的清除按钮移除选择。节点切换相机设备时会按新设备重新解析；本地节点执行时还会再次使用校正组增益，保证未打开属性面板的旧流程也不会把旧增益用于取图。

本地相机管理同样会同步并锁定校正组增益，同步后的 `DisplayConfig.Gain` 会用于本地 `CM_SetGain` 和取图元数据。如果窗口由“本地相机取图”节点打开，该增益也会同步回节点参数。

`CameraRunParam.SetAllExposure(value)` 同时写四个曝光字段；`LocalCameraNode.BuildCameraParameters()` 用它构造节点参数，并拒绝非有限/非正曝光、非有限/负增益以及小于 1 的平均次数。这是该节点的检查，不能扩展成所有配置入口都有同样数值校验。改参数模板、显示面板和节点字段是三件不同的事。

流程编辑器右键新建 L/BV、CV、本地“相机取图”和三个直接填写参数的 AOI 相机节点时，会从节点对应逻辑相机的当前显示配置读取一次曝光、翻转、平均次数、增益和所选校正模板。CV 节点保留 R/G/B 三路曝光；其余节点使用单路 `ExpTime`。若校正模板绑定的校正组指定增益，以组增益作为初值。节点设备代码还是默认占位值且找不到同名相机时，使用首个可用的逻辑相机并写入其设备代码；没有可用相机或源参数无效时保留节点原有默认值。已保存流程的加载、复制、导入、拖拽及 Copilot 加节点不执行这次同步；创建后更改手动面板也不会回写已有节点。通用/模板型相机和循环取图节点继续按自身的模板或循环参数运行。

## 两种 ROI 与物理保存约束

物理 ROI 是 `ConfigPhyCamera.CameraCfg` 的 `PointX / PointY / Width / Height`（经 `PhyCameraCfg.ROI` 编辑）；`DisplayCameraConfig.LocalVideoRoi` 则用于实时分析/画面 ROI，由 `ApplyLocalVideoRoiToRealtimeConfig` 传给实时配置，不等于修改传感器采集 ROI。

`EditConfigPhyCamera` 使用配置克隆和独立的 CFW 编辑副本。确认时，对 `HK_USB / HK_CARD / HK_FG_CARD` 检查物理 ROI 宽高是否按 `PhyCameraCfg.HkRoiAlignment`（32）对齐。未对齐时显示当前值和未对齐项，询问是否继续保存，默认选择“否”；选择“是”按原值继续保存，选择“否”或关闭提示则停留在配置窗口。此提示不强制调整 ROI，也不包含所有型号/坐标合法性检查，不应宣称每个保存入口都执行了它。

创建与编辑物理配置的 `PhyCameraConfigEditor` 按相同规则约束可选通道：BV 可选单通道或三通道，CV 固定三通道，LV 固定单通道并隐藏通道选择。切换到 CV 模式启用 `CFW.IsUseCFW` 并选中 CFW 分类，切换到其他模式关闭该开关并返回可用分类；初次载入保留原有 CFW 开关。`PhyCamera.SaveConfig()` 保存前还会规范化 CFW：不启用时清空 `ChannelCfgs` 并关闭 `IsCOM`；绑定独立 ND 设备时清空串口名，否则清空 ND 绑定代码。这些不是只影响显示的开关。

## 改数据路径与保存的副作用

物理窗口确认时，`HandleFileServerPathChanged` 比较旧/新 `FileBasePath`，先创建目标相机目录；若旧目录存在且同意弹窗，实际调用 `ShellFileOperations.Move(sourceDir, newBasePath)`。即使界面文案提到复制，实现仍是**移动原目录**，不是保留原路径的备份。

拒绝这次移动提示、旧目录不存在，或移动异常被函数捕获后，确认流程仍会继续把新路径复制回物理配置并由调用方保存；这几种情况都不等于取消路径变更。创建目标目录等未被捕获的异常则可能中断提交。核对时分别检查配置里的新路径、目标目录实际内容和原目录是否仍在；文件移动与配置保存没有整体回滚。

修改/提交相机配置前应确认目标设备、会影响的服务、文件目录与授权；路径迁移尤其需要确认可移动的确切目录和数据保全方案。窗口关闭、配置写入、后台服务重启请求、硬件采用新参数是不同阶段，不能用一次“保存”提示证明全部生效。

## 验证与排障入口

参数不一致时，按“当前入口 → 逻辑设备与物理绑定 → 本次读取的配置对象 → `ApplyTo`/回调覆盖 → 实际请求参数”核对；物理导入可能重置配置的独立风险见[物理相机管理](./camera-management.md)。

`ConfigPhyCameraApplyTests` 覆盖 `ApplyTo` 默认同步物理 ID 和型号/位深；`DeviceCameraAssociationTests` 覆盖绑定切换、同一绑定修复旧逻辑 ID 和物理 ID 更新通知；`PhysicalCameraIdentityTests` 覆盖身份写入本地配置库、未知配置字段保留、重复扫描、失败重试及不重启设备；`LocalCameraSessionTests` 覆盖旧配置无需打开管理窗口即可解析正确 ID，以及 ID 更新后拒绝复用旧连接。`CameraRunParamTests` 覆盖 `SetAllExposure`；`CameraNodeCreationDefaultsTests` 覆盖新建节点初始化。这些测试不代替 MySQL、服务重启、路径移动和真机采集验收，测试文件存在不代表已运行。
