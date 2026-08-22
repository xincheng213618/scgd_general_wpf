# Conoscope 当前架构

这份文档描述当前代码，不为目录或 `partial` 数量辩护。判断一个类型是否应该存在，只看它是否拥有独立状态、生命周期或可测试规则。

## 先看三个组合点

| 组合点 | 文件 | 责任 |
|---|---|---|
| 窗口 Shell | `ConoscopeWindow.xaml(.cs)` | 文档标签、Ribbon、采集入口、活动文档切换、分析结果窗口 |
| 单文档 Viewer | `ConoscopeView.xaml(.cs)` | 把文档状态投影到 WPF，编排坐标轴、参考曲线、关注点、显示和导出 |
| 轻量图像宿主 | `ConoscopeImageHost.xaml(.cs)` | Zoombox、DrawCanvas、图像替换和关注点编辑器的生命周期 |

`ConoscopeView` 和 `ConoscopeWindow` 不再按方法类别机械拆成十几个共享同一可变对象的 partial 文件。当前保留单一 code-behind，是为了让调用环和剩余职责能够被直接看见。以后只有出现真正独立的所有者时才抽类型，不能再以“文件太长”为理由搬运方法。

## 真正的边界

### ConoscopeDocument：数据与所有权

`ConoscopeDocument.cs` 独占一张 CVCIE 的 X/Y/Z `Mat`、文件名、曝光摘要、取消源和加载版本。View 只能读取 Mat，不能替换或释放它们。

```text
ConoscopeView.OpenConoscope
        ↓
ConoscopeDocument.OpenAsync
        ↓
直接读取 Y ──→ InitialDisplayReady ──→ 首屏
        ↓
后台顺序读取 X、Z ──→ DeferredChannelsReady ──→ 衍生通道/分析
```

必须保留的不变量：

- `CVFileUtil.ReadCIEFileChannel` 只读指定的内嵌通道；不能退回通用 `OpenLocalCVFile`。
- Y-first、X/Z 后台补齐；无联合灰尘处理时 Y 不重复读取或处理。
- 同一文档 single-flight/latest-wins；取消的请求不能提交 Mat。
- Mat 的创建方负责释放，只有成功提交后所有权才转移给 Document。
- 联合预处理即使在调度前取消，也必须进入负责释放 Y 的委托。

### ConoscopeViewState：持久语义状态

每个 View 只有一个 `ConoscopeViewState`。它保存显示通道、伪彩、预处理、色差/对比度选择、坐标轴参数以及当前能力状态。活动文档 Ribbon 的普通值通过 Binding 读取 State；需要校验或产生渲染副作用的写操作仍调用 View 的语义方法。

不要再引入：

- `RenderingConfig` / `PreprocessConfig` 等指向同一对象的别名；
- Window 与 View 各自维护一份快照；
- 以 ComboBox、ToggleButton 或 TextBox 的当前值作为业务状态。

鼠标捕获、缩放、DrawCanvas 和 ScottPlot 属于 WPF 视图机制，保留在 code-behind/Host，不为追求形式上的 MVVM 包装成命令或 DialogService。

### Application：可测试工作流和外部边界

- `Application/Capture/`：采集工作流和纯结果模型。
- `Application/Analysis/`：测量快照、色域和对比度会话。
- `Application/Preprocess/`：预处理选项和 Mat 替换规则。
- `Application/FocusPoiTemplateRepository.cs`：POI 模板数据库事务；View 不创建 SqlSugar 连接。

Application 代码不得显示 MessageBox，也不拥有 WPF 控件。

## 显示通道

Y 只需要 Y；Contrast 只需要 Y 和同尺寸的对侧参考 Y；X/Z/CIE/色差需要完整 XYZ。所有入口（显示、参考曲线、3D、当前导出和高级导出）使用同一套 readiness 规则。

```text
State.DisplayChannel
        ↓ 先验证能力和参考数据
借用 X/Y/Z 或创建衍生 Mat
        ↓
有界范围计算 / 灰度或伪彩
        ↓
冻结 WriteableBitmap
        ↓
ImageHost.ReplaceDisplayedImage
```

衍生通道创建失败不能静默回退到 Y 后仍标记为 Δuv/Contrast。ImageCenter 色差参考在文档数据改变时失效，并且每个数据版本只计算一次，曲线或导出逐点读取不会重复扫描 51×51 ROI。

## ImageHost 为什么不复用完整 ImageView

完整 `ColorVision.ImageEditor.ImageView` 会初始化 EditorContext、工具工厂、反射工具、图层、设置和拖放等能力。Conoscope 只需要 Zoombox、DrawCanvas 和关注点 overlay；换回完整 ImageView 会恢复不需要的启动成本并改变 Clear/SetImageSource 语义。

`ConoscopeImageHost` 因此保留为轻量 viewport。内部 `FocusCircleEditor` 独占圆的 visual、选择、绘制/擦除、菜单、边界和 debounce。Host 对 View 暴露 `ResetDocument`、`ReplaceDisplayedImage`、缩放、指针位置和单一 `FocusCircleInteractionMode`，不再模拟旧 `ImageShow/Zoombox1` API。

- 新文档使用 `ResetDocument`，清除旧文档关注点。
- 通道/伪彩刷新使用 `ReplaceDisplayedImage`，保留当前文档关注点。
- `Dispose` 幂等，并释放 DrawCanvas、事件和命令绑定。

## 配置与全局参考

- `ConoscopeConfig` 是唯一可序列化全局配置，并在加载后集中迁移旧值。
- 设置窗口编辑 working copy；只有“应用并保存”才提交。
- 预处理设置页直接 TwoWay 绑定配置，不再手写 13 份控件同步。
- Window 合并同一 Dispatcher 周期内的配置 PropertyChanged，避免一次 Apply 触发十几轮刷新。
- `ConoscopeGlobalReferenceStore` 独占参考 Mat 和持久化，并通过 `Changed` 通知窗口；View 不再反向调用静态 Window 单例刷新 UI。

## 关注点

- `FocusCircleInteractionMode` 保证 Browse/Select/Draw/Erase 互斥。
- Host 管低层 visual 与鼠标交互；View 管测量编排和反馈。
- `FocusPointMeasurementService` 管 ROI、像素/极坐标换算。
- `FocusPointPolarEditModel` 是草稿；只有提交才写回 visual，取消不会修改圆。
- `FocusPoiTemplateRepository` 管数据库读取和事务，View 只决定如何显示错误。

## 维护规则

1. 先确认状态和资源由谁拥有，再决定是否抽类型。
2. 禁止新增 `ConoscopeView.*.cs` / `ConoscopeWindow.*.cs` 式机械 partial；抽出的类型必须能说清独立输入、输出和生命周期。
3. Application 和领域处理不得显示消息、创建窗口或拥有控件；`FocusPointMeasurementService` 暂以 `System.Windows.Point` 作为几何兼容输入，新领域 API 应优先使用纯数值几何。`ConoscopeModuleService` 是宿主 UI 集成边界，可以定位/打开窗口并反馈入口校验，但 View/领域服务不得借静态 Window 反向刷新 UI。
4. 不为一个调用点新增 interface/factory/forwarding service。
5. 大 Mat 不为“代码整洁”而 clone；任何改动都要审查峰值内存和异常路径 Dispose。
6. 显示可以使用有界近似，测量、分析和导出必须读取原始数值。

## 验证

```powershell
dotnet build .\Plugins\Conoscope\Conoscope.csproj -p:Platform=x64
dotnet test .\Test\Conoscope.Tests\Conoscope.Tests.csproj -p:Platform=x64
```

真实大图回归可设置 `CONOSCOPE_REAL_SAMPLE` 后运行：

- `ReadsConfiguredRealWorldSampleOneChannelAtATime`：记录三个通道的尺寸、payload、耗时、采样 hash 和峰值工作集。
- `OpensConfiguredRealWorldSampleThroughStagedDocumentOwner`：验证 Y-ready、XYZ-ready、文档所有权和分阶段加载的峰值工作集。

环境变量使用测试机自己的样本路径；不要把个人机器绝对路径写入仓库。
