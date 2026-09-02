---
knowledge_id: "engine.file-io"
knowledge_type: "topic"
status: "current"
summary: "CVRAW/CVCIE 读取、内嵌 XYZ 真彩显示与原图回退、手动校正数值校验，以及版本写回和失败边界。"
aliases: ["CVCIE文件为什么打不开", "ColorVision.FileIO", "CVFileUtil", "CVCIEFile", "ReadCIEFileChannel", "ReadCVCIE", "WriteCIEFile", "NDPort", "内嵌XYZ通道", "CVCIE关联源文件", "CVCIE版本写回", "文件写入失败原文件", "CVCIE真彩显示", "三刺激值转sRGB", "CvcieSrgbRenderer", "CvcieDisplayConfig", "CVRawManualCieCalculator", "校正文件异常"]
code_paths: ["Engine/ColorVision.FileIO/CVFileUtil.cs", "Engine/ColorVision.FileIO/CVCIEFile.cs", "Engine/ColorVision.FileIO/ColorVision.FileIO.csproj", "Engine/ColorVision.FileIO/README.md", "Engine/ColorVision.Engine/Media/MediaHelper.cs", "Engine/ColorVision.Engine/Media/CVRawBatchImageLoader.cs", "Engine/ColorVision.Engine/Media/CVRawOpen.cs", "Engine/ColorVision.Engine/Media/CvRawLayerController.cs", "Engine/ColorVision.Engine/Media/CvcieSrgbRenderer.cs", "Engine/ColorVision.Engine/Media/CvcieDisplayConfig.cs", "Engine/ColorVision.Engine/Media/CvcieDisplaySettingProvider.cs", "Engine/ColorVision.Engine/Media/CVRawManualCieCalculator.cs", "Engine/ColorVision.Engine/Media/CVRawManualCieWindow.xaml.cs", "UI/ColorVision.ImageEditor/Settings/ImageViewSettingsWindow.xaml.cs", "Plugins/Conoscope/ConoscopeDocument.cs"]
test_paths: ["Test/ColorVision.UI.Tests/ExportCieTests.cs", "Test/Conoscope.Tests/CvcieChannelReaderTests.cs", "Test/ColorVision.UI.Tests/CvcieSrgbRendererTests.cs", "Test/ColorVision.UI.Tests/CvcieDisplayIntegrationTests.cs", "Test/ColorVision.UI.Tests/CvcieDisplaySettingsTests.cs", "Test/ColorVision.UI.Tests/CVRawManualCieCalculatorTests.cs", "Test/ColorVision.UI.Tests/CvFilePixelSafetyTests.cs", "Test/ColorVision.UI.Tests/CvcieFloatChannelRendererTests.cs"]
related: ["engine.index", "ui.image-editor", "engine.shell-extension", "plugins.conoscope", "delivery.index", "engine.cv-image-export"]
---

# CV 文件读取、通道与写回契约

`Engine/ColorVision.FileIO/` 负责 `CVCIE` 魔数这一组专有二进制图像文件的解析和序列化；`CVRAW`、`CVCIE`、`CVSRC` 共用这一入口。它不是标准图片解码器，也没有通用 JSON/YAML、压缩、批量任务或异步 I/O 框架。核心实现是 `CVFileUtil` 和数据载体 `CVCIEFile`；不要从旧示例推断存在 `CVRawFile.LoadAsync` 或 `FileValidator`。

先确定调用方要的是**当前文件的内嵌数据**、**关联源图**还是**显示用位图**。`Read`、`ReadCVCIE`、`ReadCIEFileChannel` 并不等价；方法返回成功也不统一意味着图像尺寸、版本往返或显示内容已经验证。

## 按问题定位责任

| 问题 | 先查的入口与边界 |
| --- | --- |
| 魔数正确但图像仍打不开 | `IsCIEFile` 只查魔数；继续核对 header、payload 和消费方的尺寸/位深要求 |
| 同一 CVCIE 打开后显示关联 RAW，而不是 XYZ | FileIO 的 `ReadCVCIE` / `OpenLocalCVFile` 仍优先关联源图；Engine 打开器还受全局 `CvcieDisplayConfig` 控制 |
| 从三刺激值显示真彩、设置亮度基准或切回原图 | `CvcieSrgbRenderer`、`CvRawLayerController` 与下文的 Engine 真彩显示契约 |
| 校正文件异常、手动生成的 XYZ 含无效值 | `CVRawManualCieCalculator` 的输入/结果校验；有限但错误的标定仍需业务校准证据 |
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

旧入口 `ReadCVCIEXYZ` 也委托 `ReadCIEFileChannel` 直接读取目标平面；成功后返回 0，并将 Channels 改为 1、类型改为 Raw。文件头失败返回 `-1`，不适用的单通道输入、无效索引或数据读取失败返回 `-2`。不再采用先全读后切片的 `Cols * Rows * Bpp / 8` 整数运算：例如 14208×10640、32 位数据在乘 32 时会超出 int，造成短数组配上大尺寸元数据。`OpenLocalFileChannel` 仍不传播这些状态码，消费方必须验证数据。其枚举虽含 RGB、色度等值，当前分支只实现 SRC 和 CIE XYZ 选择，不能从枚举名推断全部通道转换可用。

[Conoscope](../plugins/standard-plugins/conoscope.md) 使用新的直接通道入口，并额外要求 32 位浮点、至少三通道、Data 恰好是一个平面；Y-first 和其后的 XYZ 就绪属于 Conoscope 文档生命周期，不属于 FileIO。

## 写入不是验证，也不是原子提交

`WriteCIEFile(path, CVCIEFile)` 直接以 `FileMode.Create` / `FileShare.None` 打开目标，已有文件会先被截断。随后异常返回 false **不恢复原文件**；也不自动创建父目录、临时文件或备份。`WriteCVRaw` / `WriteCVCIE` 只是这个 writer 的包装，不增加扩展名、版本或尺寸检查。

两个 writer 都按 GBK 编码源文件名，代码中有 UTF-8 回退；reader 固定 GBK，不能据此承诺编码自动识别。写出 Exp 不足的通道补 0、多余曝光截断；写出 Cols 再 Rows；仅 Version=2 使用 64 位数据长度。`FilePath`、`FileExtType` 不进入二进制格式，NDPort 当前也未写出。

载体重载允许 Data 为空并写零长度，然后返回 true，而全量 reader 会拒绝零长度 payload。参数重载虽先拒绝空 Data，仍不核对尺寸乘积，且创建默认曝光数组等操作不全在 bool 失败捕获内。因此 `true` 只表示该写入流程完成，**不证明可读回、元数据无损或格式完整**。任何新增安全写回契约，都需要单独实现和测试，不能只改调用文案。

## 数据所有权与消费方

`CVCIEFile` 不持有打开的文件流。`Dispose` 仅将自身 Data/Exp 引用置 null 并标记已释放；不会清零数组内容、清掉其它对象持有的引用、强制 GC 或建立后续方法调用门禁。

FileIO 不负责 OpenCV/WPF 显示转换。`MediaHelper.ToMat` 可能借用 Data；进入 `Mat.FromPixelData` 前检查正尺寸、1/3/4 通道、8/16/32/64 位深以及 checked long 计算的精确数据长度，`ToWriteableBitmap` 复用此入口。完整三通道 CIE 必须提供三个平面；直接读取的单平面需先设置 Channels=1，避免 native 按大于实际数组的尺寸访问。`CVRawBatchImageLoader.Load` 会先 Clone，再释放载体与临时 Mat。Conoscope 同样从单通道 Data 建 Mat 后 Clone。修改读取或减少复制时，必须同时核对这些生命周期，不能仅因为用了 `using` 就认为所有下游数据仍独立有效。32/64 位浮点的显示归一化、导出精度和缩略图策略由各自消费方决定；XYZ 真彩使用下面的独立显示转换。

项目是独立纯托管 AnyCPU 库；目标框架与 NuGet 内容以 `ColorVision.FileIO.csproj` 为准，构建平台例外见[构建入口](../../02-developer-guide/README.md)。不要把宿主 x64 规则机械套到此包。

## Engine 真彩显示与原图回退

`CVRawOpen` 和 `CvRawLayerController` 为 Engine 图像打开器提供 **真彩 sRGB（XYZ）** 模式。它读取 CVCIE 文件自身连续存储的 X、Y、Z 三个平面，不跟随 `SrcFileName` 来构造真彩。**FileIO 的 `ReadCVCIE`、`OpenLocalCVFile` 和文件写入语义没有因此改变**；其它直接使用这些 API 的消费方不会自动启用真彩。

全局持久设置位于 **图像设置 → 默认值 → CVCIE 显示**，配置 `CvcieDisplayConfig` 和两个显示枚举归属 `Engine/ColorVision.Engine/Media/`。Engine 的 `CvcieDisplaySettingProvider` 实现 `IImageComponent`，通过 `ImageView.RegisterSettings` 注册全局配置与保存委托；加载 Engine 后即可在默认值页末尾看到此组，不要求先打开 CVCIE。ImageEditor 只提供通用设置宿主与属性编辑器，FileIO 不承载显示偏好或新增 UI 依赖。点击保存或关闭设置窗口时执行配置保存：

| 设置 | 默认值与生效含义 |
| --- | --- |
| `EnableTrueColor`（启用真彩显示） | 默认关闭；开启后新打开的 CVCIE 默认采用 XYZ 真彩 sRGB，关闭后默认原图。本地手动计算后接入的内存 CVCIE 也使用该开关 |
| `BrightnessMode` | `Auto`（自动适配）；整幅图共享一个亮度除数，取所有有效线性 RGB 分量中的最大正值；全黑图保持黑色 |
| `ReferenceWhiteLuminance` | `65535`；仅 `ReferenceWhite` 模式显示此设置，单位与输入 Y 相同，可调整为任意正有限数值；固定该值可保留不同图片之间的相对亮暗，超出显示范围的值会裁剪 |

`65535` 只是未配置参考白时的软件初始值，不是 sRGB 标准规定的参考白，也不说明 XYZ 与 16 位 RAW 具有相同数值尺度；实际固定参考白应根据输入 Y 的单位和比较要求设置。默认亮度模式仍为 `Auto`，不使用此值；已保存的参考白不会被初始值覆盖。

启用开关通过 `DisplayMode` 持久化：`Source` 为关闭，`Srgb` 为开启。配置服务按类名 `CvcieDisplayConfig` 读写，移动命名空间不改变配置键；旧配置无需迁移且不会产生两个互相矛盾的默认设置。

图层下拉框中的 `Composite`、`真彩 sRGB（XYZ）` 和 X/Y/Z 可临时切换当前视图，不回写全局启用开关；再次选择真彩图层时读取当前全局亮度参数。设置修改不会主动重新渲染所有已打开图片。header 声明三通道、每采样 32 位 float 或 64 位 double 时提供真彩图层，实际渲染还需通过完整数据校验；单通道文件提供亮度显示。

`CvcieSrgbRenderer.Render` 使用与 CIE 背景绘制一致的 D65 XYZ→线性 sRGB 矩阵，完成整幅统一亮度缩放后，将各分量裁剪到 0…1，再应用标准 sRGB 分段编码，输出冻结的 8 位 `Bgr24` 位图。不会逐像素归一化、独立拉伸 X/Y/Z，或重新应用曝光、增益和相机白平衡。原始 XYZ 数组不改动，POI 继续读取测量缓冲。`Auto` 只适合观察本幅图的颜色和相对亮暗，不能据此比较不同图的绝对亮度；普通 Y 灰度回退仍按单通道显示路径归一化，不使用真彩的固定参考白参数。

Renderer 要求正尺寸、checked 长度计算和**恰好三个平面**的 Data，拒绝缺失/多余数据、NaN/Infinity、转换溢出及无效亮度参数，并给出可读异常。有限的负 XYZ 或色域外线性 RGB 不直接判为坏校正，显示阶段才裁剪。转换通过 typed span 直接读取输入，`Auto` 两遍扫描、固定白一遍；大于等于 1048576 像素时按 65536 像素分块、最多半数逻辑 CPU 并行。没有降采样、近似 gamma 或额外全幅浮点 RGB 缓冲；支持分块取消，位图输出独立于文件载体。扫描遍数不代表总耗时的固定排序：像素裁剪比例、sRGB 编码、磁盘读取和位图复制都会影响速度。

首开选择真彩时先读取并转换内嵌 XYZ，仅失败才读取 CVRAW/Y，成功不再提前加载和归一化原图。临时图层读取与转换在后台执行，连续选择仅允许最后一次结果回写；同一控制器的重加载串行，防止取消尚未完成的大文件读取时又并行分配另一整幅数据。完整 XYZ 文件读取目前仍在读取完成后才响应取消。控制器只缓存最近的真彩和最近一个 X/Y/Z 灰度显示位图，各不超过 512 MiB；不缓存原始 XYZ 或 RAW。真彩按亮度模式/参考白及文件长度/修改时间匹配，灰度还匹配通道。换图、清空或替换控制器会取消旧选择并释放缓存；缓存不提供源文件的一致性快照。

X/Y/Z 切换只直接读取所选平面。32/64 位单通道由 `MediaHelper.RenderFloatChannel` 在托管代码中校验有限值并按 MinMax 输出冻结 Gray8，大图采用同样的有界分块并行，输入保持不变，不再交给 native 原位 Normalize。单通道文件的 CIE Y 取第 0 平面。文件打开也以递增请求编号和当前路径共同拒绝过期结果，防止 A→B→A 或同路径重载时旧结果覆盖新参数。

Engine 的显示原图加载入口是 `CvRawLayerController.LoadSourceFile`。它依次尝试 header 的关联原图、相对 CVCIE 目录解析的关联原图和同名 `.cvraw`；专有 RAW 要通过基本尺寸/长度校验，普通图像需能解码。没有可用原图时，直接读取内嵌 Y（单通道文件取第 0 平面，其余取第 1 平面），而不是把第一个 X 平面当作灰度源。浮点 Y 回退拒绝非有限值。

真彩加载或临时切换失败时只写日志，优先回退可用 CVRAW/关联原图，再使用有效 Y 灰度；图层选择同步到实际显示内容。两种回退均不可用时，切换操作保留当前图像，文件打开失败记录错误；不会把失败结果伪装为有效黑图，也不会修复、覆盖或重写已有 CVCIE 文件。

### 手动 CIE 校正的校验边界

`CVRawManualCieCalculator` 只处理手动计算路径的 8/16 位三通道 CVRAW，不是所有相机或外部算法生成 CIE 的统一验证器。导入四色校正文件时，要求增益、曝光和 a…i 矩阵字段存在、能解析且为有限数值；导入失败只记录日志、结束当前导入对话框，保留原始 CVRAW，不以默认矩阵继续提交。

计算前检查 RAW 正尺寸、完整且恰好等长的 payload、有限矩阵系数、有限配置曝光/增益。已有的“有限且非正配置值表示使用源文件曝光/增益”规则保留，但实际选用的源值必须是正有限数；配置曝光还必须能表示为正 float。每个输出 XYZ 转为 float 时再次检查，拒绝非有限结果及 32 位浮点溢出。失败不会接入新的 CIE 测量结果；打开器记录日志并恢复原始 CVRAW，回退也失败则保留当前图像。此路径计算的是内存结果，不因此增加 CIE 文件写入。

这些校验只能发现格式、缺失、非有限和数值溢出问题。**全部数值有限但设备不匹配、矩阵系数填错或标定本身失准的 XYZ，无法仅凭标准 XYZ→sRGB 转换可靠识别。** 真彩预览可能仍然偏色；也不能用负数或超出 sRGB 色域作为坏校正的通用判断。校正正确性仍需相应设备、校正文件来源及已知参考测量的验证，不由显示转换自动修复。

普通图像输出的窗口操作、命令行参数、通道命名与覆盖规则见 [CVRAW / CVCIE 图像导出](./cv-image-export.md)。文件解析成功不代表导出得到所需的完整通道集合。

## 验证入口与明确缺口

- `Test/Conoscope.Tests/CvcieChannelReaderTests.cs`：合成 v1/v2 文件、只取指定平面但保留 header 元数据、越界索引返回 false。fixture 填入未创建的关联文件名仍可读目标平面，但没有验证存在可读源图时是否访问它；“不跟随”的完整结论来自源码。可选真实样本仅在设置 `CONOSCOPE_REAL_SAMPLE` 时读取，未设置时直接返回，不证明真实样本通过。
- `Test/ColorVision.UI.Tests/ExportCieTests.cs`：消费方的 RAW 位深、选定浮点通道、关联源图和导出策略；不是 FileIO 全版本解析认证。
- `Test/ColorVision.UI.Tests/CvcieSrgbRendererTests.cs`：D65 白/黑/sRGB 基色、分段编码、整图公共缩放、固定参考白、曝光/增益无关、负值显示裁剪，以及长度、尺寸、非有限值和溢出的拒绝；32/64 位两模式的并行输出与原串行实现逐字节对照及取消。
- `Test/ColorVision.UI.Tests/CvcieDisplayIntegrationTests.cs`：合成 CVCIE 与关联 CVRAW，经真实图像打开/图层切换入口检查默认模式、有效 XYZ 与源图分离、无 RAW 的 Y 回退、无效 XYZ 回退、X/Y/Z、快速连选及换图后的旧结果抑制、参考白变更、配置序列化和完成事件状态；不是实际屏幕色彩还原或真实校正文件的验收。
- `Test/ColorVision.UI.Tests/CvcieDisplaySettingsTests.cs`：未打开 CVCIE 时的设置注册、跨视图共享全局配置、默认值页合并和保存委托；不替代实际窗口及显示器验收。
- `Test/ColorVision.UI.Tests/CvFilePixelSafetyTests.cs`：大尺寸元数据配短数组在 native 调用前拒绝、合法 payload、旧 XYZ 入口的 v1/v2 切片及截断文件返回码。
- `Test/ColorVision.UI.Tests/CvcieFloatChannelRendererTests.cs`：32/64 位灰度范围、常量黑图、输入不变、有限极值、非法数据与取消。
- `Test/ColorVision.UI.Tests/CVRawManualCieCalculatorTests.cs`：8/16 位 BGR 输入到连续 XYZ、负矩阵系数保留、曝光/增益回退兼容、输入长度和维度校验，以及校正导入/计算的非有限数和输出溢出拒绝。
- 当前关联测试没有证明 v3 writer/reader 往返、恶意长度、所有大小写入口、源文件替换一致性、取消时序或写失败后原文件恢复。上面的实现缺口尚未修复；文档对齐不代表这些测试已运行或问题已消失。

修改 FileIO 时先用 `knowledge.mjs impact "Engine/ColorVision.FileIO/CVFileUtil.cs"` 找消费方主题，按变更补合成文件回归。只读理解协议不需要启动主程序、加载真实测量样本或执行 Explorer 注册脚本。
