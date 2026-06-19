# Conoscope 插件

Conoscope 是当前仓库里的 VAM/锥镜分析插件，源码位于 `Plugins/Conoscope/`。它用于锥镜图像观察、参考坐标分析、关注点采样、综合色域计算和黑白对比度计算。

## manifest 信息

| 字段 | 当前值 |
| --- | --- |
| `Id` | `Conoscope` |
| `name` | `Conoscope` |
| `version` | `1.4.6.1` |
| `dllpath` | `Conoscope.dll` |
| `requires` | 当前 manifest 未声明 |

## 插件定位

当前实现已经把主要职责拆成三层：

- 图像视图层：`ConoscopeView` 负责图像显示、通道切换、关注点圆、参考线/极角圆和局部交互。
- 主窗口层：`ConoscopeWindow` 负责 Ribbon、当前活动 View、采集、预处理、分析和导出入口。
- 业务服务层：`Application/Analysis`、`Application/Preprocess`、`Core` 等目录负责计算、预处理、导出和模型配置。

不要把它写成单一窗口工具。现在的 Conoscope 已经包含视图、工作流、预处理、结果展示和导出几条链。

## 主要入口

| 文件 | 作用 |
| --- | --- |
| `ConoscopeWindow.xaml(.cs)` | 主窗口 |
| `ConoscopeWindow.Ribbon.cs` | Ribbon 组织 |
| `ConoscopeWindow.HomeQuickControls.cs` | 首页当前视图快捷控制 |
| `ConoscopeWindow.AnalysisRibbon.cs` | 分析页按钮和状态 |
| `ConoscopeView.xaml(.cs)` | 单个图像视图 |
| `ConoscopeView.FocusPoint.cs` | 关注点圆绘制和采样 |
| `ConoscopeView.ReferenceAxis.cs` | 参考线、极角圆和坐标辅助 |
| `Application/Analysis/ConoscopeAnalysisWorkflow.cs` | 色域、对比度分析工作流 |
| `Application/Analysis/FocusPointMeasurementService.cs` | 关注点测量服务 |
| `Application/Preprocess/ConoscopePreprocessPipeline.cs` | 图像预处理流水线 |
| `Core/ConoscopeManager.cs` | 插件运行时管理 |
| `Core/ConoscopeConfig.cs` | 插件配置 |

## 用户可见流程

### 打开与采集

工具菜单中的 `VAM` 入口打开 `ConoscopeWindow`。进入窗口后，用户可以导入 CVCIE 图像、打开观察相机、选择型号，并在多个视图标签页之间切换。

### 当前视图控制

首页“当前视图”快捷区会跟随活动标签页同步：

- 切换显示通道。
- 切换参考图形模式。
- 编辑参考半径或参考角度。
- 进入 3D、CIE、方位导出、极角导出和高级导出。

没有活动视图时，快捷区保留布局但禁用交互。

### 关注点采样

Conoscope 使用插件内的本地关注点逻辑。每个关注点以圆形 overlay 绘制在图像上，可以拖动和右键计算。色域和对比度不是只取单点即时值，而是记录“当前 View 的全部关注点快照”。

### 色域分析

典型流程：

1. 打开 R 图并调整关注点。
2. 记录 R。
3. 切换到 G 图并记录 G。
4. 切换到 B 图并记录 B。
5. 选择标准色域。
6. 计算综合色域。

结果会进入独立的 `ColorGamutResultWindow`，支持总览和单关注点查看。

### 对比度分析

典型流程：

1. 打开白场图并记录白。
2. 打开黑场图并记录黑。
3. 计算对比度。

结果进入 `ContrastResultWindow`，按关注点展示白场亮度、黑场亮度和对比度。

## 预处理链路

预处理相关代码集中在：

- `ConoscopePreprocessSettingsWindow.xaml(.cs)`
- `ConoscopePreprocessSettingsControl.xaml(.cs)`
- `Application/Preprocess/ConoscopePreprocessPipeline.cs`
- `Processing/Preprocess/DustRemovalProcessor.cs`
- `Processing/Preprocess/ImageFilterProcessor.cs`
- `Processing/Preprocess/XyzClampProcessor.cs`

当前支持滤波、伪彩、灰尘修复、阈值、裁剪和 XYZ clamp 等处理。预处理后会刷新当前视图显示。

## MVS 相机链路

`MVS/` 目录承接观察相机相关能力：

- `MVCamera.cs`
- `MVSViewWindow.xaml(.cs)`
- `MVSViewManager.cs`
- `MVSGratingSettingsWindow.xaml(.cs)`
- `MVSGratingOverlayVisual.cs`

交接时把它看作 Conoscope 内部观察/辅助采集链，不要和 Engine 的通用 `DeviceCamera` 混写。

## 构建与输出

构建命令：

```powershell
dotnet build Plugins/Conoscope/Conoscope.csproj -c Release -p:Platform=x64
```

PostBuild 会把以下文件复制到主程序插件目录：

```text
ColorVision/bin/x64/<Config>/net10.0-windows/Plugins/Conoscope/
  Conoscope.dll
  manifest.json
  README.md
  CHANGELOG.md
```

打包命令：

```powershell
Scripts\package_plugin.bat Conoscope --no-upload
```

## 交接验收表

| 验收项 | 操作 | 通过标准 |
| --- | --- | --- |
| 插件装载 | 检查 `manifest.json`、`dllpath` 和 Tool 菜单 | Tool 菜单出现 `VAM`，可以打开 `ConoscopeWindow` |
| 交付结构 | 构建或打包后检查插件目录 | `Conoscope.dll`、`manifest.json`、`README.md`、`CHANGELOG.md` 存在；需要观察相机时同步记录 MVS/native 依赖 |
| 图像打开 | 导入 CVCIE 或现场样例图 | `ConoscopeView` 正常渲染图像，通道切换和参考线/极角圆可见 |
| 当前视图同步 | 切换多个图像标签页并修改首页快捷区 | 活动 View、Ribbon 状态和快捷区启用/禁用状态一致 |
| 关注点采样 | 添加或拖动关注点圆，右键执行计算 | 关注点数值刷新，圆形 overlay 位置稳定 |
| 色域分析 | 依次记录 R/G/B，选择标准色域后计算 | `ColorGamutResultWindow` 打开，能查看总览和单关注点结果 |
| 对比度分析 | 记录白场和黑场后计算 | `ContrastResultWindow` 打开，能查看白场亮度、黑场亮度和对比度 |
| 预处理 | 调整滤波、灰尘修复、阈值、裁剪或 XYZ clamp | 当前视图刷新，处理前后效果可复核 |
| MVS 观察相机 | 在有相机和驱动的机器上打开 MVS 视图 | 能枚举相机、预览画面并显示光栅 overlay；无硬件时在交接记录中标记未验证 |
| 结果导出 | 执行方位、极角或高级导出 | 文件包含预期列和关注点数据，导出路径可回收 |

## 故障首查

| 现象 | 先查什么 |
| --- | --- |
| Tool 菜单没有 `VAM` | 插件目录、`manifest.json` 的 `Id/dllpath`、`Conoscope.dll` 是否复制到主程序 `Plugins/Conoscope/` |
| 窗口能打开但没有图像 | 样例文件路径、CVCIE 格式、`ConoscopeView` 图像载入链和 FileIO 边界 |
| 关注点数值异常 | 当前活动 View、显示通道、关注点半径、参考坐标和采样位置 |
| 色域或对比度结果为空 | R/G/B 或白/黑快照是否完整记录，是否选择了标准色域 |
| 首页快捷区不影响图像 | 活动标签页同步、当前 View 绑定和 Ribbon 状态刷新 |
| 预处理没有变化 | `ConoscopePreprocessPipeline` 是否启用，对应参数是否写入并触发视图刷新 |
| MVS 画面为空 | `MvCameraControl.dll`、MVS 驱动、相机权限、线缆和 `MVSViewManager` 状态；不要按 Engine `DeviceCamera` 排查 |
| 导出缺字段 | 导出模型、关注点快照和结果窗口字段是否一起更新 |

## 交接注意事项

- 关注点逻辑是插件本地实现，不等同于 Engine POI 模板的通用逻辑。
- 色域/对比度结果窗是独立结果展示，不应再把大量结果控件堆回主窗口。
- 首页快捷区和视图内控件需要保持双向同步。
- 修改帮助入口时同时更新 `Plugins/Conoscope/README.md` 和 `CHANGELOG.md`，运行时帮助窗口会读取这些文件。
- 修改分析字段时同步检查 CSV 导出、结果窗口和批量记录模型。

## 推荐阅读顺序

1. `Plugins/Conoscope/README.md`
2. `Plugins/Conoscope/ConoscopeWindow.xaml.cs`
3. `Plugins/Conoscope/ConoscopeWindow.Ribbon.cs`
4. `Plugins/Conoscope/ConoscopeView.xaml.cs`
5. `Plugins/Conoscope/ConoscopeView.FocusPoint.cs`
6. `Plugins/Conoscope/Application/Analysis/ConoscopeAnalysisWorkflow.cs`
7. `Plugins/Conoscope/Application/Preprocess/ConoscopePreprocessPipeline.cs`
8. `Plugins/Conoscope/Core/ConoscopeConfig.cs`

## 继续阅读

- [现有插件现场验收与交接清单](../plugin-field-acceptance.md)
- [插件能力与交接矩阵](../plugin-capability-matrix.md)
- [插件运行与交接场景手册](../plugin-handoff-playbook.md)
