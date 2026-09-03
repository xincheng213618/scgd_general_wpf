---
knowledge_id: "projects.arvr-pro-demura"
knowledge_type: "reference"
status: "current"
summary: "ProjectARVRPro Demura 烧录的 PG TCP 连接、GECS 帧、配置默认值、逐步回包和故障定位；写入成功回包不等于光学效果验收。"
aliases: ["Demura", "Demura 烧录", "生成后自动烧录", "PG 烧录", "GecsProtocol", "DemuraProcess", "DemuraProcessConfig", "BurnAfterGenerate", "SENDFILE", "DemuraDynamic.bin"]
code_paths: ["Projects/ProjectARVRPro/Process/Demura/", "Projects/ProjectARVRPro/ARVRWindow.xaml.cs"]
test_paths: []
related: ["projects.arvr-pro", "operations.device-configuration"]
---

# Demura 烧录与 PG 通信

ProjectARVRPro 的 `DemuraProcess` 使用 PG 的 TCP 连接发送 GECS 指令，完成文件下发、确认上电、擦除和写入。本文用于配置烧录及定位通信故障；流程组、结果保存和项目装载见 [ProjectARVRPro](./project-arvr-pro.md)。

`SENDFILE` 帧携带源 bin 的完整路径和 PG 目标文件名，帧内没有 bin 原始内容。PG 如何访问该路径、是否另建文件传输通道，以及最终 FLASH 地址由 PG 服务或固件决定。

## 使用前提

- 已获授权对目标设备执行上电、FLASH 擦除和写入；手工调试前停止自动流程，避免两个客户端同时控制同一 PG 通道。
- 已配置持有 PG 地址、端口的通用传感器，且目标 PG 可连接。连接目标是 PG TCP 服务；ARVRPro 的 JSON Socket 服务处理项目事件，不能接收原生 GECS 帧。
- 已取得完整的 W128/W255 CSV 和打包的 Demura 工具资源。自动烧录位于工具准备链中：只有执行到 `PrepareDemuraToolAsync` 的烧录分支才会触发。
- 已核对源文件及 PG 对路径的访问方式。ColorVision 检查本地文件存在，不验证 PG 能否读取它，也不验证 bin 与本轮采集是否匹配。

`BurnAfterGenerate` 默认开启。即使本轮 bin 生成失败，工具准备链仍可能继续检查已存在的源文件并尝试烧录；不能把该开关理解为“仅本轮生成成功才烧录”。只准备文件时应关闭“生成后自动烧录”。

## PG 连接和文件位置

`FindGeneralSensor` 在 `DeviceSensor` 列表中先按 `GeneralSensorCode` 查找，未命中才按 `GeneralSensorCategory` 查找；两次比较均忽略大小写。读取找到的设备的 `Config.Addr` / `Config.Port`，由 `TcpClient` 建立新连接。烧录借用配置，不经传感器 MQTT 转发，也不关闭原有传感器服务。

工具工作目录为 `%LOCALAPPDATA%\ColorVision\ProjectARVRPro\DemuraTool`，默认源文件和 PG 目标文件名均为 `DemuraDynamic.bin`。

- `BurnSourceBinName` 为绝对路径时直接使用；为空时使用动态 bin；匹配静态、动态或合并 bin 的文件名时使用对应产物路径；其他相对名称拼到工具工作目录。
- `BurnTargetFileName` 为空或等于 `DemuraMerged.bin` 时，按有效兼容规则转换为 `DemuraDynamic.bin`，比较忽略大小写。
- 结果中的 `FlashAddress:0x00003000` 不随当前 GECS 指令发送，不能据此确认实际硬件写入地址。

## 烧录配置

在 Demura 流程实例的配置中查看“烧录配置”分类；默认值来自 `DemuraProcessConfig.cs`。

| 界面名称 / 配置项 | 默认值 | 含义 |
| --- | --- | --- |
| 生成后自动烧录 / `BurnAfterGenerate` | `true` | 到达工具准备链末尾时调用烧录 |
| 通用传感器Code / `GeneralSensorCode` | `DEV.Sensor.Default` | 优先查找条件 |
| 通用传感器Category / `GeneralSensorCategory` | `Sensor.Default` | Code 未命中时的查找条件 |
| 烧录源Bin / `BurnSourceBinName` | `DemuraDynamic.bin` | 本地源文件 |
| PG目标文件名 / `BurnTargetFileName` | `DemuraDynamic.bin` | PG 使用的文件名 |
| PG通道 / `BurnPgChannel` | `01` | 只作用于 `SENDFILE` |
| PG文件序号 / `BurnFileIndex` | `1` | `SENDFILE,START` 后的文件序号 |
| 成功回包关键字 / `BurnSuccessResponse` | `SENDFILE,END,OK` | 仅文件下发步骤的成功标识 |
| TCP连接超时ms / `BurnTcpConnectTimeoutMs` | `5000` | 建立 TCP 连接的等待上限 |
| TCP回包超时ms / `BurnTcpResponseTimeoutMs` | `60000` | 每条指令独立的回包等待上限 |

两项超时配置在运行时至少按 `1000 ms` 执行。`POWER` 和 `DEMURA` 指令固定使用通道 `1`；修改 `BurnPgChannel` 不会改变它们。需由 PG 协议提供方确认 `01` 与 `1` 是否代表同一通道。

## 指令顺序与完成条件

ColorVision 在同一连接中逐条发送并等待对应成功回包。只有当前步骤成功才继续，失败则停止后续烧录。

| 顺序 | Message Text | 成功回包包含的文本 |
| --- | --- | --- |
| 1. 下发文件 | `PG,{BurnPgChannel},SENDFILE,START,{BurnFileIndex},{源绝对路径},{目标文件名}` | `BurnSuccessResponse`，默认 `SENDFILE,END,OK` |
| 2. 查询电源 | `PG,1,POWER,STATE` | `POWER,STATE,ON` 或 `POWER,STATE,OFF` |
| 3. 仅 OFF 时上电 | `PG,1,POWER,ON` | `POWER,ON,END,OK` |
| 4. 擦除 FLASH | `PG,1,DEMURA,ERASE,START` | `DEMURA,ERASE,END,OK` |
| 5. 写入 | `PG,1,DEMURA,WRITE,START` | `DEMURA,WRITE,END,OK` |

全部适用步骤成功后，`BurnSucceeded=true`。正常完成保留电源状态。收到写入成功回包只代表这条通信链完成，仍需按现场方案验证实际烧录内容和光学效果。

### 回包和失败

`SendCommandAndWaitAsync` 累积收到的字节并按 UTF-8 解码，以忽略大小写的子串匹配判断回包；不会校验完整 GECS 帧、长度或命令关联。匹配当前步骤的成功关键字后返回成功；没有成功关键字时，`END,NG`、`FAIL` 或 `ERROR` 导致失败。

成功匹配在失败匹配之前。如果同一累计回包包含两者，当前实现会先返回成功。这与“任意失败标识都应阻止后续烧录”的期望存在差异，不能把混合或无关回包当成可靠的成功依据。

超时或对端关闭连接时返回失败。对端提前关闭也可能显示“等待回包超时”，应结合原始回包和 PG 端日志判断，不能只凭错误文字认定等待了完整超时时间。

### 失败后的电源状态

`SendBurnCommandAsync` 失败时停止后续指令，该方法内没有自动下电。外层流程若调用独立的 `ExecuteFailure`，会新建 TCP 连接发送 `PG,1,POWER,OFF`，并等待 `POWER,OFF,END,OK`。

因此，任意烧录失败都不能证明设备已下电。确认日志中的失败处理结果及 PG 实际状态；执行下电也不会恢复被擦除或部分写入的数据。

## GECS 帧格式

帧结构为 `STX + Network Number + Message Length + Message Text + ETX`，末尾不追加 CR/LF。

| 字段 | 编码 |
| --- | --- |
| STX | 原始字节 `0x02` |
| Network Number | 原始字节 `0xFF` |
| Message Length | `messageText.Length.ToString("X4")` 生成的十六进制 ASCII，至少四位 |
| Message Text | UTF-8 编码的指令正文 |
| ETX | 原始字节 `0x03` |

长度按 .NET 字符串长度计算，非 ASCII 路径的 UTF-8 字节数可能不同；`X4` 也不限制超长正文只占四位。与 PG 的长度解释是否兼容需单独确认，手工复现宜使用 ASCII 路径。

日志中的 `[02][FF]0010PG,1,POWER,STATE[03]` 是可读表示，实际字节为：

```text
02 FF 30 30 31 30 50 47 2C 31 2C 50 4F 57 45 52 2C 53 54 41 54 45 03
```

`[02]`、`[FF]`、`[03]` 各表示一个原始字节，不是方括号文本。

### 生成测试帧

下面的 PowerShell 仅打印帧和 HEX，不连接设备。按实际路径或上表指令修改 `$demuraMessage`，长度会随之计算；不要沿用旧长度头。

```powershell
$demuraMessage = 'PG,01,SENDFILE,START,1,C:\Demura\DemuraDynamic.bin,DemuraDynamic.bin'
$demuraLength = $demuraMessage.Length.ToString('X4')
$demuraPacket = [byte[]](
    @(0x02, 0xFF) +
    [System.Text.Encoding]::UTF8.GetBytes($demuraLength + $demuraMessage) +
    @(0x03)
)
"[02][FF]$demuraLength$demuraMessage[03]"
($demuraPacket | ForEach-Object { $_.ToString('X2') }) -join ' '
```

该示例的长度为 `0044`。只改变路径也会改变帧，不能把示例当作任意文件通用指令。

## 使用网络调试助手复现

在满足“使用前提”后执行以下步骤。这些步骤会控制真实设备，擦除和写入不可用来验证文档是否正确。

1. 选择 **TCP Client**，连接通用传感器配置中的 PG `Addr:Port`，发送格式选择 **HEX**，关闭自动追加 CR/LF。
2. 保持同一连接，按指令表顺序发送。先用实际源路径生成 `SENDFILE` 帧，不使用助手的“发送文件”功能发送 bin 原始字节。
3. 每条指令收到对应成功回包后再继续；电源为 ON 时跳过上电。不要把整组指令一次性连续粘贴发送。
4. 保存发送文本、HEX、回包及失败步骤。出现失败时停止，并核对 PG 状态；按获授权的现场方案决定是否下电和如何恢复。

## 故障定位

| 现象 / 错误 | 检查顺序 |
| --- | --- |
| 没有进入烧录 | 检查 `PrepareDemuraTool`、W128/W255 CSV、工具资源和 `BurnAfterGenerate`，再看工具准备日志 |
| `烧录源bin不存在` | 核对 `BurnSourceFile` 的最终路径、文件是否生成及当前进程读取权限 |
| `未找到通用传感器服务` | 核对 Code，再核对 Category 和已加载的 `DeviceSensor` |
| `通用传感器PG连接配置无效` / `连接PG超时` | 核对选中的设备、`Addr`、`Port` 和 PG 服务状态 |
| `PG指令失败(SendFile)` | 核对 PG 可访问的源路径、目标名、帧长度、通道和原始回包 |
| 擦除或写入失败 | 确认前序成功回包、固定通道 `1`、设备状态及 PG 错误日志；不要直接重发整组指令 |
| 显示成功但效果不符 | 核对 bin 来源与本轮采集、PG 实际写入内容和光学验收，不能只看 `BurnSucceeded` |

`DemuraTestResult` 保存实际源/目标、设备及连接信息、命令文本与 HEX、回包和烧录结果。实现入口是 `DemuraProcess.cs` 的 `BurnDemuraBinAsync` / `SendCommandAndWaitAsync`；组帧在 `GecsProtocol.cs`。

当前未登记 Demura 烧录链的专门自动化测试。源码可核验客户端组帧和判断顺序；PG 的文件读取、FLASH 实现、通道解释及设备效果需由 PG 协议和获授权的现场验证确认。
