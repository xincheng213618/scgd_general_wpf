# Conoscope 代码结构

这份文档只描述当前代码。目标是让第一次接触插件的人能先找到入口，再沿一条明确的数据流阅读代码。

## 先记住五块

Conoscope 不再追求七层或更多“教科书分层”。当前按职责分成五块：

| 模块 | 主要文件 | 负责什么 |
|---|---|---|
| 窗口 Shell | `ConoscopeWindow*.cs` | 标签页、Ribbon、采集入口、活动视图、分析槽位 |
| 单图 Viewer | `ConoscopeView*.cs`、`ConoscopeImageHost*` | 一张图的状态、显示、坐标轴、关注点和参考曲线 |
| Imaging | `ConoscopeView.Data.cs`、`Core/ConoscopeColorimetry.cs`、`Core/ConoscopePseudoColorRenderer.cs`、`Processing/Preprocess/`、`Core/ConoscopeExportService.cs` | CVCIE 加载、Mat 处理、渲染和导出 |
| Analysis | `Analysis/`、`Application/Analysis/` | 关注点测量、色域/对比度计算、结果窗口 |
| Settings / Integration | `Core/ConoscopeConfig.cs`、`Core/ConoscopeModelProfile.cs`、`Core/ConoscopeGlobalReferenceStore.cs`、`Core/ConoscopeModuleService.cs`、`MVS/` | 配置、型号、全局参考、宿主入口和相机边界 |

`Application/Capture/ConoscopeCaptureWorkflow.cs` 和
`Application/Preprocess/ConoscopePreprocessPipeline.cs` 是值得保留的工作流边界；不要为了“层数整齐”再给每个简单调用增加接口、工厂或转发类。

## 打开一张图时发生什么

```text
菜单 / 宿主 ImageView
        ↓
ConoscopeModuleService.OpenModule
        ↓
ConoscopeWindow.AddConoscopeView
        ↓
ConoscopeView.OpenConoscope
        ↓
后台读取 Y → 预处理 Y → 首屏显示
        ↓
后台顺序读取 X、Z → 预处理 → 开放衍生通道/分析/导出
```

关键实现：

- `CVFileUtil.ReadCIEFileChannel` 只读取 CVCIE 内嵌的指定通道，不跟随头部的 `SrcFileName`。
- 首屏只需要 Y；X/Z 在后台补齐，Y 不会再读取或处理第二次。
- 同一 View 的加载是 single-flight/latest-wins：新请求取消旧请求，只有当前版本能回写。
- `ConoscopeView.Dispose()` 先取消后台加载，再释放 Mat。
- 不要把这里替换成通用 `OpenLocalCVFile` / `ToMat`。通用路径可能打开关联 TIFF，并会改变 32F 分析数据的语义。

## 一张图只有一个状态源

每个 `ConoscopeView` 有且只有一个 `ConoscopeViewState State`。它保存：

- 当前显示/导出通道；
- 伪彩与范围限制；
- 预处理参数；
- 色差和对比度选择；
- 当前坐标轴参数。

Ribbon 读取活动 View 的 `State`，写操作通过 View 的校验方法完成。不要重新引入隐藏的
ComboBox/TextBox 作为状态仓库，也不要在窗口和视图之间复制第二份快照。

全局 `ConoscopeConfig` 只提供新 View 的默认值和需要持久化的设置。已经打开的多个 View
各自持有本地状态，因此切换标签页不会互相覆盖。

## 显示与测量要分开理解

显示路径：

```text
XYZ Mat
  → 选择或计算显示通道
  → 获取显示范围
  → 8 位灰度 / 伪彩
  → 冻结的 WriteableBitmap
  → ConoscopeImageHost
```

- X/Y/Z 直接借用源 Mat，渲染器不再为它们克隆整张图。
- 对比度的 99.5% 显示上限使用有界采样，避免把三千万个 float 放入 `List` 后全排序。
- 色差矩阵共享分母并减少全尺寸临时 Mat。
- 这些优化只影响显示过程或中间存储，不应改变导出和测量值。

测量路径：

```text
图像坐标 / 关注点圆
  → FocusPointMeasurementService
  → ImageMeasurement / MeasurementCapture
  → ConoscopeAnalysis
  → ConoscopeAnalysisSession 的五个槽位
  → 结果窗口 / CSV
```

方位角统一使用 0–360°。坐标的正变换和反变换都应放在
`FocusPointMeasurementService`，不要在编辑器里再写一份角度公式。

## 参考曲线

`PolarAngleLine`（方位线）和 `ConcentricCircleLine`（极角圆）共享
`ReferenceCurve` 的值与采样集合。`ConoscopeView.ReferencePlot.cs` 使用同一绘图方法，
只通过 `IsClosed` 区分是否闭合。

`RgbSample` 是只读值类型。采样时预分配集合，避免拖动参考线时为每个像素创建对象。

## 配置边界

- `ConoscopeConfig` 是唯一可序列化的全局配置；直接访问其属性，不再包
  `Rendering/Preprocess/Export/...` 转发对象。
- `ConoscopeModelProfile` 保存型号差异。
- `ConoscopeGlobalReferenceStore` 独占参考 Mat 的生命周期和持久化。
- 设置窗口先编辑临时副本，只有“应用并保存”才写回全局配置。
- `ConoscopeModuleService` 只负责从宿主打开/找到窗口；活动 View 和打开文档由
  `ConoscopeWindow` 自己管理。

## 去哪里改

| 想改的功能 | 从这里开始 |
|---|---|
| 文件打开、取消、首屏速度 | `ConoscopeView.Data.cs` |
| 通道/伪彩显示 | `ConoscopeView.Display.cs`、`Core/ConoscopePseudoColorRenderer.cs` |
| 色度、色差、对比度公式 | `Core/ConoscopeColorimetry.cs` |
| 滤波和灰尘修复 | `Processing/Preprocess/` |
| 坐标轴与参考曲线 | `ConoscopeView.ReferenceAxis.cs`、`ConoscopeView.ReferencePlot.cs` |
| 关注点绘制/测量 | `ConoscopeImageHost.xaml.cs`、`ConoscopeView.FocusPoint.cs`、`FocusPointMeasurementService.cs` |
| 色域/对比度分析 | `MeasurementCaptureModels.cs`、`ConoscopeAnalysisSession.cs` |
| CSV 导出 | `ConoscopeView.Export.cs`、`Core/ConoscopeExportService.cs` |
| 全局设置/型号 | `Core/ConoscopeConfig.cs`、`Core/ConoscopeModelProfile.cs` |
| Ribbon 或标签页 | 对应的 `ConoscopeWindow.*.cs` |

## 维护规则

1. 先找现有状态源和生命周期所有者，再决定是否需要新类型。
2. 一个方法只被一处调用、且没有独立语义时，优先就近写清楚，不新增转发层。
3. Mat 的创建方必须明确谁负责 `Dispose`；异步结果在提交给 View 后才转移所有权。
4. 显示近似可以有界采样，但测量、分析和导出必须使用原始数值。
5. 可能改变 XYZ 的算法或默认参数必须用真实金样回归，不能与纯性能重构混在一起。
6. 新增 UI 设置优先使用仓库的 PropertyGrid 元数据或现有标准控件，并补中/英/繁资源。

## 验证

在仓库根目录执行：

```powershell
dotnet build .\Plugins\Conoscope\Conoscope.csproj -p:Platform=x64
dotnet test .\Test\Conoscope.Tests\Conoscope.Tests.csproj -p:Platform=x64
```

`Test/Conoscope.Tests` 当前覆盖：

- CVCIE 指定通道读取和格式元数据；
- 色差/对比度矩阵的数值边界；
- 0–360° 方位角与关注点 ROI；
- 色域/对比度分析槽位和关注点对齐；
- 插件版本和 Application 层 UI 依赖约束。

大文件性能回归使用真实 CVCIE 样本单独执行，至少记录总耗时、峰值工作集和通道校验值。
