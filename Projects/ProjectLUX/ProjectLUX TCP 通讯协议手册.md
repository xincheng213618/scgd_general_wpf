# ProjectLUX TCP 通讯协议手册

完整协议统一维护在 [LUX TCP 通讯协议](../../docs/04-api-reference/projects/project-lux-protocol.md)，包含握手、VID、光学中心、光通量、SocketCode 流程、异常响应、消息边界与联调示例。

服务端需要加载 ProjectLUX，在 ColorVision 中启用 Socket Server 并选择 Text 模式（默认端口 6666）。联机命令会改变当前 SN 或触发真实设备测试，应使用与项目包版本对应的仓库文档，并按现场授权操作。

Flow、Recipe/Fix 与结果保存位置见 [ProjectLUX 配置](../../docs/04-api-reference/projects/project-lux.md)。
