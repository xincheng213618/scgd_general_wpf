# Demura 烧录指令说明

> 适用范围：`ProjectARVRPro.Process.Demura` 当前代码<br>
> 整理日期：2026-07-28<br>
> 协议：PG GECS V2.4 风格数据帧

## 1. 结论

Demura 烧录不是把 `.bin` 文件内容直接写到串口，也不是通过 ARVRPRO 的 JSON Socket 端口转发。

当前实现会：

1. 从“通用传感器”服务配置中取得 PG 的 IP 地址和 TCP 端口；
2. 新建一条到 PG 的 TCP 连接；
3. 在同一条 TCP 连接上按 GECS 格式逐条发送：
   `SENDFILE` → `POWER,STATE` → 必要时 `POWER,ON` → `DEMURA,ERASE` → `DEMURA,WRITE`；
4. 每发送一条指令都等待对应成功回包，成功后才发送下一条；
5. 任一步失败或超时都停止后续烧录。

第一条 `SENDFILE` 发送的是“源 bin 的完整文件路径和 PG 目标文件名”，网络帧中不包含 bin 文件的原始二进制内容。

因此现场调试应使用支持 **TCP Client + HEX 发送** 的网络调试助手。只有 COM 串口模式的“串口助手”不能直接复现当前 ColorVision 的发送方式；也不能使用助手的“发送文件”功能把 bin 原始字节直接发给 PG。

## 2. 代码调用链

```text
DemuraProcess.Execute
└─ ExecuteCoreAsync
   └─ PrepareDemuraToolAsync
      ├─ 生成 DemuraStatic.bin
      ├─ 生成 DemuraDynamic.bin
      ├─ 生成 DemuraMerged.bin
      └─ BurnAfterGenerate = true
         └─ BurnDemuraBinAsync
            ├─ ResolveBurnSourceFile
            ├─ FindGeneralSensor
            └─ SendBurnCommandAsync
               ├─ SENDFILE
               ├─ POWER,STATE
               ├─ POWER,ON（仅状态为 OFF 时）
               ├─ DEMURA,ERASE
               └─ DEMURA,WRITE
```

主要代码位置：

| 文件 | 作用 |
| --- | --- |
| `DemuraProcess.cs` | 触发烧录、建立 TCP 连接、逐条发送并等待回包 |
| `GecsProtocol.cs` | 定义 GECS 指令、帧格式和成功回包关键字 |
| `DemuraProcessConfig.cs` | 烧录开关、PG 服务、源文件、通道和超时配置 |
| `DemuraTestResult.cs` | 保存实际发送命令、HEX、回包和烧录结果 |

## 3. PG 地址和端口从哪里来

烧录代码不使用固定 IP 和端口，而是查找 `DeviceSensor`：

1. 优先按 `GeneralSensorCode` 查找，默认 `DEV.Sensor.Default`；
2. 如果未找到，再按 `GeneralSensorCategory` 查找，默认 `Sensor.Default`；
3. 读取该服务的 `Config.Addr` 和 `Config.Port`；
4. 用 `TcpClient` 直接连接 `Addr:Port`。

这里只借用了通用传感器中的 PG 地址和端口。烧录代码不会通过传感器 MQTT 服务转发命令，也不会为了烧录关闭已有的通用传感器服务。

注意：原生 GECS 帧应发往这里查到的 **PG TCP 地址和端口**，不能发往 ARVRPRO 的 JSON Socket 端口。ARVRPRO 的 `PGPassThroughGECS` 外部接口当前也不允许转发 `SENDFILE` 和 `DEMURA` 指令。

## 4. 默认 bin 文件

Demura 工具工作目录为：

```text
%LOCALAPPDATA%\ColorVision\ProjectARVRPro\DemuraTool
```

默认烧录源文件为：

```text
%LOCALAPPDATA%\ColorVision\ProjectARVRPro\DemuraTool\DemuraDynamic.bin
```

默认 PG 目标文件名也是：

```text
DemuraDynamic.bin
```

`SENDFILE` 前，ColorVision 会先检查源文件是否存在。`BurnSourceBinName` 也可以配置为绝对路径，或配置为工作目录内的其他文件名。

如果旧配置中的目标文件名仍为 `DemuraMerged.bin`，当前代码会把它兼容转换为 `DemuraDynamic.bin`。结果界面可能显示 `FlashAddress:0x00003000`，但当前 GECS 指令没有发送这个地址；实际写入位置由 PG 端的 `DEMURA,WRITE,START` 实现决定。

## 5. GECS 数据帧格式

每条指令的实际字节结构为：

```text
STX + Network Number + Message Length + Message Text + ETX
```

| 字段 | 长度 | 当前值/规则 |
| --- | ---: | --- |
| STX | 1 byte | `0x02` |
| Network Number | 1 byte | 固定 `0xFF` |
| Message Length | 4 bytes | Message Text 长度的四位大写十六进制 ASCII |
| Message Text | 可变 | 指令正文，使用 UTF-8 编码 |
| ETX | 1 byte | `0x03` |

代码和日志中的：

```text
[02][FF]0010PG,1,POWER,STATE[03]
```

只是便于阅读的显示方式。实际发送的是：

```text
02 FF 30 30 31 30 50 47 2C 31 2C 50 4F 57 45 52 2C 53 54 41 54 45 03
```

其中：

- `[02]` 是一个原始字节 `0x02`，不是字符 `[`、`0`、`2`、`]`；
- `[FF]` 是一个原始字节 `0xFF`，不是 ASCII 字符 `F``F`；
- `0010` 是四个 ASCII 字节 `30 30 31 30`；
- `[03]` 是一个原始字节 `0x03`；
- 帧尾不追加 CR、LF 或 `\r\n`。

当前代码用 `messageText.Length.ToString("X4")` 计算长度。指令和路径全部为 ASCII 时，字符数与 UTF-8 字节数相同。现场手工测试建议使用纯 ASCII 路径，避免 PG 端对非 ASCII 长度的解释不一致。

## 6. 当前烧录顺序

### 6.1 第一步：下发文件

Message Text：

```text
PG,{BurnPgChannel},SENDFILE,START,{BurnFileIndex},{源文件绝对路径},{PG目标文件名}
```

默认参数：

```text
BurnPgChannel = 01
BurnFileIndex = 1
PG目标文件名 = DemuraDynamic.bin
```

默认成功判定：回包文本中包含：

```text
SENDFILE,END,OK
```

例如源文件为 `C:\Demura\DemuraDynamic.bin` 时：

```text
Message Text:
PG,01,SENDFILE,START,1,C:\Demura\DemuraDynamic.bin,DemuraDynamic.bin

Message Length:
68（十进制）= 0044（十六进制）

显示帧:
[02][FF]0044PG,01,SENDFILE,START,1,C:\Demura\DemuraDynamic.bin,DemuraDynamic.bin[03]

实际 HEX:
02 FF 30 30 34 34 50 47 2C 30 31 2C 53 45 4E 44 46 49 4C 45 2C 53 54 41 52 54 2C 31 2C 43 3A 5C 44 65 6D 75 72 61 5C 44 65 6D 75 72 61 44 79 6E 61 6D 69 63 2E 62 69 6E 2C 44 65 6D 75 72 61 44 79 6E 61 6D 69 63 2E 62 69 6E 03
```

注意：修改源路径、文件序号、通道或目标文件名后，`Message Length` 必须重新计算，不能继续使用示例中的 `0044`。

### 6.2 第二步：查询上电状态

当前代码固定使用通道 `1`：

```text
Message Text:
PG,1,POWER,STATE

显示帧:
[02][FF]0010PG,1,POWER,STATE[03]

实际 HEX:
02 FF 30 30 31 30 50 47 2C 31 2C 50 4F 57 45 52 2C 53 54 41 54 45 03
```

以下任一回包关键字都表示本步通信成功：

```text
POWER,STATE,ON
POWER,STATE,OFF
```

如果无法从回包中解析出 ON 或 OFF，烧录立即失败。

### 6.3 第三步：必要时上电

当状态为 `ON` 时跳过本步。

当状态为 `OFF` 时发送：

```text
Message Text:
PG,1,POWER,ON

显示帧:
[02][FF]000DPG,1,POWER,ON[03]

实际 HEX:
02 FF 30 30 30 44 50 47 2C 31 2C 50 4F 57 45 52 2C 4F 4E 03
```

成功回包关键字：

```text
POWER,ON,END,OK
```

### 6.4 第四步：擦除 FLASH

```text
Message Text:
PG,1,DEMURA,ERASE,START

显示帧:
[02][FF]0017PG,1,DEMURA,ERASE,START[03]

实际 HEX:
02 FF 30 30 31 37 50 47 2C 31 2C 44 45 4D 55 52 41 2C 45 52 41 53 45 2C 53 54 41 52 54 03
```

成功回包关键字：

```text
DEMURA,ERASE,END,OK
```

### 6.5 第五步：写入

```text
Message Text:
PG,1,DEMURA,WRITE,START

显示帧:
[02][FF]0017PG,1,DEMURA,WRITE,START[03]

实际 HEX:
02 FF 30 30 31 37 50 47 2C 31 2C 44 45 4D 55 52 41 2C 57 52 49 54 45 2C 53 54 41 52 54 03
```

成功回包关键字：

```text
DEMURA,WRITE,END,OK
```

只有 `SENDFILE`、电源状态确认、必要的上电、擦除和写入全部收到成功回包，ColorVision 才将本次烧录判为成功。

## 7. 通道号的当前实现差异

当前代码中有一个需要现场特别注意的细节：

- `SENDFILE` 使用可配置的 `BurnPgChannel`，默认是 `01`；
- `POWER,STATE`、`POWER,ON`、`POWER,OFF`、`DEMURA,ERASE` 和 `DEMURA,WRITE` 固定使用 `1`。

也就是说，默认完整流程会同时出现 `PG,01,...` 和 `PG,1,...`。如果 PG 端不把 `01` 与 `1` 视为同一通道，需要先与 PG 协议提供方确认，不能只修改 `BurnPgChannel` 后假设其他指令也会一起改变。

## 8. 回包、超时和失败判定

### 8.1 逐条握手

ColorVision 在同一条 TCP 连接中执行以下循环：

1. 写入一条完整 GECS 帧；
2. 刷新网络流；
3. 累积读取 PG 回包；
4. 找到本步成功关键字后进入下一步。

代码不要求成功关键字等于整个回包，只要求回包文本中包含对应关键字。因此带有 STX、长度头、通道等其他内容的完整 GECS 回包也可以被识别。

### 8.2 失败关键字

任一回包中包含下列内容时，本步立即判失败：

```text
END,NG
FAIL
ERROR
```

### 8.3 超时

默认配置：

| 配置 | 默认值 | 用途 |
| --- | ---: | --- |
| `BurnTcpConnectTimeoutMs` | 5000 ms | 连接 PG |
| `BurnTcpResponseTimeoutMs` | 60000 ms | 每一条指令单独等待回包 |

程序会把小于 1000 ms 的配置按 1000 ms 执行。

### 8.4 下电行为

正常烧录完成后不会发送 `POWER,OFF`。

烧录步骤自身发生失败时，`SendBurnCommandAsync` 也只是停止后续步骤，不会在该方法内自动下电。另有一条独立的 Demura 流程失败处理入口，在外层流程调用 `ExecuteFailure` 时会新建 TCP 连接并发送：

```text
[02][FF]000EPG,1,POWER,OFF[03]
```

实际 HEX：

```text
02 FF 30 30 30 45 50 47 2C 31 2C 50 4F 57 45 52 2C 4F 46 46 03
```

成功回包关键字：

```text
POWER,OFF,END,OK
```

现场调试时不要假设任意烧录失败后 ColorVision 都一定已经自动下电，应根据实际日志和 PG 状态确认。

## 9. 用网络调试助手复现

### 9.1 准备

1. 在 ColorVision 的通用传感器设置中确认实际 PG `Addr` 和 `Port`；
2. 确认要烧录的 `DemuraDynamic.bin` 已生成；
3. 确认 PG 服务能够解析 `SENDFILE` 中的完整路径；
4. 停止自动流程，避免 ColorVision 与调试助手同时向同一 PG 通道发指令；
5. 确认设备处于允许擦除和烧录的安全状态。

### 9.2 助手设置

```text
模式：TCP Client
远端：通用传感器配置中的 Addr:Port
发送格式：HEX
自动追加 CR/LF：关闭
连接方式：整个烧录过程保持同一连接
```

### 9.3 发送顺序

1. 根据真实 bin 绝对路径生成 `SENDFILE` 帧并发送；
2. 等待包含 `SENDFILE,END,OK` 的回包；
3. 发送 `POWER,STATE`；
4. 如果回包为 OFF，发送 `POWER,ON` 并等待成功；如果为 ON，跳过；
5. 发送 `DEMURA,ERASE,START` 并等待成功；
6. 发送 `DEMURA,WRITE,START` 并等待成功；
7. 根据现场要求决定是否另行发送 `POWER,OFF`。

不要把以上五条帧一次性连续粘贴发送。当前 ColorVision 的行为是每条指令收到成功回包后才继续，现场复现也应保持相同握手顺序。

## 10. 根据真实路径生成 HEX

下面的 PowerShell 只负责生成数据帧并打印 HEX，不连接设备，也不会执行烧录：

```powershell
$messageText = 'PG,01,SENDFILE,START,1,C:\Demura\DemuraDynamic.bin,DemuraDynamic.bin'
$messageLength = $messageText.Length.ToString('X4')
$packet = [byte[]](
    @(0x02, 0xFF) +
    [System.Text.Encoding]::UTF8.GetBytes($messageLength + $messageText) +
    @(0x03)
)

"[02][FF]$messageLength$messageText[03]"
($packet | ForEach-Object { $_.ToString('X2') }) -join ' '
```

修改 `$messageText` 中的实际路径后运行，再把输出 HEX 复制到 TCP 调试助手。不要手工沿用旧长度头。

## 11. 默认烧录配置汇总

| 配置项 | 默认值 | 说明 |
| --- | --- | --- |
| `BurnAfterGenerate` | `true` | bin 生成后自动烧录 |
| `GeneralSensorCode` | `DEV.Sensor.Default` | 优先用于查找 PG 连接配置 |
| `GeneralSensorCategory` | `Sensor.Default` | Code 未命中时的回退查找条件 |
| `BurnSourceBinName` | `DemuraDynamic.bin` | 默认源 bin |
| `BurnTargetFileName` | `DemuraDynamic.bin` | PG 目标文件名 |
| `BurnPgChannel` | `01` | 只用于 `SENDFILE` |
| `BurnFileIndex` | `1` | `SENDFILE,START` 后的文件序号 |
| `BurnSuccessResponse` | `SENDFILE,END,OK` | 文件下发成功关键字 |
| `BurnTcpConnectTimeoutMs` | `5000` | TCP 连接超时 |
| `BurnTcpResponseTimeoutMs` | `60000` | 每条指令回包超时 |

## 12. 现场确认清单

- [ ] 使用的是 PG 的 TCP 地址和端口，不是 ARVRPRO JSON Socket 端口；
- [ ] 调试助手处于 TCP Client 模式，不是 COM 串口模式；
- [ ] 助手按 HEX 发送，`02`、`FF`、`03` 是原始字节；
- [ ] 未自动追加 CR/LF；
- [ ] `SENDFILE` 中的源路径是 PG 服务可访问的真实路径；
- [ ] 修改路径后重新计算了四位长度头；
- [ ] 每条指令都等到成功回包后再继续；
- [ ] 确认了 PG 是否把通道 `01` 和 `1` 视为同一通道；
- [ ] 擦除和写入完成后，根据现场要求确认是否需要下电；
- [ ] 保存 ColorVision 日志中的 `command`、`hex`、`response` 和失败步骤，便于与 PG 端日志对照。

## 13. 代码层面能确认和不能确认的边界

从当前仓库代码可以确认：

- ColorVision 发送的是 TCP GECS 文本命令帧；
- `SENDFILE` 帧只包含源文件路径和目标文件名；
- 烧录顺序、帧格式、成功关键字、失败关键字和超时规则如本文所述。

当前仓库代码不能确认：

- PG 收到 `SENDFILE` 后如何读取该路径；
- PG 是否在内部另建文件传输通道；
- `DEMURA,WRITE,START` 最终写入的硬件地址及底层烧写算法；
- PG 固件是否严格区分通道字符串 `01` 与 `1`。

这些部分属于 PG 服务或固件实现，现场若要用第三方调试助手独立复现，需要结合 PG 端协议手册和日志进一步确认。
