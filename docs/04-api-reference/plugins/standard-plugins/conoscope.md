# Conoscope 插件

Conoscope 是 `Plugins/Conoscope/` 下的 VAM/锥镜分析插件，用于锥镜图像观察、关注点采样、综合色域计算、黑白对比度计算、预处理和结果导出。

## manifest

| 字段 | 当前值 |
| --- | --- |
| `Id` | `Conoscope` |
| `name` | `Conoscope` |
| `version` | `1.4.6.1` |
| `dllpath` | `Conoscope.dll` |
| `requires` | 当前 manifest 未声明 |

## 先查什么

| 现象 | 第一检查点 |
| --- | --- |
| Tool 菜单没有 `VAM` | 插件目录、`manifest.json`、`dllpath`、`Conoscope.dll` |
| 窗口打开但没有图像 | 样例路径、CVCIE 格式、`ConoscopeView` 图像载入链 |
| 关注点数值异常 | 当前活动 View、显示通道、关注点半径、参考坐标和采样位置 |
| 色域/对比度结果为空 | R/G/B 或白/黑快照是否完整，是否选择标准色域 |
| 首页快捷区不影响图像 | 活动标签页同步、当前 View 绑定、Ribbon 状态刷新 |
| 预处理没有变化 | `ConoscopePreprocessPipeline` 是否启用，参数是否写入并刷新视图 |
| MVS 画面为空 | `MvCameraControl.dll`、MVS 驱动、相机权限、线缆、`MVSViewManager` |
| 导出缺字段 | 导出模型、关注点快照、结果窗口字段是否同步更新 |

## 当前能力

| 能力 | 当前入口 | 说明 |
| --- | --- | --- |
| 主窗口 | `ConoscopeWindow.xaml(.cs)` | Ribbon、活动 View、采集、预处理、分析和导出入口 |
| 图像视图 | `ConoscopeView.xaml(.cs)` | 图像显示、通道切换、关注点圆、参考线/极角圆 |
| 关注点 | `ConoscopeView.FocusPoint.cs`、`ConoscopeImageHost` | 插件本地关注点 overlay 和采样逻辑 |
| 色域/对比度 | `Application/Analysis/` | R/G/B 快照、白/黑快照、结果窗口 |
| 预处理 | `Application/Preprocess/`、`Processing/Preprocess/` | 滤波、伪彩、灰尘修复、阈值、裁剪、XYZ clamp |
| 观察相机 | `MVS/` | Conoscope 内部观察/辅助采集链，不等同 Engine 通用相机 |
| 运行配置 | `Core/ConoscopeConfig.cs`、`Core/ConoscopeManager.cs` | 插件配置和运行时管理 |

## 用户流程

| 流程 | 关键步骤 |
| --- | --- |
| 打开与采集 | Tool 菜单 `VAM` -> `ConoscopeWindow` -> 导入 CVCIE/打开观察相机/选择型号 |
| 当前视图控制 | 活动标签页变化后，同步通道、参考图形、参考半径/角度和导出入口 |
| 色域分析 | 记录 R/G/B 关注点快照 -> 选择标准色域 -> 打开 `ColorGamutResultWindow` |
| 对比度分析 | 记录白场和黑场 -> 打开 `ContrastResultWindow` |
| 结果导出 | 方位、极角或高级导出要包含关注点数据和结果字段 |

## 构建与交付

```powershell
dotnet build Plugins/Conoscope/Conoscope.csproj -c Release -p:Platform=x64
Scripts\package_plugin.bat Conoscope
```

PostBuild 会把主 DLL 和静态元数据复制到：

```text
ColorVision/bin/x64/<Config>/net10.0-windows/Plugins/Conoscope/
```

交付目录至少应包含 `Conoscope.dll`、`manifest.json`、`ReadMe.md`、`Changelog.md`。需要观察相机时，同步记录 MVS/native 依赖是否已验证。

## 验收

| 验收项 | 通过标准 |
| --- | --- |
| 插件装载 | Tool 菜单出现 `VAM`，可打开 `ConoscopeWindow` |
| 图像打开 | `ConoscopeView` 正常渲染，通道切换和参考线/极角圆可见 |
| 当前视图同步 | 多标签切换后 Ribbon、快捷区启用状态和活动 View 一致 |
| 关注点采样 | 添加/拖动关注点圆后数值刷新，overlay 位置稳定 |
| 色域分析 | R/G/B 记录完整后可查看综合色域总览和单关注点结果 |
| 对比度分析 | 白/黑记录完整后可查看白场亮度、黑场亮度和对比度 |
| 预处理 | 滤波、灰尘修复、阈值、裁剪或 XYZ clamp 后视图刷新 |
| MVS 相机 | 有硬件时能枚举、预览并显示光栅 overlay；无硬件时明确标记未验证 |
| 导出 | 文件包含预期列和关注点数据 |

## 边界

- 关注点逻辑是插件本地实现，不等同 Engine POI 模板。
- 色域/对比度结果窗是独立展示，不要把结果控件堆回主窗口。
- 首页快捷区和视图内控件要保持双向同步。
- 修改帮助入口时同步 `Plugins/Conoscope/ReadMe.md` 和 `Changelog.md`。
- 修改分析字段时同步 CSV 导出、结果窗口和批量记录模型。

## 关键文件

| 任务 | 先看 |
| --- | --- |
| 主窗口/Ribbon | `ConoscopeWindow.xaml.cs`、`ConoscopeWindow.Ribbon.cs` |
| 图像与关注点 | `ConoscopeView.xaml.cs`、`ConoscopeView.FocusPoint.cs` |
| 分析 | `Application/Analysis/ConoscopeAnalysisWorkflow.cs` |
| 预处理 | `Application/Preprocess/ConoscopePreprocessPipeline.cs` |
| 配置 | `Core/ConoscopeConfig.cs` |
