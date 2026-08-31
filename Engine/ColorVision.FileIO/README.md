# ColorVision.FileIO

ColorVision 专有二进制图像格式的纯托管读写库。目标框架为 .NET Framework 4.6.1、.NET 6、.NET 8 和 .NET 10，平台 AnyCPU；以本目录 `ColorVision.FileIO.csproj` 为准。

## 源码入口

- `CVFileUtil.cs`：`CVCIE` 魔数、版本 header、payload、通道读取和写回。
- `CVCIEFile.cs`：尺寸、曝光、源文件名和 Data 载体，以及 `CVType`。

本库没有 `CVRawFile`、`FileValidator`、通用 JSON/YAML 处理器、压缩/批量框架或异步 `LoadAsync` / `SaveAsync` API。标准图片解码和显示转换由消费方承担。

## 先选对读取语义

- `CVFileUtil.Read` 读取当前文件的内嵌 payload。
- `ReadCVCIE` 可能优先读取 `SrcFileName` 指向的关联源文件，不保证返回内嵌 XYZ。
- `ReadCIEFileChannel` 同步读取一个零基通道平面，不跟随关联源文件；Data 只有一个通道，但 Channels/Exp 保留原 header。在检查点观察到取消时抛出 `OperationCanceledException`；header 失败可能先返回 false，详见完整契约。
- `CVCIEFile.Dispose` 清除自身 Data/Exp 引用，不关闭消费方对象或强制回收其持有的数组。

`WriteCIEFile` 返回成功不等于格式验证成功：路径重载直接覆盖目标，没有原子替换/回滚；当前 v3 writer 未写 reader 所需的 NDPort。不要将单通道读取结果当作完整多通道文件直接写回。

完整布局、失败语义、消费方与测试只维护在仓库的[CV 文件契约](../../docs/04-api-reference/engine-components/ColorVision.FileIO.md)中。该链接用于源码仓库；从 NuGet 单独取得此包时，请查对应源码版本的主题和测试，不将最新分支当作已安装版本保证。

## 本地构建

从仓库根目录执行；仅构建本地库，不发布 NuGet：

```powershell
dotnet build .\Engine\ColorVision.FileIO\ColorVision.FileIO.csproj -p:Platform=AnyCPU
```
