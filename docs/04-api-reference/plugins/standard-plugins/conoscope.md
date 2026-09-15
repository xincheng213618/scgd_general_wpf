---
knowledge_id: "plugins.conoscope"
knowledge_type: "topic"
status: "current"
summary: "Conoscope 的采集、CVCIE 首屏/XYZ 就绪、Polar 与三种 H/V 显示、Mat 与分析快照契约；按钮成功不代表文档加载完成。"
aliases: ["锥镜图像怎么看","Conoscope 依赖哪些 DLL","锥镜采集完成没有图像","Conoscope","VAM","H/V 坐标","Horizontal-Vertical Coordinates","笛卡尔角度坐标","North Polar","East Polar","HV North Polar","HV East Polar","ConoscopeCaptureWorkflow","ConoscopeFlowCaptureResult","ConoscopeCameraCaptureResult","ConoscopeDocument","ConoscopeDocumentChangeKind","ConoscopeView","ConoscopeViewState","ConoscopeImageHost","ConoscopeHorizontalVerticalProjection","ConoscopeAnalysisSession","MeasurementCaptureAlignment","ConoscopeConfigWindow","ConoscopeGlobalReferenceStore","FocusPoiTemplateRepository","CONOSCOPE_REAL_SAMPLE"]
code_paths: ["Plugins/Conoscope/README.md","Plugins/Conoscope/Docs/ARCHITECTURE.md","Plugins/Conoscope/Conoscope.csproj","Engine/cvColorVision/cvColorVision.csproj","Plugins/Conoscope/manifest.json","Plugins/Conoscope/Core/ConoscopeModuleService.cs","Plugins/Conoscope/ConoscopeWindow.xaml","Plugins/Conoscope/ConoscopeWindow.xaml.cs","Plugins/Conoscope/Application/Capture/ConoscopeCaptureWorkflow.cs","Plugins/Conoscope/ConoscopeDocument.cs","Plugins/Conoscope/ConoscopeView.xaml","Plugins/Conoscope/ConoscopeView.xaml.cs","Plugins/Conoscope/ConoscopeImageHost.xaml.cs","Plugins/Conoscope/Core/ConoscopeCoordinateAxis.cs","Plugins/Conoscope/Core/ConoscopeHorizontalVerticalProjection.cs","Plugins/Conoscope/Core/ConoscopePseudoColorRenderer.cs","Plugins/Conoscope/Application/Preprocess/ConoscopePreprocessPipeline.cs","Plugins/Conoscope/Processing/Preprocess/","Plugins/Conoscope/Application/Analysis/ConoscopeAnalysisSession.cs","Plugins/Conoscope/Application/Analysis/FocusPointMeasurementService.cs","Plugins/Conoscope/Analysis/MeasurementCaptureModels.cs","Plugins/Conoscope/Analysis/AnalysisResultCsvExporter.cs","Plugins/Conoscope/Application/FocusPoiTemplateRepository.cs","Plugins/Conoscope/Core/ConoscopeConfig.cs","Plugins/Conoscope/Core/ConoscopeConfigWindow.xaml.cs","Plugins/Conoscope/ConoscopePreprocessSettingsControl.xaml","Plugins/Conoscope/Core/ConoscopeGlobalReferenceStore.cs","Plugins/Conoscope/Core/ConoscopeReferenceMatSerializer.cs","Plugins/Conoscope/Core/ConoscopeExportService.cs","Plugins/Conoscope/MVS/","PluginProject.HostCopy.targets"]
test_paths: ["Test/Conoscope.Tests/Conoscope.Tests.csproj","Test/Conoscope.Tests/ConoscopeDocumentTests.cs","Test/Conoscope.Tests/CvcieChannelReaderTests.cs","Test/Conoscope.Tests/ConoscopeViewBoundaryTests.cs","Test/Conoscope.Tests/ConoscopeHorizontalVerticalProjectionTests.cs","Test/Conoscope.Tests/ConoscopeAnalysisSessionTests.cs","Test/Conoscope.Tests/ConoscopeColorimetryTests.cs","Test/Conoscope.Tests/MvsCaptureSessionTests.cs","Test/Conoscope.Tests/ArchitectureSmokeTests.cs","Test/Conoscope.Tests/AdvancedExportSettingsTests.cs"]
related: ["plugins.index","plugins.capabilities","engine.file-io","flow.session","operations.camera","ui.configuration","plugins.getting-started","engine.native-bindings"]
---

# Conoscope 图像、采集与分析

Conoscope 是 VAM/锥镜图像观察、关注点采样、色域/对比度计算与导出插件。本主题是其单一正文；源码旁 README 保留包入口和必要风险，`Docs/ARCHITECTURE.md` 只指向这里，不再要求同一行为维护三套说明。

## 入口与运行依赖

宿主 Tool 菜单的 `VAM` 进入 `ConoscopeWindow`。ImageEditor 右键入口由 `ConoscopeModuleService.CanOpenFromImageView` 检查当前文件存在、`CVFileUtil.IsCVCIEFile` 为真且编辑器配置 `Channel == 3`；这不是仅凭扩展名允许任意图像进入。模块入口负责寻找/打开窗口，单文档 View 不靠静态 Window 单例刷新业务状态。

身份和最低宿主要求读取 `manifest.json`，发布版本以 `Conoscope.csproj` 生成的 DLL `FileVersion` 为准。当前工程为 Windows/x64、`net10.0-windows` WPF，引用 Engine、ImageEditor 和 Solution；完整运行还依赖匹配的 ColorVision/cvColorVision 库、OpenCV 及所用功能的供应商运行库，不能只交付一个插件 DLL。`CVCommCore.*` / `MQTTMessageLib.*` 当前类型来源与旧独立程序集的兼容要求见 [cvColorVision 命名空间与程序集](../../engine-components/cvColorVision.md#命名空间与程序集)，不把历史 DLL 名称当作本项目固定输出。

未打开文档时，主页显示紧凑的打开、型号和观察相机操作，工作区显示“打开数据文件”和“前往采集”入口，空文档标签栏不占位；采集、处理、分析和系统页仍可访问。打开文档后恢复主页的视图与导出分组，关闭最后一个文档后返回开始页。空工作区按整个停靠布局（包括浮动文档）判断，不以当前活动图像是否就绪判断，避免加载和切换时误回开始页。采集期间开始页入口暂时禁用并显示进度；切换工作区状态不改变用户手动折叠 Ribbon 的选择。

这里有三种不同来源：

| 来源 | 责任与前提 |
| --- | --- |
| 已有 CVCIE | 从本地文件读取内嵌通道；不要求相机硬件。文件格式、通道读取及版本限制见 [FileIO](../../engine-components/ColorVision.FileIO.md) |
| Ribbon 测量采集 | `ConoscopeCaptureWorkflow` 调用 Engine Flow 或服务列表中的 `DeviceCamera`，需要对应模板/设备服务与结果记录 |
| MVS 观察相机 | `MVSViewManager`、`MvsCaptureSession` 和观察窗口管理预览/光栅；另需海康驱动及 `MvCameraControl.dll`，不是 Engine 测量相机的替代实现 |

打开本地文件不授权触发设备；Flow、相机采集、数据库保存和导出分别需要相应权限及现场授权。菜单出现、驱动存在或观察画面正常不证明 Engine 测量链可用。

## 采集完成、文件发现与打开不是一个信号

`ConoscopeWindow` 的按钮通过 `ConoscopeCaptureWorkflow` 执行业务，再尝试打开结果。进度按钮的默认预期时间为 `20000ms`，是显示估计，不是执行超时。

| 阶段 | 当前判据 | 不代表什么 |
| --- | --- | --- |
| Flow 返回 | `ConoscopeFlowCaptureResult.Started` 仅检查返回对象非空，`Completed` 检查 `FlowStatus.Completed` | 不代表已经找到 CVCIE，也不是原生设备安全确认 |
| 相机回包 | 等待 `MsgRecordState.Success / Fail / Timeout`，只有 `Success` 继续找文件 | 不代表文件已经落地或本地可读 |
| 找到文件 | `HasFile` 表示得到非空路径；查找器只接受现存、扩展名为 `.cvcie` 的候选 | 不验证文件内容、三个通道或最终渲染 |
| 窗口操作成功 | 业务成功且有文件时调用 `OpenConoscope`，然后将按钮操作标为成功 | 不等待文档首屏或完整 XYZ 加载 |
| 文档/显示 | 由下节事件、数据状态和 View 渲染另行完成 | 与 Flow/消息状态、按钮计时结果不同 |

Flow 路径先用返回结果的 `SerialNumber` 查询批次，找不到则回退当前 `FlowEngineManager.Batch`；因此该回退不能被描述为严格绑定本次结果。有效批次下最多查询结果10轮，每次未找到后等待300ms，并取枚举中第一个可用 CVCIE。相机路径最多查询8轮，每次未找到后等待300ms：从当前 `MsgReturn.Data.MasterId` 查结果，整数读取失败返回0；两条链都按 `FileUrl`、`RawFile` 顺序找现存文件，不负责下载远端 URL。

`CaptureCameraAsync` 按相机配置选择单曝光或 R/G/B 三曝光，传入选中的标定参数，以及 ID 为 `-1` 的自动曝光/JSON 模板参数。它复用的是 [Engine 相机契约](../../../01-user-guide/devices/camera.md)，Flow 执行另见 [FlowExecutionSession](../../../01-user-guide/workflow/execution.md)。

`WaitForMsgRecordAsync` 订阅状态事件后再次检查状态以缩小漏接终态的窗口，完成后退订；本层没有独立超时或取消参数，等待仍依赖上游进入三种终态。文件查询轮数也不包含之前的 Flow/消息等待时间。窗口关闭后的 `disposed` 检查可阻止继续打开结果，但不是对在途 Flow/相机请求的取消或设备停止确认。

## 文档加载、Y-first 例外与 Mat 所有权

`ConoscopeView.OpenConoscope` 清理当前显示和关注点后，以 fire-and-forget 方式调用 `ConoscopeDocument.OpenAsync`。Document 新请求会取消旧请求并先释放原文档数据；失败不会自动恢复上一张图。

| OpenAsync 路径 | 提交与事件 |
| --- | --- |
| 无需联合预处理 | 读内嵌 Y，完成适用的单通道处理后提交 Y，发布 `InitialDisplayReady`；随后顺序读取 X、Z并补齐，发布 `DeferredChannelsReady` |
| `applyPreprocess && DustRemovalEnabled` | 先读 Y 但不发布首屏；读取 X/Z并联合预处理后一次提交 XYZ，只发布 `InitialDisplayReady` |
| 首屏前失败 | 通过 `LoadFailed(exception, initialDisplayCompleted: false)` 报告，View 显示打开错误 |
| Y 首屏已提交、后续 X/Z失败 | 保留已提交 Y，状态栏显示“仅 Y 可用”及重新加载入口，悬停可看错误原因；完整 XYZ 能力仍未就绪 |

`InitialDisplayReady` 表示对应数据已提交，不证明 WPF 渲染成功。View 收到该事件才调用 `RefreshDisplayedImage`；渲染异常另记录日志并提示。Document 对 `Changed` / `LoadFailed` / `LoadStateChanged` 逐订阅者隔离异常。状态栏跟随活动文档显示加载过程及失败状态。`RetryLoadAsync` 仅在失败且无在途加载时生效，保留打开时的处理参数并重读全部通道，避免将旧 Y 与磁盘上已修复或替换文件的新 X/Z 混合。重试会重置当前预览和关注点；已捕获曲线快照保留，成功后清除错误状态。加载内部捕获取消和一般异常，因此等待 `OpenAsync` Task 正常返回也不证明成功；调用方应结合事件及 `HasDisplayData / HasXyzData`，不能等待一个所有路径都会发出的 `DeferredChannelsReady`。

资源约束属于 Document，而不是窗口状态：

- 每个 Document 通过加载信号量串行处理请求；提交同时校验取消源身份、加载版本和取消状态，过期请求不能接管当前 Mat。它不是跨标签页的全局加载锁。
- X/Y/Z 的替换与释放由 Document 负责；View 借用引用，不另存一套生命周期。未成功提交的候选由创建路径释放。
- 联合预处理的后台委托接管 Y 后，不把令牌传给 `Task.Run` 的调度参数，保证即使调度前取消也进入委托的清理路径；工作内部仍检查取消。
- `ApplyPreprocess` 通过 `ref` 更新 Mat，并在 `finally` 保留已替换通道；中途异常不等于整个处理已回滚。`Reload` 是同步重读 XYZ并按配置执行非正值 clamp，不是 `OpenAsync` 的分阶段过程。
- `DataVersion` 随数据变化递增；ImageCenter 色差参考随数据版本失效，避免曲线/导出逐点重复扫描同一参考 ROI。

文件层只调用 `CVFileUtil.ReadCIEFileChannel` 读取指定内嵌通道，不改走会跟随关联源文件的通用打开流程。底层格式与读取限制归 [FileIO](../../engine-components/ColorVision.FileIO.md)，此处不另维护二进制协议。

## 视图状态、通道能力与轻量 Host

每个 View 的普通语义值集中在 `ConoscopeViewState`：通道、伪彩、预处理、色差/对比度选择、坐标轴与可用能力。活动标签页的 Ribbon 跟随该对象；需要校验或触发渲染的操作由 View 方法完成。没有活动 View 时保留快捷区布局并禁用操作，不用控件当前值反向充当第二份业务状态。

文档停靠由 `Presentation/Docking/ConoscopeDocumentLayout` 处理：从完整布局树查找文档，包括分栏与浮动窗口；激活和新文档插入使用实际所属面板，重复打开按同一文档标识定位。移位和取消关闭不释放文档，只有 `Closed` 才释放对应 View，主窗口退出仍释放所有打开的 View。停靠外观复用共享 AvalonDock 主题资源，旧宿主缺少共享资源时回退到兼容主题，不复制维护一套共享停靠模板。

图像坐标选择、关注点工具和色阶保持悬浮在图像上的布局，避免固定工具栏压缩有效图像区。左上角只显示坐标选择框，名称由工具提示与辅助功能提供；它切换已渲染图像的坐标投影，不能复用主程序图像窗口的通道切换状态。Ribbon 按任务页组织功能组，主要操作使用图标和文字，次级操作紧凑排列；图标集中在 `Presentation/ConoscopeIcons.xaml`。高度随内容布局，横向滚动仅在实际超宽时出现；右上角可收起命令区，点击任务页签重新展开。直角与极坐标参考图统一从 `ConoscopePlotTheme` 获取背景、文字、网格和通道颜色；主题切换仅刷新参考图显示，保留当前直角图缩放范围，不重新加载图像或改动文档数值。主题事件由订阅时的发布者实例退订。

坐标网格默认使用半透明浅灰细线、12 DIP 白色描边标注和珊瑚色参考线，标注随缩放保持屏幕字号，并约束在图像边界内。已有型号保存的字号、线宽与显示开关继续生效；默认外观调整不覆盖用户配置。`ConoscopeCoordinateAxisPresentationTests` 检查不同坐标与缩放下的字形边界，`ConoscopeDocumentLayoutTests` 检查分栏、浮动、关闭取消和最终释放；两者不能替代真实窗口的主题、悬浮控件遮挡和窄窗口验收。


通道能力由同一套检查用于显示、参考曲线及导出：Y只要求 Y；Contrast 要求 Y与同尺寸的对应参考 Y；X/Z/CIE和色差要求完整 XYZ，色差的自定义/参考图模式还需各自有效参考。衍生 Mat 生成失败不能用 Y冒充目标通道。方位、极角导出沿用当前显示通道；高级导出也须通过对应通道检查。

`ConoscopeImageHost` 不创建完整 `ImageView`，但仍拥有 `DrawEditorContext`、选择 visual、Zoombox 和 DrawCanvas。内部 `FocusCircleEditor` 管理圆形 visual、选择/绘制/擦除、菜单、边界和延迟刷新；`FocusCircleInteractionMode` 表达互斥交互状态。悬浮工具首个选择箭头开关控制关注点编辑：开启时按 Ctrl 拖动平移，关闭时直接拖动平移；工具提示和辅助功能名称明确该语义。

- 新文档使用 `ResetDocument`，清除旧关注点和编辑状态。
- 同文档换通道或伪彩使用 `ReplaceDisplayedImage`，在清理画布时保留关注点与交互模式。
- Host 的 `Dispose` 幂等，退订内部事件并释放绘制/鼠标资源；Window 退订配置、参考、服务和主题事件，并释放其打开的 View。
- 鼠标捕获、缩放和绘图仍是 WPF 组合点职责；不为缩短文件而重新拆成共享状态的机械 partial，也不引入 Window/View 两份状态快照。

测量与分析使用 Document 当前 XYZ 数值，不从伪彩/缩放后的屏幕像素反算；若已经预处理，这些数值也已处理，不能把“数值计算”误写成总是原文件未处理数据。

## Polar 与三种 H/V 图像坐标

图像区左上角的坐标 ComboBox 提供 `Polar`、`H/V`、`North Polar` 与 `East Polar`；这是单 View 的显示状态，不写回型号配置。后三项都是 Horizontal-Vertical 角度坐标，不是把图像像素简单改名为平面 x/y。`H/V` 对应 EZCom 手册所称 Azimuthal，另外两项采用不同的顺序旋转定义。

`ConoscopeHorizontalVerticalProjection` 先把源极坐标写成方向分量 `x = sin(theta) cos(phi)`、`y = sin(theta) sin(phi)`、`z = cos(theta)`，再按目标系统计算 H/V；反算通过方向分量和 `atan2` 保留完整象限：

```text
H/V (Azimuthal): H = atan2(x, z), V = atan2(y, z)
North Polar:      H = atan2(x, z), V = asin(y)
East Polar:       H = asin(x),     V = atan2(y, z)

North inverse: x = cos(V) sin(H), y = sin(V),        z = cos(V) cos(H)
East inverse:  x = sin(H),        y = cos(H) sin(V), z = cos(H) cos(V)
theta = atan2(sqrt(x^2 + y^2), z), phi = atan2(y, x)
```

只有 `theta <= MaxAngle` 的目标像素有效，所以三种 H/V 的四角都显示为黑色；Azimuthal 与 North/East 的有效域和内部形变并不相同，North 与 East 互为不同旋转顺序而不是图片整体旋转。当前显示路径先按原始全分辨率通道确定伪彩范围，再以双线性插值生成最长边不超过 `2049px` 的方形预览；缓存的是当前坐标系统约 2049² 的反向映射，不复制或替换 Document 持有的全分辨率 XYZ。切换通道、伪彩和参考线复用该映射；切换坐标系统、换文件、改变 FOV/计算直径/手动系数或释放 View 时销毁并按需重建映射。

三种投影网格的横轴均为 H、纵轴均为 V。鼠标悬停先按当前系统从 H/V 位置反算源图坐标，再读取 Document 当前 XYZ，并同时显示系统名、H/V 与极角/方位角；右侧方位直径和极角圆周参考曲线仍在源 Polar 数据上采样，叠加到图上的参考线按当前系统的角度关系弯曲。

在任一 H/V 投影中，主页参考模式还提供 H、V：**固定 H 是竖线，沿 V 采样；固定 V 是横线，沿 H 采样**。可输入固定角度，或启用参考交互后在图像有效域内点击、拖动。切回 Polar 时恢复方位线，保留分别记录的方位角、极角及 H/V 值，不把固定角度偷换为另一种坐标。固定角输入限制在 `[-MaxAngle, MaxAngle]`，非有限输入不改变当前值。固定 H/V 曲线用直角坐标绘制，极坐标曲线显示按钮禁用。

`ConoscopeHorizontalVerticalCrossSection` 以当前投影公式将每个 H/V 采样点反算至全分辨率 Polar 源图，双线性读取 XYZ，再计算所选通道。色差参考图与对比度参考图在相同源坐标双线性读取；并不先对预览伪彩图或低分辨率结果采样。实时步长为 `0.1°`，变化角从 `-MaxAngle` 到 `+MaxAngle`，最后一个端点始终包含；当步长不能整除范围时，最后一段较短。极角超出型号有效域、映射越出源图或所需源数值非有限时保留 `NaN` 断口，不夹取边缘或补零。有效性依赖型号的中心与像素/度系数，插值不是额外的镜头标定。

固定 H/V 的“导出当前”沿用步长、小数位与元数据选项，独立输出 `H (degrees),V (degrees),Value,Valid,SourceX,SourceY`；`Valid` 同时要求几何有效及通道结果有限。元数据包含源文件名、型号、投影、固定坐标、通道、单位、步长、插值方式、源几何、数据版本、实际加载/预处理参数及参考来源。它与历史方位/极角导出为不同契约。

参考曲线右上角的保留按钮冻结当时的横轴位置和通道数值；快照列表按钮打开命名、显示勾选、比较、删除与 CSV 导出窗口。`ConoscopeCurveSnapshot` 复制数组，只持有数值与元信息，不持有 Mat 或回调；拖动、换通道、重新预处理不改写旧快照。比较以选中快照为基准，只叠加变化角度含义（H/V 包含投影定义）与单位兼容的曲线，并提示不兼容数量。方位直径与圆周快照仍标识源 Polar 横轴，不因图像投影改变而冒充 H/V。快照 CSV 用 `R` 精度输出捕获的数值，不重新采样；当前截线 CSV 按所选步长重新采样并采用所选小数位。

快照由 `ConoscopeWindow` 的工作区持有，全部文档共享同一个列表；主页“曲线快照”入口在关闭全部图像后仍可使用。关闭快照窗口仅隐藏，关闭源文档也保留曲线及来源信息；退出工作区时，未保存修改会提示保存、放弃或取消。源文件完整路径区分同名文件，路径只是来源元信息，恢复时不读取它，也不会随源文件更新重算快照。

“保存会话”写入版本 1 的 `.conocurves` JSON 文件，保留名称、来源、捕获 UTC 时间、投影与横轴语义、通道、单位、处理元信息、完整精度位置/数值、勾选、颜色与当前选择。非有限数值以 JSON `null` 保存为断口，恢复为 NaN。“导入会话”先验证整份文件，再追加到当前列表，不覆盖现有曲线；重启后可从主页手动导入，不自动加载上次会话。文件上限 128 MiB、每个会话合计最多 2,000,000 个采样值，避免无界反序列化；未知版本、缺失关键字段和不等长数组会拒绝导入。保存使用同目录临时文件，成功后才替换目标。

Manual EZCom Software 第 109/111 页按线条横竖命名 Marker，第 110/112 页按固定角度命名截线；第 52 页描述跟踪，第 122–123 页明确是 VT 比较。通用命名快照是本产品的扩展，不应把这些手册章节解读成完整的通用快照生命周期定义。

边界如下：

- 三种 H/V 模式均不转换圆形关注点。存在关注点时拒绝切换，进入任一投影模式后关注点工具栏禁用；返回 Polar 后恢复。
- 现有方位、极角和高级导出仍是源 Polar 数据语义；固定 H/V 仅提供当前一条截线导出，没有 H/V 全栅格导出。
- 3D 视图仍使用源通道与 Polar 圆形掩膜，不把 H/V 预览当测量数据输入。

`ConoscopeView.CreateExportContext` 使用源 XYZ 的 `sourceImageCenter` / `sourcePixelsPerDegree`，不使用 H/V 预览的中心与比例。圆截面、直径截面及矩阵导出因此保持源 Polar 采样位置，不随显示投影改变。固定逗号分隔的 CSV 对角度行列头、通道值及数值元信息统一使用 `InvariantCulture`，不随 Windows 小数分隔符改变列结构；既有字段名、顺序和精度设置保留。`ConoscopeExportGeometryTests` 验证不同投影下的源图采样，`ConoscopeExportCultureTests` 验证小数逗号区域设置下的圆与线输出。

方位/极角矩阵导出逐行取样并直接写 CSV，仅保存两个角度轴和文件缓冲，不再分配完整采样矩阵。原始角度累加、端点容差、最近像素取样、行列顺序和数值精度保持不变。矩阵及当前截线导出的最小采样步长统一为 0.1°，默认仍为 1°；旧配置中的更小步长读取时提升到 0.1°，其他配置与旧字段保留，非有限步长回退到默认值。直接调用导出服务时拒绝小于 0.1° 的步长。VA60 方位/径向均为 0.1° 时为 2,161,800 个采样值。快照 CSV 保留捕获位置，不重采样。高级导出允许仅选截面，并按当前视场和所选步长显示采样值数量与估计文件大小；文件大小随数值位数变化，不是磁盘空间保证。

简单矩阵导出及高级导出的全部文件在后台串行执行；1.5 秒内完成时不显示进度窗，超过该时间才显示当前文件和采样进度并支持取消。成功自动关闭进度窗，当前截线成功也不再弹确认框；失败仍明确显示，取消的批次保留已完成文件数量供查看。取消与失败保留已完成文件，当前文件用同目录临时文件写入，结束前不替换已有目标。原有截线导出同样使用原子写入。`ConoscopeExportProgressWindow` 持有导出数据直到后台任务结束，强制关闭拥有者时也先取消任务；`ConoscopeExportSource` 在 UI 线程保留当前原始 XYZ 和参考图的 OpenCV 引用计数头，并固定型号、几何、参考模式与参考数值。后台回调不读取 View/Config，不克隆完整 XYZ。此方式依赖已发布通道只读：预处理和参考更新必须生成新 Mat 后替换，不能原地改写被导出持有的缓冲区。

导出回归入口包括 `ConoscopeStreamingExportTests`（旧采样/格式、进度、取消和失败原子性）、`ConoscopeExportSourceTests`（源释放及预处理后旧缓冲仍有效）、`ConoscopeCurveSessionTests`（会话完整精度往返、来源、断口和无效文件）。还应操作两个源文档捕获兼容曲线，关闭源文档、保存会话、重建窗口并导入，确认名称、勾选与比较结果；界面夹具应验证后台导出成功和取消时既有目标文件保留。最小步长全量导出、多通道批次与目标磁盘耗时应在受控样本上测量；界面夹具还应核对快速导出静默、延迟显示进度、成功自动关闭和失败重试。`ConoscopeExportSettingsTests` 检查最小步长、非有限配置与旧配置往返。

- 几何精度仍受当前型号的中心、`MaxAngle` 与线性像素/度系数约束；CVCIE 文件本身没有在此层提供独立镜头畸变标定时，H/V 变换不能替代现场标定。

固定 H/V 的回归入口为 `ConoscopeHorizontalVerticalCrossSectionTests`（三投影、非零固定角、双线性、有效域、方向及原分辨率）、`ConoscopeCoordinateAxisInteractionTests`（真实坐标图元与交互）、`ConoscopeCrossSectionWorkflowTests`（View 到快照/CSV 的值和模式契约）、`ConoscopeCurveSnapshotTests`（不可变性、比较语义和完整精度 CSV）。界面还应使用实际样本检查 H 竖线、V 横线、曲线跟踪、三投影切换、Polar 回退及快照窗口关闭重开；仅构建通过不能替代这些操作验收。

## 配置 working copy、参考与持久化

`ConoscopeConfig` 持有全局默认值和型号配置；单 View 的 State 是当前文档状态。`ConoscopeConfigWindow` 复制可编辑设置为 working copy，预处理控件直接绑定该副本，恢复默认也只先修改副本。取消或未应用即关闭不会主动提交这些编辑。

“应用并保存”先更新绑定源并检查输入验证错误，再备份活配置、把副本复制到活配置、调用 `ConfigService.Instance.Save<ConoscopeConfig>()`，正常返回后设置 `DialogResult = true`。catch 中只在实际抛出异常时复制备份回活对象。当前 `ConfigHandler.Save<T>()` 丢弃 `TrySave` 的失败结果，因此窗口返回成功不证明落盘成功，内存通知也可能已经发出；不能宣称此窗口具有“文件与全部消费者一起回滚”的事务。完整保存机制见[配置持久化](../../ui-components/configuration.md)。

Window 将预处理/显示属性变更合并到待执行的 Dispatcher 刷新中，再更新打开的 View；这不是同步完成所有渲染的承诺，也不表示所有 View 始终与全局默认值相同。

`ConoscopeGlobalReferenceStore` 独占全局色差 U/V及黑/白参考 Y Mat，保存参考文件、维护配置路径，并用 `Changed` 通知窗口。参考矩阵、配置文件和事件不是一笔原子事务：例如色差参考顺序写 U/V，再替换内存和保存配置；加载/删除参考文件失败有记录日志后继续的路径。不得以文件存在或通知发出单独证明全部参考已持久化、恢复或删除。

## 关注点模板与分析快照

关注点 ROI 计算属于插件本地 `FocusPointMeasurementService`，不等同于 Engine 的滤除计算；但关注点模板持久化仍复用 Engine POI 模型与 MySQL。`FocusPoiTemplateRepository` 承担读取/创建/保存，View 负责显示结果和错误，不自己创建数据库连接。保存先调用主表 DAO，再用独立事务删除并重插明细；明细失败回滚不包含之前的主表保存，不能宣称整份模板创建/保存原子完成。

记录 R/G/B或白/黑时，View 一次遍历当前全部关注点，从当前 XYZ求 ROI均值，构造 `MeasurementCapture` 并写入该窗口的 `ConoscopeAnalysisSession` 槽位；任一点失败不会提交这次完整槽位。它是当时的采样快照，不会随之后换图、移动圆或预处理自动重新采样。`FocusPointPolarEditModel` 作为编辑草稿，提交才写回圆；修改位置后若要比较同一物理区域，应重新记录相关槽位。

`CanComputeGamut` 只检查 R/G/B槽位和标准非空；`CanComputeContrast` 只检查白/黑槽位非空。实际计算的点位对应由 `MeasurementCaptureAlignment.Align` 决定：

- 多点快照优先取 `Key` 交集；当前 View 用关注点名称同时生成 `Key` 和 `Name`，不比较实际坐标。
- 单点参考可广播到多个点；全部都是单点时直接形成一组。
- 多点快照没有共同 Key但数量相同时按列表顺序对齐；否则报不匹配。存在部分共同 Key时，只计算交集，不保证输出全部已记录点。

因此槽位完整或按钮可点，不证明位置一致或所有点都被计算。色域值为样本 RGB色度三角形面积除以标准面积再乘100，不是两个色域交集面积；对比度为白场亮度除以黑场亮度，黑场必须大于0。Session 把计算异常转成结果为空和错误文本，由窗口提示；成功结果进入独立的色域/对比度结果窗口。

结果窗口可查看汇总和单关注点。当前视图的方位/极角/高级导出与分析结果 CSV由各自导出入口负责；它们不是自动附属于采集成功的副作用。变更分析字段时需同时核对结果模型、结果窗口与 CSV，不在 README再复制一份流程。

## 本地构建、宿主复制与发布

普通构建和测试写本地产物，不授权设备操作或上传。从仓库根目录使用 PowerShell：

```powershell
dotnet build .\Plugins\Conoscope\Conoscope.csproj -c Release -p:Platform=x64
dotnet test .\Test\Conoscope.Tests\Conoscope.Tests.csproj -c Release -p:Platform=x64
```

上述项目直接构建在未提供有效 `SolutionDir` 时通常只保留项目输出；一旦该属性有效，两个独立 target 会写宿主：

| target | 当前副作用 |
| --- | --- |
| 导入的 `PluginProject.HostCopy.targets / PostBuild` | 把本次主 DLL及存在的 manifest、README、CHANGELOG同步复制到宿主 Debug和Release的 `Plugins/Conoscope/`，不是只写当前配置 |
| `Conoscope.csproj / CopyHostProjectReferences` | 从 `ReferenceCopyLocalPaths` 筛选顶层 ProjectReference DLL（排除资源程序集），将其及存在的 PDB复制到两套宿主根目录，`SkipUnchangedFiles=true` |

目录根为 `ColorVision/bin/x64/Debug/net10.0-windows/` 与 `ColorVision/bin/x64/Release/net10.0-windows/`。这会更新宿主依赖，不是完全隔离的插件构建；复制文件存在也不是完整运行依赖或交付已经验证。通用插件产物、manifest和安装边界见[插件产物与交付](../../../02-developer-guide/plugin-development/getting-started.md)。

只有用户明确要求发布 Conoscope 时才执行：

```powershell
.\Scripts\package_plugin.bat Conoscope
```

wrapper 会构建、校验、上传并清理本地 `.cvxp`；不把它当本地验证命令，不使用 `--no-upload`。构建成功不代表发布成功；退出结果、远端版本与可下载包属于另一层交付证据。包内 README保留平台/依赖和风险，完整正文需要匹配版本的源码仓库。

## 测试范围与验证缺口

| 测试文件 | 实际可参考的范围 |
| --- | --- |
| `ConoscopeDocumentTests` | 无联合预处理路径的 Y→XYZ事件、latest-wins、观察者异常隔离和失败元数据；不证明所有预处理路径均有 Y-first |
| `CvcieChannelReaderTests` | 内嵌通道选择、越界通道拒绝和按需大图读取 |
| `ConoscopeViewBoundaryTests` | State通知与 Y/XYZ/Contrast/色差参考的能力判定，不是完整 WPF交互验证 |
| `ConoscopeHorizontalVerticalProjectionTests` | Azimuthal、North Polar、East Polar 与 Polar 的手册公式、往返象限、不同有效域、预览上限、中心反查和小图重映射；不代替真实镜头标定或完整 WPF 输入测试 |
| `ConoscopeAnalysisSessionTests` | 按名称对齐的对比度、单点广播和缺槽位错误；不证明物理位置自动匹配 |
| `ConoscopeColorimetryTests`、`AdvancedExportSettingsTests` | 色度/色差/对比度矩阵规则，以及导出设置兼容，不代表所有最终导出文件已验收 |
| `MvsCaptureSessionTests` | 观察相机会话启动/停止、代次与延迟清理等边界，不替代 Engine Flow/DeviceCamera测量或真机验证 |
| `ArchitectureSmokeTests` | Application消息框、Document UI依赖、机械 partial及 View数据库/旧控件访问的源码模式检查，不是完整架构证明 |

两项真实大图测试 `ReadsConfiguredRealWorldSampleOneChannelAtATime` 和 `OpensConfiguredRealWorldSampleThroughStagedDocumentOwner` 依赖 `CONOSCOPE_REAL_SAMPLE`；未设置时直接返回。后者显式关闭预处理。它们记录通道、耗时和峰值工作集，不是固定性能 SLA；不能用普通测试数量推断已经执行了真实大图验收。

当前未发现 `ConoscopeCaptureWorkflow` 的专项自动化测试。Flow批次回退、相机消息终态、文件出现时序、按钮完成与实际显示、联合灰尘预处理失败、设置保存失败和真实驱动释放仍有验证缺口。测试引用不是通过记录，文档构建不能替代产品、数据库、真机或发布验收。
