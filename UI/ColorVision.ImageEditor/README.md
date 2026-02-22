# ColorVision.ImageEditor

> 版本: 1.5.1.1 | 目标框架: .NET 8.0 / .NET 10.0 Windows | UI框架: WPF

## 🎯 功能定位

专业图像编辑和标注控件库，提供完整的图像显示、编辑、标注、视频播放和保存功能。支持RGB48高位深图像、矢量绘图、3D可视化、CIE色彩空间分析和视频流播放。

## 作用范围

图像处理UI层，为应用程序提供强大的图像查看、编辑和视频播放能力，是ColorVision平台的核心可视化组件。

## 主要功能点

### 图像显示
- **多格式支持** - 支持常见图像格式（PNG、JPG、BMP、TIFF等）
- **高位深支持** - 支持RGB48等高位深图像格式
- **缩放和平移** - 流畅的图像缩放和平移操作
- **自适应显示** - 自动适配窗口大小的图像显示

### 视频播放 (新增)
- **多格式支持** - MP4、AVI、MKV、MOV、WMV、FLV、WebM
- **音频同步** - 基于 WPF MediaPlayer 的音视频同步播放
- **播放控制** - 播放/暂停/停止、进度拖拽、倍速播放 (0.25x-4x)
- **缩放预览** - 支持 1x/1/2/1/4/1/8 降采样预览，优化高分辨率视频性能
- **自动隐藏** - 播放时工具栏自动隐藏，鼠标移动时显示
- **帧丢弃** - 高分辨率视频自动帧丢弃保证播放流畅性
- **音画同步** - 自动检测并修正音频漂移

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
- **标尺工具** - 距离测量和比例尺显示

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

## 视频播放架构

```
┌─────────────────────────────────────────────────────────────┐
│                    ColorVision.ImageEditor                    │
│                                                              │
│  ┌─────────────┐    ┌─────────────┐    ┌─────────────┐      │
│  │  VideoOpen  │───▶│  HImage     │───▶│ Writeable   │      │
│  │  (C#)       │    │  (C++ interop)    │ Bitmap      │      │
│  └─────────────┘    └─────────────┘    └─────────────┘      │
│         │                                              │      │
│         ▼                                              ▼      │
│  ┌─────────────┐                                ┌──────────┐ │
│  │ OpenCVMedia │                                │  ImageView│ │
│  │ Helper      │                                │  (WPF)    │ │
│  │ (C++ DLL)   │                                └──────────┘ │
│  └─────────────┘                                             │
│         │                                                    │
│         ▼                                                    │
│  ┌─────────────┐    ┌─────────────┐                          │
│  │   FFmpeg    │    │   WPF       │                          │
│  │   (Decode)  │    │  MediaPlayer│ (Audio)                  │
│  └─────────────┘    └─────────────┘                          │
└─────────────────────────────────────────────────────────────┘
```

### 性能优化策略

1. **最新帧槽模型** - 生产者（C++解码）不等待消费者（UI渲染），始终保留最新帧
2. **pyrDown 降采样** - 使用 OpenCV 图像金字塔进行高效下采样
3. **并行内存拷贝** - 大帧（>1MB）使用 Parallel.For 多线程拷贝
4. **帧丢弃机制** - UI 线程繁忙时自动跳过帧
5. **UI 更新节流** - 进度条和时间显示每 10 帧更新一次

### 分辨率支持

| 分辨率 | 帧率 | 状态 |
|--------|------|------|
| 1080p | 60fps | ✅ 流畅 |
| 4K (3840×2160) | 60fps | ✅ 流畅 |
| 8K (7680×4320) | 60fps | ⚠️ 需降采样至 1/4 或 1/8 |

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

### 视频播放示例
```csharp
// 打开视频文件（自动识别格式）
var videoOpen = new VideoOpen(editorContext);
videoOpen.OpenImage(editorContext, "path/to/video.mp4");

// 视频播放控制通过UI工具栏完成：
// - ▶/⏸ 按钮：播放/暂停
// - ■ 按钮：停止并回到开头
// - 进度条：拖拽或点击跳转
// - 速度选择：0.25x - 4x
// - 缩放选择：1x, 1/2, 1/4, 1/8
// - 🔊/🔇 按钮：静音切换
// - Auto Hide 复选框：自动隐藏工具栏
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

### VideoOpen
视频播放控制器，负责：
- 通过 OpenCVMediaHelper 与 C++ 层交互
- 管理 WPF MediaPlayer 音频播放
- 处理播放控制UI（工具栏）
- 实现帧丢弃和降采样逻辑

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
# 构建项目
dotnet build UI/ColorVision.ImageEditor/ColorVision.ImageEditor.csproj

# 运行测试
dotnet test
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
  - `Algorithms/` - 图像处理算法（亮度、对比度、Gamma等）
- `Video/` - 视频播放功能
  - `VideoOpen.cs` - 视频播放器实现
- `Assets/` - 资源文件
  - `Colormap/` - 伪彩色映射图
  - `Image/` - 图标和图像资源
- `Abstractions/` - 接口定义
- `Tif/` - TIFF图像处理

## 性能优化

### 图像处理
- **GPU加速** - 利用显卡加速图像渲染
- **多线程处理** - 图像处理操作异步执行
- **内存管理** - 大图像的智能内存管理
- **渲染优化** - 仅重绘变化区域

### 视频播放
- **最新帧槽模型** - 生产者永不阻塞，始终推送最新帧
- **pyrDown 链式降采样** - 原地链式下采样，减少内存分配
- **并行内存拷贝** - 大帧使用多线程拷贝（8K帧从~25ms降至~5ms）
- **帧丢弃** - UI繁忙时自动丢弃帧，保证时间线稳定
- **UI更新节流** - 进度条和时间显示每10帧更新一次

## 技术细节

### C++/C# 互操作
视频解码在 C++ 层通过 OpenCV + FFmpeg 完成：
- `M_VideoOpen` - 打开视频文件
- `M_VideoPlay` - 开始播放（带回调函数）
- `M_VideoPause` - 暂停播放
- `M_VideoSeek` - 跳转到指定帧
- `M_VideoSetPlaybackSpeed` - 设置播放速度
- `M_VideoSetResizeScale` - 设置降采样比例

### 音频同步
- 使用 WPF MediaPlayer 独立播放音频
- 定期检测视频时间与音频位置差异
- 漂移超过阈值（0.5秒）时重新同步

## 相关文档链接

- [图像处理指南](../../docs/04-api-reference/ui-components/ColorVision.ImageEditor.md)
- [改进建议文档](./改进建议.md) - 详细的优化方案和最佳实践
- [算法结果可视化](../../docs/04-api-reference/algorithms/README.md)
- [UI组件概览](../../docs/ui-components/UI组件概览.md)

## 维护者

ColorVision UI团队

## 更新日志

### v1.5.1.1 (2025-02)
- ✅ 新增视频播放功能（MP4/AVI/MKV/MOV/WMV/FLV/WebM）
- ✅ 实现 C++/C# 跨语言视频解码架构
- ✅ 添加音频同步播放支持
- ✅ 实现最新帧槽模型（无阻塞生产者）
- ✅ 添加 pyrDown 降采样预览优化
- ✅ 实现帧丢弃机制（高分辨率视频流畅播放）
- ✅ 添加自动隐藏工具栏功能
- ✅ 优化 8K 视频播放性能（并行内存拷贝）
- ✅ 支持 0.25x-4x 倍速播放

### v1.4.1.1
- 基础图像编辑功能
- RGB48 高位深支持
- 矢量绘图工具
- 3D 可视化
