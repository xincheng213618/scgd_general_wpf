---
knowledge_id: "projects.lux-protocol"
knowledge_type: "reference"
status: "current"
summary: "ARVRPro 的 LUX TCP 文本入口：T0000 握手、VID、光学中心、光通量与 SocketCode 流程，以及回包和连接限制。"
aliases: ["LUX TCP 协议","ProjectLUX TCP 通讯协议手册","LuxSocketDispatcher","T0000","T0001","T0002","T0031","T00XX","H03AR","MachineNO","No SN","No right Code","No Dispatcher Hanle","FlowFailed","LUX 光通量","LUX VID"]
code_paths: ["Projects/ProjectARVRPro/Services/LuxSocketDispatcher.cs","Projects/ProjectARVRPro/Services/LuxDeviceMeasurements.cs","Projects/ProjectARVRPro/ARVRWindow.Lux.cs","Projects/ProjectARVRPro/Process/ProcessManager.cs","Projects/ProjectARVRPro/ProjectARVRProConfig.cs","UI/ColorVision.SocketProtocol/SocketManager.cs","UI/ColorVision.SocketProtocol/SocketTextDispatcher.cs","UI/ColorVision.SocketProtocol/SocketConfig.cs"]
test_paths: ["Test/ProjectARVRPro.Tests/LuxProtocolTests.cs","Test/ProjectARVRPro.Tests/LuxVidMeasurementTests.cs","Test/ProjectARVRPro.Tests/ProcessManagerPersistenceTests.cs","Test/ProjectARVRPro.Tests/SocketControlTests.cs"]
related: ["projects.arvr-pro","projects.arvr-pro-processes","projects.arvr-pro-protocol","ui.socket-protocol"]
---

# LUX TCP 通讯协议

外部产线控制程序通过 ARVRPro 的 `LuxSocketDispatcher` 发送文本命令并接收原 LUX 格式响应。现场安装 `ProjectARVRPro`，项目版本及宿主要求读取其 `.csproj` 与 manifest。旧独立 ProjectLUX 的源码、专属测试和协议说明通过仓库 `Projects/ARCHIVED.md` 的归档标签查阅，交付前用它核对差异；后续迭代在 Pro。

## 部署与配置

1. 在“网络通信 → 服务设置”启用 Socket Server，解析模式选择 **Text**。默认监听 `0.0.0.0:6666`，UTF-8；服务默认关闭。Pro JSON 客户端使用 Json 模式，两种模式按现场用途选择。
2. 在“流程处理配置 → 其他配置”填写 **LUX 协议设备号**（默认 `H03`，AR 光学中心站使用 `H03AR`）、VID 测量方式、限值和 K/B；设备号只组成回包前缀。
3. 普通流程在活动组的 **SocketCode** 列填写命令末两位，绑定实际 Flow 模板与 Pro 实例 Recipe。旧 LUX 配置不自动迁移，现场需重新配置；流程和配置存储见 [Pro 流程组](./project-arvr-pro-processes.md)。
4. 准备相机、光谱仪、Flow 服务和输出目录。普通流程、VID、光通量分别使用 Pro 的 CSV 输出目录生成 `C_<SN>.csv`、`B_<SN>.csv`、`D_<SN>.csv`。

共享文本分派器只调用首个发现的 handler，不按命令遍历项目。部署时核对实际接收者，避免同时加载其它文本项目；默认缓冲和协议模式见[通用 Socket 服务](../ui-components/ColorVision.SocketProtocol.md)。协议不提供 TLS 或应用层身份认证，需在受控产线网络中使用。

## 请求与消息边界

```text
T00XX,<SN>;
```

客户端使用大写 `T`、两位命令编号和非空 SN。SN 不含逗号、分号、换行或 Windows 文件名非法字符；它还用于导出文件名。服务端只检查 `T00` 前缀、取命令末两字符，并按第一个逗号分开 SN；宽松解析不构成额外命令保证。

服务端把一次 TCP Read 当成完整请求，不按分号拆包或缓存半包。保持一个长连接，串行完成一条命令后再发送下一条；不要主动拆分或粘连请求。**一次 Write 不保证对应一次完整 Read**，仍需在真实网络条件下核验分包与断线。

客户端也需缓存半包。VID/光通量的数值位于分号之后，末尾没有结束符；流程失败会在基础响应后追加文本。看到第一个分号不能证明完整响应已到达。可变长数值没有可靠的长度/终止标记，静默等待只是接收策略，截断或缺失值不能当成有效测量。

## 命令与响应

| 命令 | 执行与响应 |
| --- | --- |
| `T0000,SN001;` | 立即握手：`H0300,SN001,00;`。空闲时设置当前 SN，忙碌时不修改正在运行的 SN；不清空已有累计结果，窗口未创建时可直接回复 |
| `T0001,SN001;` | 按 VID 测量方式调用相机 `AutoFocus` 或 `GetPosition`，取设备转换后的 VID，再应用 K/B，输出 `B_` CSV；如 `H0301,SN001,00;123.45` |
| `T0031,SN001;` | 调用光谱仪 `GetData`，取得光谱结果时输出 `D_` CSV；如 `H0331,SN001,00;125.42` |
| `T0002,SN001;` | 按活动组 `SocketCode=02` 运行普通流程；设备号为 `H03AR` 时使用光学中心专用回包 |
| `T00XX,SN001;` | 运行活动组首个 `SocketCode=XX` 匹配项绑定的模板，完成后输出 `C_` CSV；如 `H0321,SN001,00;` |

`00`、`01`、`31` 有固定用途，不分配给普通流程；`H03AR` 的 `02` 对应光学中心。普通命令不检查步骤 `IsEnabled`，不会自动发送下一项 `SwitchPG` 或最终 JSON。模板按该步骤的实际绑定查找，不使用旧 LUX 的名称子串匹配。

普通回包为 `<MachineNO><XX>,<SN>,00;`。固定 `00` 不代表 Recipe PASS 或整机合格；步骤或模板缺失时也可能只返回此前缀。客户端结合结果界面、SQLite 和 CSV 判定业务结果，不能把回包当成完成全部测试的证据。

光学中心专用响应取当前光学中心结果的 Rotation、TiltX、TiltY，与步骤索引无关：

```text
H03AR02,SN001,1,2,3,00;
```

三个值有任一缺失时只返回 `H03AR02,SN001`，不能当作有效三轴结果。VID/光通量失败通常回复数值 `0`；光谱命令成功但无结果模型时也可能只有前缀，客户端需区分缺值和有效零值。光谱本地结果使用此次返回模型，服务结果仍按 MasterId 查询；设备执行方式由其后端决定。

### 错误与超时

| 响应或现象 | 含义与处理 |
| --- | --- |
| `No SN` | 命令或 SN 为空；补齐请求，不启动测量 |
| `No right Code X0000` | 命令不以 `T00` 开头；核对协议模式与命令 |
| `No Dispatcher Hanle` | 未找到文本处理器，保留宿主原错误拼写；核对项目加载和 handler 发现 |
| `H0321,SN001,00;FlowFailed:OverTime,Timeout;` | Flow 失败信息追加在原前缀后；事件和参数由执行结果决定 |
| `H03AR02,SN001FlowFailed:Failed,detail;` | 光学中心失败沿用专用前缀，不额外补分隔符 |
| 忙碌时无响应 | 新测量沿用原协议忽略，不返回 BUSY；握手仍可立即回复 |

客户端必须设置业务超时。超时后先查 Pro 执行状态、设备日志和结果记录，再决定重试；协议没有请求 ID 或跨重试去重保证。CSV 保存失败会记录日志，不能只凭 TCP 成功回复证明导出成功。

## 连接与异步边界

每个接受的测量命令固定自身的 SN、设备号、处理项和响应前缀，异步完成最多回包一次。LUX 测量与 JSON 的控制流程共用 `SocketControl.Current.Stream`；新 LUX 测量通过忙碌检查后才记录该连接，握手和被忽略的测量不接管旧任务连接。JSON 执行和推进命令沿用原连接更新规则，详见 [Pro 连接说明](./project-arvr-pro-protocol.md#建立连接)。

最终发送时读取当前 Stream，失败记录日志、不自动补发；这不是按连接隔离的多站并发协议。关闭窗口会取消本窗口设备等待，不能假定取消一定产生测量回包。

## 验证与交付

`LuxProtocolTests` 保护报文字面值、特殊分号位置、光学中心字段及单次异步回复；`LuxVidMeasurementTests` 检查 VID 测量方式；流程配置测试保护 SocketCode 的复制、保存重载及活动组查找；`SocketControlTests` 验证回环连接归属。构建与测试命令使用 [ARVRPro 的本地入口](./project-arvr-pro.md#本地构建与测试)。

现场仍需串行验证握手、普通流程、`H03AR`、两种 VID 方式和光通量，核对 CSV、测量值、Recipe 判定、失败回包、忙碌超时与断线恢复。真实设备与产线操作须在授权范围内执行；离线和回环测试不能替代业务交付验收。
