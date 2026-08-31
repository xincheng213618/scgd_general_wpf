# ColorVision.Core

ColorVision 的原生图像/视频互操作与 WPF 位图桥接层，不是高层图像编辑框架。`HImage` 是包含非托管指针的值类型；`OpenCVMediaHelper` 和 `OpenCVCuda` 提供 native 调用，`ImageCompute` 当前负责 Fusion 的 CPU/CUDA 分流。

## 包与运行前提

- 当前目标为 `net8.0-windows7.0;net10.0-windows7.0`，原生运行资产仅提供 Windows x64；准确输入及版本以 `ColorVision.Core.csproj` 为准。
- `opencv_helper.dll`、OpenCV runtime 与所调用的 CUDA 依赖须能被宿主加载；托管程序集可引用不等于原生入口可用。
- 仓库构建会按条件复用 helper DLL，缺失时加入 C++ 项目引用，不能承诺只装 .NET SDK 就可首次构建。当前打包输入仍无条件包含 `opencv_cuda.dll`；运行时不选 CUDA 不代表构建时可缺少它。
- 使用 `HImage` 时明确缓冲区所有权、分配/释放约定及 WPF 线程边界。`NativeLogBridge` 默认不启用日志捕获，订阅事件不等于原生来源已就绪。

## 源码知识入口

- [Core 互操作契约](../../docs/04-api-reference/ui-components/ColorVision.Core.md)：数据结构、调用边界、原生日志与验证入口。
- [原生集成与构建前提](../../docs/02-developer-guide/engine-development/opencv-integration.md)：DLL 选择、native 工具链与部署边界。
- [源图像帧与内存生命周期](../../docs/04-api-reference/ui-components/image-frame-lifetime.md)：借用/复制、租约释放、revision 失效与位图转换的输入释放责任。
- [景深融合](../../docs/04-api-reference/ui-components/image-fusion.md)：Auto/CPU/GPU 分流和调用失败语义。

本 README 会作为 NuGet 包说明打包到包根目录；上述相对链接用于源码仓库，包内不保证包含 `docs/`。需要完整契约时读取与包版本匹配的源码，不把另一版本的文档当成当前包的保证。
