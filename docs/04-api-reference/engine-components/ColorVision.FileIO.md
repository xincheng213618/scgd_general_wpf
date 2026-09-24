---
knowledge_id: "engine.file-io"
knowledge_type: "topic"
status: "current"
summary: "CVRAW/CVCIE 读取、动态校正参数覆盖、RAW 按需色度测量、内嵌 XYZ 显示，以及版本写回和失败边界。"
aliases: ["CVFileMetadata", "RAW色度参数", "动态参数覆盖", "HasCieMeasurements", "用户校正", "LumFourColorRecentImages", "CVCIE文件为什么打不开", "ColorVision.FileIO", "CVFileUtil", "CVCIEFile", "ReadCIEFileChannel", "ReadCVCIE", "WriteCIEFile", "NDPort", "内嵌XYZ通道", "CVCIE关联源文件", "CVCIE版本写回", "文件写入失败原文件", "CVCIE真彩显示", "三刺激值转sRGB", "CvcieSrgbRenderer", "CvcieDisplayConfig", "CVRawManualCieCalculator", "LumFourColorCalibrationWorkflowWindow", "四色校正采集", "校正文件异常", "替换当前文件并重启服务", "校正文件备份"]
code_paths: ["Engine/ColorVision.FileIO/CVFileMetadata.cs", "Engine/ColorVision.Engine/Services/Devices/Camera/Local/ColorCalibrationSnapshot.cs", "Engine/ColorVision.Engine/Services/Devices/Camera/Local/LocalFrameCalibrationService.cs", "Engine/ColorVision.Engine/Services/POI/RawColorMeasurementSource.cs", "Native/opencv_helper/algorithm/calibration/calibration_raw_color.cpp", "Engine/ColorVision.FileIO/CVFileUtil.cs", "Engine/ColorVision.FileIO/CVCIEFile.cs", "Engine/ColorVision.FileIO/ColorVision.FileIO.csproj", "Engine/ColorVision.FileIO/README.md", "Engine/ColorVision.Engine/Media/MediaHelper.cs", "Engine/ColorVision.Engine/Media/CVRawBatchImageLoader.cs", "Engine/ColorVision.Engine/Media/CVRawOpen.cs", "Engine/ColorVision.Engine/Media/CvRawLayerController.cs", "Engine/ColorVision.Engine/Media/CvcieSrgbRenderer.cs", "Engine/ColorVision.Engine/Media/CvcieDisplayConfig.cs", "Engine/ColorVision.Engine/Media/CvcieDisplaySettingProvider.cs", "Engine/ColorVision.Engine/Media/CVRawManualCieCalculator.cs", "Engine/ColorVision.Engine/Services/PhyCameras/Calibration/LumFourColorCorrectionCalculator.cs", "Engine/ColorVision.Engine/Services/PhyCameras/Calibration/LumFourColorCorrectionWindow.xaml", "Engine/ColorVision.Engine/Services/PhyCameras/Calibration/LumFourColorCorrectionWindow.xaml.cs", "Engine/ColorVision.Engine/Services/PhyCameras/Calibration/LumFourColorCalibrationWorkflow.cs", "Engine/ColorVision.Engine/Services/PhyCameras/Calibration/LumFourColorDataChecks.cs", "Engine/ColorVision.Engine/Services/PhyCameras/Calibration/LumFourColorCalibrationReplacement.cs", "Engine/ColorVision.Engine/Services/PhyCameras/Calibration/LumFourColorCalibrationFile.cs","Engine/ColorVision.Engine/Services/PhyCameras/Calibration/LumFourColorPoiEditor.cs","Engine/ColorVision.Engine/Services/PhyCameras/Calibration/LumFourColorMeasurementClipboard.cs", "Engine/ColorVision.Engine/Services/PhyCameras/Calibration/LumFourColorSpectrumSelectionWindow.xaml", "Engine/ColorVision.Engine/Services/PhyCameras/Calibration/LumFourColorSpectrumSelectionWindow.xaml.cs", "Engine/ColorVision.Engine/Services/PhyCameras/Calibration/LumFourColorCalibrationWorkflowWindow.xaml","Engine/ColorVision.Engine/Services/PhyCameras/Calibration/LumFourColorRecentImages.cs", "Engine/ColorVision.Engine/Services/PhyCameras/Calibration/LumFourColorCalibrationWorkflowWindow.xaml.cs", "Engine/ColorVision.Engine/Media/CVRawManualCieWindow.xaml.cs", "UI/ColorVision.ImageEditor/Settings/ImageViewSettingsWindow.xaml.cs", "Plugins/Conoscope/ConoscopeDocument.cs"]
test_paths: ["Test/ColorVision.UI.Tests/CVFileMetadataTests.cs", "Test/ColorVision.UI.Tests/RawColorCalibrationTests.cs", "Test/ColorVision.UI.Tests/CalibratedRawDisplayTests.cs", "Test/opencv_helper_test/test_calibration.cpp", "Test/ColorVision.UI.Tests/ExportCieTests.cs", "Test/Conoscope.Tests/CvcieChannelReaderTests.cs", "Test/ColorVision.UI.Tests/CvcieSrgbRendererTests.cs", "Test/ColorVision.UI.Tests/CvcieDisplayIntegrationTests.cs", "Test/ColorVision.UI.Tests/CvcieDisplaySettingsTests.cs", "Test/ColorVision.UI.Tests/CVRawManualCieCalculatorTests.cs", "Test/ColorVision.UI.Tests/CalibrationEditorInteractionTests.cs", "Test/ColorVision.UI.Tests/LumFourColorWorkflowSafetyTests.cs", "Test/ColorVision.UI.Tests/LumFourColorFileCompatibilityTests.cs", "Test/ColorVision.UI.Tests/LumFourColorReplacementTests.cs", "Test/ColorVision.UI.Tests/CvFilePixelSafetyTests.cs", "Test/ColorVision.UI.Tests/CvcieFloatChannelRendererTests.cs"]
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
| RAW 不另存 CVCIE，但要保留当时的色度参数和测量能力 | `CVFileMetadata`、`ColorCalibrationSnapshot`、`RawColorMeasurementSource`；见下文的动态参数契约 |
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

`CVFileReadCache` 默认启用当前进程共用的 **1 个 CVRAW 文件槽位**，缓存完整文件字节（header、像素和参数尾部）。路径写入器把同一批字节同步写入磁盘与槽位，文件关闭成功后才发布路径；写入失败不发布半成品。换路径、通道数或位深只改变文件内容与绑定，容量足够时复用同一 native 指针，超过容量才扩容，释放后下次使用重新分配。参数更新只同步替换尾部，预留的 64 KiB 可避免常规参数变化导致整幅图像重新分配。

命中检查规范化路径（忽略大小写）、文件长度和 UTC 修改时间；每次仍持有 `FileShare.Read` 的真实文件句柄，缺文件不会返回缓存旧图，外部修改长度或时间会失效。同长度且刻意恢复原修改时间的外部写入不属于这项指纹保证。完整读取/按通道读取未命中时可填充槽位；只探测 header 或参数不会为未命中文件加载整幅图。若槽位正被其它读者使用，另一路未命中直接读文件，不创建第二份缓存。返回给消费者的数据仍是独立数组/帧；消费者必须释放读取流，覆盖和释放会等待现有读取结束。

`CVRawOpen` 及 `CvRawLayerController` 仍负责显示，底层通过 `CVFileUtil` 命中该槽位；显示位图沿用尺寸/格式一致且未冻结时的 `WriteableBitmap` 复用。`LocalFrameFileService` 的文件回退共用同一入口，本地校正和实时 POI 仍优先使用已有流程帧。按需 XYZ 测量继续使用原来的只读映射，不新增全幅 XYZ 缓存；CVCIE 文件本身不占 CVRAW 槽位。“本地缓存管理”的两个 Tab 分别查看校正缓存和图像文件缓存，“释放全部”统一释放这两类进程内缓存并保留磁盘文件。此功能保持同步保存，旧服务仍可读取已完成的文件，不提供异步保存完成前的路径承诺。

### 关联源文件

`ReadCVCIE` 在 header 的 `SrcFileName` 非空时调用 `ReadCVCIESrc`：先按该字符串检查文件是否存在，找不到才拼接 CVCIE 所在目录。没有“只能访问同目录”的限制；绝对路径或当前工作目录可解析的相对路径也可能被使用。

关联文件有 `CVCIE` 魔数时走 `Read`；没有魔数时仅通过 `ReadFile` 装入原始文件字节并标记 Tif，**不在 FileIO 内验证或解码 TIFF**。该分支甚至没有用 Data 非空决定返回值。成功后 `ReadCVCIE` 会采用源图载体，并恢复原 `SrcFileName`、将 `FilePath` 设为传入 CVCIE 路径；路径标签和实际数据来源可能不同。

这些便捷入口不是“不抛异常”的统一封装；例如关联路径组合位于相应 reader 的捕获边界之外。`OpenLocalCVFile` 返回载体而忽略内部 bool，调用方仍须检查有效尺寸和 Data。

### 新的直接通道读取与旧切片

`ReadCIEFileChannel` 使用**零基嵌入索引**，XYZ 的 X/Y/Z 分别是 0/1/2。它检查索引、正尺寸、Bpp 为正且能被 8 整除，使用 checked 长度运算；单通道必须不超过 `int.MaxValue`。声明 payload 至少要容纳 header 声明的全部通道且不得超过文件剩余量，但不要求两者恰好等长。

它只分配目标通道数组，全部读满后才赋给 Data。保留的 `Channels` 仍是原文件通道数，例如 3；不能再用该值把返回 Data 当三通道图像，也不能直接当完整文件写回。取消在 header 读取后、每个读取块前检查；捕获 `OperationCanceledException` 时 Dispose 载体并**重新抛出**，不是返回 false，也不是异步 I/O。header 失败先返回 false，不保证预取消 token 优先于文件错误。

旧入口 `ReadCVCIEXYZ` 也委托 `ReadCIEFileChannel` 直接读取目标平面；成功后返回 0，并将 Channels 改为 1、类型改为 Raw。文件头失败返回 `-1`，不适用的单通道输入、无效索引或数据读取失败返回 `-2`。不再采用先全读后切片的 `Cols * Rows * Bpp / 8` 整数运算：例如 14208×10640、32 位数据在乘 32 时会超出 int，造成短数组配上大尺寸元数据。`OpenLocalFileChannel` 仍不传播这些状态码，消费方必须验证数据。其枚举虽含 RGB、色度等值，当前分支只实现 SRC 和 CIE XYZ 选择，不能从枚举名推断全部通道转换可用。

[Conoscope](../plugins/standard-plugins/conoscope.md) 使用新的直接通道入口，并额外要求 32 位浮点、至少三通道、Data 恰好是一个平面；Y-first 和其后的 XYZ 就绪属于 Conoscope 文档生命周期，不属于 FileIO。

## RAW/CVCIE 动态参数与按需色度测量

`CVFileMetadata` 在 header 声明的像素 payload 后保存可选参数区，不修改版本号、像素长度、像素字节或文件扩展名。旧版全量读取和单平面读取仍按原 payload 长度工作。FileIO 只存储 `kind → (version, bytes)`，不依赖 JSON、WPF 或 Native；色度参数的 JSON 结构由 Engine 的 `ColorCalibrationSnapshot` 管理。

参数区由若干 UTF-8 key、记录版本和长度限定的 byte 值组成，末尾是 64 字节 footer：`CVMD0001` 魔数、容器版本、记录数量、像素结束偏移、参数正文长度与正文 SHA-256。正文上限 64 MiB。`SetProperty` 替换同 key 的当前值，保留其他 key 和未知记录版本，然后截断旧尾部；反复校正不积累历史。未知尾部、未知容器版本、校验失败或布局不一致会拒绝覆盖，避免擦除无法理解的数据。

写入规则如下：

- **只在实际色度/亮度校正成功后更新** `colorvision.calibration.color`；普通打开、查看通道、POI、仅基本校正都不写参数。
- 本地校正与手动 RAW 校正保存实际执行的系数、曝光、布局、模板和时间。Native 路径从已加载的校正上下文提取参数，不在计算完成后重新读可能已改变的 `.dat`。
- Flow 本地校正/校正+实时 POI/相机取图的可选加速路径可直接提取参数而不生成完整 CIE；基础校正仍执行。`LocalFrameFileService.Load` 将 CVRAW 内嵌的 JSON 参数交给帧租约，本地 POI 可直接测量 RAW 区域；布局不匹配、缺失参数或 `CanReplay=false` 不允许回放。此流程路径不会采用显示测量源的累计区域阈值来缓存完整 XYZ。
- 对已有对应 CVRAW，无论保存图像开关是否启用都更新当前参数。保存图像时，新 RAW 和 CVCIE 各带同一快照；没有落盘 RAW 的纯内存采集只能先随帧保留参数，不能凭空修改一个不存在的文件。
- 基本校正已经改变 RAW 像素时，新保存的校正后 RAW 可以重放色度矩阵；原始传感器 RAW 仅记录该快照并标记 `CanReplay=false`，避免在未经过相同基本校正的像素上套矩阵。恢复完整基本校正链不属于此参数版本。
- 只读或被占用的 RAW 会报告写入失败。参数更新使用排他文件句柄、`Flush(true)` 和遇到写异常时的尽力回滚；不承诺进程崩溃或断电原子性。校验失败后打开器保留 RAW 显示并禁用这组色度能力，不修改像素来“修复”参数。

可重放参数使 RAW 获得 `HasCieMeasurements` 能力，但 `IsCVCIE` 仍为 false，文件身份和原图保持 RAW。支持光标实时数据、CIE 图、POI 及 X/Y/Z 灰度通道；单通道亮度文件只有 Y。此路径不提供 XYZ→sRGB 全彩图层。真正的 CVCIE 保持原有全彩功能。

小 POI 按实际区域逐像素生成 float XYZ 后累计，保留现有矩形边界、圆形严格内点及封闭区域行段语义，不用抽稀点阵近似测量。通道显示只生成一个 float 平面，缓存最近显示结果。累计测量覆盖达到半幅且完整 XYZ 不超过 1 GiB、运行时总可用内存的 1/8 和托管数组上限时，才缓存完整 XYZ；低内存设备继续按需扫描。已有手动计算的完整 XYZ 直接复用。仍要求完整缓冲区的兼容调用会显式生成完整 XYZ。

重放速度取决于图幅、区域数量、CPU、缓存和存储；不保证与直接读取 CVCIE 等速。比较时分别测量读取、单通道计算、显示归一化、区域计算和文件参数更新，不把热文件缓存结果当作冷盘或实际相机吞吐。

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

打开器将完整源替换交给 `SetImageSource`。复用同尺寸位图的原位更新分支先更新像素元数据，通过 `Presentation.Publish` 恢复此次源显示，再以 `CommitSourcePixels` 登记像素变更，最后通知 `ImageSourceLoaded`；不能将兼容源 setter 当成版本提交。`MediaHelper.MatUpdateWriteableBitmap` 对冻结目标返回 `false`，打开器据此分配新位图，不写入先前的视频或实时帧。源、显示和完成信号的责任见[编辑器上下文](../ui-components/image-editor-context.md)与[图像打开契约](../ui-components/ColorVision.ImageEditor.md#打开图像与完成信号)，FileIO 本身不持有编辑器状态。

全局持久设置位于 **图像设置 → 文件打开 → CVCIE**，配置 `CvcieDisplayConfig` 和两个显示枚举归属 `Engine/ColorVision.Engine/Media/`。Engine 的 `CvcieDisplaySettingProvider` 实现 `IImageComponent`，通过 `ImageView.RegisterSettings` 注册全局配置与保存委托；加载 Engine 后即可在文件打开页看到此组，不要求先打开 CVCIE。ImageEditor 只提供通用设置宿主与属性编辑器，FileIO 不承载显示偏好或新增 UI 依赖。点击保存或完成/关闭设置窗口时只保存发生改动的配置目标；当前图层调整不写全局默认，详见[图像设置的保存语义](../../02-developer-guide/core-concepts/image-editor-settings-plan.md)：

| 设置 | 默认值与生效含义 |
| --- | --- |
| `EnableTrueColor`（启用真彩显示） | 默认关闭；开启后新打开的 CVCIE 默认采用 XYZ 真彩 sRGB，关闭后默认原图。本地手动计算后接入的内存 CVCIE 也使用该开关 |
| `BrightnessMode` | `Auto`（自动适配）；整幅图共享一个亮度除数，取所有有效线性 RGB 分量中的最大正值；全黑图保持黑色 |
| `ReferenceWhiteLuminance` | `65535`；仅 `ReferenceWhite` 模式显示此设置，单位与输入 Y 相同，可调整为任意正有限数值；固定该值可保留不同图片之间的相对亮暗，超出显示范围的值会裁剪 |

`65535` 只是未配置参考白时的软件初始值，不是 sRGB 标准规定的参考白，也不说明 XYZ 与 16 位 RAW 具有相同数值尺度；实际固定参考白应根据输入 Y 的单位和比较要求设置。默认亮度模式仍为 `Auto`，不使用此值；已保存的参考白不会被初始值覆盖。

启用开关通过 `DisplayMode` 持久化：`Source` 为关闭，`Srgb` 为开启。配置服务使用配置类型的完整名称作为持久键，并兼容读取短类名键；保留现有配置类型身份，避免产生两份默认设置。

图层下拉框中的 `Composite`、`真彩 sRGB（XYZ）` 和 X/Y/Z 可临时切换当前视图，不回写全局启用开关；再次选择真彩图层时读取当前全局亮度参数。设置修改不会主动重新渲染所有已打开图片。header 声明三通道、每采样 32 位 float 或 64 位 double 时提供真彩图层，实际渲染还需通过完整数据校验；单通道文件提供亮度显示。

`CvcieSrgbRenderer.Render` 使用与 CIE 背景绘制一致的 D65 XYZ→线性 sRGB 矩阵，完成整幅统一亮度缩放后，将各分量裁剪到 0…1，再应用标准 sRGB 分段编码，输出冻结的 8 位 `Bgr24` 位图。不会逐像素归一化、独立拉伸 X/Y/Z，或重新应用曝光、增益和相机白平衡。原始 XYZ 数组不改动，POI 继续读取测量缓冲。`Auto` 只适合观察本幅图的颜色和相对亮暗，不能据此比较不同图的绝对亮度；普通 Y 灰度回退仍按单通道显示路径归一化，不使用真彩的固定参考白参数。

Renderer 要求正尺寸、checked 长度计算和**恰好三个平面**的 Data，拒绝缺失/多余数据、NaN/Infinity、转换溢出及无效亮度参数，并给出可读异常。有限的负 XYZ 或色域外线性 RGB 不直接判为坏校正，显示阶段才裁剪。转换通过 typed span 直接读取输入，`Auto` 两遍扫描、固定白一遍；大于等于 1048576 像素时按 65536 像素分块、最多半数逻辑 CPU 并行。没有降采样、近似 gamma 或额外全幅浮点 RGB 缓冲；支持分块取消，位图输出独立于文件载体。扫描遍数不代表总耗时的固定排序：像素裁剪比例、sRGB 编码、磁盘读取和位图复制都会影响速度。

首开选择真彩时先读取并转换内嵌 XYZ，仅失败才读取 CVRAW/Y，成功不再提前加载和归一化原图。临时图层读取与转换在后台执行，连续选择仅允许最后一次结果回写；同一控制器的重加载串行，防止取消尚未完成的大文件读取时又并行分配另一整幅数据。完整 XYZ 文件读取目前仍在读取完成后才响应取消。控制器只缓存最近的真彩和最近一个 X/Y/Z 灰度显示位图，各不超过 512 MiB；不缓存原始 XYZ 或 RAW。真彩按亮度模式/参考白及文件长度/修改时间匹配，灰度还匹配通道。换图、清空或替换控制器会取消旧选择并释放缓存；缓存不提供源文件的一致性快照。

X/Y/Z 切换只直接读取所选平面。32/64 位单通道由 `MediaHelper.RenderFloatChannel` 在托管代码中校验有限值并按 MinMax 输出冻结 Gray8，大图采用同样的有界分块并行，输入保持不变，不再交给 native 原位 Normalize。单通道文件的 CIE Y 取第 0 平面。文件打开也以递增请求编号和当前路径共同拒绝过期结果，防止 A→B→A 或同路径重载时旧结果覆盖新参数。

Engine 的显示原图加载入口是 `CvRawLayerController.LoadSourceFile`。它依次尝试 header 的关联原图、相对 CVCIE 目录解析的关联原图和同名 `.cvraw`；专有 RAW 要通过基本尺寸/长度校验，普通图像需能解码。没有可用原图时，直接读取内嵌 Y（单通道文件取第 0 平面，其余取第 1 平面），而不是把第一个 X 平面当作灰度源。浮点 Y 回退拒绝非有限值。

真彩加载或临时切换失败时只写日志，优先回退可用 CVRAW/关联原图，再使用有效 Y 灰度；图层选择同步到实际显示内容。两种回退均不可用时，切换操作保留当前图像，文件打开失败记录错误；不会把失败结果伪装为有效黑图，也不会修复、覆盖或重写已有 CVCIE 文件。

### 手动 CIE 校正的校验边界

`CVRawManualCieCalculator` 只处理手动计算路径的 8/16 位三通道 CVRAW，不是所有相机或外部算法生成 CIE 的统一验证器。导入四色校正文件时，要求增益、曝光和 a…i 矩阵字段存在、能解析且为有限数值；导入失败只记录日志、结束当前导入对话框，保留原始 CVRAW，不以默认矩阵继续提交。

计算前检查 RAW 正尺寸、完整且恰好等长的 payload、有限矩阵系数、有限配置曝光/增益。已有的“有限且非正配置值表示使用源文件曝光/增益”规则保留，但实际选用的源值必须是正有限数；配置曝光还必须能表示为正 float。每个输出 XYZ 转为 float 时再次检查，拒绝非有限结果及 32 位浮点溢出。成功后先更新该 RAW 的当前参数，再接入测量并复用已计算的 XYZ；不因此增加 CIE 文件写入。失败会显示原因并保留原 RAW，已有有效参数仍可在回退时加载。

这些校验只能发现格式、缺失、非有限和数值溢出问题。**全部数值有限但设备不匹配、矩阵系数填错或标定本身失准的 XYZ，无法仅凭标准 XYZ→sRGB 转换可靠识别。** 真彩预览可能仍然偏色；也不能用负数或超出 sRGB 色域作为坏校正的通用判断。校正正确性仍需相应设备、校正文件来源及已知参考测量的验证，不由显示转换自动修复。

### 四色校正系数转换

`LumFourColorCorrectionCalculator` 提供单点与 RGBW 两种计算模式，沿用 MATLAB 算法，使用 `CVRawManualCieConfig` 承载基于原矩阵修正后的九个完整校正系数。输入统一为相机侧与光谱参考侧的 `Y/CIE x/CIE y`；`Yxy → XYZ` 使用原始有限数值，只要求作为分母的 CIE y 不为 0，不裁剪负 Y、负色度、中间反解通道或最终矩阵系数。

`LumFourColorCalibrationFile` 在修正入口识别两种 JSON 原文件，文件结构不改变计算算法；单点 / RGBW 的另存保持以下结构：

| 原文件结构 | 修正读取与另存 | 模板校正类型 |
| --- | --- | --- |
| `Gain_x/Gain_y/Gain_z`、`Texp_x/Texp_y/Texp_z`、`a…i` | 校验所需字段，只更新原文档的九个系数 | `LumFourColor` |
| `Gain` 数组、`pa` 数组 | `pa` 必须恰好有九个有限数字，按行组成 3×3 矩阵；只更新 `pa` | `LumMultiColor` |

`Gain/pa` 对应 Native 多色加载器的通道增益语义，要求 `Gain` 至少包含三个有限数值且前三项非零；原数组全部保留，不转成 `Gain_x`。两种格式都保留位深 `bpp`、附加字段和原始归一化参数，不会因另存丢失设备约束。重复键、同时出现 `pa` 和 `a…i`、错误数组长度或非有限数值会拒绝加载；旧版非 JSON 行文本没有可靠样本和读回契约，暂不支持。该兼容用于已校正 XYZ 的矩阵修正，不扩展手动 CVRAW→CIE 的文件归一化契约。

- 单点修正分别用原 3×3 矩阵反解相机与参考 XYZ 的 RGB 响应，再按两个响应的逐通道比例缩放原矩阵的 R/G/B 三列。
- RGBW 修正先反解四个画面的相机 RGB 响应，以四组参考 x/y、z/y 形成八个色度方程，并用 W 的参考 Y 形成唯一亮度方程，求得新的九个矩阵系数。这与当前 MATLAB 算法一致；R/G/B 的参考 Y 不参与方程，W 的参考 Y 决定整体亮度尺度。
- `LumFourColorCalibrationSession.SetMode` 使用 `LumFourColorCorrectionMode`，单点建立一组，RGBW 建立四组数据；`IsComplete` 同时检查组数和各组有效性。两个窗口显示“单点”和“RGBW 四色”，切换时清除测量数据与旧结果，未知模式值拒绝。从采集窗口打开手工窗口时带入当前模式。
- `LumFourColorSourceSnapshot.SaveCopy` 使用原文档的 `SerializeCorrection` 按原格式另存，默认文件名带 `_Corrected`；另存禁止覆盖原文件并复核内容指纹，不自动安装到校正模板。历史 `_PythonRGB_XYZ` 文件属于未与原矩阵合成的独立 XYZ→XYZ 变换，不是本入口输出的完整校正系数；不能据其 `a…i` 外观认定可用于原文件替换。
- `LumFourColorCalibrationWorkflowWindow` 显示“用户校正”，可从相机属性的“校准与校正”分组、校正文件管理及“应用与工具”进入。`DeviceCamera.UserCalibrationCommand` 使用现有命令元数据分组并通过 `ShowWindow(camera: this)` 带入当前相机，复用已有窗口时忙碌状态不允许切换上下文。单点采集一组相机 POI 与光谱仪数据，RGBW 按相同过程完成四组；相机侧与光谱侧的采集顺序不限。侧栏显示相机、光谱各一行 Y/x/y 六个输入框；图像来源、POI、曝光、XYZ、光谱结果 ID 与完整相对光谱放入默认折叠的“测量详情”。
- `LumFourColorRecentImages` 与 POI 导图使用同一 `MeasureResultImgModel` 拍摄记录表，独立连接按当前逻辑相机 `DeviceCode` 查询最近 100 条（时间、ID 倒序）；列表刷新不默认选中，不改变现有测量。“导入最新图像”明确取第一条，导入前清除旧相机数据，查询或文件加载失败不复用旧数据、不自动跳到更早记录。仅使用记录自身的 `FileUrl` / `RawFile`，优先 CVCIE 路径，不猜测同目录替代文件。导入复核设备归属、拍摄成功、文件存在及三通道浮点 CVCIE；RAW/普通预览图拒绝。读取在后台执行，按原 CVCIE 路径加载内嵌 XYZ，再应用当前 POI 模板或手动画点；不访问真实相机、不修改记录，文件的校正来源仍待核对。
- `LumFourColorPoiEditor` 复用 `ImageView` 的圆形、矩形绘图及尺寸面板，右键编辑复用 `DrawingVisualBaseDVContextMenu` 的属性编辑器。菜单提供编辑、删除、绘制与 POI 模板入口，预览区域不接受另外打开、拖入或裁剪图片，以免显示图与测量帧脱离。每个色块一个 POI；形状或位置改变立即撤销旧测量，绘制结束后交给 `PoiMeasurementService.CalculateRaw` 重算并同步选中框，保留原始负值，越界区域不能用于计算。
- POI 模板经 `TemplatePoi.Params` 选择，已有数据库模板在应用时重新读取点列表，仅取第一个点；模板声明的图像宽高须与当前帧一致。支持圆形、中心矩形和左上角矩形，圆形按既有模板的 `PixWidth` 直径语义转换。应用前清除旧读数，无效首点不跳过、读取失败不回用旧点。带入后的绘图属性独立于原模板，仍可手动画点、拖动或右键编辑；取图后继续应用当前选中的模板。
- 相机采集复用 `LocalCameraCaptureService`，未连接时按当前设备 Camera ID、测量模式和位深自动连接，选择的校正模板必须启用四色或多色校正并生成 CIE；在连接相机前检查模板类型与文件结构匹配，采集记录校正文件内容指纹。模板启用多色时带入其 `Gain/pa` 文件，不误用模板中未启用的四色文件。无设备环境可加载已有 `.cvcie` 验证 POI 和计算链；重新取图只清除该色块的 POI 与相机测量值，保留已采集的光谱。
- `ILumFourColorCameraCaptureProvider` 与 `ILumFourColorSpectrumCaptureProvider` 分开封装两个采集动作。窗口汇总当前色块的独立采集状态，不自动切换外部画面、ND 或光谱仪；这些联动仍保留在提供方边界之外。
- 采集窗口的 `CameraYInput` / `CameraCieXInput` / `CameraCieYInput` 与对应的 `Reference*Input` 支持直接录入及覆盖测量值。每次输入先撤销该侧有效值和旧计算，再校验完整 Yxy；相机手动值不要求图像，按 Yxy 换算 XYZ，`IsCameraEdited` 与原始 POI 快照分开保存，`RestoreCamera` 恢复精确的原始 XYZ/x/y。未修改的相机字段保留原始浮点数值，显示格式不会改变计算精度。
- `IsReferenceEdited` 区分手动参考与原始光谱记录。手动参考不要求波形，交给计算器的 `Spectrum` 为 null；原始波形和元数据仅保留供核对，`RestoreReference` 恢复原始 Yxy。只有未修改的设备记录参与重复结果 ID 检查；两侧手动值统一进入计算前的人工复核，编辑不写回任何源记录。
- `LumFourColorCorrectionWindow` 保留手工单点/RGBW 录入作为辅助入口。`LumFourColorMeasurementClipboard` 处理 Excel TSV 的矩形粘贴，先校验整块的尺寸、目标名称与有限数值再应用，支持选区复制和含表头整表导出。输入变更会撤销人工质量确认与计算结果；两种窗口都禁止另存到原文件路径，保存前核对原文件指纹，临时文件写出并读回通过后再替换目标副本。
- 两个窗口把“计算校正”放在右侧面板底部、保存区上方，底部提供“另存为”和“替换当前文件并重启服务”。单点 / RGBW 的 `ReplaceOriginal` 先序列化并读回临时文件，再复制原文件到同目录带日期、唯一标识和 `_backup` 后缀的文件；核对备份指纹与原文件指纹后才 `File.Replace` 原子替换，保留原 JSON 格式与其他字段。旧备份不覆盖，写入失败不触发重启。
- `LumFourColorCalibrationReplacement.ReplaceAndRestartAsync` 在两个窗口间阻止并发替换，写入完成后调用传入的服务重启动作。生产入口复用 `DisplayFlow.RestartColorVisionServicesAsync`，经 ServiceHost 停止并启动 `RegistrationCenterService`、`CVMainService_x64`、`CVMainService_dev`，随后刷新注册中心连接。该动作会中断服务，须由用户明确点击替换入口；另存不重启，也不修改模板或数据库资源。服务重启异常作为独立结果返回：新文件与备份保留，提示手动检查并重启，不将它报成文件保存失败或自动回滚。
- 替换期间禁用编辑、保存与关闭。文件替换后无论重启是否成功，采集窗口重读原文件并清除旧相机测量，手工窗口清空测量表及确认状态，避免基于新矩阵再次应用旧数据。底部显示可悬停查看的完整备份路径。`LumFourColorReplacementTests` 覆盖两种格式的原字节备份、元数据保留、旧快照失效、无效 / 被修改 / 被占用文件不重启，以及重启失败与并发门禁；窗口测试通过替代的重启动作验证忙碌状态和旧数据清理，不操作真实服务。
- `LumFourColorDataChecks` 在采集侧检查 Yxy、峰值 AD 与波长顺序，IP 合格范围为 30%～95%，不裁剪有限负值。样本变更事件统一撤销旧计算；重采失败不能复用旧侧数据，未修改的光谱记录不得以同一结果 ID 分配到多个色块。文件来源未知或不一致、IP 缺失或范围异常可由操作员在默认取消的集中提示中明确继续；这不是自动质量认证。历史选择、状态失效与曝光边界见[用户校正](../../01-user-guide/devices/calibration.md#四色校正采集)。

`LumFourColorFileCompatibilityTests` 以实际执行外部 Python 单点函数与 MATLAB `solveFourColorCal` 得到的输出作为对照，验证两种原文件格式的系数一致性、RGBW 的参考色度与 W 亮度约束、原格式及附加字段往返、无效输入和保存保护。`LumFourColorWorkflowSafetyTests` 覆盖两个窗口的单点 / RGBW 切换、W 数据完整性、Excel 粘贴及旧结果失效。零 CIE y、零反解通道或不可逆方程仍拒绝计算。

普通图像输出的窗口操作、命令行参数、通道命名与覆盖规则见 [CVRAW / CVCIE 图像导出](./cv-image-export.md)。文件解析成功不代表导出得到所需的完整通道集合。

## 验证入口与明确缺口

- `CVFileReadCacheTests`：路径/尺寸/位深/通道切换的指针复用、参数尾部扩容与收缩、文件变化失效、并发读取与覆盖/释放、失败写入隔离、统一释放、`CVRawOpen` 显示内存复用。设置 `COLORVISION_CVRAW_SAMPLE_DIR` 可额外运行本地真实样本回归，源文件仅用于读取；`COLORVISION_CVRAW_REPORT_DIR` 指定独立的计时报告目录。文件复制计时可能受操作系统缓存影响，不能作为现场 CT 收益。
- `CVFileMetadataTests`：v1/v2/v3 读取兼容、像素字节不变、参数替换与收缩、未知记录保留、损坏尾部拒绝覆盖，以及只读/占用失败。
- `RawColorCalibrationTests` 与 Native `--calibration-smoke`：已加载参数快照、8/16 位及两种布局、亮度/单色/四色/多色重放与原校正输出逐字节一致、POI/封闭区域及完整缓存对照、仅成功校正写回和 RAW/CVCIE 快照匹配。
- `CalibratedRawDisplayTests`：RAW 身份、色度能力、无 sRGB 图层、X/Y/Z 与原图切换，打开和切换不写文件。这些测试不代替真实屏幕、远程校正服务或采集硬件验收。

- `Test/Conoscope.Tests/CvcieChannelReaderTests.cs`：合成 v1/v2 文件、只取指定平面但保留 header 元数据、越界索引返回 false。fixture 填入未创建的关联文件名仍可读目标平面，但没有验证存在可读源图时是否访问它；“不跟随”的完整结论来自源码。可选真实样本仅在设置 `CONOSCOPE_REAL_SAMPLE` 时读取，未设置时直接返回，不证明真实样本通过。
- `Test/ColorVision.UI.Tests/ExportCieTests.cs`：消费方的 RAW 位深、选定浮点通道、关联源图和导出策略；不是 FileIO 全版本解析认证。
- `Test/ColorVision.UI.Tests/CvcieSrgbRendererTests.cs`：D65 白/黑/sRGB 基色、分段编码、整图公共缩放、固定参考白、曝光/增益无关、负值显示裁剪，以及长度、尺寸、非有限值和溢出的拒绝；32/64 位两模式的并行输出与原串行实现逐字节对照及取消。
- `Test/ColorVision.UI.Tests/CvcieDisplayIntegrationTests.cs`：合成 CVCIE 与关联 CVRAW，经真实图像打开/图层切换入口检查默认模式、有效 XYZ 与源图分离、无 RAW 的 Y 回退、无效 XYZ 回退、X/Y/Z、快速连选及换图后的旧结果抑制、参考白变更、配置序列化和完成事件状态；不是实际屏幕色彩还原或真实校正文件的验收。
- `Test/ColorVision.UI.Tests/CvcieDisplaySettingsTests.cs`：未打开 CVCIE 时的设置注册、跨视图共享全局配置、默认值页合并和保存委托；不替代实际窗口及显示器验收。
- `Test/ColorVision.UI.Tests/CvFilePixelSafetyTests.cs`：大尺寸元数据配短数组在 native 调用前拒绝、合法 payload、旧 XYZ 入口的 v1/v2 切片及截断文件返回码。
- `Test/ColorVision.UI.Tests/CvcieFloatChannelRendererTests.cs`：32/64 位灰度范围、常量黑图、输入不变、有限极值、非法数据与取消。
- `Test/ColorVision.UI.Tests/CVRawManualCieCalculatorTests.cs`：8/16 位 BGR 输入到连续 XYZ、负矩阵系数保留、曝光/增益回退兼容、输入长度和维度校验，以及校正导入/计算的非有限数和输出溢出拒绝。
- `Test/ColorVision.UI.Tests/LumFourColorWorkflowSafetyTests.cs`：IP 端点与异常、原始负值和元数据保留、无效替换清除旧值、来源待核对、重复光谱 ID、文件指纹与副本保护、六项编辑与原值恢复、模板首点/尺寸/形状校验、POI 右键菜单，以及窗口输入变化 / 失败后的保存门禁；不替代真机采集、真实数据库历史选择或相机过曝检测。
- 当前关联测试没有证明 v3 writer/reader 往返、恶意长度、所有大小写入口、源文件替换一致性、取消时序或写失败后原文件恢复。上面的实现缺口尚未修复；文档对齐不代表这些测试已运行或问题已消失。

修改 FileIO 时先用 `knowledge.mjs impact "Engine/ColorVision.FileIO/CVFileUtil.cs"` 找消费方主题，按变更补合成文件回归。只读理解协议不需要启动主程序、加载真实测量样本或执行 Explorer 注册脚本。
