# CHANGELOG

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
