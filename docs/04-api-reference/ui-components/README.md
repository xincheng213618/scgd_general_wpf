---
knowledge_id: "ui.index"
knowledge_type: "index"
status: "current"
summary: "按问题路由到 UI 模块、属性编辑契约、运行时发现与 DLL 发布证据。"
aliases: ["UI模块在哪里","ColorVision.UI","PropertyGrid"]
code_paths: ["UI/Directory.Build.props","UI/ColorVision.UI/ColorVision.UI.csproj","UI/ColorVision.ImageEditor/ColorVision.ImageEditor.csproj"]
related: ["ui.control-catalog","ui.discovery","ui.property-grid","ui.package-boundaries","ui.publishing","ui.solution","ui.documents"]
---

# UI 知识入口

本页按问题指向 UI 契约与源码入口。已有类名、菜单文案或日志时，先在源码中定位。

## 按问题检索

| 问题或修改目标 | 主题 | 关键符号 |
| --- | --- | --- |
| 属性为什么没有编辑器、自定义编辑器如何选择与复用 | [PropertyGrid 契约](./property-grid.md) | `IPropertyEditor.GenProperties`、`PropertyEditorRegistry` |
| 菜单、设置、工具为什么没有出现在宿主中 | [运行时发现](./ui-runtime-handoff.md) | `AssemblyHandler`、`MenuManager`、`ConfigService` |
| 打开文件夹或项目、取消切换、恢复 cvsln | [工作区与资源路由](./ColorVision.Solution.md) | `ResourceOpenService`、`SolutionManager` |
| 默认编辑器、重复标签、保存/关闭和布局恢复 | [编辑器与文档生命周期](./editor-document-lifecycle.md) | `EditorManager`、`EditorDocumentService`、`DockLayoutManager` |
| 找控件、模板、主题和图像工具的位置 | [组件目录](./control-catalog.md) | `UI/` |
| 历史结果或算法 overlay 如何进入画布 | [结果链](../engine-components/result-handoff-chain.md)、[ImageEditor](./ColorVision.ImageEditor.md) | `ResultHandleRegistry`、`AlgorithmOverlayManager` |
| 改动应属于哪个 UI 包 | [包边界](./component-handbook.md) | 各项目的 `ProjectReference` |
| 如何发布 DLL 或 NuGet 包 | [发布约束](./publishing.md) | 目标 `.csproj`、包依赖与运行时文件 |

## 按模块定位

各类库的职责和修改归属统一见[UI 包边界](./component-handbook.md)；按源码目录查主题使用[生成的 UI 知识地图](../../knowledge/code/source-UI.md)。本页的问答表负责问题分流，不另维护一份模块清单。

## 依赖约束

`Common`、`Themes` 等底层库不应反向依赖高层窗口或客户项目。具体依赖以目标 `.csproj` 为准；模块说明不是允许跨层引用的白名单。新问题无法映射到现有主题时，先检索精确符号及调用方，不要从页面缺失推断功能不存在。

## 验证入口与缺口

先核对模块实现及消费方，需要现场验证时查看所属专题。
