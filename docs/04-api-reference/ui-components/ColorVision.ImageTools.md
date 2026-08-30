---
knowledge_id: "ui.image-tools"
knowledge_type: "topic"
status: "current"
summary: "ImageTools内置注册、多图列表中的单张预览、刷新与SQLite缩略图缓存；重选不保证重载，关窗不清缓存，缓存关闭也不等于零数据库访问。"
aliases: ["多图查看", "缩略图缓存", "ColorVision.ImageTools", "ImageToolsModule", "MultiImageViewer", "MultiImageViewerEditor", "MultiImageViewerConfig", "ImageFileInfo", "ThumbnailCacheManager", "ThumbnailCache.db"]
code_paths: ["UI/ColorVision.ImageTools/README.md", "UI/ColorVision.ImageTools/ImageToolsModule.cs", "UI/ColorVision.ImageTools/ColorVision.ImageTools.csproj", "UI/ColorVision.ImageTools/MultiImageViewer/MultiImageViewer.xaml", "UI/ColorVision.ImageTools/MultiImageViewer/MultiImageViewer.xaml.cs", "UI/ColorVision.ImageTools/MultiImageViewer/MultiImageViewerConfig.cs", "UI/ColorVision.ImageTools/MultiImageViewer/ImageFileInfo.cs", "UI/ColorVision.ImageTools/MultiImageViewer/ThumbnailCacheManager.cs", "UI/ColorVision.ImageTools/MultiImageViewer/ThumbnailCacheEntry.cs", "UI/ColorVision.Common/Interfaces/Assembly/ModuleCatalog.cs", "UI/ColorVision.Common/Interfaces/ThumbnailProviderFactory.cs", "Engine/ColorVision.Engine/Media/CVRawThumbnailProvider.cs", "ColorVision/BuiltInModules.cs"]
test_paths: ["Test/ColorVision.UI.Tests/ModuleCatalogTests.cs"]
related: ["ui.index", "ui.solution", "ui.image-editor", "ui.documents", "ui.image-fusion", "plugins.model"]
---

# 多图查看、刷新与缩略图缓存

`UI/ColorVision.ImageTools/` 通过 `ImageToolsModule.Register` 将程序集加入宿主的 `ModuleCatalog`，当前包含多图查看器和[景深融合](./image-fusion.md)两条独立能力。内部仍沿用 `ColorVision.Solution.MultiImageViewer` 等命名空间，不代表实现位于 Solution 项目。

注册仅调用 `AddBuiltIn`，不创建窗口、不读目录或运行融合。应使用宿主相同的目录并在 `Seal()` 前注册；封存后的调用会抛异常，包括同一模块的重复注册。实际入口还需由消费者发现并装配；通用登记、程序集过滤及消费者缓存的完整边界见[模块装载与发现](../../02-developer-guide/plugin-development/overview.md)。

多图查看器是“一个文件列表 + 一个 `ImageView`”，不是同时显示多张大图的比较器。当前列表采用默认单选，处理器只读取 `SelectedItem`；不要把融合窗口的多选文件列表语义套到这里。

## 入口、文件枚举与完成条件

| 入口 | 实际行为 | 生命周期边界 |
| --- | --- | --- |
| 文件夹编辑器 `MultiImageViewerEditor` | 以 `colorvision.folder.multi-image` 注册，调用 `EditorDocumentService.Open`，在工厂中发起文件夹加载 | 向文档服务传入 viewer 的 `Dispose` 回调，但不等待加载完成再返回 |
| 图像工具菜单 `ZoomEditorToolContextMenu` | 当前图片存在且有父目录时，创建独立窗口，预设 `FilePath` 后加载父目录 | 不等待加载任务；当前入口没有把 `Window.Closing` 绑定到 viewer 的 `Dispose` |
| `LoadFromFolderAsync` / `LoadFromFilesAsync` | 从目录或显式列表重建文件列表和缩略图，再选择指定图片或首项 | 完成不等于大图解码已经成功，常规图像打开器具有 `async void` 处理路径 |

目录加载只枚举顶层，按扩展名过滤后取 `MaxDisplayCount`，没有显式排序。显式文件列表保留传入顺序，过滤后同样截断；文件列表入口不逐项要求文件存在。当前列表接受 `.jpg/.jpeg/.png/.bmp/.gif/.tiff/.tif/.cvraw/.cvcie`；融合入口的 `ImageResourceFileTypes` 不是本查看器的格式表。扩展名入列不等于宿主拥有相应解码器。

进入 `LoadFilesAsync` 后先清 `ImageFiles` 和大图，再构造 `ImageFileInfo`；无效目录以及 null/空的 `List<string>` 会在上层入口提前返回，保留旧内容。启用 `ShowThumbnail` 时，按 `max(1, CPU逻辑处理器数/2)` 限制并发，等待列表中存在文件的缩略图任务全部结束后才设置选中项；XAML 列表虚拟化不等于缩略图按可视范围懒加载。列表数量只证明已入列，缩略图缺失和大图失败应分别判断。

## 选择、刷新和外部文件变更

`OpenImage` 有两道按路径去重：与 `ImageView.Config.FilePath` 相同则返回；与 `_currentOpeningFile` 相同也返回。后一个字段在发起打开前设置，但当前没有完成/失败后重置，也不在 `Clear` 或重新加载列表时重置。它不是完整的并发任务锁。

因此，重新点同一路径不保证重新解码；刷新先清空大图，如果随后又选择上次路径，可能被第二道去重挡住而没有重开。这里是源码可达的限制，不是已完成的窗口复现或已修复行为。查看器没有文件监听器；缩略图更新时间、大图内容和当前路径标签不能当作同一版本的原子快照。切换到其它图片和实际解码完成属于[图像编辑器打开链](./ColorVision.ImageEditor.md)。

## SQLite 缓存与格式分流

`ThumbnailCacheManager.Instance` 延迟创建 `%APPDATA%/ColorVision/Cache/ThumbnailCache.db` 和 `ThumbnailCache` 表，存 PNG 缩略图、原尺寸、文件大小和修改时间。它不同于 Explorer 的系统缩略图缓存，也不同于 Solution 工作区缓存。

- `GetOrCreateThumbnailAsync` 按传入路径查记录，以 `LastWriteTime` 相等且 PNG blob 非空判断命中；缩略图尺寸、文件大小和内容哈希不参与命中。改变 `ThumbnailSize` 或替换文件但保留修改时间，不保证生成新的缩略图。
- 缓存 PNG 解码失败时返回 `null`，不会再读原图重建；缓存查询抛异常也直接进入失败返回。新缩略图生成成功后的数据库保存异常会被吞掉并写 Debug 信息，所以显示成功不证明下次可命中缓存。
- `ImageFileInfo.LoadFileInfo` 在文件存在且成功读取基本信息后，无论缓存/缩略图开关都会尝试读取缓存尺寸，不检查缓存的修改时间；即使关闭 `EnableThumbnailCache` 或 `ShowThumbnail`，仍可能创建目录/数据库并查询 SQLite。初始尺寸可能陈旧，不能用于验证源图布局。
- 缓存开启且需新建时，先由 `ThumbnailProviderFactory` 查自定义 provider，否则走 WPF 解码。当前 `.cvraw/.cvcie` 的 `CVRawThumbnailProvider` 在 Engine，要求宿主已加载并发现该实现；单独安装 ImageTools 包不自动带来它。已选择的 provider 生成失败不会再尝试 WPF。
- 缓存关闭时，`ImageFileInfo.LoadThumbnailDirectAsync` 直接走 WPF `BitmapDecoder` 的第一帧，不经过自定义 provider。这不是仅停止磁盘保存的等价开关；尤其不能承诺自定义格式仍具有相同缩略图能力。

标准 WPF 缩略图生成经 Dispatcher 执行，读取期间以 `FileShare.Read` 打开源图，并按目标尺寸等比缩小、不放大小图。`Async` 名称不代表所有解码工作都离开 UI 线程；多页/动画文件的第一帧也不代表完整内容已验证。

## 配置、清理与所有权

`MultiImageViewerConfig` 是共享的 `IConfig` 活对象：默认显示缩略图且启用缓存，`ThumbnailSize=120`（限制 50–300），`MaxDisplayCount=1000`（最小 10）。属性编辑不等于配置落盘，持久化遵循宿主[配置契约](./configuration.md)。`ImageReadDelay` 和 `ListHeight` 当前只有配置声明，没有在本查看器的读取/布局链消费，不能根据属性名承诺防抖或列表高度已经生效。修改显示/大小配置也不自动重新生成已有缓存。

| 动作 | 实际影响 | 不保证的行为 |
| --- | --- | --- |
| viewer `Clear()` | 清列表、大图、当前目录和显式列表引用 | 不删 SQLite，也不取消缩略图任务或重置 `_currentOpeningFile` |
| viewer `Dispose()` | 标记释放并调用 `ImageView.Dispose()` | 不清文件项缩略图引用、不释放缓存单例、不取消在途生成/写入 |
| manager `RemoveCache(path)` | 删除该路径的缓存行 | 不清当前界面图像，也不阻止在途任务随后写回 |
| manager `ClearCache()` / 配置清缓存命令 | 尝试删除全部缓存行并执行 `VACUUM` | 不受当前窗口目录筛选，不删除源图；异常被捕获，无聚合成功结果 |
| manager `Dispose()` | 关闭其客户端并重置单例 | 不删除数据库，不等待其它任务使用的独立客户端；下次访问仍可重新初始化 |

当前没有容量上限、LRU 或自动淘汰。`GetCacheSize` 只报告主数据库文件长度，不是进程内位图内存；`GetCacheCount` 的零也可能来自查询失败。关窗、列表清空、客户端释放和持久缓存删除是四种不同状态。

## 验证入口与缺口

`Test/ColorVision.UI.Tests/ModuleCatalogTests.cs` 检查注册基础及内置模块 ID 包含 ImageTools，不覆盖文件选择/重载、缩略图 SQLite、provider、窗口关闭或缓存释放。当前未登记这些行为的专门自动化测试。

构建需满足 [native 依赖前提](../../02-developer-guide/engine-development/opencv-integration.md)。以下仅本地构建，可能还原依赖并产生输出/NuGet 包，不启动窗口或发布：

```powershell
dotnet build .\UI\ColorVision.ImageTools\ColorVision.ImageTools.csproj -c Release -p:Platform=x64
```

行为验收应使用隔离账号/缓存和可丢弃图片，分别检查：同路径修改后重选/刷新；不同缩略图尺寸与损坏 PNG 缓存；开关缓存后的自定义格式；加载中清空/关闭；独立窗口与文档标签的释放差异。清缓存是整个用户缓存库的写入/删除操作，不是只读诊断，也不要在用户真实缓存上验证文档。
