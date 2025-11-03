# ColorVision.ImageEditor

> 版本: 1.3.8.5 | 目标框架: .NET 8.0 / .NET 6.0 Windows | UI框架: WPF

## 🎯 功能定位

专业图像编辑和标注控件库，提供完整的图像显示、编辑、标注和保存功能。支持RGB48高位深图像、矢量绘图、3D可视化和CIE色彩空间分析。

## 作用范围

图像处理UI层，为应用程序提供强大的图像查看和编辑能力，是ColorVision平台的核心可视化组件。

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
主图像视图控件，提供图像显示和基础操作。支持缩放、平移、全屏等功能。

### DrawCanvas
基于WPF的DrawingVisual实现的高性能图形渲染系统，支持撤销/重做、图层管理等。

### 图形元素类
- `DrawingRectangle` - 矩形图形绘制
- `DrawingCircle` - 圆形和椭圆图形
- `DrawingLine` - 直线和折线
- `DrawingText` - 文本标注和说明
- `DrawingBezier` - 贝塞尔曲线
- `DrawingPolygon` - 多边形区域
- `DrawingRuler` - 标尺和测量工具
- `DrawingSpecial` - 特殊标注（POI点、十字线等）

### 编辑工具
- `Zoom` - 缩放工具（放大、缩小、适配窗口）
- `FullScreen` - 全屏显示模式
- `Rotate` - 图像旋转功能
- `AppCommand` - 应用程序命令集成

## 开发调试

```bash
dotnet build UI/ColorVision.ImageEditor/ColorVision.ImageEditor.csproj
```

## 目录说明

- `Draw/` - 绘图工具和图形元素核心实现
  - `Rectangle/` - 矩形绘制组件
  - `Circle/` - 圆形绘制组件
  - `Line/` - 线条绘制组件
  - `Text/` - 文本标注组件
  - `Polygon/` - 多边形组件
  - `BezierCurve/` - 贝塞尔曲线组件
  - `Ruler/` - 标尺和测量工具
  - `Special/` - 特殊标注组件
- `EditorTools/` - 编辑工具集
  - `Zoom/` - 缩放工具
  - `FullScreen/` - 全屏模式
  - `Rotate/` - 旋转工具
  - `AppCommand/` - 命令集成
- `Assets/` - 资源文件
  - `Colormaps/` - 伪彩色映射图
  - `Image/` - 图标和图像资源
- `Interfaces/` - 接口定义
- `Tif/` - TIFF图像处理

## 性能优化

- **GPU加速** - 利用显卡加速图像渲染
- **多线程处理** - 图像处理操作异步执行
- **内存管理** - 大图像的智能内存管理
- **渲染优化** - 仅重绘变化区域

## 相关文档链接

- [图像处理指南](../../docs/04-api-reference/ui-components/ColorVision.ImageEditor.md)
- [改进建议文档](./改进建议.md) - 详细的优化方案和最佳实践
- [算法结果可视化](../../docs/04-api-reference/algorithms/README.md)
- [UI组件概览](../../docs/ui-components/UI组件概览.md)

## 维护者

ColorVision UI团队