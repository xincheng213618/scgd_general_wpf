# ColorVision.ImageTools

`UI/ColorVision.ImageTools/` 是主程序内置的图像工具模块，当前负责多图查看、缩略图缓存和文件夹景深融合入口。它通过 `ImageToolsModule` 注册到主程序的 `ModuleCatalog`，不属于 `ColorVision.Solution` 的核心实现。

## 当前入口

| 能力 | 代码入口 | 说明 |
| --- | --- | --- |
| 模块注册 | `ImageToolsModule.cs` | 以 `ColorVision.ImageTools` 注册内置程序集 |
| 多图查看 | `MultiImageViewer/` | 管理图片列表、文件信息、缩略图缓存和窗口配置 |
| 景深融合 | `Fusion/FusionWindow.xaml(.cs)` | 对选定图片集合执行融合操作 |
| 文件夹菜单 | `Fusion/FusionFolderMenuContribution.cs` | 在 Solution 文件夹右键菜单中提供“景深融合” |

景深融合菜单只接受单个现有文件夹，并按文件名顺序读取 BMP、JPEG、PNG 和 TIFF。菜单贡献依赖 Solution 的扩展接口，但窗口和图像处理实现仍归 `ColorVision.ImageTools`。

## 构建与验证

```powershell
dotnet build .\UI\ColorVision.ImageTools\ColorVision.ImageTools.csproj -c Release -p:Platform=x64
```

至少验证内置模块已注册、多图窗口能读取目录图片、缩略图缓存能释放，以及文件夹右键菜单只在适用选择上出现。目标框架、包版本和依赖以 `ColorVision.ImageTools.csproj` 为准。

## 关键文件

| 任务 | 先看 |
| --- | --- |
| 内置发现 | `ImageToolsModule.cs`、`ColorVision/BuiltInModules.cs` |
| 多图查看 | `MultiImageViewer/MultiImageViewer.xaml.cs`、`MultiImageViewer/ThumbnailCacheManager.cs` |
| 融合入口 | `Fusion/FusionWindow.xaml.cs`、`Fusion/FusionFolderMenuContribution.cs` |
| 支持格式 | `ImageResourceFileTypes.cs` |
