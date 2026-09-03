# ImageProjector — 图片投影工具

ImageProjector 是 ColorVision 的 Windows x64 / .NET 10 WPF 插件，同时保留独立 `ImageProjector.exe` 入口。程序集和插件 ID 均为 `ImageProjector`，版本由 `ImageProjector.csproj` 的 `VersionPrefix` 管理。

支持图片列表、显示器选择、全屏投影、上一张/下一张，以及适应、拉伸、居中、填充四种显示方式；按 Esc 关闭投影窗口。显示器选择和全屏显示直接影响本机屏幕，测试时先确认目标显示器。

依赖同版本源码构建的 ColorVision UI。普通构建不会复制到主程序输出，也不会发布；正式插件包依赖宿主提供共享文件，不是完整的独立应用发行包。

在仓库根目录构建：

```powershell
dotnet build .\Plugins\ImageProjector\ImageProjector.csproj -c Release -p:Platform=x64
```

从“工具 → 图片投影工具”或 Pattern 窗口的投影入口打开。图片列表、选中图片、显示器和显示方式通过 `ConfigService` 保存。

完整行为、开发 HostCopy、独立启动和发布入口见 [图卡生成与图片投影](../../docs/04-api-reference/plugins/standard-plugins/pattern.md)。此链接面向匹配版本的源码仓库，随包阅读时需另行取得该仓库文档。
