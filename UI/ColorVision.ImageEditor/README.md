# ColorVision.ImageEditor

> 版本: 1.5.5.1 | 目标框架: .NET 10.0 Windows

## 功能定位

专业图像编辑和标注控件库，支持 RGB48 高位深图像、矢量绘图、视频播放、3D 可视化和 CIE 色彩空间分析。

## 主要功能

### 图像显示
- 多格式支持（PNG/JPG/BMP/TIFF/RAW）
- RGB48 高位深图像
- 缩放/平移/全屏/旋转

### 视频播放
- 多格式支持（MP4/AVI/MKV/MOV/WMV/FLV/WebM）
- C++/C# 跨语言解码（OpenCV + FFmpeg）
- 音频同步（WPF MediaPlayer）
- 倍速播放（0.25x-4x）、降采样预览、帧丢弃

### 矢量绘图 (Draw/)
| 工具 | 说明 |
|------|------|
| Rectangle | 矩形绘制 |
| Circle | 圆形/椭圆 |
| Line | 直线/折线 |
| Text | 文本标注 |
| Polygon | 多边形区域 |
| BezierCurve | 贝塞尔曲线 |
| Ruler | 标尺/测量 |
| Special | POI 点/十字线 |

### 编辑工具 (EditorTools/)
- **Zoom** — 缩放工具
- **FullScreen** — 全屏模式
- **Rotate** — 图像旋转
- **Histogram** — 直方图
- **3D** — HelixToolkit 3D 可视化
- **Algorithms/** — 图像算法（SFR、FindLightBeads、亮度/对比度/Gamma）

### 其他
- **Tif/** — TIFF 图像读写
- **WindowCIE** — CIE 色彩空间分析窗口
- **ScottPlot** — 图表绘制

## 依赖关系

- **引用**: ColorVision.Common, ColorVision.Core, ColorVision.Themes, ColorVision.UI, HelixToolkit.Core.Wpf, ScottPlot.WPF, log4net, Newtonsoft.Json
- **被引用**: ColorVision.Solution

## 构建

```bash
dotnet build UI/ColorVision.ImageEditor/ColorVision.ImageEditor.csproj
```
