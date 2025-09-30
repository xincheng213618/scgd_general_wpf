# ColorVision.ImageEditor

## 🎯 功能定位

专业图像编辑和标注控件库，提供完整的图像显示、编辑、标注和保存功能。

## 作用范围

图像处理UI层，为应用程序提供强大的图像查看和编辑能力。

## 主要功能点

### 图像显示
- **多格式支持** - 支持常见图像格式（PNG、JPG、BMP等）
- **高位深支持** - 支持RGB48等高位深图像格式
- **缩放和平移** - 流畅的图像缩放和平移操作
- **自适应显示** - 自动适配窗口大小的图像显示

### 图像调整
- **对比度调整** - 实时调整图像对比度
- **Gamma校正** - 支持Gamma值调整
- **色调调整** - HSV色调、饱和度、明度调整
- **亮度调整** - 图像亮度实时调节

### 图形绘制
- **文本标注** - 在图像上添加文字说明
- **矩形绘制** - 绘制矩形区域标注
- **圆形绘制** - 绘制圆形区域标注
- **直线绘制** - 绘制直线测量和标注
- **贝塞尔曲线** - 绘制平滑的曲线
- **多边形** - 绘制任意多边形区域

### 编辑功能
- **选择模式** - 选择和移动已绘制的图形
- **编辑模式** - 编辑图形的属性和位置
- **删除功能** - 删除选中的图形元素
- **撤销/重做** - 支持操作的撤销和重做
- **右键菜单** - 便捷的右键快捷操作

### 图像保存
- **保存为图片** - 将编辑结果保存为图像文件
- **保存标注** - 单独保存标注数据
- **导出功能** - 支持多种导出格式

## 与主程序的依赖关系

**被引用方式**:
- ColorVision.Engine - 算法结果可视化展示
- ColorVision - 图像查看和编辑功能
- 插件和项目 - 图像分析和标注

**引用的程序集**:
- ColorVision.UI - 基础UI组件
- ColorVision.Core - C++互操作接口
- OpenCV相关库 - 图像处理算法

## 使用方式

### 引用方式
```xml
<ProjectReference Include="..\ColorVision.ImageEditor\ColorVision.ImageEditor.csproj" />
```

### 基础使用示例
```csharp
// 在XAML中使用ImageEditor控件
<imageEditor:ImageView x:Name="imageView" />

// 在代码中加载图像
imageView.LoadImage("path/to/image.png");

// 设置编辑模式
imageView.EditMode = EditMode.DrawRectangle;

// 保存编辑结果
imageView.SaveImage("path/to/output.png");
```

### 绘制图形示例
```csharp
// 绘制矩形
var rect = new DrawingRectangle
{
    X = 100,
    Y = 100,
    Width = 200,
    Height = 150,
    StrokeColor = Colors.Red
};
imageView.AddDrawing(rect);

// 绘制文本
var text = new DrawingText
{
    X = 150,
    Y = 150,
    Text = "标注文字",
    FontSize = 16,
    Color = Colors.Blue
};
imageView.AddDrawing(text);
```

### 图像调整示例
```csharp
// 调整对比度
imageView.AdjustContrast(1.5);

// 调整Gamma
imageView.AdjustGamma(2.2);

// 调整色调
imageView.AdjustHue(30); // 色调偏移30度
```

## 主要组件

### ImageView
主图像视图控件，提供图像显示和基础操作。

### DrawingVisual系统
基于WPF的DrawingVisual实现的高性能图形渲染系统。

### 图形元素类
- `DrawingRectangle` - 矩形图形
- `DrawingCircle` - 圆形图形
- `DrawingLine` - 直线图形
- `DrawingText` - 文本标注
- `DrawingBezier` - 贝塞尔曲线
- `DrawingPolygon` - 多边形

## 开发调试

```bash
dotnet build UI/ColorVision.ImageEditor/ColorVision.ImageEditor.csproj
```

## 目录说明

- `Controls/` - 图像编辑控件实现
- `Draw/` - 绘图工具和图形元素
- `Adorners/` - WPF装饰器实现
- `Converters/` - 图像转换器

## 性能优化

- **GPU加速** - 利用显卡加速图像渲染
- **多线程处理** - 图像处理操作异步执行
- **内存管理** - 大图像的智能内存管理
- **渲染优化** - 仅重绘变化区域

## 相关文档链接

- [图像处理指南](../../docs/ui-components/ColorVision.ImageEditor.md)
- [算法结果可视化](../../docs/algorithms/README.md)
- [UI组件概览](../../docs/ui-components/UI组件概览.md)

## 维护者

ColorVision UI团队