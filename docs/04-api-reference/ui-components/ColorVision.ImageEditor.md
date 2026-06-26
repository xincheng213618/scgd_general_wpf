# ColorVision.ImageEditor

本页是 `UI/ColorVision.ImageEditor/` 的维护速查。用户怎么操作图像编辑器，看 [图像编辑器概览](../../01-user-guide/image-editor/overview.md)；按控件和工具找源码，看 [UI 组件目录](./control-catalog.md)；排查运行时发现链路，看 [UI 运行时组件](./ui-runtime-handoff.md)。

## 模块定位

`ColorVision.ImageEditor` 不是纯图片控件，而是图像宿主、可缩放画布、绘图图元、打开器、工具栏、overlay 和设置系统的组合模块。

它负责“看见和交互”，不负责客户项目的 OK/NG 判定、MES 字段、CSV/PDF 导出业务映射。结果业务链路继续看 [Engine 结果展示链路](../engine-components/result-handoff-chain.md)。

## 主链路

| 阶段 | 关键入口 | 说明 |
| --- | --- | --- |
| 创建视图 | `ImageView.xaml(.cs)` | 图像编辑器主控件，接线画布、工具栏、菜单、状态和文件命令 |
| 建立上下文 | `EditorContext.cs` | 保存当前视图、画布、配置、工具工厂、opener 和局部服务 |
| 装配工具 | `EditorToolFactory.cs` | 反射装配工具、右键菜单、图像组件和打开器 |
| 承载绘制 | `DrawCanvas.cs`、`Draw/` | 显示图像、图元、命中测试、撤销重做和 overlay |
| 打开文件 | `IImageOpen`、`Tif/`、`Video/` | 按扩展名打开普通图、TIF、视频等文件 |
| 运行工具 | `EditorTools/` | 缩放、保存、注释、伪彩、滤镜、直方图、3D、CIE 等工具 |
| 保存配置 | `ImageViewConfig.cs`、`Settings/` | 工具可见性、打开器和显示配置 |

阅读顺序优先是 `ImageView.xaml.cs` -> `EditorContext.cs` -> `EditorToolFactory.cs` -> `DrawCanvas.cs`。这条链比单独追某个工具窗口更能解释运行时行为。

## 常见修改

| 需求 | 优先位置 | 注意 |
| --- | --- | --- |
| 新增图像打开方式 | 实现 `IImageOpen` | 如需临时工具栏，再实现 opener tool provider / lifecycle 接口 |
| 新增工具栏工具 | `EditorTools/`，实现 `IEditorTool` 等工具接口 | 确认可见性配置和 `GuidId` 覆盖行为 |
| 新增右键菜单 | `IDVContextMenu` 或 `IIEditorToolContextMenu` | 构造参数和当前 `EditorContext` 必须满足 |
| 新增图元或 overlay | `Draw/`、`Abstractions/Draw/` | 优先复用已有图元和注释链，不要另造独立 Canvas |
| 新增注释导入导出 | `Draw/Annotations/`、`AnnotationMapper` | 导出后必须能重新导入并保持坐标 |
| 调整伪彩或滤镜 | `EditorTools/PseudoColor/`、`EditorTools/Filters/` | 同步检查 colormap 图片和 shader 资源 |
| 调整 CIE 或 3D | `Cie/`、`EditorTools/ThreeD/` | 先确认资源和输入数据适合可视化 |

## 发布检查

`ColorVision.ImageEditor.csproj` 当前目标为 `net10.0-windows7.0`，平台包含 `AnyCPU;x64`，并生成 NuGet 包。发布时不要只看 DLL 是否存在，还要抽查资源和 native runtime。

| 检查项 | 重点 |
| --- | --- |
| 依赖 | `ColorVision.Common`、`ColorVision.Core`、`ColorVision.Themes`、`ColorVision.UI` 版本一致 |
| OpenCV | `OpenCvSharp4`、`OpenCvSharp4.runtime.win`、`ColorVision.Core` native 依赖完整 |
| shader | `EditorTools/Filters/Shaders/*.ps` 能被打入资源 |
| 伪彩 | `Assets/Colormap/colorscale_*.jpg` 完整 |
| CIE | `Assets/Data/CIE_cc_1931_2deg.csv` 和 CIE 图片资源完整 |
| README | `PackageReadmeFile` 指向的 `README.md` 进入包 |

发布细节继续看 [UI DLL 发布](./publishing.md)。

## 最小烟测

| 烟测项 | 通过标准 |
| --- | --- |
| 普通图像 | PNG/JPG/BMP/TIF 能打开，缩放和平移正常 |
| 图元编辑 | 画矩形、线、文本后撤销/重做可用 |
| 注释 | 导出注释再导入，图元数量和坐标一致 |
| overlay | 打开真实算法结果，ROI/POI/文本层和原图坐标一致 |
| 伪彩/滤镜 | 切换色表或 shader 后显示变化正确 |
| CIE | CIE 窗口背景、谱线和点位显示正常 |
| 3D | 3D 窗口非空，可旋转缩放 |
| 设置 | 隐藏/显示一个工具，重开图像后配置仍生效 |

## 故障分流

| 现象 | 先查 |
| --- | --- |
| 图片区域空白 | opener 是否命中、`SetImageSource(...)` 是否执行、文件是否完整 |
| TIF 或大图失败 | OpenCV runtime、`ColorVision.Core` native DLL、x64 环境 |
| 工具栏缺项 | `EditorToolFactory` 反射扫描、工具可见性配置、`GuidId` |
| ROI/POI 坐标偏移 | `DrawCanvas` 缩放、图像裁剪/旋转、Engine 结果坐标系 |
| 伪彩或滤镜无效果 | shader、colormap 资源、当前图像数据类型 |
| CIE 空白 | CIE CSV 和背景图片资源 |
| 3D 空白 | HelixToolkit、显卡驱动、输入数据是否适合 3D |
| 设置不保存 | `ImageViewConfig`、设置文件权限、重开图像是否重新加载配置 |

## 边界

- `ImageView` 初始化会装配工具、菜单、状态和服务，不是无副作用的轻量图片框。
- 工具发现是反射驱动的，插件或项目包扩展不出现时要先看程序集发现和工具工厂。
- `EditorContext` 是运行时状态容器，也承担局部服务定位角色；排查时不要假设配置、工具和 opener 已完全解耦。
- 客户判定、MES 上传、CSV 字段和项目结果格式不放在 ImageEditor，应该在 Engine handler 或项目包里处理。
