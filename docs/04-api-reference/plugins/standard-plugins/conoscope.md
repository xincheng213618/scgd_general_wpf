---
knowledge_id: "plugins.conoscope"
knowledge_type: "topic"
status: "current"
summary: "Conoscope 的采集、CVCIE 首屏/XYZ 就绪、Mat 与分析快照契约；按钮成功不代表文档加载完成，联合灰尘预处理不走 Y-first。"
aliases: ["锥镜图像怎么看","Conoscope 依赖哪些 DLL","锥镜采集完成没有图像","Conoscope","VAM","ConoscopeCaptureWorkflow","ConoscopeFlowCaptureResult","ConoscopeCameraCaptureResult","ConoscopeDocument","ConoscopeDocumentChangeKind","ConoscopeView","ConoscopeViewState","ConoscopeImageHost","ConoscopeAnalysisSession","MeasurementCaptureAlignment","ConoscopeConfigWindow","ConoscopeGlobalReferenceStore","FocusPoiTemplateRepository","CONOSCOPE_REAL_SAMPLE"]
code_paths: ["Plugins/Conoscope/README.md","Plugins/Conoscope/Docs/ARCHITECTURE.md","Plugins/Conoscope/Conoscope.csproj","Plugins/Conoscope/manifest.json","Plugins/Conoscope/Core/ConoscopeModuleService.cs","Plugins/Conoscope/ConoscopeWindow.xaml","Plugins/Conoscope/ConoscopeWindow.xaml.cs","Plugins/Conoscope/Application/Capture/ConoscopeCaptureWorkflow.cs","Plugins/Conoscope/ConoscopeDocument.cs","Plugins/Conoscope/ConoscopeView.xaml.cs","Plugins/Conoscope/ConoscopeImageHost.xaml.cs","Plugins/Conoscope/Application/Preprocess/ConoscopePreprocessPipeline.cs","Plugins/Conoscope/Processing/Preprocess/","Plugins/Conoscope/Application/Analysis/ConoscopeAnalysisSession.cs","Plugins/Conoscope/Application/Analysis/FocusPointMeasurementService.cs","Plugins/Conoscope/Analysis/MeasurementCaptureModels.cs","Plugins/Conoscope/Analysis/AnalysisResultCsvExporter.cs","Plugins/Conoscope/Application/FocusPoiTemplateRepository.cs","Plugins/Conoscope/Core/ConoscopeConfig.cs","Plugins/Conoscope/Core/ConoscopeConfigWindow.xaml.cs","Plugins/Conoscope/ConoscopePreprocessSettingsControl.xaml","Plugins/Conoscope/Core/ConoscopeGlobalReferenceStore.cs","Plugins/Conoscope/Core/ConoscopeReferenceMatSerializer.cs","Plugins/Conoscope/Core/ConoscopeExportService.cs","Plugins/Conoscope/MVS/","PluginProject.HostCopy.targets"]
test_paths: ["Test/Conoscope.Tests/Conoscope.Tests.csproj","Test/Conoscope.Tests/ConoscopeDocumentTests.cs","Test/Conoscope.Tests/CvcieChannelReaderTests.cs","Test/Conoscope.Tests/ConoscopeViewBoundaryTests.cs","Test/Conoscope.Tests/ConoscopeAnalysisSessionTests.cs","Test/Conoscope.Tests/ConoscopeColorimetryTests.cs","Test/Conoscope.Tests/MvsCaptureSessionTests.cs","Test/Conoscope.Tests/ArchitectureSmokeTests.cs","Test/Conoscope.Tests/AdvancedExportSettingsTests.cs"]
related: ["plugins.index","plugins.capabilities","engine.file-io","flow.session","operations.camera","ui.configuration","plugins.getting-started"]
---

# Conoscope 图像、采集与分析

Conoscope 是 VAM/锥镜图像观察、关注点采样、色域/对比度计算与导出插件。本主题是其单一正文；源码旁 README 保留包入口和必要风险，`Docs/ARCHITECTURE.md` 只指向这里，不再要求同一行为维护三套说明。

## 入口与运行依赖

宿主 Tool 菜单的 `VAM` 进入 `ConoscopeWindow`。ImageEditor 右键入口由 `ConoscopeModuleService.CanOpenFromImageView` 检查当前文件存在、`CVFileUtil.IsCVCIEFile` 为真且编辑器配置 `Channel == 3`；这不是仅凭扩展名允许任意图像进入。模块入口负责寻找/打开窗口，单文档 View 不靠静态 Window 单例刷新业务状态。

身份和最低宿主要求读取 `manifest.json`，发布版本以 `Conoscope.csproj` 生成的 DLL `FileVersion` 为准。当前工程为 Windows/x64、`net10.0-windows` WPF，引用 Engine、ImageEditor 和 Solution；完整运行还依赖匹配的 ColorVision 库、`CVCommCore.dll`、`MQTTMessageLib.dll` 与 OpenCV 原生运行库，不能只交付一个插件 DLL。

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
| Y 首屏已提交、后续 X/Z失败 | 保留已提交 Y，报告后台失败；View 不再用首屏错误弹窗重复提示，完整 XYZ 能力仍未就绪 |

`InitialDisplayReady` 表示对应数据已提交，不证明 WPF 渲染成功。View 收到该事件才调用 `RefreshDisplayedImage`；渲染异常另记录日志并提示。Document 对 `Changed` / `LoadFailed` 逐订阅者隔离异常。加载内部捕获取消和一般异常，因此等待 `OpenAsync` Task 正常返回也不证明成功；调用方应结合事件及 `HasDisplayData / HasXyzData`，不能等待一个所有路径都会发出的 `DeferredChannelsReady`。

资源约束属于 Document，而不是窗口状态：

- 每个 Document 通过加载信号量串行处理请求；提交同时校验取消源身份、加载版本和取消状态，过期请求不能接管当前 Mat。它不是跨标签页的全局加载锁。
- X/Y/Z 的替换与释放由 Document 负责；View 借用引用，不另存一套生命周期。未成功提交的候选由创建路径释放。
- 联合预处理的后台委托接管 Y 后，不把令牌传给 `Task.Run` 的调度参数，保证即使调度前取消也进入委托的清理路径；工作内部仍检查取消。
- `ApplyPreprocess` 通过 `ref` 更新 Mat，并在 `finally` 保留已替换通道；中途异常不等于整个处理已回滚。`Reload` 是同步重读 XYZ并按配置执行非正值 clamp，不是 `OpenAsync` 的分阶段过程。
- `DataVersion` 随数据变化递增；ImageCenter 色差参考随数据版本失效，避免曲线/导出逐点重复扫描同一参考 ROI。

文件层只调用 `CVFileUtil.ReadCIEFileChannel` 读取指定内嵌通道，不改走会跟随关联源文件的通用打开流程。底层格式与读取限制归 [FileIO](../../engine-components/ColorVision.FileIO.md)，此处不另维护二进制协议。

## 视图状态、通道能力与轻量 Host

每个 View 的普通语义值集中在 `ConoscopeViewState`：通道、伪彩、预处理、色差/对比度选择、坐标轴与可用能力。活动标签页的 Ribbon 跟随该对象；需要校验或触发渲染的操作由 View 方法完成。没有活动 View 时保留快捷区布局并禁用操作，不用控件当前值反向充当第二份业务状态。

通道能力由同一套检查用于显示、参考曲线及导出：Y只要求 Y；Contrast 要求 Y与同尺寸的对应参考 Y；X/Z/CIE和色差要求完整 XYZ，色差的自定义/参考图模式还需各自有效参考。衍生 Mat 生成失败不能用 Y冒充目标通道。方位、极角导出沿用当前显示通道；高级导出也须通过对应通道检查。

`ConoscopeImageHost` 不创建完整 `ImageView`，但仍拥有 `DrawEditorContext`、选择 visual、Zoombox 和 DrawCanvas。内部 `FocusCircleEditor` 管理圆形 visual、选择/绘制/擦除、菜单、边界和延迟刷新；`FocusCircleInteractionMode` 表达互斥交互状态。

- 新文档使用 `ResetDocument`，清除旧关注点和编辑状态。
- 同文档换通道或伪彩使用 `ReplaceDisplayedImage`，在清理画布时保留关注点与交互模式。
- Host 的 `Dispose` 幂等，退订内部事件并释放绘制/鼠标资源；Window 退订配置、参考、服务和主题事件，并释放其打开的 View。
- 鼠标捕获、缩放和绘图仍是 WPF 组合点职责；不为缩短文件而重新拆成共享状态的机械 partial，也不引入 Window/View 两份状态快照。

测量与分析使用 Document 当前 XYZ 数值，不从伪彩/缩放后的屏幕像素反算；若已经预处理，这些数值也已处理，不能把“数值计算”误写成总是原文件未处理数据。

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
| `ConoscopeAnalysisSessionTests` | 按名称对齐的对比度、单点广播和缺槽位错误；不证明物理位置自动匹配 |
| `ConoscopeColorimetryTests`、`AdvancedExportSettingsTests` | 色度/色差/对比度矩阵规则，以及导出设置兼容，不代表所有最终导出文件已验收 |
| `MvsCaptureSessionTests` | 观察相机会话启动/停止、代次与延迟清理等边界，不替代 Engine Flow/DeviceCamera测量或真机验证 |
| `ArchitectureSmokeTests` | Application消息框、Document UI依赖、机械 partial及 View数据库/旧控件访问的源码模式检查，不是完整架构证明 |

两项真实大图测试 `ReadsConfiguredRealWorldSampleOneChannelAtATime` 和 `OpensConfiguredRealWorldSampleThroughStagedDocumentOwner` 依赖 `CONOSCOPE_REAL_SAMPLE`；未设置时直接返回。后者显式关闭预处理。它们记录通道、耗时和峰值工作集，不是固定性能 SLA；不能用普通测试数量推断已经执行了真实大图验收。

当前未发现 `ConoscopeCaptureWorkflow` 的专项自动化测试。Flow批次回退、相机消息终态、文件出现时序、按钮完成与实际显示、联合灰尘预处理失败、设置保存失败和真实驱动释放仍有验证缺口。测试引用不是通过记录，文档构建不能替代产品、数据库、真机或发布验收。
