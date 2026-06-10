# OpenCV 和 native 集成开发交接手册

本页说明当前仓库里 OpenCV/native 能力的真实边界。Engine 侧有 `cvColorVision` 这种设备 SDK / 算法 DLL 绑定层，UI/Core 侧有 `opencv_helper.dll` / `opencv_cuda.dll` 的 P/Invoke 包装，文件打开链路还包含 `.cvraw` / `.cvcie` 解析和缩略图。

## 当前分层

| 层级 | 目录或文件 | 职责 |
| --- | --- | --- |
| 设备 SDK 绑定 | `Engine/cvColorVision/` | 相机、光谱仪、传感器、OLED 算法、MQTTMessageLib 数据类型和 native DLL 入口 |
| UI/Core native 包装 | `UI/ColorVision.Core/` | `HImage`、`OpenCVMediaHelper`、`OpenCVCuda`、`ImageCompute`、native 日志桥 |
| 文件解析和展示 | `Engine/ColorVision.Engine/Media/` | `.cvraw`、`.cvcie` 打开、缩略图、CIE 导出、鼠标探针和图像工具 |
| 测试工程 | `Test/opencv_helper_test/` | C++ 验证工程，当前重点验证 `M_FindLuminousArea` |
| 文档入口 | [cvColorVision](../../04-api-reference/engine-components/cvColorVision.md)、[ColorVision.Core](../../04-api-reference/ui-components/ColorVision.Core.md) | 模块边界和 DLL 发布注意事项 |

## 什么时候改哪一层

| 需求 | 首选落点 |
| --- | --- |
| 新增相机、光谱仪、传感器 SDK 导出 | `Engine/cvColorVision/` 对应 wrapper |
| 新增图像处理函数给 WPF 调用 | `UI/ColorVision.Core/OpenCVMediaHelper.cs` 或 `OpenCVCuda.cs` |
| 新增 `.cvraw` / `.cvcie` 打开或缩略图行为 | `Engine/ColorVision.Engine/Media/` |
| 调整亮区、伪彩、SFR、白平衡等 helper 行为 | native `opencv_helper.dll` 和 `UI/ColorVision.Core` 签名一起核对 |
| 调整 CUDA 融合 | `opencv_cuda.dll`、`OpenCVCuda`、`ImageCompute` |
| 验证 native helper | `Test/opencv_helper_test/` |

## P/Invoke 维护规则

- C# 签名必须和 native 导出保持一致，包括 calling convention、字符串编码、结构体布局和内存释放方式。
- `HImage` 带 native buffer，调用失败时要释放已经分配的输出，避免内存泄漏。
- 返回 `IntPtr` 字符串的 helper 要确认是否需要调用 `FreeResult()`。
- x64 是主交付目标，native DLL、测试工程和主程序平台要一致。
- `opencv_helper.dll`、`opencv_cuda.dll`、OpenCV runtime 和项目输出目录要一起验证。
- 不要把 `cvColorVision` 写成纯托管算法库，它主要是 native 能力绑定层和消息数据类型集合。

## `.cvraw` / `.cvcie` 链路

| 入口 | 说明 |
| --- | --- |
| `FileCVCIE` | 读取 CIE/RAW 文件头和图像数据 |
| `CVRawOpen` | 在图像编辑器中打开 `.cvraw`，提供 CIE 探针和图形工具 |
| `CVRawThumbnailProvider` | 为 `.cvraw` / `.cvcie` 生成缩略图 |
| `ColorVision.ShellExtension` | Windows Explorer 缩略图扩展，独立打包和注册 |

修改文件格式时，要同时验证主程序打开、缩略图、导出、ShellExtension 和旧文件兼容。

## 验证命令

```powershell
dotnet build UI/ColorVision.Core/ColorVision.Core.csproj -c Release -p:Platform=x64
dotnet build Engine/cvColorVision/cvColorVision.csproj -c Release -p:Platform=x64
msbuild Test/opencv_helper_test/opencv_helper_test.vcxproj /p:Configuration=Debug /p:Platform=x64
Test/opencv_helper_test/build_test_find_luminous.bat
```

如果当前机器没有 Visual Studio C++ 或 OpenCV native 依赖，至少要记录无法执行的原因，并在交接记录里说明由哪台构建机补验。

## 验收清单

| 项目 | 验收方式 |
| --- | --- |
| P/Invoke 签名 | Debug/Release x64 都能加载 DLL，没有 `BadImageFormatException` 或入口点缺失 |
| 内存 | 连续处理多张图，进程内存不会持续单向增长 |
| 图像结果 | 输出尺寸、通道、位深、stride 和颜色顺序正确 |
| 文件打开 | `.cvraw` / `.cvcie` 能打开、缩略图能生成、旧文件不崩溃 |
| 算法 helper | `M_FindLuminousArea` 等 native 测试通过，错误码和结果 JSON 可解释 |
| 打包 | 主程序、插件或项目包输出里包含需要的 native DLL 和 runtime |

## 相关文档

- [cvColorVision](../../04-api-reference/engine-components/cvColorVision.md)
- [ColorVision.Core](../../04-api-reference/ui-components/ColorVision.Core.md)
- [ColorVision.ShellExtension](../../04-api-reference/engine-components/ColorVision.ShellExtension.md)
- [测试与验证交接手册](../testing.md)
