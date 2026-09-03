# Demura 烧录指令说明

`DemuraProcess` 直连通用传感器配置中的 PG TCP 地址，使用 GECS 命令完成文件下发、上电确认、FLASH 擦除和写入。运行前须确认目标设备、源 bin 与设备操作授权；`BurnAfterGenerate` 默认开启。

[Demura 烧录与 PG 通信](../../../../docs/04-api-reference/projects/project-arvr-pro-demura.md)集中说明配置默认值、帧格式、成功与失败回包、HEX 生成、手工复现和故障定位。完整说明需在匹配版本的源码仓库或文档站点查看。

实现入口：

- `DemuraProcess.cs`：工具准备、源文件选择、PG 连接、逐步回包及失败处理。
- `GecsProtocol.cs`：指令和帧编码。
- `DemuraProcessConfig.cs`：配置与默认值。
- `DemuraTestResult.cs`：发送命令、回包和结果记录。
