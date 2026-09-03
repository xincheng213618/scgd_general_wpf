# Pattern — 图卡生成工具

Pattern 是 ColorVision 的 Windows x64 / .NET 10 WPF 插件，同时保留独立 `Pattern.exe` 入口。程序集和插件 ID 均为 `Pattern`，版本由 `Pattern.csproj` 的 `VersionPrefix` 管理。

提供纯色、隔行点亮、环形、线对 MTF、九点、点阵、十字网格、十字、棋盘格、噪声和四象限线栅。四象限线栅默认 2×2，可按行列数量或单元格像素宽高排列，并设置线宽、三种颜色及视场。颜色编辑器提供 R/G/B/W/K 快选。

依赖同版本源码构建的 ColorVision UI/ImageEditor 与 OpenCV runtime；`ImageProjector.dll` 是私有项目依赖，不能因宿主共享依赖剔除而遗漏。普通构建不会复制到主程序输出，也不会发布。正式插件包不是完整的独立应用发行包。

在仓库根目录构建：

```powershell
dotnet build .\Plugins\Pattern\Pattern.csproj -c Release -p:Platform=x64
```

从“工具 → 图卡生成工具”打开窗口。设置参数后点击“生成图卡”，再保存图片；参数修改或重置不会自动刷新已生成图片。“保存到默认”写入该类型的默认 JSON，点击“重置”才读取；普通图案切换不会自动载入它。

模板默认在用户文档的 `ColorVision\Pattern`，用户默认值固定在文档 `ColorVision\Pattern\UserDefaults`，不随自定义模板路径移动。导入 ZIP 和“清空模板列表”会删除模板目录内容，默认位置下包括用户默认文件；操作前核对目录并备份。搜索后的模板复制、删除、重命名存在视图索引与原集合不一致的风险，先清除筛选并核对真实文件。

完整行为、开发 HostCopy、独立启动和发布入口见 [图卡生成与图片投影](../../docs/04-api-reference/plugins/standard-plugins/pattern.md)。此链接面向匹配版本的源码仓库，随包阅读时需另行取得该仓库文档。
