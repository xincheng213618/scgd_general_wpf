# cvColorVision

ColorVision 的供应商 native 绑定层，通过 P/Invoke 和薄包装暴露相机、光谱仪、XYZ、OLED、图卡与源表等入口；不是纯托管视觉算法实现，也不负责设备服务、模板或宿主流程编排。

## 包与运行前提

- 当前目标为 `net10.0-windows7.0`，原生运行资产提供 Windows x64。框架、包版本、DLL 和配置资源清单以 `cvColorVision.csproj` 为准。
- 运行需要匹配的 `cvCamera.dll`、`cvoled.dll` 及所调用功能的供应商 runtime、驱动和配置；只有这两个 DLL 或只有托管程序集并不充分。项目从 `DLL/scgd_internal_dll/` 复制并打包现成输入，不在这里构建供应商 C++ 实现。
- Release 构建会检查高立通 x64 运行依赖是否齐全。托管构建或 NuGet 打包成功不证明设备可用、native ABI 全部兼容或许可证分支已验证。
- 句柄、缓冲区大小与释放顺序须按具体入口核对；接口混用 `int`、`bool`、`void`，不能套用统一的成功返回码，也不能把初始化状态码当新句柄。
- 连接、采集、校准、图卡切换及源表输出可能改变真实设备状态。文档示例和接口存在不构成操作授权；不得为验证文档而连接硬件或执行这些调用。

## 源码知识入口

- [cvColorVision 绑定契约](../../docs/04-api-reference/engine-components/cvColorVision.md)：当前源码入口、返回值边界、打包输入与外部许可证构建约定。
- [Engine 设备与服务](../../docs/04-api-reference/engine-components/device-service-chain.md)：设备资源和远端服务的装配职责。
- [原生集成与构建前提](../../docs/02-developer-guide/engine-development/opencv-integration.md)：native 依赖、工具链与部署边界。

本 README 会作为 NuGet 包说明放入包根目录；上述相对链接用于源码仓库，包内不保证包含 `docs/`。完整知识应在与包版本匹配的源码中读取，不把当前网站或另一分支的说明当作该包的行为保证。
