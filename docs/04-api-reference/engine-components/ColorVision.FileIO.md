---
knowledge_id: "engine.file-io"
knowledge_type: "topic"
status: "current"
summary: "CVRAW/CVCIE 读取、动态校正参数覆盖、RAW 按需色度测量、内嵌 XYZ 显示，以及版本写回和失败边界。"
aliases: ["CVFileMetadata", "RAW色度参数", "动态参数覆盖", "HasCieMeasurements", "用户校正", "LumFourColorRecentImages", "CVCIE文件为什么打不开", "ColorVision.FileIO", "CVFileUtil", "CVCIEFile", "ReadCIEFileChannel", "ReadCVCIE", "WriteCIEFile", "NDPort", "内嵌XYZ通道", "CVCIE关联源文件", "CVCIE版本写回", "文件写入失败原文件", "CVCIE真彩显示", "三刺激值转sRGB", "CvcieSrgbRenderer", "CvcieDisplayConfig", "CVRawManualCieCalculator", "LumFourColorCalibrationWorkflowWindow", "四色校正采集", "校正文件异常", "替换当前文件并重启服务", "校正文件备份"]
code_paths: ["Engine/ColorVision.Engine/Services/Devices/Camera/Local/CameraFileMetadata.cs","Engine/ColorVision.Engine/Media/CalibratedImageContentProvider.cs","Engine/ColorVision.FileIO","Engine/ColorVision.Engine/Services/Devices/Camera/Local/ColorCalibrationSnapshot.cs","Engine/ColorVision.Engine/Services/Devices/Camera/Local/LocalFrameCalibrationService.cs","Engine/ColorVision.Engine/Services/POI/RawColorMeasurementSource.cs","Native/opencv_helper/algorithm/calibration/calibration_raw_color.cpp","Engine/ColorVision.Engine/Media/MediaHelper.cs","Engine/ColorVision.Engine/Media/CVRawBatchImageLoader.cs","Engine/ColorVision.Engine/Media/CVRawOpen.cs","Engine/ColorVision.Engine/Media/CvRawPixelBuffer.cs","Engine/ColorVision.Engine/Media/CvRawLayerController.cs","Engine/ColorVision.Engine/Media/CvcieSrgbRenderer.cs","Engine/ColorVision.Engine/Media/CvcieDisplayConfig.cs","Engine/ColorVision.Engine/Media/CvcieDisplaySettingProvider.cs","Engine/ColorVision.Engine/Media/CVRawManualCieCalculator.cs","Engine/ColorVision.Engine/Services/PhyCameras/Calibration/LumFourColorCorrectionCalculator.cs","Engine/ColorVision.Engine/Services/PhyCameras/Calibration/LumFourColorCorrectionWindow.xaml","Engine/ColorVision.Engine/Services/PhyCameras/Calibration/LumFourColorCorrectionWindow.xaml.cs","Engine/ColorVision.Engine/Services/PhyCameras/Calibration/LumFourColorCalibrationWorkflow.cs","Engine/ColorVision.Engine/Services/PhyCameras/Calibration/LumFourColorDataChecks.cs","Engine/ColorVision.Engine/Services/PhyCameras/Calibration/LumFourColorCalibrationReplacement.cs","Engine/ColorVision.Engine/Services/PhyCameras/Calibration/LumFourColorCalibrationFile.cs","Engine/ColorVision.Engine/Services/PhyCameras/Calibration/LumFourColorPoiEditor.cs","Engine/ColorVision.Engine/Services/PhyCameras/Calibration/LumFourColorMeasurementClipboard.cs","Engine/ColorVision.Engine/Services/PhyCameras/Calibration/LumFourColorSpectrumSelectionWindow.xaml","Engine/ColorVision.Engine/Services/PhyCameras/Calibration/LumFourColorSpectrumSelectionWindow.xaml.cs","Engine/ColorVision.Engine/Services/PhyCameras/Calibration/LumFourColorCalibrationWorkflowWindow.xaml","Engine/ColorVision.Engine/Services/PhyCameras/Calibration/LumFourColorRecentImages.cs","Engine/ColorVision.Engine/Services/PhyCameras/Calibration/LumFourColorCalibrationWorkflowWindow.xaml.cs","Engine/ColorVision.Engine/Media/CVRawManualCieWindow.xaml.cs","UI/ColorVision.ImageEditor/Settings/ImageViewSettingsWindow.xaml.cs","Plugins/Conoscope/ConoscopeDocument.cs"]
related: ["engine.index", "ui.image-editor", "engine.shell-extension", "plugins.conoscope", "delivery.index", "engine.cv-image-export"]
---

# CV 文件读取、通道与写回契约

`Engine/ColorVision.FileIO/` 的 `CVFileUtil` 和 `CVCIEFile` 读写使用 `CVCIE` 魔数的 CVRAW、CVCIE、CVSRC。先确认消费方要求的是文件内嵌数据、关联源图还是显示位图，再选择读取接口。

## 按问题定位责任

| 问题 | 先查的入口与边界 |
| --- | --- |
| 魔数正确但仍打不开 | `IsCIEFile` 只查魔数。继续核对文件头、payload 与消费方布局要求 |
| CVCIE 显示了关联原图 | `ReadCVCIE` / `OpenLocalCVFile` 优先关联源图。Engine 另受 `CvcieDisplayConfig` 控制 |
| 只读取内嵌 Y | `ReadCIEFileChannel(path, 1, ...)`。元数据仍描述原文件 |
| RAW 中的色度重放或真彩异常 | `CVFileMetadata`、`ColorCalibrationSnapshot`、`RawColorMeasurementSource` 与 `CvcieSrgbRenderer` |
| 写入成功但无法读回 | 检查尺寸与声明长度，以及是否写入零长度 payload |
| 同步替换异常 | 检查目标访问权限和同目录 `.bak` |
| Explorer 缩略图异常 | [ShellExtension](./ColorVision.ShellExtension.md) |

## 识别与 header 不证明完整有效

`IsCIEFile(path/bytes)` 只检查开头五字节对应 `CVCIE`；五字节文件也可能返回 true。`IsCVCIEFile(path)` 进一步要求 header 解析成功、推断出的 `FileExtType == CIE`，但仍不读取或验证 payload。

`ReadCIEFileHeader` 成功返回**数据长度前缀所在偏移**，失败返回 `-1`；不是完整文件长度，也不是跳过长度前缀后的像素起点。失败时 out 对象仍存在，字段可能只填了一部分，不能按对象非空判断成功。

两种 header 入口的类型与路径语义不同：

- 路径 header 和打开入口优先识别 `.cvraw`、`.cvcie`、`.cvsrc`，忽略大小写及目录名。其他名称保留各入口的旧推断规则。`FilePath` 在成功解析时赋值。
- 字节数组入口不设置 `FileExtType` 或 `FilePath`。类型保留枚举默认值 Raw，不是按魔数识别出 CIE；知道格式的调用方需要自行赋值。

## 二进制布局和版本差异

托管读写共用以下布局。整数和浮点均为小端，源文件名按 GBK 编码，长度以字节计。

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

`WriteCIEFile` 的磁盘、仅缓存和字节数组输出均支持 v1、v2、v3。v3 往返保留有符号 NDPort，v1、v2 沿用原有布局。其他版本在准备输出前返回 `false`。

路径 reader 与字节 reader 的异常长度处理也不完全相同。例如字节入口拒绝负文件名长度；路径入口对不满足可读条件的文件名段可能直接跳过读取后继续解释后续字段。当前不能把 header 解析成功当作恶意/畸形文件的完整安全验证。

## 三种读取语义

| 入口 | 读取内容 | 成功、失败及元数据 |
| --- | --- | --- |
| `Read(path/bytes)` / `ReadCVRaw(path)` | 本文件 header + 全 payload；不跟随关联源图 | bool；全量数据按**声明长度**分配，没有统一验证长度等于尺寸乘积 |
| `ReadCVCIE(path)` / CIE 分支的 `OpenLocalCVFile` | 优先尝试 `SrcFileName` 指向的源文件，失败再读本文件 payload | 成功跟随源图时载体来自源图，不能再认为 Data/尺寸/类型属于内嵌 XYZ |
| `ReadCIEFileChannel(path, index, ...)` | 直接定位本文件第 index 个连续通道平面，不跟随 `SrcFileName` | bool；保留原 header 的 Channels/Exp 等字段，Data 仅含一个通道 |

### 全量 payload

`ReadCIEFileData` 依据 Version 决定 4/8 字节长度前缀；要求声明长度大于 0 且不超出文件剩余长度，再分配整个数组并分段读满。它没有按 Rows/Cols/Bpp/Channels 检查 payload 恰好等长，也允许 payload 后还有字节。OOM/读取异常通常被捕获并返回 false，但 out/ref 载体不统一清空；部分读取后 Data 仍可能存在。

一次读取从同一个文件流或缓存快照取得文件头和像素。分别调用路径版 header/data 时会各自打开，需一致时应共用同一流。`ReadFile` 不校验读满。

`CVFileReadCache` 默认关闭。启用后，`MaximumEntries` 限制 CVRAW 槽位，默认 1；`Synchronous=0` 和旧值 1 同步保存，`MemoryOnly=2` 依赖缓存启用。命中按规范路径忽略大小写，不打开磁盘文件，也不探测外部写入、删除或占用。程序内保存和参数写入成功后发布新快照，已有读取继续使用旧快照。单独探测 header 或参数不加载整幅图。槽位不足且不能回收空闲磁盘槽位时直接读盘，保留仅缓存图像；新内存保存或主动清理仍可淘汰它们。

“全局选项 → 常规 → 文件归档”和状态栏“内存缓存管理”共用 `CvRawFileCacheConfig`。减少数量或关闭时空闲缓冲立即释放，在途读写继续使用原缓冲直至归还。关闭后停止缓存命中和填充；同步保存仍可写盘。只存内存的图像被淘汰后无法恢复，需留存时使用“保存文件”。该开关只作用于应用的文件缓存。

内存管理按模块显示图像文件、截图、校正及相机缓冲。汇总仅包含成功读取的接入模块，相机页只计空闲缓冲，校正值为估算。释放前核对确认框中的实际范围，搜索不会改变已勾选项或“释放全部”的范围。校正上下文与在途导出仍可使用持有的缓冲。磁盘文件保留；相机释放不关闭设备。截图缓冲的统计与生命周期由 `SnapshotBufferCache` 和 `SnapshotCacheModule` 管理。

`CVRawOpen` 的 POI、原图层和剖面借用本次打开的 RAW 与参数，重开时换快照。`LocalFrameFileService` 使用同一帧或文件快照核对布局，按需 XYZ 在完成有效读取后才接入结果。

### 关联源文件

`ReadCVCIE` 通过 `ReadCVCIESrc` 读取 `SrcFileName`。绝对路径直接使用，相对路径先按 CVCIE 所在目录解析，缺失时再尝试当前工作目录，以兼容旧数据。

关联文件从同一打开的快照识别并读取。普通图像字节读满后标记为 Tif，解码由消费方负责。关联文件缺失、读不到或为空时，`ReadCVCIE` 回退到内嵌数据；采用关联图时仍保留原 `SrcFileName` 和 CVCIE 的 `FilePath`。

`OpenLocalCVFile` 返回载体而忽略内部 bool，调用方须检查尺寸和 Data。

### 新的直接通道读取与旧切片

`ReadCIEFileChannel` 使用**零基嵌入索引**，XYZ 的 X/Y/Z 分别是 0/1/2。它检查索引、正尺寸、Bpp 为正且能被 8 整除，使用 checked 长度运算；单通道必须不超过 `int.MaxValue`。声明 payload 至少要容纳 header 声明的全部通道且不得超过文件剩余量，但不要求两者恰好等长。

它只分配目标通道数组，全部读满后才赋给 Data。保留的 `Channels` 仍是原文件通道数，例如 3；不能再用该值把返回 Data 当三通道图像，也不能直接当完整文件写回。取消在 header 读取后、每个读取块前检查；捕获 `OperationCanceledException` 时 Dispose 载体并**重新抛出**，不是返回 false，也不是异步 I/O。header 失败先返回 false，不保证预取消 token 优先于文件错误。

`ReadCVCIEXYZ` 使用同一通道读取逻辑，成功返回 0 并设置 Channels=1、类型 Raw。文件头失败返回 `-1`，单通道输入、无效索引或像素读取失败返回 `-2`。`OpenLocalFileChannel` 只实现 SRC 和 CIE XYZ，消费方须检查数据。

[Conoscope](../plugins/standard-plugins/conoscope.md) 使用新的直接通道入口，并额外要求 32 位浮点、至少三通道、Data 恰好是一个平面；Y-first 和其后的 XYZ 就绪属于 Conoscope 文档生命周期，不属于 FileIO。

## RAW/CVCIE 动态参数与按需色度测量

`CVFileMetadata` 在 header 声明的像素 payload 后保存可选参数区，不修改版本号、像素长度、像素字节或文件扩展名。旧版全量读取和单平面读取仍按原 payload 长度工作。FileIO 只存储 `kind → (version, bytes)`，不依赖 JSON、WPF 或 Native；色度参数的 JSON 结构由 Engine 的 `ColorCalibrationSnapshot` 管理。

参数区由若干 UTF-8 key、记录版本和长度限定的 byte 值组成，末尾是 64 字节 footer：`CVMD0001` 魔数、容器版本、记录数量、像素结束偏移、参数正文长度与正文 SHA-256。正文上限 64 MiB。`SetProperty` 替换同 key 的当前值，保留其他 key 和未知记录版本，然后截断旧尾部；反复校正不积累历史。未知尾部、未知容器版本、校验失败或布局不一致会拒绝覆盖，避免擦除无法理解的数据。

采集来源和校正来源分别使用 `colorvision.acquisition.camera`、`colorvision.calibration.provenance`，当前记录版本均为 1。采集信息固化当时的设备、曝光/增益/ND 和像素布局。校正来源从实际加载的校正资源取得标识及 SHA-256，缓存命中也沿用原上下文。旧文件缺失的字段不从当前配置猜测。完整矩阵由 `colorvision.calibration.color` 负责，并与来源摘要作为同一次有效尾部更新提交。可选记录损坏或版本未知时可保留基础像素与其他可读记录。

写入规则如下：

- **只在实际色度/亮度校正成功后更新** `colorvision.calibration.color`；普通打开、查看通道、POI、仅基本校正都不写参数。
- 本地校正与手动 RAW 校正保存实际执行的系数、曝光、布局、模板和时间。Native 路径从已加载的校正上下文提取参数，不在计算完成后重新读可能已改变的 `.dat`。
- Flow 本地校正/校正+实时 POI/相机取图的可选加速路径可直接提取参数而不生成完整 CIE；基础校正仍执行。`LocalFrameFileService.Load` 将 CVRAW 内嵌的 JSON 参数交给帧租约，本地 POI 可直接测量 RAW 区域；布局不匹配、缺失参数或 `CanReplay=false` 不允许回放。此流程路径不会采用显示测量源的累计区域阈值来缓存完整 XYZ。
- 对已有对应 CVRAW，无论保存图像开关是否启用都更新当前参数。本地保存只生成 CVRAW，并携带当前校正快照；CVCIE 保留旧服务结果读取兼容。没有落盘 RAW 的纯内存采集只能先随帧保留参数，不能凭空修改一个不存在的文件。
- 基本校正已经改变 RAW 像素时，新保存的校正后 RAW 可以重放色度矩阵；原始传感器 RAW 仅记录该快照并标记 `CanReplay=false`，避免在未经过相同基本校正的像素上套矩阵。恢复完整基本校正链不属于此参数版本。
- 只读或被占用的 RAW 会报告写入失败。参数更新使用排他文件句柄，尾块写入、截断和 `Flush()` 在返回前完成并关闭文件，随后可从磁盘路径重新读回；与同步 RAW 保存一致，数据经操作系统文件缓存写入，不额外强制硬件刷盘，避免小参数更新等待此前整幅 RAW 的写入。遇到写异常时仍尽力回滚；不承诺数据已到达硬件介质、进程崩溃或断电原子性。校验失败后打开器保留 RAW 显示并禁用这组色度能力，不修改像素来“修复”参数。

可重放参数使 RAW 获得 `HasCieMeasurements` 能力，但 `IsCVCIE` 仍为 false，文件身份和原图保持 RAW。支持光标实时数据、CIE 图、POI 及 X/Y/Z 灰度通道；单通道亮度文件只有 Y。此路径不提供 XYZ→sRGB 全彩图层。真正的 CVCIE 保持原有全彩功能。

“图像设置 → 图像信息”可查看实际记录的色度校正参数。三通道按 X/Y/Z 行、R/G/B 列显示矩阵；系数已含执行时的归一化，不能再叠加曝光或增益。`CanReplay=false` 仍可查看参数。

小 POI 按真实区域逐像素累积 XYZ，单通道显示可独立生成一平面。达到覆盖阈值且内存预算允许时才缓存完整 XYZ，入口 `RawColorMeasurementSource`。比较性能时拆分读取、计算、显示及参数更新，热缓存不能当作冷盘或实际相机吞吐。

## 同步写入的提交与验证边界

`WriteCIEFile` 同步保存要求目标目录允许创建、重命名和删除暂存文件。Windows 覆盖保留目标 DACL，拒绝只读、隐藏、系统或重解析点目标。暂存文件完整后才提交，失败保留旧文件和缓存。替换异常时尝试恢复原路径，恢复受阻可检查同目录的 `.bak`。仅 CVRAW 使用缓存。调用方创建父目录并提供正确尺寸，掉电持久性由存储系统决定。

本地相机同步保存 CVRAW 时，文件提交后可用仍持有的帧租约和同一份文件头、元数据直接填充独立缓存。带指针的 `WriteCVRaw` 在返回前读取像素，调用方须保证此期间像素有效且不可变。

`WriteImageHeader` 统一写入文件名、曝光与尺寸，v3 保留 `NDPort`，只有 v2 的 payload 长度使用 Int64。文件名由 GBK 读回，不能依赖编码自动识别。曝光不足补 0，额外的曝光截断。`FilePath`、`FileExtType` 不写入文件。

载体重载可以写出零长度 payload，而 reader 会拒绝它。参数重载拒绝空数组，流式 writer 核对实际写出长度；成功仍不证明 payload 与尺寸乘积匹配。

## 数据所有权与消费方

`CVCIEFile` 不持有打开的文件流。`Dispose` 仅将自身 Data/Exp 引用置 null 并标记已释放；不会清零数组内容、清掉其它对象持有的引用、强制 GC 或建立后续方法调用门禁。

界面和批处理由消费方校验布局与长度。`MediaHelper` 管理图像转换及关联图回退，Conoscope 在消费方检查单平面负载。显示转换不改变 FileIO 的读取契约。

项目是独立纯托管 AnyCPU 库；目标框架与 NuGet 内容以 `ColorVision.FileIO.csproj` 为准，构建平台例外见[构建入口](../../02-developer-guide/README.md)。不要把宿主 x64 规则机械套到此包。

## Engine 真彩显示与原图回退

Engine 的 `CVRawOpen` / `CvRawLayerController` 为 CVCIE 提供“真彩 sRGB（XYZ）”，只取文件内嵌 XYZ。`ReadCVCIE` 与 `OpenLocalCVFile` 仍按自己的关联图语义工作；FileIO 不依赖 UI。

全局设置位于“图像设置 → 文件打开 → CVCIE”，由 `CvcieDisplayConfig` 和 `CvcieDisplaySettingProvider` 管理。`EnableTrueColor` 默认关闭持久为 `Source`，开启为 `Srgb`，只决定新打开 CVCIE（含手动计算接入的内存结果）的初始图层。当前视图切换 `Composite`、真彩及 X/Y/Z 不保存为全局默认。改全局设置不会主动刷新所有已打开文件，重新选择真彩才读取当前亮度参数。 [设置保存语义](../../02-developer-guide/core-concepts/image-editor-settings-plan.md)

`CvcieSrgbRenderer` 接受完整的三平面 32 位 float 或 64 位 double 数据。它按 D65 XYZ 到线性 sRGB 转换、共享亮度缩放与标准分段编码输出 8 位 BGR。源 XYZ 和 POI 数值保持原样。默认 `Auto` 按整图最大正分量缩放，适合观察单幅图片。跨图比较可固定 `ReferenceWhiteLuminance`，单位与 Y 相同；初始值 65535 是应用配置，并非计量参考或 sRGB 标准。有限负值、色域外和值超显示范围的分量在显示时裁剪。非有限像素、长度错误、无效参数或转换溢出拒绝真彩。

真彩失败写日志，回退尺寸匹配的关联原图、同名 CVRAW 或有效 Y。单通道文件用第 0 平面显示 Y。X/Y/Z 读取单一平面，浮点灰度走独立 MinMax，不受真彩参考白影响。读图与切换都拒绝过期结果；所需文件变化时保留当前显示，重新打开才能读新文件。缓存只作显示复用，不能充当源文件快照。原始测量与关联图显示各自绑定本次打开的来源。 [编辑器上下文](../ui-components/image-editor-context.md) · [打开图像与完成信号](../ui-components/ColorVision.ImageEditor.md#打开图像与完成信号)

### 手动 CIE 校正的校验边界

`CVRawManualCieCalculator` 只处理手动计算路径的 8/16 位三通道 CVRAW，不是所有相机或外部算法生成 CIE 的统一验证器。导入四色校正文件时，要求增益、曝光和 a…i 矩阵字段存在、能解析且为有限数值；导入失败只记录日志、结束当前导入对话框，保留原始 CVRAW，不以默认矩阵继续提交。

计算前检查 RAW 正尺寸、完整且恰好等长的 payload、有限矩阵系数、有限配置曝光/增益。已有的“有限且非正配置值表示使用源文件曝光/增益”规则保留，但实际选用的源值必须是正有限数；配置曝光还必须能表示为正 float。每个输出 XYZ 转为 float 时再次检查，拒绝非有限结果及 32 位浮点溢出。成功后先更新该 RAW 的当前参数，再接入测量并复用已计算的 XYZ；不因此增加 CIE 文件写入。失败会显示原因并保留原 RAW，已有有效参数仍可在回退时加载。

这些校验只能发现格式、缺失、非有限和数值溢出问题。**全部数值有限但设备不匹配、矩阵系数填错或标定本身失准的 XYZ，无法仅凭标准 XYZ→sRGB 转换可靠识别。** 真彩预览可能仍然偏色；也不能用负数或超出 sRGB 色域作为坏校正的通用判断。校正正确性仍需相应设备、校正文件来源及已知参考测量的验证，不由显示转换自动修复。

校正文件的通用参数编辑与离线生成归属 Engine，相应入口、保存边界和输入前提见[校正参数编辑与离线生成](../../01-user-guide/devices/calibration.md#校正参数编辑与离线生成)。下节描述基于已有矩阵的用户修正，不能用它的严格九项 `pa` 校验代替通用编辑器对 Native 数组附加项的保留契约。

### 四色校正系数转换

`LumFourColorCorrectionCalculator` 提供单点与 RGBW 两种计算模式，沿用 MATLAB 算法，使用 `CVRawManualCieConfig` 承载基于原矩阵修正后的九个完整校正系数。输入统一为相机侧与光谱参考侧的 `Y/CIE x/CIE y`；`Yxy → XYZ` 使用原始有限数值，只要求作为分母的 CIE y 不为 0，不裁剪负 Y、负色度、中间反解通道或最终矩阵系数。

`LumFourColorCalibrationFile` 根据原 JSON 结构自动识别校正文件。文件结构不改变计算算法：

| 原字段 | 更新内容 | 模板校正类型 |
| --- | --- | --- |
| `Gain_x/Gain_y/Gain_z`、`Texp_x/Texp_y/Texp_z`、`a…i` | 更新九个系数 | `LumFourColor` |
| `Gain`、`pa` | 按行更新 3×3 `pa`，保留完整 `Gain` 数组 | `LumMultiColor` |

重复键、两种矩阵共存、字段缺失、非有限系数或不合格 `Gain/pa` 数组会拒绝。旧版非 JSON 行文本暂不支持。原增益、曝光、位深及附加字段保留。这一兼容用于已校正 XYZ 的矩阵修正，单独的手动 RAW→CIE 仍遵守本页上一节。

- 单点修正分别用原 3×3 矩阵反解相机与参考 XYZ 的 RGB 响应，再按两个响应的逐通道比例缩放原矩阵的 R/G/B 三列。
- RGBW 修正先反解四个画面的相机 RGB 响应，以四组参考 x/y、z/y 形成八个色度方程，并用 W 的参考 Y 形成唯一亮度方程，求得新的九个矩阵系数。这与当前 MATLAB 算法一致；R/G/B 的参考 Y 不参与方程，W 的参考 Y 决定整体亮度尺度。
采集、图像或光谱输入、POI 和实际替换步骤归用户校正。实现从 `LumFourColorCalibrationWorkflowWindow`、`LumFourColorCorrectionWindow`、`LumFourColorRecentImages`、`LumFourColorPoiEditor` 和 `LumFourColorCalibrationReplacement` 定位。历史 `_PythonRGB_XYZ` 文件属于未合成原矩阵的独立 XYZ→XYZ 变换，不能替换设备的完整系数文件。输入更改会撤销旧结果，手动值不会写回源记录。另存只写新路径；替换核对指纹并创建备份后才原子写回。重启失败单独提示，保留新文件及备份。 [用户校正](../../01-user-guide/devices/calibration.md#四色校正采集)

零 CIE y、零反解通道或不可逆方程仍拒绝计算。

普通图像输出的窗口操作、命令行参数、通道命名与覆盖规则见 [CVRAW / CVCIE 图像导出](./cv-image-export.md)。文件解析成功不代表导出得到所需的完整通道集合。PNG/TIFF 容器读取归 ImageEditor；Engine 的可选 `CalibratedImageContentProvider` 校验内嵌实际参数与样本绑定，再接入与 CVRAW 共用的 `CalibratedImageCapability`。已计算的浮点 Y 直接测量，不做 RAW 矩阵重放；参数记录与实际测量资格分开，文件类型保持 PNG/TIFF。单个 X/Z 平面没有完整 XYZ 测量资格。

## 验证入口与明确缺口

- 现场复核需只读使用真实样本，将输出和计时报告写入独立目录。复制耗时可能受系统缓存影响，评估 CT 或内存需在对应处理链实际测量。
- 16 位四通道可用于输入缓冲核对。当前编辑器的 `ImageSourceMetadata` 不接受 Rgba64。
- 实际屏幕色彩、校正文件、远程校正服务、采集硬件和数据库历史选择按现场条件分别验收。文件系统部分替换后的恢复需在目标环境核对。

理解协议可直接核对 `CVFileUtil` 和实际消费方；涉及屏幕、校正与真机时按现场环境另行验收。
