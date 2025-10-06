# 接口重组说明 / Interface Reorganization Guide

## 概述 / Overview

本文档记录了 ColorVision.ImageEditor 和 ColorVision.Engine 模块中接口和实现的重组工作。

This document records the reorganization work of interfaces and implementations in ColorVision.ImageEditor and ColorVision.Engine modules.

## 重组原则 / Reorganization Principles

1. **接口与实现分离** - 接口定义放在 Abstractions 文件夹
   - Interfaces separated from implementations - Interface definitions in Abstractions folder

2. **相关接口合并** - 功能相关的接口合并到同一文件
   - Related interfaces merged - Functionally related interfaces combined into single files

3. **保持向后兼容** - 原文件保留并标记为废弃
   - Backward compatibility maintained - Original files kept and marked as deprecated

4. **命名空间不变** - 保持原有命名空间，确保现有代码无需修改
   - Namespace unchanged - Original namespaces preserved to ensure existing code works

## ColorVision.ImageEditor 模块重组

### 核心接口 / Core Interfaces

#### `Abstractions/IImageEditor.cs`
合并了以下接口：
- `IImageComponent` - 图像组件接口
- `IImageOpen` - 图像打开接口

原文件：
- `IImageComponent.cs` (废弃 / deprecated)
- `IImageOpen.cs` (废弃 / deprecated)

### 绘图接口 / Drawing Interfaces

#### `Abstractions/Draw/IDrawing.cs`
合并了以下接口和类：
- `IDrawingVisual` - 绘图可视化接口
- `IDrawingVisualDatum` - 绘图数据接口
- `ISelectVisual` - 选择可视化接口
- `ITextProperties` - 文本属性接口
- `IDVContextMenu` - 上下文菜单接口
- `IDrawingVisualDVContextMenu` - 上下文菜单实现
- `DrawEditorManager` - 绘图编辑器管理器

原文件：
- `Draw/IDrawingVisual.cs` (废弃 / deprecated)
- `Draw/IDrawingVisualDatum.cs` (废弃 / deprecated)
- `Draw/ISelectVisual.cs` (废弃 / deprecated)
- `Draw/ITextProperties.cs` (废弃 / deprecated)
- `Draw/IImageContentMenuProvider.cs` (废弃 / deprecated)

#### `Abstractions/Draw/IShapes.cs`
合并了以下形状接口：
- `ICircle` - 圆形接口
- `IRectangle` - 矩形接口
- `IBezierCurve` - 贝塞尔曲线接口
- `ILine` - 线条接口（修正为接口，原为空类）

原文件：
- `Draw/Circle/ICircle.cs` (废弃 / deprecated)
- `Draw/Rectangle/IRectangle.cs` (废弃 / deprecated)
- `Draw/BezierCurve/IBezierCurve.cs` (废弃 / deprecated)
- `Draw/Line/ILine.cs` (废弃 / deprecated)

### 编辑器工具 / Editor Tools

#### `Abstractions/IEditorTool.cs`
合并了以下接口和类型：
- `ToolBarLocal` - 工具栏位置枚举
- `IEditorTool` - 编辑器工具接口
- `IEditorToggleTool` - 可切换工具接口
- `IEditorToggleToolBase` - 可切换工具基类
- `IIEditorToolContextMenu` - 工具上下文菜单接口
- `ToolBarLocalExtensions` - 工具栏扩展方法

#### `EditorToolFactory.cs`
分离出的工厂实现：
- `IEditorToolFactory` - 编辑器工具工厂类

原文件：
- `IEditorTool.cs` (废弃 / deprecated)

## ColorVision.Engine 模块重组

### 结果处理 / Result Handling

#### `Abstractions/ViewResultAlgType.cs`
独立的枚举定义：
- `ViewResultAlgType` - 算法结果类型枚举（60+ 个值）

#### `Abstractions/IResultHandlers.cs`
合并了以下接口和基类：
- `IViewImageA` - 视图图像接口
- `IResultHandle` - 结果处理接口
- `IResultHandleBase` - 结果处理基类

原文件：
- `Abstractions/IResultHandle.cs` (废弃 / deprecated)

## 文件映射表 / File Mapping Table

### ColorVision.ImageEditor

| 原文件 / Original | 新位置 / New Location | 状态 / Status |
|------------------|---------------------|--------------|
| IImageComponent.cs | Abstractions/IImageEditor.cs | 已废弃 / Deprecated |
| IImageOpen.cs | Abstractions/IImageEditor.cs | 已废弃 / Deprecated |
| Draw/IDrawingVisual.cs | Abstractions/Draw/IDrawing.cs | 已废弃 / Deprecated |
| Draw/IDrawingVisualDatum.cs | Abstractions/Draw/IDrawing.cs | 已废弃 / Deprecated |
| Draw/ISelectVisual.cs | Abstractions/Draw/IDrawing.cs | 已废弃 / Deprecated |
| Draw/ITextProperties.cs | Abstractions/Draw/IDrawing.cs | 已废弃 / Deprecated |
| Draw/IImageContentMenuProvider.cs | Abstractions/Draw/IDrawing.cs | 已废弃 / Deprecated |
| Draw/Circle/ICircle.cs | Abstractions/Draw/IShapes.cs | 已废弃 / Deprecated |
| Draw/Rectangle/IRectangle.cs | Abstractions/Draw/IShapes.cs | 已废弃 / Deprecated |
| Draw/BezierCurve/IBezierCurve.cs | Abstractions/Draw/IShapes.cs | 已废弃 / Deprecated |
| Draw/Line/ILine.cs | Abstractions/Draw/IShapes.cs | 已废弃 / Deprecated |
| IEditorTool.cs | Abstractions/IEditorTool.cs + EditorToolFactory.cs | 已废弃 / Deprecated |

### ColorVision.Engine

| 原文件 / Original | 新位置 / New Location | 状态 / Status |
|------------------|---------------------|--------------|
| Abstractions/IResultHandle.cs | ViewResultAlgType.cs + IResultHandlers.cs | 已废弃 / Deprecated |

## 使用指南 / Usage Guide

### 对于新代码 / For New Code

推荐使用新的接口位置：

```csharp
// 推荐 / Recommended
using ColorVision.ImageEditor;  // 自动包含 Abstractions 中的接口

// 绘图接口
using ColorVision.ImageEditor.Draw;  // 自动包含 Abstractions/Draw 中的接口
```

### 对于现有代码 / For Existing Code

无需修改！所有命名空间保持不变，接口自动从新位置导入。

No changes needed! All namespaces remain the same, interfaces automatically imported from new locations.

### 如果需要删除废弃文件 / If Deprecating Old Files

在确认所有引用都正常工作后，可以删除标记为 "已废弃" 的文件。

After confirming all references work correctly, deprecated files can be removed.

## 重组效果 / Results

### 优点 / Benefits

1. ✅ **更清晰的代码组织** - 接口和实现分离
2. ✅ **更好的可维护性** - 相关功能集中
3. ✅ **减少文件碎片** - 从多个小文件合并为几个有意义的文件
4. ✅ **向后兼容** - 现有代码无需修改
5. ✅ **更易于重构** - 为后续重构打下基础

### 统计 / Statistics

- **ColorVision.ImageEditor**: 11 个分散的接口文件 → 3 个 Abstractions 文件 + 1 个工厂文件
- **ColorVision.Engine**: 1 个大文件 → 2 个专注的文件
- **总计**: 减少了约 8 个需要维护的文件

## 后续工作 / Future Work

1. 可选：在确认稳定后删除废弃文件
2. 更新文档引用新的文件位置
3. 考虑是否需要进一步重组其他模块

---

最后更新 / Last Updated: 2025
