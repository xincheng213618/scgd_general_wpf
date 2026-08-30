---
knowledge_id: "engine.file-io"
knowledge_type: "topic"
status: "current"
summary: "CVRAW/CVCIE 二进制读取、关联源文件与内嵌通道的区别，以及版本写回、长度校验和失败边界。"
aliases: ["CVCIE文件为什么打不开", "ColorVision.FileIO", "CVFileUtil", "CVCIEFile", "ReadCIEFileChannel", "ReadCVCIE", "WriteCIEFile", "NDPort", "内嵌XYZ通道", "CVCIE关联源文件", "CVCIE版本写回", "文件写入失败原文件"]
code_paths: ["Engine/ColorVision.FileIO/CVFileUtil.cs", "Engine/ColorVision.FileIO/CVCIEFile.cs", "Engine/ColorVision.FileIO/ColorVision.FileIO.csproj", "Engine/ColorVision.FileIO/README.md", "Engine/ColorVision.Engine/Media/MediaHelper.cs", "Engine/ColorVision.Engine/Media/CVRawBatchImageLoader.cs", "Plugins/Conoscope/ConoscopeDocument.cs"]
test_paths: ["Test/ColorVision.UI.Tests/ExportCieTests.cs", "Test/Conoscope.Tests/CvcieChannelReaderTests.cs"]
related: ["engine.index", "ui.image-editor", "engine.shell-extension", "plugins.conoscope", "delivery.index"]
---

# CV 文件读取、通道与写回契约

`Engine/ColorVision.FileIO/` 负责 `CVCIE` 魔数这一组专有二进制图像文件的解析和序列化；`CVRAW`、`CVCIE`、`CVSRC` 共用这一入口。它不是标准图片解码器，也没有通用 JSON/YAML、压缩、批量任务或异步 I/O 框架。核心实现是 `CVFileUtil` 和数据载体 `CVCIEFile`；不要从旧示例推断存在 `CVRawFile.LoadAsync` 或 `FileValidator`。

先确定调用方要的是**当前文件的内嵌数据**、**关联源图**还是**显示用位图**。`Read`、`ReadCVCIE`、`ReadCIEFileChannel` 并不等价；方法返回成功也不统一意味着图像尺寸、版本往返或显示内容已经验证。

## 按问题定位责任

| 问题 | 先查的入口与边界 |
| --- | --- |
| 魔数正确但图像仍打不开 | `IsCIEFile` 只查魔数；继续核对 header、payload 和消费方的尺寸/位深要求 |
| 同一 CVCIE 打开后显示关联 RAW，而不是 XYZ | `ReadCVCIE` / `OpenLocalCVFile` 的 `SrcFileName` 优先分支 |
| 只想读内嵌 Y，避免加载全部 XYZ | `ReadCIEFileChannel(path, 1, ...)`，不是通用打开或旧的全量切片接口 |
| 大小写改名后类型变了 | 路径 header 使用大小写敏感的完整路径 `Contains`；不同便捷入口推断方式不一致 |
| 写入返回 true，但再次读取失败 | 写入不验证完整格式；尤其检查 Version=3 的 `NDPort` 缺失和零长 Data |
| 写失败后旧文件丢失或残缺 | 路径 writer 直接 `FileMode.Create`，没有临时文件替换或回滚 |
| Explorer 缩略图异常 | [ShellExtension](./ColorVision.ShellExtension.md)，其 provider 会覆盖解析出的类型 |
| 显示或导出后的像素值变化 | `Engine/.../Media/MediaHelper.cs`、导出入口与[图像编辑器](../ui-components/ColorVision.ImageEditor.md)，不是文件解析本身 |

## 识别与 header 不证明完整有效

`IsCIEFile(path/bytes)` 只检查开头五字节对应 `CVCIE`；五字节文件也可能返回 true。`IsCVCIEFile(path)` 进一步要求 header 解析成功、推断出的 `FileExtType == CIE`，但仍不读取或验证 payload。

`ReadCIEFileHeader` 成功返回**数据长度前缀所在偏移**，失败返回 `-1`；不是完整文件长度，也不是跳过长度前缀后的像素起点。失败时 out 对象仍存在，字段可能只填了一部分，不能按对象非空判断成功。

两种 header 入口的类型与路径语义不同：

- 路径入口用完整 `filePath.Contains(".cvraw")`、然后 `Contains(".cvsrc")` 推断 Raw/Src，否则 CIE。这不是严格后缀验证，也不忽略大小写；路径中的目录名也可能影响结果。`FilePath` 在成功解析时赋值。
- 字节数组入口不设置 `FileExtType` 或 `FilePath`。类型保留枚举默认值 Raw，不是按魔数识别出 CIE；知道格式的调用方需要自行赋值。
- `OpenLocalFileChannel` 的便捷入口先将后缀转小写；`OpenLocalCVFile` 的后缀判断仍大小写敏感。两者再调用 reader，不能假设所有入口有一致的扩展名规则。

## 二进制布局和版本差异

以下是当前托管 reader 的布局。整数/浮点由 `BinaryReader` 或 `BitConverter` 读取，在本项目 Windows 环境为小端；源文件名按 GBK 解码，长度是字节数，不是字符数。

| 顺序 | v1 / v2 | v3 |
| --- | --- | --- |
| 标识与版本 | 5 字节 `CVCIE` + UInt32 Version | 相同 |
| 源文件名 | Int32 字节数 + GBK 字节 | 相同 |
| 附加元数据 | 无 NDPort | Int32 NDPort |
| 增益与通道 | Single Gain + 4 字节 Channels | 相同位置关系；路径 reader 对 Channels 用 Int32 |
| 曝光 | 每通道一个 Single，长度 Channels | 相同 |
| 尺寸与位深 | 4 字节 Cols、Rows、Bpp，**宽在高前** | 相同顺序；路径 reader 用 Int32 |
| 数据长度 | v1 为 Int32；v2 为 Int64 | Int32 |
| payload | 声明长度的原始字节 | 相同 |

Reader 仅接受版本 1、2、3。`Bpp` 在通道计算中是**每通道采样位数**，一个通道字节数为 `Rows * Cols * (Bpp / 8)`。`CVCIEFile.Depth` 将 8/16/32/64 分别映射到 OpenCV 深度 0/2/5/6；未知值回退 0，并非验证器。

**当前 v3 读写不对称是实现缺口，不是兼容保证。** 两个 `WriteCIEFile` writer 都在写源文件名后直接写 Gain，没有写 v3 reader 所需的 NDPort；仅设置 `Version = 3` 不能得到符合该 reader 布局的 v3 文件。Writer 也不拒绝未知版本。需要改格式时先建立版本往返和旧样本回归，不要在文档中承诺所有版本无损兼容。

路径 reader 与字节 reader 的异常长度处理也不完全相同。例如字节入口拒绝负文件名长度；路径入口对不满足可读条件的文件名段可能直接跳过读取后继续解释后续字段。当前不能把 header 解析成功当作恶意/畸形文件的完整安全验证。

## 三种读取语义

| 入口 | 读取内容 | 成功、失败及元数据 |
| --- | --- | --- |
| `Read(path/bytes)` / `ReadCVRaw(path)` | 本文件 header + 全 payload；不跟随关联源图 | bool；全量数据按**声明长度**分配，没有统一验证长度等于尺寸乘积 |
| `ReadCVCIE(path)` / CIE 分支的 `OpenLocalCVFile` | 优先尝试 `SrcFileName` 指向的源文件，失败再读本文件 payload | 成功跟随源图时载体来自源图，不能再认为 Data/尺寸/类型属于内嵌 XYZ |
| `ReadCIEFileChannel(path, index, ...)` | 直接定位本文件第 index 个连续通道平面，不跟随 `SrcFileName` | bool；保留原 header 的 Channels/Exp 等字段，Data 仅含一个通道 |

### 全量 payload

`ReadCIEFileData` 依据 Version 决定 4/8 字节长度前缀；要求声明长度大于 0 且不超出文件剩余长度，再分配整个数组并分段读满。它没有按 Rows/Cols/Bpp/Channels 检查 payload 恰好等长，也允许 payload 后还有字节。OOM/读取异常通常被捕获并返回 false，但 out/ref 载体不统一清空；部分读取后 Data 仍可能存在。

路径 header 和 data 分别以 `FileShare.Read` 打开、关闭文件，**不是同一句柄上的一致性快照**。每次调用结束会释放流，但读取期间不能承诺允许其它进程写入或删除。通用 `ReadFile` 只做一次 `BinaryReader.Read`，未核对实际读取字节数，不能借它宣称所有入口都有读满保证。

### 关联源文件

`ReadCVCIE` 在 header 的 `SrcFileName` 非空时调用 `ReadCVCIESrc`：先按该字符串检查文件是否存在，找不到才拼接 CVCIE 所在目录。没有“只能访问同目录”的限制；绝对路径或当前工作目录可解析的相对路径也可能被使用。

关联文件有 `CVCIE` 魔数时走 `Read`；没有魔数时仅通过 `ReadFile` 装入原始文件字节并标记 Tif，**不在 FileIO 内验证或解码 TIFF**。该分支甚至没有用 Data 非空决定返回值。成功后 `ReadCVCIE` 会采用源图载体，并恢复原 `SrcFileName`、将 `FilePath` 设为传入 CVCIE 路径；路径标签和实际数据来源可能不同。

这些便捷入口不是“不抛异常”的统一封装；例如关联路径组合位于相应 reader 的捕获边界之外。`OpenLocalCVFile` 返回载体而忽略内部 bool，调用方仍须检查有效尺寸和 Data。

### 新的直接通道读取与旧切片

`ReadCIEFileChannel` 使用**零基嵌入索引**，XYZ 的 X/Y/Z 分别是 0/1/2。它检查索引、正尺寸、Bpp 为正且能被 8 整除，使用 checked 长度运算；单通道必须不超过 `int.MaxValue`。声明 payload 至少要容纳 header 声明的全部通道且不得超过文件剩余量，但不要求两者恰好等长。

它只分配目标通道数组，全部读满后才赋给 Data。保留的 `Channels` 仍是原文件通道数，例如 3；不能再用该值把返回 Data 当三通道图像，也不能直接当完整文件写回。取消在 header 读取后、每个读取块前检查；捕获 `OperationCanceledException` 时 Dispose 载体并**重新抛出**，不是返回 false，也不是异步 I/O。header 失败先返回 false，不保证预取消 token 优先于文件错误。

旧的 `ReadCVCIEXYZ` 先读整个 payload 再 `Buffer.BlockCopy`；没有采用直接通道读取。它忽略 data reader 的 bool，成功切片返回 0；失败可返回 `-1`/`-2`，且切片前已经把 Channels 改为 1、类型改为 Raw。`OpenLocalFileChannel` 又不传播这些状态码。其枚举虽含 RGB、色度等值，当前分支只实现 SRC 和 CIE XYZ 选择，不能从枚举名推断全部通道转换可用。

[Conoscope](../plugins/standard-plugins/conoscope.md) 使用新的直接通道入口，并额外要求 32 位浮点、至少三通道、Data 恰好是一个平面；Y-first 和其后的 XYZ 就绪属于 Conoscope 文档生命周期，不属于 FileIO。

## 写入不是验证，也不是原子提交

`WriteCIEFile(path, CVCIEFile)` 直接以 `FileMode.Create` / `FileShare.None` 打开目标，已有文件会先被截断。随后异常返回 false **不恢复原文件**；也不自动创建父目录、临时文件或备份。`WriteCVRaw` / `WriteCVCIE` 只是这个 writer 的包装，不增加扩展名、版本或尺寸检查。

两个 writer 都按 GBK 编码源文件名，代码中有 UTF-8 回退；reader 固定 GBK，不能据此承诺编码自动识别。写出 Exp 不足的通道补 0、多余曝光截断；写出 Cols 再 Rows；仅 Version=2 使用 64 位数据长度。`FilePath`、`FileExtType` 不进入二进制格式，NDPort 当前也未写出。

载体重载允许 Data 为空并写零长度，然后返回 true，而全量 reader 会拒绝零长度 payload。参数重载虽先拒绝空 Data，仍不核对尺寸乘积，且创建默认曝光数组等操作不全在 bool 失败捕获内。因此 `true` 只表示该写入流程完成，**不证明可读回、元数据无损或格式完整**。任何新增安全写回契约，都需要单独实现和测试，不能只改调用文案。

## 数据所有权与消费方

`CVCIEFile` 不持有打开的文件流。`Dispose` 仅将自身 Data/Exp 引用置 null 并标记已释放；不会清零数组内容、清掉其它对象持有的引用、强制 GC 或建立后续方法调用门禁。

FileIO 不负责 OpenCV/WPF 显示转换。`MediaHelper.ToMat` 可能借用 Data；`CVRawBatchImageLoader.Load` 会先 Clone，再释放载体与临时 Mat。Conoscope 同样从单通道 Data 建 Mat 后 Clone。修改读取或减少复制时，必须同时核对这些生命周期，不能仅因为用了 `using` 就认为所有下游数据仍独立有效。32 位浮点的显示归一化、导出精度和缩略图策略由各自消费方决定。

项目是独立纯托管 AnyCPU 库；目标框架与 NuGet 内容以 `ColorVision.FileIO.csproj` 为准，构建平台例外见[构建入口](../../02-developer-guide/README.md)。不要把宿主 x64 规则机械套到此包。

## 验证入口与明确缺口

- `Test/Conoscope.Tests/CvcieChannelReaderTests.cs`：合成 v1/v2 文件、只取指定平面但保留 header 元数据、越界索引返回 false。fixture 填入未创建的关联文件名仍可读目标平面，但没有验证存在可读源图时是否访问它；“不跟随”的完整结论来自源码。可选真实样本仅在设置 `CONOSCOPE_REAL_SAMPLE` 时读取，未设置时直接返回，不证明真实样本通过。
- `Test/ColorVision.UI.Tests/ExportCieTests.cs`：消费方的 RAW 位深、选定浮点通道、关联源图和导出策略；不是 FileIO 全版本解析认证。
- 当前关联测试没有证明 v3 writer/reader 往返、恶意长度、所有大小写入口、源文件替换一致性、取消时序或写失败后原文件恢复。上面的实现缺口尚未修复；文档对齐不代表这些测试已运行或问题已消失。

修改 FileIO 时先用 `knowledge.mjs impact "Engine/ColorVision.FileIO/CVFileUtil.cs"` 找消费方主题，按变更补合成文件回归。只读理解协议不需要启动主程序、加载真实测量样本或执行 Explorer 注册脚本。
