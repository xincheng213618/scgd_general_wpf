# 实时图像改造现状梳理

本文档只基于当前工作区里尚未提交的修改整理，目标不是评价方案好坏，而是先把“现在已经改成什么样”与“下一步应该怎么收口”说清楚。

## 1. 当前修改分成了哪几块

### A. ImageView 实时帧基础设施

涉及文件：

- UI/ColorVision.ImageEditor/ImageView.Realtime.cs
- UI/ColorVision.ImageEditor/ImageView.xaml.cs
- UI/ColorVision.ImageEditor/Realtime/RealtimeImageViewService.cs
- UI/ColorVision.ImageEditor/Realtime/RealtimeFramePresenter.cs
- UI/ColorVision.ImageEditor/Realtime/RealtimeFrameOptions.cs
- UI/ColorVision.ImageEditor/Realtime/RealtimeFrameStats.cs
- UI/ColorVision.ImageEditor/Realtime/RealtimeFrameSnapshot.cs
- UI/ColorVision.ImageEditor/DrawCanvas.cs
- UI/ColorVision.ImageEditor/EditorTools/Realtime/FreezeRealtimeFrameEditorTool.cs
- UI/ColorVision.ImageEditor/EditorTools/Realtime/SnapshotRealtimeFrameEditorTool.cs
- UI/ColorVision.ImageEditor/EditorTools/Realtime/RealtimeDiagnosticsEditorTool.cs
- UI/ColorVision.ImageEditor/EditorTools/Realtime/RealtimeDiagnosticsWindow.cs

### B. 相机实时显示链路迁移

涉及文件：

- Engine/ColorVision.Engine/Services/Devices/Camera/Video/CameraRealtimeFramePipeline.cs
- Engine/ColorVision.Engine/Services/Devices/Camera/Video/VideoReader.cs
- Engine/ColorVision.Engine/Services/Devices/Camera/DisplayCamera.xaml.cs
- Engine/ColorVision.Engine/Services/Devices/Camera/CameraLocalWindow.xaml.cs

### C. Conoscope/MVS 插件接入 Realtime

涉及文件：

- Plugins/Conoscope/MVS/MVSViewManager.cs
- Plugins/Conoscope/MVS/MVSViewWindow.xaml.cs

### D. 像素探针与直方图增强

涉及文件：

- UI/ColorVision.ImageEditor/Draw/Special/ImageMouseInfoProvider.cs
- UI/ColorVision.ImageEditor/Draw/Special/ImagePixelSample.cs
- UI/ColorVision.ImageEditor/Draw/Special/MouseMagnifier.cs
- UI/ColorVision.ImageEditor/EditorTools/Histogram/HistogramChartWindow.xaml.cs
- UI/ColorVision.ImageEditor/EditorTools/Histogram/HistogramEditorTool.cs

### E. 资源与工程文件清理

涉及文件：

- UI/ColorVision.ImageEditor/ColorVision.ImageEditor.csproj
- UI/ColorVision.ImageEditor/Assets/Image/CIE1931xy1.png
- UI/ColorVision.ImageEditor/Assets/Image/Paomedia-Small-N-Flat-Key.ico
- UI/ColorVision.ImageEditor/Assets/Image/pictureBox1.Image.png

## 2. 当前代码逻辑是什么

### 2.1 ImageView 现在新增了一条通用 Realtime 服务

当前 ImageView 增加了懒加载属性 `Realtime`，由 `RealtimeImageViewService` 统一暴露实时显示能力。

这条服务当前负责：

- 配置实时显示选项，入口在 RealtimeImageViewService.Configure
- 接收指针帧或托管字节帧，入口在 RealtimeImageViewService.SubmitFrame
- 从当前实时帧抓取原始快照，入口在 RealtimeImageViewService.CaptureCurrentFrame
- 导出当前显示图 PNG，入口在 RealtimeImageViewService.SaveDisplayedPng
- 统一做 Reset、Freeze、Stats 和 Overlay 管理

底层实际渲染由 RealtimeFramePresenter 完成。Presenter 当前会做这些事情：

- 把外部提交的最新帧复制到内部双缓冲
- 通过 MaxDisplayFps 做显示节流
- 只在 UI 线程真正写入 WriteableBitmap
- 统计 Submitted/Accepted/Displayed/Dropped/FrozenDropped 和显示 FPS
- 支持从最后一帧构建 RealtimeFrameSnapshot

这一层现在已经是一个独立的“实时显示基础设施”，不再要求每个调用方自己维护 WriteableBitmap、帧率统计和冻结逻辑。

### 2.2 Realtime 配置对象现在走稳定实例语义

RealtimeFrameOptions 现在提供 ApplyFrom，RealtimeImageViewService.Configure 与 RealtimeFramePresenter.Configure 都是字段级更新，而不是替换整份 Options 对象。

这意味着：

- FreezeRealtimeFrameEditorTool 这类订阅 `Options.PropertyChanged` 的调用方不会因为重新 Configure 而失联
- RealtimeFrameStats.Reset 会保留当前冻结状态，而不是 Reset 后把状态丢掉

这一点是当前实时工具链能成立的前提。

### 2.3 DrawCanvas 现在区分了普通 Visual 和 Overlay Visual

DrawCanvas 新增了 overlayVisuals 列表，以及：

- AddOverlayVisual
- RemoveOverlayVisual
- ClearOverlayVisuals

实时帧链路里的 ROI 和状态文字不再依赖普通的 AddVisualCommand/RemoveVisualCommand，而是走 Overlay 语义。

这说明现在的方向是：

- 用户编辑产生的图形是一类 visual
- 实时显示附加层是另一类 overlay visual

这个边界是合理的，也更适合后续冻结、诊断和状态叠加。

### 2.4 CameraRealtimeFramePipeline 已经成为 DisplayCamera 和 VideoReader 的共用实时相机管线

当前新增的 CameraRealtimeFramePipeline 已经承担了以下职责：

- Start/Stop 时统一配置 Realtime 选项与 Overlay
- 提交 raw buffer 或 pointer frame
- 根据 ROI 和 PseudoColorService 构造 VideoFrameProcessingRequest
- 统一回收 articulation 与 pseudo image 处理结果
- 统一维护状态文字、fps、first frame 行为
- 对 pointer frame 路径统一处理 flip

目前它已经被两个入口使用：

- VideoReader 在 Start/SubmitFrame/Stop 上都切到了这条管线
- DisplayCamera 的本地视频开关和回调也切到了这条管线

其中当前 pointer path 的 flip 已经收进管线内部，这意味着 DisplayCamera 外部现在只负责提交一份指针帧和参数，flip 不再在外层单独写一遍。

### 2.5 目前一共存在四种“实时入口模式”

虽然 Realtime 基础设施已经有了，但当前仓库里还没有只剩一种接入方式，而是并存四种模式：

#### 模式 1：CameraLocalWindow 直接提交到 ImageView.Realtime

CameraLocalWindow 的回调里现在直接调用 `ImageView.Realtime.SubmitFrame(...)`，关闭时调用 `ImageView.Realtime.Reset(true)`。

特点：

- 直接
- 简单
- 没走 CameraRealtimeFramePipeline
- 也没有统一处理 pseudo/articulation/FPS 这一套相机特有逻辑

#### 模式 2：DisplayCamera 走 CameraRealtimeFramePipeline

DisplayCamera 构造时创建 `_localRealtimePipeline`，开视频时 Start，回调里只 SubmitFrame，关视频时 Stop。

特点：

- 已经把原来分散在 DisplayCamera 内的处理逻辑收进了管线
- flip 由 flipModeProvider 提供给管线内部处理
- DisplayCamera 外侧比之前干净很多

#### 模式 3：VideoReader 走 CameraRealtimeFramePipeline

VideoReader 当前也已经把以前自己维护的：

- 伪彩请求构建
- articulation 处理
- fps 统计
- overlay 管理

都改成了复用 CameraRealtimeFramePipeline。

特点：

- DisplayCamera 与 VideoReader 已经共享了同一种“相机实时管线”
- 这条管线现在已经依赖 ImageEditor 层的共享 `DefaultRealtimeCameraConfig`
- `VideoReaderConfig` 不再是当前主链路里的配置归属

#### 模式 4：Conoscope/MVS 直接接入 Realtime 服务

MVSViewWindow 现在直接配置 `imgDisplay.Realtime.Configure(...)`，收到相机帧后直接 `imgDisplay.Realtime.SubmitFrame(...)`，并通过 `FrameRendered` 事件更新计数和 overlay。

特点：

- 它复用的是 ImageView.Realtime 基础设施
- 但没有走 CameraRealtimeFramePipeline
- 它现在也已经共用了 `DefaultRealtimeCameraConfig.MaxDisplayFps`
- 更像“通用实时显示 + 共享显示配置”，不是“相机实时处理管线”

### 2.6 新增的实时工具已经出现，但它们依赖的语义还没有完全收口

当前已经新增三类工具：

- FreezeRealtimeFrameEditorTool：切换 IsFrozen
- SnapshotRealtimeFrameEditorTool：保存 raw、当前帧 PNG、显示图 PNG
- RealtimeDiagnosticsEditorTool + Window：查看实时统计

这些工具的基础能力是成立的，但它们现在还依赖一个前提：

- “当前 raw frame”
- “当前显示图”
- “当前伪彩图”

这三者的关系必须稳定。

而当前这层语义还没有完全统一，因为伪彩逻辑仍然绕过了 Realtime 服务。

### 2.7 当前最关键的结构性问题：PseudoColorController 仍然直接切换 ImageShow.Source

PseudoColorController 现在仍然有两段关键逻辑：

- ApplyProcessedImage
- RestoreSource

它依然直接操纵：

- `_owner.FunctionImage`
- `_owner.ViewBitmapSource`
- `_owner.ImageShow.Source`

这意味着当前仓库里同时存在两套显示语义：

- RealtimeFramePresenter 认为当前显示图应由它控制
- PseudoColorController 认为当前显示图可以直接切换到伪彩结果

这也是目前最容易让 Freeze、Snapshot、Histogram live source、Pixel probe 语义互相打架的地方。

### 2.8 像素探针和直方图功能已经顺手增强了，但它们属于另一条需求线

当前还有一组和实时基础设施不完全同层的增强：

- ImageMouseInfoProvider 新增了 ColorimetryText，支持输出 XYZ、xy、u'v'
- MouseMagnifier 额外显示第三行色度信息
- HistogramChartWindow 支持 AttachLiveSource，通过 DispatcherTimer 周期刷新 live histogram
- HistogramEditorTool 现在把 live source 绑定到 `EditorContext.DrawCanvas.Source as BitmapSource`

这部分更像“实时显示之上的用户功能增强”，而不是“实时底层结构收口”。

它们可以保留，但如果当前目标是先把实时链路收干净，这部分最好从结构改造里分离出来单独看。

### 2.9 当前还有一组和主线弱相关的资源删除

当前还删除了三张图片资源，并同步改了 ImageEditor csproj 的 Resource/None Remove 列表。

这部分和实时图像改造没有直接关系。

如果这些删除不是本次主线需求的一部分，建议从当前改造中拆出去，不要混在同一批里。

## 3. 当前修改的主要问题

### 问题 1：一个改造批次里混了太多层次

当前 diff 同时包含：

- 实时基础设施新增
- Camera 链路收口
- MVS 接入迁移
- Freeze/Snapshot/Diagnostics 工具
- 像素探针增强
- 直方图 live 刷新
- 资源删除

这会导致 review、回归验证和后续继续改动都很难聚焦。

### 问题 2：Realtime 和 PseudoColor 仍然不是同一套显示语义

目前 raw/display/pseudo 还没有统一在一个状态面上。

如果不先解决这个问题，后面这些需求都会变得暧昧：

- Freeze 到底冻结 raw 还是冻结当前显示态
- Snapshot 保存的是 raw、当前显示图、还是伪彩图
- Histogram 的 live source 看的是 raw 还是显示图
- Pixel probe 读的是原图还是伪彩图

### 问题 3：共享配置已经下沉，但 direct-Realtime 和 pipeline 的能力边界还要继续收口

现在共享 realtime 配置已经下沉到 ImageEditor 层的 `DefaultRealtimeCameraConfig`：

- DisplayCamera / VideoReader 的 pipeline 直接依赖它
- CameraLocalWindow / MVS 这类 direct-Realtime 入口也已经共用它的 `MaxDisplayFps`

所以这一步已经不再是“VideoReaderConfig 命名不干净”的问题。

现在真正还没完全收口的是：

- direct-Realtime 入口主要只消费显示帧率这类通用显示参数
- pipeline 入口还会额外消费清晰度计算、ROI、状态文字等相机处理参数

也就是说，配置归属已经比之前干净很多，但能力边界还需要继续明确。

### 问题 4：实时入口还没有完全统一

当前 CameraLocalWindow、DisplayCamera、VideoReader、MVS 仍是两类不同接法：

- 直接接 Realtime
- 接 CameraRealtimeFramePipeline

这本身不一定错，但需要明确标准：

- 什么场景只需要通用 Realtime
- 什么场景必须走相机处理管线

现在这条边界还没有被明确定义。

### 问题 5：用户功能增强和结构改造混在一起，容易误判“结构已稳定”

像素探针色度、live histogram、snapshot 等功能本身没有问题，但它们建立在显示语义清晰的前提上。

如果底层显示状态还在变化，这些功能看起来“能用”，但后面很容易反复返工。

### 问题 6：overlay 运行态已经收进 pipeline，但 direct-Realtime 入口和 pipeline 入口的统一语义仍未完成

当前这一步已经完成：

- DisplayCamera / VideoReader 不再显式持有 `DVRectangleText` / `DVText`
- 这两个 overlay 实体已经由 `CameraRealtimeFramePipeline` 内部创建和持有

所以“宿主持有过多 runtime overlay 对象”这个问题已经明显缓解。

现在更值得关注的是：

- pipeline 入口有 ROI / articulation / pseudo 回流这套运行态
- direct-Realtime 入口目前仍只有显示层语义

这也是后面是否继续统一 CameraLocalWindow、MVS 与 pipeline 抽象时要面对的核心差异。

## 4. 对统一配置的建议

### 4.1 当前已经完成的一步：共享配置已经下沉到 ImageEditor 层

当前代码已经完成了这一步：

- 新增 `DefaultRealtimeCameraConfig`
- 配置归属下沉到 ImageEditor 层
- DisplayCamera / VideoReader / CameraLocalWindow / MVS 至少已经共用同一份 `MaxDisplayFps`

这意味着“修改一次显示 FPS，不同入口打开效果一致”这件事已经成立。

### 4.2 需要继续收口，但不要把所有东西硬并成一个大对象

结论是：

- 共享 realtime 配置放在 ImageEditor 层是对的
- `DVRectangleText` / `DVText` 也应该继续收进统一抽象
- 但这些配置态和运行态不应该简单合成一个“大配置类”

更合理的拆法是区分三层：

### A. 持久配置层

这一层只放“可编辑、可持久化、跨会话可复用”的选项，例如：

- `MaxDisplayFps`
- `IsUseCacheFile`
- `IsCalArtculation`
- `EvaFunc`
- `StatusTextProperties`
- `RoiTextProperties`

也就是说，真正属于“共享相机实时处理”的部分应该继续稳定在 ImageEditor 层这份全局配置里。

### B. 会话运行态层

建议把下面这些东西视为“每个 ImageView / 每次实时会话独有”的运行态，而不是外部字段：

- `DVRectangleText`
- `DVText`
- 当前绑定的 `ImageView`
- 当前 session 的 overlay 是否已挂载
- 当前显示缓冲、fps 计数、articulation 状态

这层应该由 `CameraRealtimeFramePipeline` 内部自持，或者由一个更明确的 `CameraRealtimeSession` / `CameraRealtimeController` 来持有。

### C. 宿主适配层

DisplayCamera / VideoReader 这类外部宿主理论上只应该提供：

- 数据源如何打开和关闭
- 帧数据如何提交
- 当前是否 active
- source-specific provider，比如 `FlipModeProvider`
- 少量展示策略，比如 `statusTextFormatter`

宿主不应再直接 new 和长期持有 `DVRectangleText` / `DVText`。

### 4.2 对当前三个字段的具体建议

#### `VideoReaderConfig`

建议：不要继续沿用这个名字给 DisplayCamera 复用。

原因：

- 语义不对
- 它会让“共享实时处理配置”和“VideoReader 专属配置”混在一起

应该拆出一个真正中性的：

- `CameraRealtimePipelineConfig`

然后：

- `VideoReader` 如有自己专属配置，再额外持有自己的 reader config
- `DisplayCamera` 只依赖中性的 realtime pipeline config

#### `DVRectangleText`

建议：收进 pipeline/session 内部。

它本质上不是业务配置，而是“实时 ROI overlay 的运行时实体”。

更合理的方式是：

- 管线根据 `RoiTextProperties` 自己创建它
- 或由统一 factory 创建，再交给管线接管生命周期

#### `DVText`

建议：同样收进 pipeline/session 内部。

它也是“状态文字 overlay 实体”，不应长期挂在外部宿主类上。

如果外部需要影响文本内容，应提供：

- `statusTextFormatter`
- 或更明确的 `IRealtimeStatusPresenter`

而不是把 `DVText` 对象本身暴露给宿主。

## 5. 如果打开的是普通图像，控制逻辑应该是什么

这是当前另一个必须先说清楚的点。

### 5.1 普通图像和实时流应该是两种显式模式

建议把 ImageView 当前工作模式明确成至少两类：

- `StaticImage`
- `RealtimeStream`

如果后面需要，还可以再细分：

- `RealtimeFrozen`
- `ProcessedPreview`

但最少也要先把“普通图像”和“实时流”区分开。

### 5.2 打开普通图像时，Realtime 不应该接管控制权

打开普通图像时建议行为：

- `ImageShow.Source` 由普通图像打开器负责
- `Realtime` 保持 idle，不主动写入 `WriteableBitmap`
- `CameraRealtimeFramePipeline` 不应启动
- realtime overlay 不应挂载
- freeze/snapshot/diagnostics 这类 realtime 工具应隐藏或至少 disabled

也就是说：

- 普通图像场景可以复用 `ImageView`
- 但不能被当成“默认带着一个激活的 realtime session”来处理

### 5.3 打开普通图像时，哪些状态该落在哪里

这里建议顺着现有 `ImageViewConfig` 的两个作用域走：

- `ViewState`：当前视图的临时状态
- `OpenerRuntime`：打开器运行态

具体建议：

- 当前是否为 realtime 模式：放 `OpenerRuntime`
- 当前 realtime source 类型（camera/video/mvs/file）：放 `OpenerRuntime`
- 当前普通图像的伪彩/阈值/探针状态：放 `ViewState`

这样“打开普通图像”时，工具和显示逻辑都可以基于 `OpenerRuntime` 判断当前是不是 realtime 上下文。

### 5.4 当前仓库里还没有 realtime 工具的上下文可见性机制

目前 `IEditorTool` 接口只有：

- `ToolBarLocal`
- `GuidId`
- `Order`
- `Icon`
- `Command`

没有现成的 `IsVisible` / `IsEnabledWhen` 这类上下文可见性钩子。

这意味着当前新增的：

- `FreezeRealtimeFrameEditorTool`
- `SnapshotRealtimeFrameEditorTool`
- `RealtimeDiagnosticsEditorTool`

如果直接注册为通用 ImageView 工具，它们会天然出现在普通图像场景里。

所以如果要把“打开普通图像时控制逻辑”做干净，后面至少要补一个机制，二选一：

#### 方案 A：给工具系统补上下文可见性

例如新增：

- `IConditionalEditorTool`
- `bool IsVisible(EditorContext context)`

这是最干净的方式。

#### 方案 B：先保守处理，保持显示但禁用

如果暂时不改工具系统，也至少应该让 realtime 工具在非 realtime 模式下：

- command 不可执行
- 或点击时明确提示“当前不是实时流上下文”

### 5.5 我建议的最终控制逻辑

如果打开的是普通图像，建议逻辑是：

1. Image opener 负责设置当前图像 source 和图像元数据。
2. `ImageView.Config.SetOpenerRuntime(...)` 标记当前上下文为 `StaticImage`。
3. 若之前存在 realtime session，则先 Stop 并清掉 realtime overlay。
4. Realtime 工具根据上下文隐藏或禁用。
5. PseudoColor、Histogram、PixelProbe 等普通图像功能继续可用，但它们读的是当前静态图像状态，不是 realtime session。

如果打开的是实时流，建议逻辑是：

1. Source opener 标记当前上下文为 `RealtimeStream`。
2. 创建或激活 realtime pipeline/session。
3. 由 pipeline 接管 overlay、fps、status、flip、snapshot raw source。
4. Realtime 工具启用。
5. 如果需要伪彩，后续由统一显示状态决定 raw/display/pseudo 的切换，而不是多个模块直接改 `ImageShow.Source`。

## 6. 建议接下来怎么改

建议按下面的顺序收口，而不是继续在当前 diff 上叠功能。

### 第一步：先缩 scope，只保留实时主线

建议先把当前修改拆成两类：

- 主线保留：ImageView.Realtime、CameraRealtimeFramePipeline、DisplayCamera、VideoReader、CameraLocalWindow、MVS 接入
- 暂时拆出：像素探针色度、live histogram、资源删除

原因：

- 这样可以先把实时底层的语义稳定下来
- 其他用户功能之后再挂上去，返工成本更低

### 第二步：先定义清楚 4 个概念

在继续改代码前，先把下面四个概念写死：

- RawFrame：采集到的原始帧
- DisplayedFrame：当前屏幕实际显示的位图
- ProcessedPseudoFrame：伪彩处理得到的显示结果
- FrozenFrame：冻结时保留下来的那一份状态

只有这几个概念先固定，下面这些接口才不会反复摇摆：

- Freeze
- Snapshot
- Histogram live source
- Pixel probe
- 外部控制 API

### 第三步：把伪彩并回统一显示状态

这是后续最优先要做的结构改动。

建议目标不是让 PseudoColorController 继续直接改 ImageShow.Source，而是改成下面二选一之一：

#### 方案 A：伪彩图也纳入 Realtime 服务

Realtime 维护：

- 当前 raw frame snapshot
- 当前 displayed bitmap
- 当前 processed pseudo bitmap

然后由 Realtime 决定当前显示的是哪一层。

#### 方案 B：Realtime 只管 raw/display，PseudoColor 改成显式 layer

让伪彩图成为 Realtime 之上的一层显示层，而不是直接抢占 ImageShow.Source。

无论选哪种，核心原则都是：

- ImageShow.Source 不能再由多个模块直接争抢

### 第四步：继续明确 shared config 和 pipeline runtime 的边界

“配置下沉”这一步已经做完，下一步不再是继续改名字，而是继续划清边界：

- `DefaultRealtimeCameraConfig` 负责共享可编辑参数
- `CameraRealtimeFramePipeline` 负责运行态、overlay 生命周期和相机处理控制
- direct-Realtime 入口只消费自己真正用得到的共享参数

### 第五步：明确哪些入口该走 Pipeline，哪些入口只走 Realtime

建议标准可以这样定：

- 只需要显示最新帧，不涉及 articulation/pseudo/ROI 状态/FPS 叠加的入口：直接走 ImageView.Realtime
- 需要相机处理请求、伪彩回流、统一状态文字、flip、ROI、snapshot 语义的入口：走 CameraRealtimeFramePipeline

按这个标准：

- DisplayCamera：应该走 Pipeline
- VideoReader：应该走 Pipeline
- CameraLocalWindow：看后续是否需要 pseudo/articulation，如果需要就也迁到 Pipeline
- MVSViewWindow：如果只是通用显示，可以继续只走 Realtime

### 第六步：在语义稳定后，再恢复用户功能增强

当 raw/display/pseudo/freeze 语义稳定后，再回头决定这些功能读哪一层：

- SnapshotRealtimeFrameEditorTool
- FreezeRealtimeFrameEditorTool
- RealtimeDiagnosticsWindow
- live histogram
- pixel probe colorimetry

这时每个功能都能明确回答“它读的是哪一份数据”，不会再反复改。

### 第七步：按统一验证矩阵回归

建议后续每次收口后固定验证这些场景：

- Engine 构建通过
- ImageEditor 构建通过
- DisplayCamera 打开/关闭正常
- VideoReader 打开/关闭正常
- flip 正常
- freeze 正常
- raw snapshot 正常
- displayed png 正常
- pseudo 显示切换正常
- ROI/状态叠加层不会残留
- MVS 实时显示正常

## 7. 如果只做一件事，下一步应该先做什么

如果当前只允许做一件事，建议先做：

**把 PseudoColorController 对 ImageShow.Source 的直接控制收回来，统一到 Realtime 状态面。**

原因很简单：

- 这是当前 Freeze、Snapshot、Histogram、Pixel probe、外部控制 API 能否语义一致的根节点
- 只要这个点不先处理，继续往上加功能都会越来越乱

## 8. 当前结论

当前修改不是“方向错了”，而是“改对了几条主线，但混进了太多并行需求”。

已经成型的部分是：

- ImageView.Realtime 基础设施
- CameraRealtimeFramePipeline
- DisplayCamera/VideoReader 的共享化趋势
- pointer frame 的 flip 内聚

还没有真正收口的部分是：

- PseudoColor 与 Realtime 的统一状态语义
- CameraLocalWindow/MVS 是否要继续统一到相同抽象
- 用户功能增强和结构改造的拆层顺序

建议从这里开始，不再继续散改，而是先按本文档把 scope 和语义收紧。