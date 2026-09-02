# Spectrum Socket API

此目录实现 Spectrum 的五个业务 handler，复用 `ColorVision.SocketProtocol`，不是独立的 Socket 服务或另一套传输协议。宿主装载 Spectrum 与独立 WPF 入口均使用该公共模块。

- [Spectrum Socket 业务契约](../../../docs/04-api-reference/plugins/standard-plugins/spectrum-socket.md)：`SpectrumStatus`、`SpectrumConnect`、`SpectrumDarkCalibration`、`SpectrumAutoIntTime`、`SpectrumMeasure` 的参数、返回字段、设备锁与取消。
- [公共 Socket 传输契约](../../../docs/04-api-reference/ui-components/ColorVision.SocketProtocol.md)：监听、JSON 分发、报文边界、发送记录和重发。
- [Spectrum 设备与标定](../../../docs/04-api-reference/plugins/standard-plugins/spectrum.md)：Manager、标定就绪、结果持久化和运行依赖。

运行需匹配的 Windows/x64 环境、Spectrum 及公共库、原生设备 DLL/驱动、许可证和标定文件；服务必须已启用且选择 JSON 模式，实际端口以运行配置为准。编译出 handler 不等于外部请求可达。

接入时先用 `SpectrumStatus` 确认业务 handler 可达，再按[接入步骤与设备门禁](../../../docs/04-api-reference/plugins/standard-plugins/spectrum-socket.md#开始接入)连接和测量。设备操作须有现场授权；30/60 秒取消源不是原生调用的硬截止。

相对知识链接仅适用于匹配版本的完整源码仓库；单独复制此目录或交付包不保证包含 `docs/`。完整字段与行为只在上述权威主题维护。
