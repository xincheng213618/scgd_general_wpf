# TemplateCreate 窗口优化说明

## 改进概述

将 `TemplateCreate` 窗口中的模板选择界面从简单的 RadioButton 文本列表优化为直观的可视化卡片展示，提升用户体验。

## 改进前

之前的实现使用简单的 RadioButton 控件，仅显示模板名称：

```
○ 默认模板
○ Template1
○ Template2
```

**问题**:
- 仅显示文件名，信息量少
- 视觉效果单调
- 难以区分不同模板
- 缺少模板的详细信息

## 改进后

### 1. 视觉卡片设计

每个模板现在显示为一个精美的卡片，包含：

```
┌─────────────────┐
│       📄        │  ← 文档图标
│   默认模板      │  ← 粗体标题
│ 使用系统默认模板 │  ← 描述信息
└─────────────────┘
```

### 2. 卡片元素

**图标** (iconBlock):
- 使用 Segoe MDL2 Assets 字体的文档图标 (U+E8A5)
- 24px 大小
- 主题色 (PrimaryBrush)
- 居中对齐

**标题** (titleBlock):
- 模板名称粗体显示
- 13px 字体大小
- 居中对齐
- 支持文本换行

**描述** (descBlock):
- 默认模板: "使用系统默认模板"
- 文件模板: "创建时间: YYYY-MM-DD"
- 10px 字体大小
- 次要文本颜色
- 居中对齐

### 3. 交互效果

**未选中状态**:
- 1px 边框 (BorderBrush)
- 区域背景色 (RegionBrush)

**选中状态**:
- 2px 主题色边框 (PrimaryBrush)
- 清晰的视觉反馈

### 4. 布局改进

**容器结构**:
```xml
<ScrollViewer MaxHeight="150" VerticalScrollBarVisibility="Auto">
    <WrapPanel x:Name="TemplateStackPanels" Orientation="Horizontal">
        <!-- 模板卡片 -->
    </WrapPanel>
</ScrollViewer>
```

**优点**:
- WrapPanel 自动换行，充分利用空间
- ScrollViewer 支持滚动查看更多模板
- MaxHeight=150 限制高度，避免占用过多空间

## 技术实现

### CreateTemplateCard 方法

```csharp
private RadioButton CreateTemplateCard(string title, string description, bool isChecked)
{
    // 创建 RadioButton 主控件
    // 创建 Border 卡片容器
    // 创建 StackPanel 内容布局
    // 添加图标、标题、描述三个 TextBlock
    // 设置选中/未选中状态的视觉反馈
    // 返回完整的卡片控件
}
```

### 模板遍历逻辑

```csharp
// 默认模板
var defaultTemplateCard = CreateTemplateCard("默认模板", "使用系统默认模板", true);

// 文件模板
foreach (var item in Directory.GetFiles(TemplateFolder))
{
    string fileName = Path.GetFileNameWithoutExtension(item);
    FileInfo fileInfo = new FileInfo(item);
    string fileDescription = $"创建时间: {fileInfo.CreationTime:yyyy-MM-dd}";
    
    var templateCard = CreateTemplateCard(fileName, fileDescription, false);
    // ...
}
```

## 用户体验提升

1. **视觉直观性**: 卡片设计让模板选择更加直观
2. **信息丰富性**: 显示创建时间等元数据
3. **操作反馈**: 选中状态有明显的边框高亮
4. **空间利用**: WrapPanel 自动换行，适应不同窗口大小
5. **可扩展性**: 未来可以轻松添加更多模板信息（如作者、版本等）

## 兼容性

- 向后兼容：不影响现有模板文件的读取和使用
- 样式安全：使用 TryFindResource 安全加载样式，避免缺失样式导致的异常
- 资源引用：使用动态资源引用，支持主题切换

## 未来改进方向

1. 添加模板预览缩略图
2. 显示模板使用次数统计
3. 支持模板标签/分类
4. 添加模板收藏功能
5. 支持模板搜索和过滤
