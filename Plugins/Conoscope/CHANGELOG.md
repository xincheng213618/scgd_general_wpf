# CHANGELOG

## [1.4.6.1] 2026.05.26
- 架构治理第一阶段：新增 CONO-00 ~ CONO-60 编号化架构分层文档（Docs/ARCHITECTURE.md）。
- 抽出 ConoscopeAnalysisWorkflow 应用服务，将色域/对比度分析的状态管理和计算编排从 ConoscopeWindow.AnalysisRibbon.cs 迁移至 Application 层。
- ConoscopeAnalysisWorkflow 返回纯结果对象（AnalysisWorkflowResult<T>），不依赖 WPF 控件，可独立测试。
- 新增 Test/Conoscope.Tests 测试项目，覆盖 workflow 状态判断、数据完整性校验和计算编排。
- ConoscopeWindow.AnalysisRibbon.cs 职责缩减为：按钮状态管理 + UI 反馈 + 结果窗口创建。
- 架构治理第二阶段：减法重构。
  - 抽出 FocusPointMeasurementService（Application/Analysis/），从 FocusPoint.cs 提取 ROI 圆形均值计算、坐标转换、测量构建等纯计算逻辑。
  - ConoscopeView.FocusPoint.cs 减少 125 行（1152→1027）。
  - ConoscopeView.Export.cs 精简：内联薄委托方法、合并重复的截面导出流程。
  - ConoscopeView.ReferenceAxis.cs 中 GetFullAzimuthAngle/GetPolarRadiusAngle 统一委托至 FocusPointMeasurementService，消除重复。
  - 新增 FocusPointMeasurementServiceTests / ConoscopeViewExportRulesTests 等测试用例，并补充版本一致性与 Application 层约束测试。
  - 测试发现修复：xUnit v3 需启用 `TestingPlatformDotnetTestSupport` 才能被 `dotnet test` 发现（MTP 模式）。
  - 架构治理第三/四阶段：内联 ConoscopeExportContextFactory 至 ConoscopeView.Export.cs，合并 ConoscopeAnalysisWorkflow 5 个 Record 方法为 RecordCapture，清理薄封装。

## [1.4.2.22] 2026.05.14
- 新增 Conoscope 帮助窗口，并在 Help 菜单和窗口页提供帮助入口，直接读取 README.md 与 CHANGELOG.md。
- 重写 README.md 为当前版本的用户帮助文档，覆盖主页快捷控制、关注点圆、参考图形、色域/对比度流程与旧窗口入口。
- 主分析流程统一为“当前活动 View 记录关注点批次数据 + 独立结果窗口展示”，色域与对比度结果从主界面拆分显示。
- 同步修正插件版本元数据，统一 Conoscope.csproj、manifest.json 与文档中的版本号。

## [1.4.2.2] 2026.04.28
- 代码重构：提取Conoscope/子目录，新增Layout/和Menus/子目录
- 新增ConoscopeModuleService模块服务，支持工作区标签页集成
- 新增DockLayoutManager布局管理器（AvalonDock布局持久化）
- 新增ConoscopeManager管理器（单例模式）
- 重构导出服务ConoscopeExportService，新增CieX/CieY/CieU/CieV通道支持
- 新增ConoscopeConfigWindow配置编辑窗口
- 新增布局菜单项（保存/应用/重置布局、面板切换）
- 菜单系统重构：ConoscopeMenuIBase基类统一管理锥光镜窗口菜单
- ExportChannel枚举扩展：新增CieX, CieY, CieU, CieV
- 项目引用更新：ColorVision.Solution替换ColorVision.UI

## [1.1.1.0] 2025.12.10
- 初始版本，基础锥光镜功能和模块
