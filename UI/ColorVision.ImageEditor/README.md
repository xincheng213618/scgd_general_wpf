# ColorVision.ImageEditor

Windows/WPF 图像交互控件库，提供 `ImageView`、绘图与注释、视频播放、算法工具和两类 3D 查看入口。客户判定、MES 字段和业务导出不属于本模块。

## 包与运行前提

- 当前面向 `net10.0-windows7.0` / x64；框架、项目引用、OpenCV runtime、HelixToolkit/SharpDX/Assimp 及资源以 `ColorVision.ImageEditor.csproj` 为准。
- 内建打开器覆盖常见位图与 TIFF；`.cvraw/.cvcie` 由 Engine 扩展提供，不能把普通 `.raw` 当成内建格式。后缀被识别也不保证任意编码均可解码。
- 可载入 RGB48 等高位深位图，不表示截图、3D 高度图或所有工具都保留原始测量值。原生、视频和 3D 能力还需各自的运行依赖。
- 控件创建会装配工具与服务；打开完成、渲染完成、保存完成是不同信号。模型导出和图像保存会写入文件，需确认目标及覆盖范围。

## 源码知识入口

[ImageEditor 权威主题](../../docs/04-api-reference/ui-components/ColorVision.ImageEditor.md)维护打开、绘制、撤销、叠加层、视频、3D 与输出边界，以及对应实现和测试。3D 高度曲面与模型查看器走不同代码路径，不共用一套材质、线框或导出契约。

[上下文、工具装配与临时选区](../../docs/04-api-reference/ui-components/image-editor-context.md)维护状态归属、构造与刷新、ROI 坐标和有效期；[ARCHITECTURE.md](ARCHITECTURE.md)仅保留源码旁入口，不维护第二套架构正文。

本 README 会作为 NuGet 包说明打包到包根目录，`docs/` 与 `ARCHITECTURE.md` 不保证随包存在。上述链接用于源码仓库；包使用者需读取与包版本匹配的源码知识，不能以当前网站或另一分支替代该包的契约。
