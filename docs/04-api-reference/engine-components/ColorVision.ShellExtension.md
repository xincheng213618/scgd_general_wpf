---
knowledge_id: "engine.shell-extension"
knowledge_type: "topic"
status: "current"
summary: "Explorer 的 CVRAW/CVCIE COM provider 如何读取像素、生成非测量用途缩略图，以及源码脚本与 ServiceHost 注册的不同副作用和失败边界。"
aliases: ["文件关联注册","文件关联","FileAssociationHelper","RegisterFileAssociations","资源管理器没有cvraw缩略图","cvcie缩略图颜色不对","缩略图读取会锁文件吗","注册缩略图","卸载缩略图","ColorVision.ShellExtension","CVThumbnailProviderBase","CVRawShellThumbnailProvider","CVCieShellThumbnailProvider","IInitializeWithStream","GetThumbnail","Register.ps1","RegisterThumbnail.ps1"]
code_paths: ["Engine/ColorVision.ShellExtension/CVThumbnailProviderBase.cs","Engine/ColorVision.ShellExtension/CVRawShellThumbnailProvider.cs","Engine/ColorVision.ShellExtension/CVCieShellThumbnailProvider.cs","Engine/ColorVision.ShellExtension/Interop/ShellInterfaces.cs","Engine/ColorVision.ShellExtension/ShellLog.cs","Engine/ColorVision.ShellExtension/ColorVision.ShellExtension.csproj","Engine/ColorVision.ShellExtension/Register.ps1","Engine/ColorVision.ShellExtension/Unregister.ps1","Engine/ColorVision.FileIO/CVFileUtil.cs","ColorVision/Update/Export/FileAssociationHelper.cs","ColorVision/ServiceHost/ServiceHostManagerWindow.xaml.cs","UI/ColorVision.UI/ServiceHost/IColorVisionServiceHostClient.cs","src/ColorVisionServiceHost/ServiceHostCommandHandler.cs","src/ColorVisionServiceHost/Tasks/RegisterThumbnail.ps1","src/ColorVisionServiceHost/Tasks/UnregisterThumbnail.ps1","src/ColorVisionServiceHost/Tasks/RegisterFileAssociations.ps1"]
test_paths: ["Test/ColorVision.UI.Tests/ShellThumbnailProviderTests.cs"]
related: ["engine.index","engine.file-io","platform.service-host","delivery.update"]
---

# Explorer 缩略图读取与 COM 注册

`ColorVision.ShellExtension` 将 `.cvraw` / `.cvcie` 的像素转换成 Explorer 缩略图，不参与主程序图像编辑、Flow、结果 overlay 或文件写回。文件头、像素布局与库 API 由 [ColorVision.FileIO](./ColorVision.FileIO.md) 负责；缩略图是便于辨识的显示结果，不能作为 CIE 色彩、检测或标定结果。

工程继承当前 `net10.0-windows` / x64 目标，启用 `EnableComHosting`、`EnableDynamicLoading` 和 unsafe 代码。部署需要同一输出中的 `.comhost.dll`、托管 DLL、FileIO 及 OpenCvSharp 托管/native 依赖。编译成功只证明制品生成，不证明已注册或 Explorer/COM surrogate 已加载正确版本。

## 输入、读取与资源生命周期

`CVThumbnailProviderBase` 实现 `IInitializeWithStream`、`IInitializeWithFile` 和 `IShellThumbnailProvider`，不是主程序中的图像加载器。

- 流初始化在同一 `IStream` 读取头部及所需像素，按块补齐短读。三通道 CIE 先核验所声明的完整 XYZ 负载长度，再仅读取 X 平面。初始化结束即释放输入 COM 流，后续生成位图只使用已读取的像素。
- 路径初始化仅保存非空路径，返回成功时尚未检查文件存在、格式或像素。`grfMode` 不用于选择实际打开权限。
- `GetThumbnail` 优先使用初始化时读取的像素，路径模式在同一个 FileIO 流读取头部和像素，并在转换前归还流。像素解释由 COM 绑定的 provider 类型决定。磁盘流使用 `FileAccess.Read + FileShare.Read`，Shell 流的共享由其提供方决定。
- 每次 `GetThumbnail` 结束后释放临时 Mat 和 `CVCIEFile`，后续请求需重新初始化。`cx` 只限制输出大小，CIE X 平面和 CVRAW 像素仍按原分辨率读取。

## 像素到 HBITMAP 的显示语义

| provider | 注册身份 | 当前像素解释 |
| --- | --- | --- |
| `CVRawShellThumbnailProvider` | `{7B5E2A3C-8F1D-4E6A-B9C2-1D3E5F7A8B9C}` | 按 `Rows/Cols/Depth/Channels` 直接创建 Mat，未进行 CIE 平面转交错或色彩空间转换 |
| `CVCieShellThumbnailProvider` | `{8C6F3B4D-9E2A-5F7B-C3D4-2E4F6A8B9C0D}` | 三通道时读取内嵌 X 平面并按单通道显示。其它通道数直接创建相应 Mat |

两个 provider 对非 8-bit 数据先做 MinMax 归一化到 0–255 并转为 8-bit；因此缩略图亮度是显示拉伸，不保留原始测量量纲。基类按 `cx` 等比缩小、不放大，使用 Area 插值，宽高至少 1 像素；最终单通道转 BGR、四通道转 BGR 并丢弃 alpha，输出顶向下、行跨度四字节对齐的 24bpp DIB，`pdwAlpha = WTSAT_RGB`。成功的 `HBITMAP` 交给调用方持有，provider 只释放中间对象。

注册入口以 FileIO 自定义头和 provider 的固定类型为准。读取像素前要求正数尺寸、1/3/4 通道、8/16/32/64 位，以及覆盖图像布局的完整负载。声明负载内超出布局的字节会跳过。输出 DIB 的跨度和总长度在原生复制前校验。

## 注册入口与副作用

注册、卸载和缓存清理是有状态维护操作，不是普通文档或构建验证；执行前必须明确授权目标安装目录、提权、受影响进程和缓存删除。不要因 Explorer 显示异常就自动执行卸载或重启。

| 入口 | 目标与绑定 | 副作用及兼容边界 |
| --- | --- | --- |
| `Engine/ColorVision.ShellExtension/Register.ps1` | 要求管理员；优先选源码目录下 Debug x64 `net10.0-windows` 的 comhost，缺失才选 Release；`.cvraw` 和 `.cvcie` **都绑定 RAW CLSID** | `regsvr32` 注册、写 HKCR 和已有 HKLM Approved 项，然后强制停止/重启 Explorer，删除当前用户缩略图及图标缓存 |
| 同目录 `Unregister.ps1` | 要求管理员；移除两种扩展名绑定和 RAW Approved 值；仅查找 Debug comhost，无 Release 回退 | 有 DLL 才调用 `regsvr32 /u`，但不检查退出码；重启 Explorer、清缩略图缓存，不清图标缓存。脚本完成文字不能证明已卸载曾注册的 Release DLL |
| ServiceHost 的 `register-thumbnail` / `unregister-thumbnail` | 使用请求的 `appDirectory`，分别绑定/移除 RAW 与 CIE CLSID；当前 UI 传主程序所在目录及用户 Explorer 缓存目录 | 通过 `RegisterThumbnail.ps1` / `UnregisterThumbnail.ps1` 执行静默 `regsvr32`，只强制结束加载 `ColorVision.ShellExtension.dll` 的 `dllhost.exe`，不重启 Explorer；尝试删除传入目录中的两类缓存 |

`ServiceHostManagerWindow` 提供上述维护动作，`FileAssociationHelper` 封装文件关联服务调用。ServiceHost 将命令映射到自身 `Tasks` 中的固定脚本，并要求 broker ticket；客户端 UI 不需要自行启动源码目录的管理员脚本。调用身份、票据与超时的统一边界见[本机权限代理](../../03-architecture/components/service-host.md)。`RegisterFileAssociations.ps1` 也是写入两种 CLSID 和扩展名绑定的来源，还会改打开命令、图标及其它文件关联，不是仅清理缩略图的等价入口。

这些脚本不是注册事务：后续阶段失败时不补偿已经修改的 COM/关联项，也不保存并恢复原有 handler。ServiceHost 以脚本进程退出码形成结果，`regsvr32` 或定向 `taskkill` 非零会中断，但缓存删除使用忽略错误策略，成功响应不证明缓存已删净或新缩略图已生成。源码脚本还存在上述 Debug/Release 和双后缀共用 RAW 的差异；切换入口前应核对实际绑定及制品路径，不能假设它们相互对称回退。

## 失败语义与定位

`GetThumbnail(cx=0)` 返回 `E_INVALIDARG`；无数据、头部/数据读取失败、Mat 为空或不能创建位图返回 `E_FAIL`，`phbmp` 默认是空指针。常规托管异常会记录后转换为 `E_FAIL`；`S_OK` 仅表示本次位图已生成，不证明 Shell 缓存、注册路径或原图数据正确。

`ShellLog` 独立写入运行该 COM 宿主账号的 `%APPDATA%/ColorVision/Log/ShellExtension.log`，不是主程序 log4net 面板。追加日志失败会被吞掉，空日志不能证明 provider 没有被调用；静态初始化中的目录创建位于日志方法的 `try` 之外，也不能承诺任意日志目录故障都会被安全忽略。代码中的 HRESULT 捕获同样不是对 native 崩溃或所有资源耗尽的隔离保证。

定位顺序是核对当前扩展名 CLSID 和 comhost 路径/位数，再看对应 provider 的初始化、头部、数据及 Mat 日志。尤其 `.cvcie` 出现异常颜色时，先判断是否被源码注册脚本绑定到 RAW provider；在已授权的隔离环境里分别验证已知有效 RAW 和 CIE 样例，不能用任意文件的偶然缩略图证明格式兼容。

## 验证入口与缺口

`ShellThumbnailProviderTests` 直接调用 provider，覆盖 `IStream` 短读、输入流关闭后渲染、CIE X 平面、RAW 交错像素及尾字节跳过，以及残缺负载、异常通道和尺寸溢出的拒绝。注册、Explorer 和 COM surrogate 装载仍需在隔离环境验收。

需独立授权后验证的项目包括：成功/失败后的句柄释放；损坏、超大及异常通道输入；CIE X 平面与 RAW 显示；当前 comhost/native 依赖装载；源码与 ServiceHost 两条注册链；部分失败后实际注册表状态、缓存和 DLL 锁。注册可能修改机器关联、终止进程和删除用户缓存，本页不把执行脚本列为无副作用的“检查命令”。
