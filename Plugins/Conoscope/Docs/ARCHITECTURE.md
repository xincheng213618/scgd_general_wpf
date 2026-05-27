# Conoscope Architecture

Version: 1.4.6.1

本文档定义 Conoscope 插件的编号化架构分层，用于指导后续代码组织和重构。

## 架构分层

```
CONO-60  Composition / Module Service
CONO-50  Presentation
CONO-40  Infrastructure
CONO-30  Application
CONO-20  Core
CONO-10  Domain
CONO-00  Bootstrap / Plugin Entry
```

### CONO-00 Bootstrap / Plugin Entry

插件入口、manifest、窗口启动。

- `App.xaml` / `App.xaml.cs` - 插件 Application 入口
- `manifest.json` - 插件元数据
- `AssemblyInfo.cs` - 程序集信息
- `Conoscope.csproj` - 项目文件

### CONO-10 Domain

纯模型、枚举、计算输入输出对象。不依赖 WPF 控件，不弹 MessageBox，不访问文件系统（纯数据对象除外）。

目录：`Domain/`

当前没有独立 Domain 模型。只有单处使用、不能表达稳定业务概念的临时数据容器不放入此层。

应迁入此层的类型（当前散落在 Core 中）：

- `Core/ExportMode.cs` 中的 `ExportChannel`、`ColorDifferenceReferenceMode`、`ContrastReferenceKind` 枚举
- `Core/ImageFilterType.cs` 中的 `ImageFilterType`、`DustRemovalMode` 枚举
- `Core/ConoscopeModelType.cs` 中的 `ConoscopeModelType` 枚举
- `Core/RgbSample.cs`、`Core/ConcentricCircleLine.cs`、`Core/PolarAngleLine.cs` 等纯数据类
- `Analysis/MeasurementCaptureModels.cs` 中的 `MeasurementPoint`、`MeasurementCapture`、`ColorGamutPointResult`、`ColorGamutComputationResult`、`ContrastPointResult`、`ContrastComputationResult` 等记录类型
- `Analysis/ManualImageAnalysis.cs` 中的 `ImageMeasurement`、`ColorGamutStandard`、`ColorGamutResult`、`ContrastResult` 等记录类型

### CONO-20 Core

色度计算、坐标变换、型号参数、数学计算、OpenCvSharp Mat 计算。尽量保持可测试，不依赖具体窗口。

目录：`Core/`

| 文件 | 层 | 说明 |
|---|---|---|
| `Core/ConoscopeColorimetry.cs` | CONO-20 | 纯色度计算引擎（xy/uv/CCT/色差/对比度矩阵） |
| `Core/ConoscopeExportService.cs` | CONO-20 | CSV 导出几何采样与写入 |
| `Core/ConoscopeConfig.cs` | CONO-20 | 配置模型（ViewModelBase + IConfig） |
| `Core/ConoscopeManager.cs` | CONO-20/60 | 单例管理器，持有 Config 和 GlobalReferenceStore |
| `Core/ConoscopeGlobalReferenceStore.cs` | CONO-20 | 全局参考图存储（文件 IO 部分可考虑迁移至 CONO-40） |
| `Core/ConoscopeModelProfile.cs` | CONO-20 | 型号配置（VA60/VA80） |
| `Core/ConoscopePseudoColorRenderer.cs` | CONO-20 | 伪彩色渲染 |
| `Core/ConoscopeCoordinateAxis.cs` | CONO-20 | 坐标轴叠加系统 |
| `Core/ConoscopeNumericHelper.cs` | CONO-20 | 数值工具 |
| `Core/ConoscopeReferenceMatSerializer.cs` | CONO-20 | Mat 二进制序列化 |
| `Core/CompositeFormatCache.cs` | CONO-20 | 字符串格式缓存 |
| `Analysis/MeasurementCaptureModels.cs` | CONO-10/20 | 数据模型 + 批量计算器（`DefaultBatchColorGamutCalculator`、`DefaultBatchContrastCalculator`） |
| `Analysis/ManualImageAnalysis.cs` | CONO-10/20 | 图像测量 + 色域/对比度计算器 |
| `Analysis/AnalysisResultCsvExporter.cs` | CONO-20 | 结果 CSV 导出 |

### CONO-30 Application

用例/工作流层。协调 Core、Domain、Infrastructure 完成业务流程。不直接操作 WPF 控件。

目录：`Application/`

| 文件 | 层 | 说明 |
|---|---|---|
| `Application/Capture/ConoscopeCaptureWorkflow.cs` | CONO-30 | 采集工作流（Flow + Camera） |
| `Application/Preprocess/ConoscopePreprocessPipeline.cs` | CONO-30 | 预处理管线 |
| **`Application/Analysis/ConoscopeAnalysisWorkflow.cs`** | CONO-30 | **新增** - 色域/对比度分析工作流 |

### CONO-40 Infrastructure

文件读取、CVCIE 加载、相机/MVS、外部依赖、序列化。所有外部 IO 集中到此层。

目录：`Infrastructure/`

| 文件 | 层 | 说明 |
|---|---|---|
| `ConoscopeView.Data.cs` | CONO-40/50 | 当前视图数据加载；CVCIE 直接读取为 `CV_32FC1` Mat |

MVS 相机相关代码位于 `MVS/` 目录，属于 CONO-40 但本次不触碰。

### CONO-50 Presentation

WPF Window、UserControl、Ribbon、View 事件处理。只负责展示、事件转发、轻量状态绑定。不放复杂计算和文件格式逻辑。

目录：`Presentation/` + 根目录 partial class 文件

| 文件 | 层 | 说明 |
|---|---|---|
| `Presentation/Helpers/ComboBoxHelper.cs` | CONO-50 | ComboBox 工具方法 |
| `Presentation/Formatters/ColormapNameFormatter.cs` | CONO-50 | 色图名称格式化 |
| `Presentation/Formatters/ConoscopeChannelDisplayFormatter.cs` | CONO-50 | 通道显示格式化 |
| `Presentation/Ribbon/*` | CONO-50 | Ribbon 资源 XAML |

### CONO-60 Composition / Module Service

ConoscopeModuleService、窗口发现、当前活动 View 注册、跨窗口协调。

| 文件 | 层 | 说明 |
|---|---|---|
| `Core/ConoscopeModuleService.cs` | CONO-60 | 模块生命周期服务 |

---

## 当前文件架构地图

### ConoscopeWindow partial 文件

| 文件 | 当前实际层 | 问题 | 迁移建议 |
|---|---|---|---|
| `ConoscopeWindow.xaml.cs` | CONO-50 | 主窗口代码，承担窗口初始化和状态管理 | 保持，逐步减少业务逻辑 |
| `ConoscopeWindow.HomeQuickControls.cs` | CONO-50 | 24KB，主页快捷控件逻辑较多 | 保持，后续可拆分配置同步逻辑 |
| `ConoscopeWindow.AnalysisRibbon.cs` | CONO-50→30 | 混合了按钮状态管理和分析计算编排 | **本次已重构**：计算逻辑抽至 ConoscopeAnalysisWorkflow |
| `ConoscopeWindow.Capture.cs` | CONO-50 | 采集工作流集成 | 保持，已有 Application/Capture 层 |
| `ConoscopeWindow.Preprocess.cs` | CONO-50 | 预处理设置窗口管理 | 保持 |
| `ConoscopeWindow.Documents.cs` | CONO-50 | 文档标签管理 | 保持 |
| `ConoscopeWindow.Ribbon.cs` | CONO-50 | Ribbon 初始化 | 保持 |
| `ConoscopeWindow.Analysis.cs` | CONO-50 | 3D/CIE 入口委托 | 保持 |

### ConoscopeView partial 文件

| 文件 | 当前实际层 | 问题 | 迁移建议 |
|---|---|---|---|
| `ConoscopeView.xaml.cs` | CONO-50 | 26KB，View 主代码 | 保持，逐步减少业务逻辑 |
| `ConoscopeView.Export.cs` | CONO-50 | 导出逻辑，含通道就绪检查、上下文构建、配置读写 | 保持，导出上下文已内联至此 |
| `ConoscopeView.FocusPoint.cs` | CONO-50 | 43KB，最大的 partial 文件 | 后续应拆分：采样计算→CONO-20，UI 绘制→CONO-50 |
| `ConoscopeView.ColorDifference.cs` | CONO-50 | 19KB，混合 UI 控件引用和色差计算 | 色差参考管理可抽至 CONO-30 |
| `ConoscopeView.Contrast.cs` | CONO-50 | 10KB，对比度参考管理 | 参考管理逻辑可抽至 CONO-30 |
| `ConoscopeView.ReferenceAxis.cs` | CONO-50 | 20KB，参考坐标轴绘制 | 保持 |
| `ConoscopeView.ReferencePlot.cs` | CONO-50 | 17KB，参考图形绘制 | 保持 |
| `ConoscopeView.Data.cs` | CONO-50 | 数据加载 | 保持 |
| `ConoscopeView.Display.cs` | CONO-50 | 显示渲染 | 保持 |
| `ConoscopeView.Preprocess.cs` | CONO-50 | 预处理应用 | 保持，已有 Application 层 |
| `ConoscopeView.Toolbar.cs` | CONO-50 | 工具栏管理 | 保持 |
| `ConoscopeView.WindowQuickControls.cs` | CONO-50 | 窗口快捷控件 | 保持 |

### Core 目录

| 文件 | 层 | 说明 |
|---|---|---|
| `Core/ConoscopeColorimetry.cs` | CONO-20 | 纯计算，保持 |
| `Core/ConoscopeExportService.cs` | CONO-20 | 纯 IO + 几何，保持 |
| `Core/ConoscopeConfig.cs` | CONO-20 | 配置模型，保持 |
| `Core/ConoscopeManager.cs` | CONO-60 | 单例管理，保持 |
| `Core/ConoscopeGlobalReferenceStore.cs` | CONO-20/40 | 参考存储，文件 IO 部分可迁移 |
| `Core/ConoscopeModelProfile.cs` | CONO-20 | 型号配置，保持 |
| `Core/ConoscopePseudoColorRenderer.cs` | CONO-20 | 渲染，保持 |
| `Core/ConoscopeCoordinateAxis.cs` | CONO-20 | 坐标轴，保持 |
| `Core/ConoscopeNumericHelper.cs` | CONO-20 | 工具，保持 |
| `Core/ConoscopeReferenceMatSerializer.cs` | CONO-20 | 序列化，保持 |
| `Core/CompositeFormatCache.cs` | CONO-20 | 缓存，保持 |
| `Core/ExportMode.cs` | CONO-10 | 枚举，后续迁移至 Domain |
| `Core/ImageFilterType.cs` | CONO-10 | 枚举，后续迁移至 Domain |
| `Core/ConoscopeModelType.cs` | CONO-10 | 枚举，后续迁移至 Domain |
| `Core/RgbSample.cs` | CONO-10 | 数据类，后续迁移至 Domain |
| `Core/ConcentricCircleLine.cs` | CONO-10 | 数据类，后续迁移至 Domain |
| `Core/PolarAngleLine.cs` | CONO-10 | 数据类，后续迁移至 Domain |
| `Core/ConoscopeImageViewContextMenu.cs` | CONO-50 | 上下文菜单，保持 |

### Analysis 目录

| 文件 | 层 | 说明 |
|---|---|---|
| `Analysis/MeasurementCaptureModels.cs` | CONO-10/20 | 数据模型 + 批量计算器 |
| `Analysis/ManualImageAnalysis.cs` | CONO-10/20 | 测量 + 色域/对比度计算器 |
| `Analysis/ColorGamutResultWindow.xaml.cs` | CONO-50 | 结果窗口 |
| `Analysis/ContrastResultWindow.xaml.cs` | CONO-50 | 结果窗口 |
| `Analysis/AnalysisResultCsvExporter.cs` | CONO-20 | CSV 导出 |

### Processing 目录

| 文件 | 层 | 说明 |
|---|---|---|
| `Processing/Preprocess/XyzClampProcessor.cs` | CONO-20 | 纯 Mat 计算 |
| `Processing/Preprocess/ImageFilterProcessor.cs` | CONO-20 | 纯 Mat 计算 |
| `Processing/Preprocess/DustRemovalProcessor.cs` | CONO-20 | 纯 Mat 计算 |

---

## 依赖规则

### 允许的依赖方向

```
CONO-50 (Presentation)  → CONO-30 (Application), CONO-20 (Core), CONO-10 (Domain)
CONO-30 (Application)   → CONO-20 (Core), CONO-10 (Domain), CONO-40 (Infrastructure)
CONO-20 (Core)          → CONO-10 (Domain)
CONO-40 (Infrastructure) → CONO-10 (Domain)
CONO-60 (Composition)   → CONO-50, CONO-30, CONO-20, CONO-10
```

### 禁止的依赖方向

- CONO-10 (Domain) 不得依赖任何其他层
- CONO-20 (Core) 不得依赖 CONO-30/40/50/60
- CONO-30 (Application) 不得依赖 CONO-50 (Presentation)
- CONO-40 (Infrastructure) 不得依赖 CONO-30/50/60

### 外部依赖约束

- CONO-10 (Domain)：仅 .NET BCL
- CONO-20 (Core)：可使用 OpenCvSharp、MathNet.Numerics，不依赖 WPF
- CONO-30 (Application)：可调用 Core 和 Infrastructure，不依赖 WPF
- CONO-40 (Infrastructure)：可使用 ColorVision.FileIO、外部 SDK
- CONO-50 (Presentation)：可使用 WPF、HandyControl、OpenTK

---

## ConoscopeWindow 与 ConoscopeView 职责边界

### ConoscopeWindow（主窗口）

- 管理 Ribbon 按钮状态和启用/禁用
- 管理多个 ConoscopeView 标签页
- 协调当前活动 View 的快捷控制同步
- 触发采集流程（委托给 Application 层）
- 触发分析计算（委托给 Application 层）
- **不直接实现计算逻辑**
- **不直接操作文件格式**

### ConoscopeView（图像视图）

- 管理单个图像的显示、通道切换、伪彩色渲染
- 管理关注点圆的绘制和交互
- 管理参考坐标轴/极角圆的绘制和交互
- 提供当前图像的状态查询接口（通道、参考模式、关注点数据）
- **不直接触发跨 View 的批量操作**
- **不直接管理 Ribbon 按钮状态**

---

## 编码约束

1. **禁止新增大 partial 文件继续堆功能**。新功能应优先考虑 Application 服务 + 薄 Presentation 适配器模式。
2. **每个新服务类建议不超过 300 行**，超过要拆分。
3. **Application 层服务不得直接引用 WPF 控件**。返回结果对象，由 Presentation 层决定如何展示。
4. **Domain 层类型不得包含 MessageBox、FileDialog 等 UI 交互**。
5. **后续新代码应优先放入 CONO-30 (Application)**，而非继续堆在 CONO-50 的 partial 文件中。

---

## 测试计划

### 当前阶段

- 新增 `Test/Conoscope.Tests` 项目（xUnit v3 + MTP 模式）
- 测试 `ConoscopeAnalysisWorkflow` 的状态判断和计算编排
- 测试数据完整/不完整场景下的返回结果
- 测试 `FocusPointMeasurementService` 的坐标转换、角度计算、ROI 均值
- 测试导出通道就绪检查（`ConoscopeView.IsChannelReady`）和截面导出选项
- 架构约束测试：VersionPrefix 与 manifest.json 一致性、Application 层无 MessageBox.Show

### 后续阶段

- 为 `ConoscopeColorimetry` 添加单元测试（纯计算，易测试）
- 为 `DefaultBatchColorGamutCalculator` / `DefaultBatchContrastCalculator` 添加单元测试
- 为 `ConoscopeExportService` 的几何采样逻辑添加单元测试
- 为 `Processing/Preprocess/` 的处理器添加单元测试
