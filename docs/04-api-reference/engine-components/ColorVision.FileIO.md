# ColorVision.FileIO

`Engine/ColorVision.FileIO/` 是 ColorVision 专有图像文件 I/O 模块，重点处理 `CVCIE` / `CVRAW` / `CVSRC` 文件的识别、头部解析、数据区读取、通道打开和写回。它不是通用 JSON/YAML/批处理文件框架。

## 先查什么

| 现象 | 第一检查点 |
| --- | --- |
| 文件存在但打不开 | 文件头是否满足 `IsCVCIEFile(...)`，header 长度是否够当前 version |
| 只读到部分图像 | `rows * cols * bpp * channels` 推导长度和真实数据区长度 |
| 多通道错位 | `OpenLocalFileChannel(...)` 通道索引和 `Buffer.BlockCopy` 偏移 |
| 大文件卡死/崩溃 | 数组分配、OOM 捕获、文件长度防御 |
| 写出的文件打不开 | 先用 `ReadCIEFileHeader(...)` 验证 header，再看数据区长度 |

## 当前能力

| 能力 | 当前入口 | 说明 |
| --- | --- | --- |
| 格式识别 | `IsCIEFile(...)`、`IsCVCIEFile(...)` | 判断是否为 ColorVision 专有格式 |
| Header 解析 | `ReadCIEFileHeader(...)` | 读取 version、rows、cols、bpp、channels、gain、Exp、文件名 |
| 数据区读取 | `ReadCIEFileData(...)`、`Read(...)` | 按偏移和尺寸读取原始字节 |
| 通道打开 | `OpenLocalFileChannel(...)` | 从多通道文件取指定通道 |
| 全文件打开 | `OpenLocalCVFile(...)` | 处理 CVCIE、CVRAW、CVSRC 分支 |
| 文件写回 | `WriteCIEFile(...)` | 把 `CVCIEFile` 或图像字节写回 CVCIE |

## 数据载体

`CVCIEFile` 是核心对象，承载：

| 字段类型 | 内容 |
| --- | --- |
| 元数据 | version、`CVType`、rows、cols、bpp、channels、gain、曝光数组、源文件名、路径 |
| 数据 | 原始图像字节数组 |
| 资源释放 | `Dispose()` 会清空大块 `Data` 和 `Exp` |

当前 `CVType` 包括 `Raw`、`Src`、`CIE`、`Calibration`、`Tif`、`Dat`，但实现重点仍是 CVCIE/CVRAW 这一组专有图像格式。

## 验收

| 验收项 | 通过标准 |
| --- | --- |
| 格式识别 | 有效 CVCIE 返回 true，空/短/错误头文件返回 false |
| Header | 不同 version 的尺寸、通道、曝光和文件名解析正确 |
| 数据区 | `Data` 长度与 header 推导尺寸一致，异常文件不越界 |
| 通道拆分 | 多通道可取指定通道，单通道不重复切分 |
| 全文件打开 | CVCIE、CVRAW、CVSRC 返回正确 `CVType` |
| 写回 | 写出的文件可再次读回，关键元数据不丢失 |
| 释放 | 批量读取后大块数组能释放 |

## 变更边界

| 变更类型 | 是否改这里 |
| --- | --- |
| CVCIE/CVRAW 文件头、版本、曝光、通道、数据偏移变化 | 是 |
| 结果图、伪彩、叠加层显示 | 通常看 `ColorVision.ImageEditor` 和结果展示链 |
| XYZ/CCT 计算 | 通常看 `cvColorVision`、模板和设备服务 |
| 批量导入导出菜单 | 通常看 UI 或业务窗口 |
| 客户项目结果字段 | 通常看项目、模板、结果模型 |

## 边界

- 当前没有通用 `FileIOManager`、JSON/YAML 处理器或批量执行器体系。
- 核心对象是二进制图像载体，不是文本配置模型。
- version 分支和防御式读取是实现重点。

## 关键文件

| 任务 | 先看 |
| --- | --- |
| 数据载体 | `Engine/ColorVision.FileIO/CVCIEFile.cs` |
| 解析和写回 | `Engine/ColorVision.FileIO/CVFileUtil.cs` |
