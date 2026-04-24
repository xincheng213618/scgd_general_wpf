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
- **Algorithms/** — 图像算法（SFR、FindLightBeads、亮度/对比度/Gamma）

### 3D 可视化 (EditorTools/ThreeD/)
基于 HelixToolkit.Wpf 的 3D 渲染模块，包含两个独立查看器：

**Window3D — 图像转 3D 表面**
- 2D 图像 → 高度图 3D 曲面（支持 RGB48/Gray8 等多格式自动转换）
- 双线性插值降采样，逐顶点法线计算
- 24 种伪彩色映射（jet/viridis/plasma 等）+ 色条图例
- 实时高度缩放（键盘 +/- 或按钮）
- 3D 拾取悬停提示（`VisualTreeHelper.HitTest` raycast）
- 截图导出（PNG/JPEG/BMP）、模型导出（OBJ/STL）

**ModelViewer3DControl — OBJ/STL 模型查看器**
- 异步后台加载 OBJ/STL 文件，自动预处理无效 mtllib 行
- 自动法线生成、材质可见性检测与回退
- 真实线框模式（`MeshGeometryHelper.FindEdges` + 边圆柱体渲染）
- 正交/透视投影实时切换
- 模型树视图（隔离/显示全部）
- 预设视角（前/后/左/右/上/下/ISO）
- 基于模型包围盒中心的对象旋转（非世界原点）
- 截图/模型导出（OBJ/STL 含材质和纹理）

**Viewport3DHelper — 共享工具类**
- 相机初始化、重置、帧适配
- 固定角坐标轴指示器（PipeVisual3D + SphereVisual3D）
- 统一键盘移动处理（L/T/R/B/A/C/D/F）
- 共享 UI 样式（`ThreeDStyles.xaml` 资源字典）
- 线框几何体生成、截图/导出

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
