---
knowledge_id: "ui.image-tools"
knowledge_type: "topic"
status: "current"
summary: "ImageTools内置注册、多图列表中的单张预览、刷新与SQLite缩略图缓存；重选不保证重载，关窗不清缓存，缓存关闭也不等于零数据库访问。"
aliases: ["多图查看", "缩略图缓存", "ColorVision.ImageTools", "ImageToolsModule", "MultiImageViewer", "MultiImageViewerEditor", "MultiImageViewerConfig", "ImageFileInfo", "ThumbnailCacheManager", "ThumbnailCache.db"]
code_paths: ["UI/ColorVision.ImageTools/README.md", "UI/ColorVision.ImageTools/ImageToolsModule.cs", "UI/ColorVision.ImageTools/ColorVision.ImageTools.csproj", "UI/ColorVision.ImageTools/MultiImageViewer/MultiImageViewer.xaml", "UI/ColorVision.ImageTools/MultiImageViewer/MultiImageViewer.xaml.cs", "UI/ColorVision.ImageTools/MultiImageViewer/MultiImageViewerConfig.cs", "UI/ColorVision.ImageTools/MultiImageViewer/ImageFileInfo.cs", "UI/ColorVision.ImageTools/MultiImageViewer/ThumbnailCacheManager.cs", "UI/ColorVision.ImageTools/MultiImageViewer/ThumbnailCacheManager.Maintenance.cs", "UI/ColorVision.ImageTools/MultiImageViewer/ThumbnailCacheEntry.cs", "UI/ColorVision.Common/Interfaces/Assembly/ModuleCatalog.cs", "UI/ColorVision.Common/Interfaces/ThumbnailProviderFactory.cs", "Engine/ColorVision.Engine/Media/CVRawThumbnailProvider.cs", "ColorVision/BuiltInModules.cs"]
test_paths: ["Test/ColorVision.UI.Tests/ModuleCatalogTests.cs", "Test/ColorVision.UI.Tests/ThumbnailCacheMaintenanceTests.cs"]
related: ["ui.index", "ui.solution", "ui.image-editor", "ui.documents", "ui.image-fusion", "ui.storage-maintenance", "plugins.model"]
---

# 多图查看、刷新与缩略图缓存

`UI/ColorVision.ImageTools/` 通过 `ImageToolsModule.Register` 将程序集加入宿主的 `ModuleCatalog`，当前包含多图查看器和[景深融合](./image-fusion.md)两条独立能力。内部仍沿用 `ColorVision.Solution.MultiImageViewer` 等命名空间，不代表实现位于 Solution 项目。

注册仅调用 `AddBuiltIn`，不创建窗口、不读目录或运行融合。应使用宿主相同的目录并在 `Seal()` 前注册；封存后的调用会抛异常，包括同一模块的重复注册。实际入口还需由消费者发现并装配；通用登记、程序集过滤及消费者缓存的完整边界见[模块装载与发现](../../02-developer-guide/plugin-development/overview.md)。

多图查看器是“一个文件列表 + 一个 `ImageView`”，不是同时显示多张大图的比较器。当前列表采用默认单选，处理器只读取 `SelectedItem`；不要把融合窗口的多选文件列表语义套到这里。

## 入口、文件枚举与完成条件

| 入口 | 实际行为 | 生命周期边界 |
| --- | --- | --- |
| 文件夹编辑器 `MultiImageViewerEditor` | 以 `colorvision.folder.multi-image` 注册，调用 `EditorDocumentService.Open`，在工厂中发起文件夹加载 | 向文档服务传入 viewer 的 `Dispose` 回调，但不等待加载完成再返回 |
| 图像工具菜单 `ZoomEditorToolContextMenu` | 当前图片存在且有父目录时，创建独立窗口，预设 `FilePath` 后加载父目录 | 加载在窗口打开后继续，窗口关闭时释放 viewer |
| `LoadFromFolderAsync` / `LoadFromFilesAsync` | 重建列表后选中指定图片或首项 | 列表和初始选择先发布，任务等缓存尺寸查询结束。大图解码和缩略图各自进行 |

目录枚举和文件属性在后台准备。目录只枚举顶层，按扩展名过滤后取 `MaxDisplayCount`，沿用目录返回顺序。显式文件列表按请求时的顺序准备，允许缺失文件入列。当前列表接受 `.jpg/.jpeg/.png/.bmp/.gif/.tiff/.tif/.cvraw/.cvcie`，实际解码由宿主支持的格式决定。

准备新列表时保留当前列表和大图，成功后一次替换并打开初始选择。目录无法读取或显式列表为空时保留当前内容。启用 `ShowThumbnail` 时，缩略图按列表实际创建的行生成，包含视口及上下预取区域。行回收或列表隐藏时取消需求并释放行持有的位图。替换列表、清空或关闭时取消旧请求。已开始的同步解码占用原并发配额直到结束，取消后的结果不会回填列表。

## 选择、刷新和外部文件变更

选中与 `ImageView.Config.FilePath` 相同的项目会复用当前打开请求。刷新会清空该路径，因此仍能打开同一个文件。缩略图完成后保留用户当前选择。查看器没有文件监听器，外部文件变化可手动刷新。大图解码及其完成信号见[图像编辑器打开链](./ColorVision.ImageEditor.md)。

## SQLite 缓存与格式分流

`ThumbnailCacheManager.Instance` 延迟创建 `%APPDATA%/ColorVision/Cache/ThumbnailCache.db` 和 `ThumbnailCache` 表，存 PNG 缩略图、原尺寸、文件大小和修改时间。它不同于 Explorer 的系统缩略图缓存，也不同于 Solution 工作区缓存。

- 缓存命中要求路径、文件修改时间、大小和请求的缩略图尺寸一致。旧库记录保留，缺少请求尺寸的 PNG 在下次使用该文件时按需重建。源文件同时保留相同修改时间和大小时，仍可能命中已有缩略图。
- PNG 无法解码或像素尺寸与记录不符时，从源图重建。数据库不可用时仍可显示新生成的缩略图。生成时检测到源文件变化则放弃当次结果。
- 文件列表和首张图的打开无需等待 SQLite。缓存尺寸在后台补齐，记录的修改时间和文件大小须与源文件匹配。关闭 `EnableThumbnailCache` 或 `ShowThumbnail` 后仍可补齐已有记录的尺寸。
- 新建缩略图时统一通过 `ThumbnailProviderFactory` 查自定义 provider，否则走 WPF 解码。关闭缓存仍沿用相同格式分流。`.cvraw/.cvcie` 的 `CVRawThumbnailProvider` 由 Engine 提供，要求宿主发现该实现。已选择的 provider 生成失败会返回空缩略图。

视口缩略图在后台获取缓存连接、查询 SQLite 和核对文件状态，等待缓存时界面仍可响应。标准 WPF 缩略图生成和缓存 PNG 编解码也在后台执行，跨查看器最多同时生成两张标准缩略图。WPF 读取首帧并按目标尺寸缩小，每边至少一像素，结果冻结后交给界面。自定义 provider 仍从调用方线程进入，由自身决定解码线程。

应用内 CVRAW/CVCIE 缩略图在后台校验并只读映射磁盘像素，映射不可用时从同一文件读取。三通道 CVCIE 校验完整 XYZ 后只使用内嵌 X。Float32 先按全图范围分块转成 8 位，再做面积缩放，保持原有舍入顺序。CVRAW 可复用已缓存的文件快照，浏览缩略图时的缓存未命中直接读盘。

CVRAW/CVCIE 提供者从同一次文件读取返回冻结缩略图和原图尺寸，多个查看器与直接调用共用两个解码名额。排队请求可取消，已开始的读取和 Float32 分块转换在检查点响应取消。同步原生调用退出并释放资源后才归还名额。已有自定义提供者可继续使用分开的尺寸读取和缩略图入口。

## 配置、清理与所有权

`MultiImageViewerConfig` 是共享的 `IConfig` 活对象。默认显示缩略图且启用缓存，`ThumbnailSize=120`（限制 50–300），`MaxDisplayCount=1000`（最小 10）。属性编辑不等于配置落盘，持久化遵循宿主[配置契约](./configuration.md)。改尺寸后，刷新列表会按新尺寸重新取图。`ImageReadDelay` 和 `ListHeight` 当前只有配置声明，读取和布局沿用现有行为。

| 动作 | 实际影响 | 不保证的行为 |
| --- | --- | --- |
| viewer `Clear()` | 取消文件列表和缩略图请求，清列表、大图、当前目录和显式列表引用 | 不删除 SQLite 缓存 |
| viewer `Dispose()` | 清空并释放当前查看器 | 缓存单例由宿主管理 |
| manager `RemoveCache(path)` | 删除该路径的缓存行，并推进维护代次，阻止此前启动的生成任务再写回 | 不清当前界面图像，不阻止此后新发起的生成 |
| manager `ClearCache()` / 配置清缓存命令 | 调用维护快照与清理入口，删除确认缓存并执行 `VACUUM` | 不受当前窗口目录筛选，不删除源图；void 入口不返回清理结果，仅对 `Succeeded=false` 写 Debug |
| manager `Dispose()` | 在 owner 锁内关闭客户端、推进代次并重置单例 | 不删除数据库；下次访问仍可重新初始化 |

当前没有容量上限、LRU 或自动淘汰。`GetCacheSize` 只报告主数据库文件长度，不是进程内位图内存；`GetCacheCount` 的零也可能来自查询失败。关窗、列表清空、客户端释放和持久缓存删除是四种不同状态。

缓存数据库读写经 owner 锁串行，图像解码发生在锁外。维护页调用不初始化单例的静态扫描入口；配置页的清缓存命令则先访问 `ThumbnailCacheManager.Instance`。快照复验、维护代次和空间整理的结果由[存储清理与设置重置](./storage-maintenance.md#缩略图必须经-owner-清理)统一说明。

## 验证入口与缺口

`ModuleCatalogTests.cs` 检查注册基础及内置模块 ID 包含 ImageTools。缩略图维护的隔离测试及其范围见[维护验证入口](./storage-maintenance.md#验证入口与缺口)；这些测试不覆盖实际图像浏览窗口、设备 provider 或跨进程完整工作流。

构建需满足 [native 依赖前提](../../02-developer-guide/engine-development/opencv-integration.md)。以下仅本地构建，可能还原依赖并产生输出/NuGet 包，不启动窗口或发布：

```powershell
dotnet build .\UI\ColorVision.ImageTools\ColorVision.ImageTools.csproj -c Release -p:Platform=x64
```

行为验收应使用隔离账号/缓存和可丢弃图片，分别检查：同路径修改后重选/刷新；不同缩略图尺寸与损坏 PNG 缓存；开关缓存后的自定义格式；加载中清空/关闭；独立窗口与文档标签的释放差异。
