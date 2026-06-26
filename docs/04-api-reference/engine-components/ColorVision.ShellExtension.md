# ColorVision.ShellExtension

`ColorVision.ShellExtension` 只负责 Windows Explorer 里的 `.cvraw` / `.cvcie` 缩略图。它不参与主程序检测、Flow 执行、结果展示或文件格式转换。

## 先查什么

| 现象 | 优先看 |
| --- | --- |
| Explorer 没缩略图 | `.comhost.dll` 是否注册、扩展名 shellex 是否存在、Explorer 是否重启、缓存是否清理 |
| 只有 `.cvraw` 正常 | `.cvcie` 是否绑定到预期 CLSID，`CVCieShellThumbnailProvider` 是否被调用 |
| 两类文件都走 RAW provider | `Register.ps1` 是否仍使用单一 `$handlerClsid` |
| 日志为空 | Explorer 是否加载扩展，`%APPDATA%\ColorVision\Log` 是否可写 |
| header 读取失败 | 文件是否是当前 `ColorVision.FileIO` 支持的 CVRAW/CVCIE，样例是否损坏 |
| native DLL 缺失 | OpenCvSharp runtime 和 `runtimes/win-x64/native` 是否随输出复制，平台是否 x64 |
| Explorer 崩溃 | 先执行 `Unregister.ps1`，清缓存，再用小样例复测 |
| 注册失败 | 是否管理员 PowerShell，comhost 路径是否存在，`regsvr32` 位数是否匹配 x64 |

## 当前能力

| 项 | 当前状态 |
| --- | --- |
| 源码目录 | `Engine/ColorVision.ShellExtension/` |
| 工程文件 | `ColorVision.ShellExtension.csproj` |
| 目标平台 | x64 |
| 关键构建属性 | `EnableComHosting=true`、`EnableDynamicLoading=true`、`AllowUnsafeBlocks=true` |
| 输出重点 | `ColorVision.ShellExtension.comhost.dll`、`ColorVision.ShellExtension.dll`、`ColorVision.FileIO.dll`、OpenCvSharp runtime |
| 支持格式 | `.cvraw`、`.cvcie` |
| 外部宿主 | Windows Explorer |
| 日志 | `%APPDATA%\ColorVision\Log\ShellExtension.log` |

它依赖 [ColorVision.FileIO](./ColorVision.FileIO.md) 读取 ColorVision 自定义文件头和像素数据，再用 OpenCvSharp 生成 `HBITMAP` 交给 Explorer。

## 调用链

Explorer 通过 `IInitializeWithStream` / `IInitializeWithFile` 初始化 provider；`CVThumbnailProviderBase` 读取文件头和像素数据；provider 按格式创建 OpenCV Mat；基类 resize / normalize 后转成 24bpp DIB `HBITMAP`；异常写入 `ShellExtension.log` 并返回 HRESULT。

## 关键文件

| 文件 | 作用 | 维护关注 |
| --- | --- | --- |
| `ColorVision.ShellExtension.csproj` | COM hosting、dynamic loading、x64 平台和依赖声明 | 是否生成 `.comhost.dll`，OpenCvSharp runtime 是否进入输出 |
| `CVThumbnailProviderBase.cs` | Shell 缩略图公共基类，实现 `IShellThumbnailProvider`、`IInitializeWithStream`、`IInitializeWithFile` | Explorer 是否能初始化数据源，异常是否只返回 HRESULT 而不是抛出 |
| `CVRawShellThumbnailProvider.cs` | `.cvraw` provider，CLSID `{7B5E2A3C-8F1D-4E6A-B9C2-1D3E5F7A8B9C}` | RAW/SRC 数据如何解释成 OpenCV Mat |
| `CVCieShellThumbnailProvider.cs` | `.cvcie` provider，CLSID `{8C6F3B4D-9E2A-5F7B-C3D4-2E4F6A8B9C0D}` | 三通道 XYZ CIE 数据当前只取第一通道用于缩略图显示 |
| `Interop/ShellInterfaces.cs` | Windows Shell COM 接口定义 | GUID 和 `PreserveSig` 不要随意改 |
| `ShellLog.cs` | Explorer 进程内日志 | 日志失败不能影响 Explorer |
| `Register.ps1` | 注册 COM server 和文件扩展名缩略图 handler | 必须管理员运行，会改 HKCR/HKLM、重启 Explorer、清缩略图缓存 |
| `Unregister.ps1` | 移除扩展名绑定和 COM server | 必须管理员运行，回退时先执行 |

## 格式边界

| 格式 | 当前处理 |
| --- | --- |
| `.cvraw` | `CVType.Raw` / `CVType.Src` 按直接像素数据建 Mat；非 8-bit normalize 到 0-255 |
| `.cvcie` | `CVType.CIE` 单通道直接显示；三通道 CIE/XYZ 当前只取第一通道 X |

Explorer 缩略图只用于快速辨识文件内容，不能作为 CIE 色彩分析、检测结果或标定结果的验收依据。

## 注册和卸载

构建命令：`dotnet build Engine/ColorVision.ShellExtension/ColorVision.ShellExtension.csproj -c Release -p:Platform=x64`。

注册和卸载都必须使用管理员 PowerShell：`Engine/ColorVision.ShellExtension/Register.ps1`、`Engine/ColorVision.ShellExtension/Unregister.ps1`。

`Register.ps1` 会注册 `ColorVision.ShellExtension.comhost.dll`，写入 `.cvraw` / `.cvcie` 的 thumbnail provider，并尝试加入 Shell Extensions Approved。脚本还会停止并重启 Explorer，删除本机缩略图和图标缓存，现场执行前要先告知用户。

当前 `Register.ps1` 的 `$handlerClsid` 是 `CVRawShellThumbnailProvider` 的 CLSID：`{7B5E2A3C-8F1D-4E6A-B9C2-1D3E5F7A8B9C}`，会把 `.cvraw` 和 `.cvcie` 都绑定到这个 handler。如果 `.cvcie` 要走 `CVCieShellThumbnailProvider`，需要改绑 `{8C6F3B4D-9E2A-5F7B-C3D4-2E4F6A8B9C0D}` 并重新测试两种文件。

## 检查表

| 验收项 | 操作 | 通过标准 |
| --- | --- | --- |
| 构建输出 | Release x64 构建 `ColorVision.ShellExtension.csproj` | `bin/x64/Release/net10.0-windows/` 下存在 `.dll`、`.comhost.dll`、`.deps.json`、`.runtimeconfig.json` |
| 依赖输出 | 检查输出目录和 `runtimes/win-x64/native` | 存在 `ColorVision.FileIO.dll`、OpenCvSharp 相关 DLL 和 native runtime |
| 注册脚本 | 管理员 PowerShell 执行 `Register.ps1` | `regsvr32` 返回成功；Explorer 重启行为已提前告知现场用户 |
| 注册表 | 检查 `.cvraw` / `.cvcie` 的 shellex thumbnail provider | 扩展名绑定到预期 CLSID；如果两类文件共用 `CVRawShellThumbnailProvider`，验收记录里明确说明 |
| 缩略图 | 分别用已知可读 `.cvraw` 和 `.cvcie` 打开 Explorer 目录 | 能生成缩略图；日志能解释当前走哪个 provider |
| 日志 | 检查 `%APPDATA%\ColorVision\Log\ShellExtension.log` | 初始化、读取 header/data、resize 或异常 HRESULT 都有记录，日志失败不影响 Explorer |
| 回退 | 执行 `Unregister.ps1` 后重新打开 Explorer | 扩展名绑定移除，缓存已清理，Explorer 稳定 |

## 不属于它的范围

- 不负责主程序里的图像查看、ROI/POI overlay 或检测结果展示。
- 不负责 Flow、模板、设备服务、MQTT 或项目包输出。
- 不替代 [ColorVision.FileIO](./ColorVision.FileIO.md) 对文件格式的正式说明。
- 不保证缩略图颜色可以作为检测或标定依据。

如果问题发生在主程序里，先回到 [Engine 结果展示链路](./result-handoff-chain.md) 或 [UI ImageEditor 文档](../ui-components/ColorVision.ImageEditor.md)；只有 Explorer 文件夹预览异常时才从本页开始。
