# ColorVision.FileIO

本页只描述当前仓库里真实可用的 `ColorVision.FileIO` 模块，不再继续维护“通用文件框架 + JSON/YAML/批处理平台”式旧稿。

## 先看这个模块现在是什么

按当前源码状态，`ColorVision.FileIO` 不是一个泛化的文件处理框架，而是一个围绕 ColorVision 自定义图像文件格式组织起来的专用 I/O 模块。当前最清晰的落点是：

- 识别和读取 `CVCIE` / `CVRAW` / `CVSRC` 类文件。
- 解析文件头、版本、曝光、尺寸、通道和原始数据区。
- 按通道或文件类型打开本地 ColorVision 图像文件。
- 用 `CVCIEFile` 作为最核心的数据载体。

因此它比旧文档里描述的“支持通用 JSON/XML/YAML/图像批处理”要窄得多，也更贴近当前实际代码。

## 当前最关键的文件

- `Engine/ColorVision.FileIO/CVFileUtil.cs`
- `Engine/ColorVision.FileIO/CVCIEFile.cs`

至少从当前模块的可读实现看，这两处已经覆盖了最核心的格式识别、头部解析和数据载体定义。

## 当前真正处理的是什么格式

`CVCIEFile.cs` 当前定义的 `CVType` 包括：

- `Raw`
- `Src`
- `CIE`
- `Calibration`
- `Tif`
- `Dat`

但从 `CVFileUtil` 当前最密集的实现来看，真正被重点展开的是 `CVCIE` / `CVRAW` 这一组 ColorVision 自定义图像文件，而不是通用办公或配置文件格式。

## 当前读取链怎么工作

`CVFileUtil` 当前的核心流程大致是：

1. 用文件头判断是不是 `CVCIE` 体系文件。
2. 读取 header 与 version。
3. 按不同版本解析文件名、增益、通道数、曝光、尺寸等元数据。
4. 再按数据区偏移读取原始字节块。
5. 把这些信息收敛到 `CVCIEFile`。

它当前同时支持：

- 从文件路径读取
- 从字节数组读取

并且大量细节都围绕“防止越界、异常、OOM、无效头部”展开，这些都是典型的格式解析代码，而不是通用文件服务层。

## `CVCIEFile` 当前承担什么角色

`CVCIEFile` 现在是模块里最核心的数据结构，负责承载：

- 文件版本
- 文件类型
- 行列与位深
- 通道数
- 增益与曝光数组
- 源文件名
- 原始数据字节
- 文件路径

它本身还实现了 `IDisposable`，在释放时会主动清空大块数据数组。这再次说明当前模块主要面对的是大体积图像数据，而不是轻量文本配置。

## 当前有哪些实用入口

从 `CVFileUtil` 当前实现看，比较关键的入口包括：

- `IsCIEFile(...)`
- `IsCVCIEFile(...)`
- `ReadCIEFileHeader(...)`
- `ReadCIEFileData(...)`
- `Read(...)`
- `OpenLocalFileChannel(...)`
- `OpenLocalCVFile(...)`

这些入口基本都围绕两个目标：

- 判定文件是不是 ColorVision 专有格式
- 把专有格式安全地解析成内存对象

## 当前几个最容易写错的点

### 它不是泛化的文件系统中台

当前源码没有实现旧文档里那套通用 `FileIOManager`、JSON 处理器、YAML 处理器、批量执行器等完整体系。继续沿用那套写法，会把并不存在的抽象层写成事实。

### 核心对象是二进制图像载体，不是文本配置模型

`CVCIEFile` 当前主要承载的是图像元数据和大块原始字节数组。这和旧稿里偏重配置、压缩、序列化服务的叙述完全不是一个重心。

### 版本分支是实现重点

`ReadCIEFileHeader(...)` 对不同 version 的分支解析是当前真实复杂度来源之一。文档如果只写“读取一个自定义格式文件”，会把这层细节抹掉。

### 安全性主要体现在防御式读取

当前代码大量检查文件长度、偏移、数组分配和异常，而不是实现所谓的“热更新型异步文件框架”。理解这一点，才能看懂为什么代码重心在 header/data 解析上。

## 推荐阅读顺序

1. `Engine/ColorVision.FileIO/CVCIEFile.cs`
2. `Engine/ColorVision.FileIO/CVFileUtil.cs`

先看数据载体，再看具体解析逻辑，会比先翻旧文档有效得多。

## 继续阅读

- [docs/04-api-reference/engine-components/cvColorVision.md](./cvColorVision.md)
- [docs/04-api-reference/engine-components/ColorVision.Engine.md](./ColorVision.Engine.md)
- [docs/03-architecture/overview/system-overview.md](../../03-architecture/overview/system-overview.md)