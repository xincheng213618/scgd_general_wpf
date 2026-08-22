# Conoscope

版本事实源是 `Conoscope.csproj` 编译出的 DLL `FileVersion`；发布脚本会同步 `manifest.json`。

Conoscope 是 ColorVision 中用于锥光镜图像观察、参考坐标分析、关注点采样和综合色域/对比度计算的插件。当前代码按六个容易定位的职责组织：

- 窗口 Shell 管理标签页、Ribbon、采集和分析槽位。
- 单图 Viewer 通过唯一的 `ConoscopeViewState` 投影参考图形、关注点和显示状态。
- `ConoscopeDocument` 独占 CVCIE 按通道加载、取消以及 X/Y/Z Mat 生命周期。
- Imaging 负责 Mat 预处理、衍生通道、渲染与导出。
- Analysis 负责关注点快照、色域/对比度计算和结果窗口。
- Settings / Integration 负责全局默认值、型号、参考图、宿主和相机边界。

## 当前实现重点

- 删除仅按方法类别拆分的 View/Window partial，主窗口和单文档 View 各保留一个可见的 WPF 组合点。
- `ConoscopeDocument` 独占 X/Y/Z Mat、取消源和加载版本；Y 通道先形成首屏，X/Z 在后台顺序补齐，连续打开只允许最新请求提交。
- 所有显示和导出入口共用通道 readiness：Contrast 只需要 Y 与同尺寸参考 Y，X/Z/CIE/色差需要完整 XYZ；衍生通道失败不会用 Y 静默冒充。
- 保留轻量 `ConoscopeImageHost`，用 `ResetDocument` 明确清理新文档状态，用 `ReplaceDisplayedImage` 在同文档换通道时保留关注点和交互模式。
- 预处理设置直接 TwoWay 绑定配置窗口 working copy；只有应用并保存才提交，避免手工控件镜像和重复刷新。
- POI 数据库事务、分析会话和全局参考通知收回各自所有者，View 不再反向依赖静态窗口刷新。

## 快速开始

1. 从主程序的工具菜单打开 VAM，进入 Conoscope 主窗口。
2. 导入或采集图像后，确认当前标签页是需要操作的活动 View。
3. 在主页使用“当前视图”快捷区切换显示通道、参考模式和参考值。
4. 在图像上添加关注点圆，必要时拖动参考线或参考圆到目标位置。
5. 在分析页记录 R/G/B 或白/黑数据，然后执行色域或对比度计算。
6. 在结果窗口中按“全部关注点”或“单关注点”查看结果，并按需要继续导出原始曲线或图像数据。

## 主窗口说明

### 主页

- 负责打开 CVCIE 图像、切换型号、打开观察相机，并统一放置当前活动 View 的功能入口。
- “当前视图”快捷区会跟随活动标签页自动同步；当没有活动 View 时会保留布局并整体灰显。
- 快捷区支持三类操作：
  - 显示通道切换。
  - 参考图形模式切换（圆 / 直线）。
  - 参考值输入，按当前模式解释为半径或角度。
- “视图功能 / 当前通道导出”支持 3D、CIE、方位导出、极角导出和高级导出。

### 采集

- 管理流程模板和测量相机相关操作。
- VA60/VA80 型号差异仍由当前模型配置决定，避免在视图层分散判断。

### 处理

- 用于执行滤波、伪彩色范围显示和灰尘修复等图像预处理。
- 预处理参数由当前视图和全局配置共同驱动，处理后立即刷新显示。

### 分析

- 色域计算：
  - 从当前活动 View 一次记录全部关注点的 R/G/B 数据。
  - 选择标准色域后直接计算。
  - 结果以独立窗口展示，不再挤占主界面。
- 对比度计算：
  - 从当前活动 View 一次记录全部关注点的白/黑数据。
  - 直接计算关注点级黑白对比度并弹出结果窗口。

### 系统

- 保存当前窗口配置。
- 打开 Conoscope 配置窗口。
- 切换主题与语言。

## 当前视图交互

### 显示通道

- 图像显示仍以当前视图为中心；每个标签页只有一个 `ConoscopeViewState`，主页快捷区直接读写活动 View 的状态。
- 切换标签页时，主页快捷区会自动切换到对应 View 的状态；没有活动 View 时会保留控件位置但禁用交互。
- 方位导出和极角导出直接沿用当前显示通道，不再单独切换导出通道。

### 关注点圆

- Conoscope 当前使用的是插件内的本地关注点逻辑，不依赖 Engine 中带滤除流程的那套关注点计算。
- 每个关注点以圆形绘制在图像上，便于直接对局部区域采样。
- 右键操作可以直接触发当前关注点计算，用于快速确认当前区域数据。
- 色域和对比度记录都会以“当前 View 的全部关注点”为一个批次快照进行保存。

### 参考线与极角圆

- 参考图形支持圆形和直线两种模式。
- 切换后仍可在图像上手动旋转或移动参考图形，便于对准实际画面。
- 如果需要只关注关注点绘制，可以先关闭参考图形，再进行圆形采样。

## 色域计算流程

1. 打开要作为 R 图的视图，并确保关注点位置已经调整好。
2. 点击“记录 R”。
3. 分别切换到 G 图、B 图后点击“记录 G”“记录 B”。
4. 在“标准”下拉框中选择目标色域。
5. 点击“计算色域”打开结果窗口。

结果窗口说明：

- 支持查看全部关注点汇总结果，也支持切换到单个关注点单独查看。
- 内置 CIE 图展示，不需要回到主窗口重复切换。
- 如果按钮已经记录成功，主 Ribbon 会以状态刷新提示当前记录是否完整。

建议：

- R/G/B 三次记录应使用一致的关注点数量和位置。
- 若重新调整了关注点位置，建议重新记录三组数据，避免不同图之间的关注点不对应。

## 对比度计算流程

1. 打开白场图，确认关注点位置后点击“记录白”。
2. 打开黑场图，使用相同关注点位置点击“记录黑”。
3. 点击“计算对比度”打开结果窗口。

结果窗口会按关注点展示：

- 白场亮度。
- 黑场亮度。
- 对比度结果。

与色域流程一样，对比度计算依赖的是“当前活动 View 的全部关注点快照”，而不是单点即时值。

## 结果与导出

- 3D 和 CIE 入口已经收口到主页的“视图功能”分组，计算结果仍独立展示。
- 主窗口主页负责当前活动 View 的 CSV 导出，支持方位模式、极角模式以及高级导出。
- 图像上方工具条只保留 - / + / 圆适，避免把窗口级功能继续堆在 View 内。
- 如果只是查看综合色域或黑白对比度，直接使用分析页记录数据并打开独立结果窗口。

## 构建、测试与发布

在仓库根目录执行：

```powershell
dotnet build .\Plugins\Conoscope\Conoscope.csproj -p:Platform=x64 -nologo
dotnet test .\Test\Conoscope.Tests\Conoscope.Tests.csproj -p:Platform=x64
```

直接构建项目时，产物留在项目 `bin` 输出；从 solution/MSBuild 构建且 `SolutionDir` 有效时，HostCopy target 才会把以下文件镜像到宿主插件目录：

- Conoscope.dll
- manifest.json
- README.md
- CHANGELOG.md

README 和 CHANGELOG 作为插件元数据保留，供源码维护和插件信息页读取。

正式发布 `.cvxp` 使用：

```powershell
.\Scripts\package_plugin.bat Conoscope
```

wrapper 会构建、校验、上传并在上传尝试结束后删除本地 `.cvxp`。构建成功不等于发布成功；必须以脚本退出码、远端版本元数据和可下载包为准。

## 运行依赖

- 目标平台：Windows x64
- 目标框架：net10.0-windows
- 必需本地依赖：CVCommCore.dll、MQTTMessageLib.dll
- UI 依赖：ColorVision.Solution、ColorVision.ImageEditor、ColorVision.Engine
- 可选观察相机依赖：海康 MVS 驱动和 `MvCameraControl.dll`；普通 CVCIE 分析不要求相机硬件

## 维护约定

如果继续调整 Conoscope 的主交互，请一起更新以下文件，避免文档和版本信息失真：

- README.md
- CHANGELOG.md
- manifest.json
- Conoscope.csproj 中的 VersionPrefix

## 相关文件

- 架构文档：Docs/ARCHITECTURE.md
- 主窗口：ConoscopeWindow.xaml / ConoscopeWindow.xaml.cs
- Ribbon 布局：ConoscopeWindow.xaml
- 单文档视图：ConoscopeView.xaml / ConoscopeView.xaml.cs
- 文档数据所有者：ConoscopeDocument.cs
- 轻量图像宿主：ConoscopeImageHost.xaml / ConoscopeImageHost.xaml.cs
- 分析会话：Application/Analysis/ConoscopeAnalysisSession.cs
- POI 模板数据库边界：Application/FocusPoiTemplateRepository.cs
- Ribbon 资源：Presentation/Ribbon/ConoscopeRibbonResources.xaml
- 批量计算模型：Analysis/MeasurementCaptureModels.cs
- 色域结果窗：Analysis/ColorGamutResultWindow.xaml
- 对比度结果窗：Analysis/ContrastResultWindow.xaml
- 测试项目：Test/Conoscope.Tests
