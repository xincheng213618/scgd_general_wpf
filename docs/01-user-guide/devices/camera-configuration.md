---
knowledge_id: "operations.camera-configuration"
knowledge_type: "topic"
status: "current"
summary: "相机参数的编辑入口、同步覆盖与保存；物理配置同步保留本地CameraID，路径移动失败或被拒绝不等于取消路径变更。"
aliases: ["曝光","增益","相机参数","相机配置被覆盖","ROI","三通道曝光","ConfigCamera","ConfigPhyCamera","DisplayCameraConfig","CameraRunParam","ApplyTo","IsExpThree","LocalVideoRoi","includeCameraId","本地相机ID","应用设置","相机数据路径"]
code_paths: ["Engine/ColorVision.Engine/Services/Devices/Camera/Configs/ConfigCamera.cs","Engine/ColorVision.Engine/Services/Devices/Camera/DeviceCamera.cs","Engine/ColorVision.Engine/Services/Devices/Camera/DisplayCamera.xaml.cs","Engine/ColorVision.Engine/Services/Devices/Camera/EditCamera.xaml","Engine/ColorVision.Engine/Services/Devices/Camera/EditCamera.xaml.cs","Engine/ColorVision.Engine/Services/Devices/Camera/Templates/CameraRunParam/CameraRunParam.cs","Engine/ColorVision.Engine/Services/PhyCameras/Configs/ConfigPhyCamera.cs","Engine/ColorVision.Engine/Services/PhyCameras/Configs/PhyCameraCfg.cs","Engine/ColorVision.Engine/Services/PhyCameras/EditConfigPhyCamera.xaml.cs","Engine/ColorVision.Engine/Services/PhyCameras/InfoPhyCamera.xaml","Engine/ColorVision.Engine/Services/PhyCameras/PhyCamera.cs","Engine/ColorVision.Engine/FlowProcessing/Nodes/LocalCameraNode.cs"]
test_paths: ["Test/ColorVision.UI.Tests/CameraRunParamTests.cs","Test/ColorVision.UI.Tests/ConfigPhyCameraApplyTests.cs"]
related: ["operations.camera","operations.physical-camera","operations.device-configuration"]
---

# 相机参数来源、同步与保存

相机参数分别保存在物理配置、逻辑服务配置、显示参数和流程节点/模板中。本页说明从哪里修改、何时同步，以及保存的影响范围。先确认执行的是远程手动采集、本地视频还是流程节点，再核对下表中的参数来源；执行与完成判据见[相机服务](./camera.md)。

## 参数放在哪一层

| 对象 / 源码入口 | 负责的状态 |
| --- | --- |
| `ConfigPhyCamera` | 物理型号、模式、通道/位深、CFW、电机、曝光/增益默认值与范围；`CameraCfg` 另含传感器 ROI、温控等物理参数 |
| `ConfigCamera` | 逻辑服务的 `CameraCode` / `CameraID`、采集模式、通道/位深、自动曝光开关与范围、ND/CFW、电机/对焦、文件缓存和保存选项 |
| `DisplayCameraConfig`（`DisplayCamera.xaml.cs`） | 按逻辑设备 `Config.Code` 获取；手动采集的曝光、增益、平均次数、翻转和模板选择，以及本地视频 ROI/显示偏好；饱和度字段也在这里，不在 `ConfigCamera` |
| `CameraRunParam` | 参数模板里的曝光、增益、平均次数、焦点/光圈等；不是显示参数的别名 |
| `LocalCameraNode` | 用节点自身的 `ExpTime`、`Gain`、`AvgCount` 构造 `CameraRunParam`；不自动沿用手动面板曝光 |
| `DeviceCamera.RealtimeCameraConfig` | 返回共享的 `DefaultRealtimeCameraConfig.Current`；不是每台相机独立克隆的配置 |

## 编辑入口与物理配置同步

编辑物理参数时，在“物理相机管理”选择目标相机，点击详情区的“修改配置”；编辑逻辑服务时使用该逻辑相机的 `EditCommand`，此入口要求管理员权限。逻辑配置窗口的“应用设置”把所选物理参数带入编辑副本，“提交”才把副本复制回服务并保存。两者都可能影响绑定服务，确认前先核对逻辑 `Code` 与物理 `CameraCode`。

`ConfigPhyCamera.ApplyTo(ConfigCamera)` 复制通道、CFW、电机、模式、型号、采集模式、位深和曝光上下限。`includeCameraId` 默认 `false`，保留逻辑服务中用于当前运行环境的 `CameraID`；`includeCameraType` 默认 `true`。它不复制物理 `CameraCfg`、文件服务配置或显示端当前曝光/增益。

| 调用位置 | `CameraID` | `CameraType` |
| --- | --- | --- |
| 逻辑配置窗口选择物理相机或“应用设置” | 保留，显式传 `false` | 保留，显式传 `false` |
| `DeviceCamera.Save()` | 保留，显式传 `false` | 从物理配置同步 |
| 物理配置保存触发 `PhyCameraConfigChanged` | 保留，显式传 `false` | 从物理配置同步 |
| 其它调用显式传 `includeCameraId: true` | 从物理配置同步 | 取决于 `includeCameraType` |

因此“应用设置”不会替你复制物理相机的 `CameraID`；随后提交可能同步 `CameraType`，仍保留本地 ID。排查硬件 ID 不一致时要核对当前运行环境中的逻辑配置，不能假设保存物理配置就会把两个 ID 改成相同。

提交按钮在 `EditCamera.xaml` 同时绑定 `Click` 和 `SaveCommand`：先把 `EditCamera` 的配置克隆复制回服务，再进入 `DeviceCamera.Save()`，按 `CameraCode` 重新关联并以表中规则同步，随后执行[数据库保存与服务重启](./configuration.md)。窗口选择列表仅包含尚未关联逻辑相机，或关联相机 `Name` 与当前设备相同的物理对象；这个界面过滤不能视为全局唯一绑定校验。

当物理相机执行 `SaveConfig()` 时，`ConfigChanged` 会触发绑定设备的 `DeviceCamera.PhyCameraConfigChanged`：再次 `ApplyTo`，将显示端 `Gain` 重置为 `GainDefault`，将 `ExpTime` 和 R/G/B 三项都重置为 `ExpDefalut`，然后 `Save()`。所以“刚调好的曝光被改回”应先检查物理配置保存和绑定事件，不应立即判成 UI 没保存。

## 曝光、通道与模板的区别

`ConfigCamera.IsExpThree` 的条件是 `TakeImageMode != Live && CameraMode == CV_MODE`；`IsChannelThree` 则只检查 `Channel == Three`。三通道图像不自动意味着发送三份曝光，Live 模式也不走这一三曝光分支。

远程手动采集根据 `IsExpThree` 发送显示端 `[ExpTimeR, ExpTimeG, ExpTimeB]` 或 `[ExpTime]`，并读取所选校准、自动曝光、HDR 模板。自动曝光返回还可更新显示曝光/饱和度和 `Config.NDPort`，因此这些值可能被成功响应改写。

`CameraRunParam.SetAllExposure(value)` 同时写四个曝光字段；`LocalCameraNode.BuildCameraParameters()` 用它构造节点参数，并拒绝非有限/非正曝光、非有限/负增益以及小于 1 的平均次数。这是该节点的检查，不能扩展成所有配置入口都有同样数值校验。改参数模板、显示面板和节点字段是三件不同的事。

## 两种 ROI 与物理保存约束

物理 ROI 是 `ConfigPhyCamera.CameraCfg` 的 `PointX / PointY / Width / Height`（经 `PhyCameraCfg.ROI` 编辑）；`DisplayCameraConfig.LocalVideoRoi` 则用于实时分析/画面 ROI，由 `ApplyLocalVideoRoiToRealtimeConfig` 传给实时配置，不等于修改传感器采集 ROI。

`EditConfigPhyCamera` 使用配置克隆和独立的 CFW 编辑副本。确认时，对 `HK_USB / HK_CARD / HK_FG_CARD` 检查物理 ROI 宽高是否按 `PhyCameraCfg.HkRoiAlignment`（32）对齐，失败则停留在配置窗口。此检查不包含所有型号/坐标合法性，也不应宣称每个保存入口都执行了它。

物理窗口按相机模式约束可选通道；切换模式可能同时改 `CFW.IsUseCFW`。`PhyCamera.SaveConfig()` 保存前还会规范化 CFW：不启用时清空 `ChannelCfgs` 并关闭 `IsCOM`；绑定独立 ND 设备时清空串口名，否则清空 ND 绑定代码。这些不是只影响显示的开关。

## 改数据路径与保存的副作用

物理窗口确认时，`HandleFileServerPathChanged` 比较旧/新 `FileBasePath`，先创建目标相机目录；若旧目录存在且同意弹窗，实际调用 `ShellFileOperations.Move(sourceDir, newBasePath)`。即使界面文案提到复制，实现仍是**移动原目录**，不是保留原路径的备份。

拒绝这次移动提示、旧目录不存在，或移动异常被函数捕获后，确认流程仍会继续把新路径复制回物理配置并由调用方保存；这几种情况都不等于取消路径变更。创建目标目录等未被捕获的异常则可能中断提交。核对时分别检查配置里的新路径、目标目录实际内容和原目录是否仍在；文件移动与配置保存没有整体回滚。

修改/提交相机配置前应确认目标设备、会影响的服务、文件目录与授权；路径迁移尤其需要确认可移动的确切目录和数据保全方案。窗口关闭、配置写入、后台服务重启请求、硬件采用新参数是不同阶段，不能用一次“保存”提示证明全部生效。

## 验证与排障入口

参数不一致时，按“当前入口 → 逻辑设备与物理绑定 → 本次读取的配置对象 → `ApplyTo`/回调覆盖 → 实际请求参数”核对；物理导入可能重置配置的独立风险见[物理相机管理](./camera-management.md)。

`ConfigPhyCameraApplyTests` 覆盖 `ApplyTo` 默认保留本地 `CameraID`、同步型号/位深及显式复制 ID；`CameraRunParamTests` 覆盖 `SetAllExposure` 和自定义编辑器一次更新全部曝光字段。它们不覆盖保存事件整链、数据库/服务重启、路径移动或硬件参数生效；这些路径需专门测试与授权环境验收，测试文件存在不代表已运行。
