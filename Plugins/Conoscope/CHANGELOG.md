# CHANGELOG

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
