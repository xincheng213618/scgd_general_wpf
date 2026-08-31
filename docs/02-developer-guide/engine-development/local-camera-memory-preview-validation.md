---
knowledge_id: "engine.camera-preview-validation-plan"
knowledge_type: "guide"
status: "planned"
summary: "列出尚未实施的相机内存预览阶段、验收用例和实施前需要重新核对的源码。"
aliases: ["相机内存预览怎样验收","LocalCameraNode","ViewCamera","LocalFlowFrame"]
code_paths: ["Engine/ColorVision.Engine/FlowProcessing/Nodes/LocalCameraNode.cs","Engine/ColorVision.Engine/Services/Devices/Camera/Local/LocalFlowFrame.cs","Engine/ColorVision.Engine/Services/Devices/Camera/Local/LocalCameraCaptureService.cs","Engine/ColorVision.Engine/Services/Devices/Camera/Views/ViewCamera.xaml.cs"]
test_paths: []
related: ["engine.camera-preview-plan","engine.camera-preview-lifecycle-plan"]
---

# 本地相机内存帧预览：实施与验证

本页是待实施验收计划，以下阶段和用例尚不是测试执行结果；实施后应补实际测试路径、运行记录与未通过项，再修改知识状态。

本文补充[方案总览](./local-camera-memory-preview.md)的实施顺序、验收要求和源码入口。

## 实施顺序

### 第一阶段：当前帧 RAW 预览

1. 增加设备级预览 Publisher。
2. 发布时取得租约，并实现 latest-wins。
3. 提取 RAW 到 `WriteableBitmap` 的 Presenter。
4. `ViewCamera` 只更新当前图像，不支持历史内存帧重开。
5. 不自动激活或切换设备 Tab。
6. 预览异常只记录日志，不改变流程执行结果。

### 第二阶段：完整 CIE 预览

1. 为 `CVRawOpen.AttachLiveCvcie(...)` 增加指针入口。
2. 明确 `CM_SetBufferXYZ` 调用后的 native 缓冲所有权。
3. 新图像到来时释放旧 CIE buffer，并重置属性与工具状态。
4. 增加 `Off`、`Raw`、`FullCie` 配置。

### 第三阶段：结果列表语义

1. 区分文件结果、当前内存结果和已过期内存结果。
2. 评估 `ViewResults` 是否按设备实例维护；此前不把租约放入全局集合。
3. 保存文件的结果继续支持历史重开。
4. 无文件结果被替换后显示“内存帧已释放”。

## 验收要求

1. `SaveFiles=false` 时不创建 CVRAW/CVCIE，绑定设备 View 仍显示当前帧。
2. 节点绑定设备 A 时，不更新设备 B 的 View。
3. View 未加载、不可见或关闭预览时，不额外持有帧。
4. 流程释放根引用后，已排队的 UI 预览仍能安全完成。
5. 新帧覆盖未显示的旧帧时，旧租约立即释放。
6. 连续执行时待显示请求始终有界，Private Bytes 不无界增长。
7. `SaveFiles=true` 的文件保存和历史打开行为不回归。
8. 预览转换或 UI 更新失败时，流程仍正常完成。
9. RAW 三通道和单通道 8/16-bit 的像素格式、通道顺序正确。
10. 完整 CIE 模式替换图像后，取点、伪彩和图层正确释放旧状态。

## 实施前源码检查

开始实现前应重新检查：

- 流程入口：`LocalCameraNode.cs`、`CVStartCFC.cs`
- 帧生命周期：`LocalCameraCaptureService.cs`、`LocalFlowFrame.cs`
- 设备与视图：`DeviceCamera.cs`、`ViewCamera.xaml.cs`、`ViewCameraConfig.cs`
- 现有显示逻辑：`CameraLocalWindow.xaml.cs`、`ImageView.xaml.cs`
- CIE 挂载：`CVRawOpen.cs`

这些文件分别位于：

- `Engine/ColorVision.Engine/FlowProcessing/Nodes/`
- `Engine/ColorVision.Engine/Services/Devices/Camera/Local/`
- `Engine/ColorVision.Engine/Services/Devices/Camera/Views/`
- `Engine/ColorVision.Engine/Services/Devices/Camera/`
- `Engine/ColorVision.Engine/Media/`
- `Engine/FlowEngineLib/Base/`
- `UI/ColorVision.ImageEditor/`

## 已确定原则

- 不使用临时文件模拟内存预览。
- 节点不直接操作 WPF View。
- 发布时取得租约，UI 完成后释放。
- 每台设备只保留最新待显示帧。
- 文件持久化与当前帧预览相互独立。
- 第一阶段不要求历史无文件结果重新打开。

## 实施前确认

- 默认预览模式是 `Raw` 还是 `Off`。
- View 不可见时跳过预览，还是保存最新的轻量显示副本。
- 是否自动把结果行加入列表并选中。
- 完整 CIE 功能是否属于第一版范围。

## 验证入口与缺口

验证缺口：本页是未来验收清单而非测试结果；预览功能实施前重新核对源码，实施后记录实际执行命令、设备与样例条件。
