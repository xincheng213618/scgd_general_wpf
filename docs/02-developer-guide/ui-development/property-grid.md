# PropertyGrid 系统

ColorVision 使用元数据驱动的 PropertyGrid，通过 C# 特性（Attribute）自动生成属性编辑 UI。

本文档待完善。请参考：
- [UI 开发概览](./README.md)
- [属性编辑器用户指南](/01-user-guide/interface/property-editor.md)

## 核心特性标注

使用以下特性标注属性，PropertyGrid 会自动渲染对应控件：

| 特性 | 说明 |
|------|------|
| `[Category]` | 属性分组 |
| `[DisplayName]` | 显示名称 |
| `[Description]` | 属性描述 |
| `[PropertyEditorType]` | 指定编辑器类型 |
| `[PropertyVisibility]` | 控制属性显示/隐藏 |

## 相关资源

- 源代码: `UI/ColorVision.UI/`
- 示例: `Engine/ColorVision.Engine/Templates/`
