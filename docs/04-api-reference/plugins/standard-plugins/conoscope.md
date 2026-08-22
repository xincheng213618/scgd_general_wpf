# Conoscope 插件

Conoscope 是 `Plugins/Conoscope/` 下的 VAM/锥镜分析插件，用于锥镜图像观察、关注点采样、综合色域计算、黑白对比度计算、预处理和结果导出。

## manifest

插件身份为 `Conoscope`，入口程序集为 `Conoscope.dll`。发布版本来自项目编译出的 DLL `FileVersion`；同目录 `manifest.json` 记录插件身份、入口程序集和最低宿主版本。

## 先查什么

| 现象 | 第一检查点 |
| --- | --- |
| Tool 菜单没有 `VAM` | 插件目录、`manifest.json`、`dllpath`、`Conoscope.dll` |
| 窗口打开但没有图像 | 样例路径、CVCIE 格式、`ConoscopeDocument` 的 Y-first 载入事件和失败状态 |
| 关注点数值异常 | 当前活动 View、显示通道、关注点半径、参考坐标和采样位置 |
| 色域/对比度结果为空 | R/G/B 或白/黑快照是否完整，是否选择标准色域 |
| 首页快捷区不影响图像 | 活动标签页的 `ConoscopeViewState` 绑定、通道能力状态和 View 语义方法 |
| 预处理没有变化 | `ConoscopePreprocessPipeline` 是否启用，参数是否写入并刷新视图 |
| MVS 画面为空 | `MvCameraControl.dll`、MVS 驱动、相机权限、线缆、`MVSViewManager` |
| 导出缺字段 | 导出模型、关注点快照、结果窗口字段是否同步更新 |

## 当前能力

| 能力 | 当前入口 | 说明 |
| --- | --- | --- |
| 主窗口 | `ConoscopeWindow.xaml(.cs)` | Ribbon、活动 View、采集、预处理、分析和导出入口 |
| 文档数据 | `ConoscopeDocument.cs` | X/Y/Z Mat 所有权、Y-first 分阶段读取、取消和 latest-wins 提交 |
| 图像视图 | `ConoscopeView.xaml(.cs)` | 将单一 `ConoscopeViewState` 投影为通道、参考曲线、关注点、显示和导出 |
| 关注点 | `ConoscopeImageHost.xaml(.cs)`、`FocusPointMeasurementService` | 轻量 viewport、互斥编辑模式、overlay 和采样换算 |
| 色域/对比度 | `Application/Analysis/ConoscopeAnalysisSession.cs` | R/G/B 快照、白/黑快照和分析会话 |
| 预处理 | `Application/Preprocess/`、`Processing/Preprocess/` | 常规滤波、灰尘检测/修复和非正 XYZ clamp；伪彩属于显示阶段 |
| 观察相机 | `MVS/` | Conoscope 内部观察/辅助采集链，不等同 Engine 通用相机 |
| 运行配置 | `Core/ConoscopeConfig.cs`、`Core/ConoscopeManager.cs` | 插件配置和运行时管理；设置页直接绑定 working copy |

## 用户流程

| 流程 | 关键步骤 |
| --- | --- |
| 打开与采集 | Tool 菜单 `VAM` -> `ConoscopeWindow` -> 导入 CVCIE/打开观察相机/选择型号 |
| 当前视图控制 | 活动标签页变化后，把 Ribbon 绑定到该 View 唯一的 `ConoscopeViewState`，副作用操作交给 View 语义方法 |
| 色域分析 | 记录 R/G/B 关注点快照 -> 选择标准色域 -> 打开 `ColorGamutResultWindow` |
| 对比度分析 | 记录白场和黑场 -> 打开 `ContrastResultWindow` |
| 结果导出 | 方位、极角或高级导出要包含关注点数据和结果字段 |

## 构建与交付

```powershell
dotnet build .\Plugins\Conoscope\Conoscope.csproj -c Release -p:Platform=x64
.\Scripts\package_plugin.bat Conoscope
```

从 solution/MSBuild 构建且 `SolutionDir` 有效时，PostBuild 才把主 DLL 和静态元数据镜像到 `ColorVision/bin/x64/<Config>/net10.0-windows/Plugins/Conoscope/`；直接构建项目时产物留在项目 `bin`。交付目录至少应包含 `Conoscope.dll`、`manifest.json`、`README.md`、`CHANGELOG.md`；需要观察相机时还要记录 MVS/native 依赖是否已验证。

## 验收

| 验收项 | 通过标准 |
| --- | --- |
| 插件装载 | Tool 菜单出现 `VAM`，可打开 `ConoscopeWindow` |
| 分阶段打开 | 大 CVCIE 的 Y 首屏先可见，X/Z 后台补齐；未就绪时 X/Z/CIE/色差入口受统一 readiness 限制 |
| 衍生通道 | Contrast 只要求 Y 和同尺寸参考 Y；衍生通道失败不会把 Y 图冒充为目标通道 |
| 当前视图同步 | 多标签切换后 Ribbon、快捷区启用状态和活动 View 一致 |
| 关注点采样 | 添加/拖动关注点圆后数值刷新；新文档清除旧关注点，同文档换通道/伪彩保留关注点和交互模式 |
| 色域分析 | R/G/B 记录完整后可查看综合色域总览和单关注点结果 |
| 对比度分析 | 白/黑记录完整后可查看白场亮度、黑场亮度和对比度 |
| 预处理 | 滤波、灰尘修复或非正 XYZ clamp 后视图刷新；失败时 Mat 所有权和画面状态明确 |
| MVS 相机 | 有硬件时能枚举、预览并显示光栅 overlay；无硬件时明确标记未验证 |
| 导出 | 文件包含预期列和关注点数据 |

## 边界

- 关注点逻辑是插件本地实现，不等同 Engine POI 模板。
- 色域/对比度结果窗是独立展示，不要把结果控件堆回主窗口。
- 首页快捷区绑定活动 View 唯一的 `ConoscopeViewState`；不要恢复 Window/View 两份状态和手工双向同步。
- 功能、运行边界或元数据变化时同步 `Plugins/Conoscope/README.md`、`CHANGELOG.md` 和 `Docs/ARCHITECTURE.md`。
- 修改分析字段时同步 CSV 导出、结果窗口和批量记录模型。

## 关键文件

| 任务 | 先看 |
| --- | --- |
| 主窗口/Ribbon | `ConoscopeWindow.xaml`、`ConoscopeWindow.xaml.cs`、`Presentation/Ribbon/ConoscopeRibbonResources.xaml` |
| 文档加载与 Mat 所有权 | `ConoscopeDocument.cs` |
| 图像与关注点 | `ConoscopeView.xaml.cs`、`ConoscopeImageHost.xaml.cs` |
| 分析 | `Application/Analysis/ConoscopeAnalysisSession.cs`、`Analysis/MeasurementCaptureModels.cs` |
| 预处理 | `Application/Preprocess/ConoscopePreprocessPipeline.cs` |
| POI 模板数据库 | `Application/FocusPoiTemplateRepository.cs` |
| 配置 | `Core/ConoscopeConfig.cs` |
| 架构不变量 | `Plugins/Conoscope/Docs/ARCHITECTURE.md` |
