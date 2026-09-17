# ProjectARVRPro SemiAuto

独立的 ARVR + GECS 半自动操作软件。它通过两条 TCP 连接调用 ARVR 和真实 PG，不依赖 ColorVision 主程序或 ProjectARVRPro 插件运行。

它与 `ProjectARVRPro.IntegrationDemo` 是两个产品：

- `IntegrationDemo`：提供给外部客户的轻量协议和结果解析示例。
- `SemiAuto`：面向操作员的半自动软件，包含真实 PG 指令、配置持久化、执行门禁和结果查看。

## 运行

```powershell
dotnet run --project Projects/ProjectARVRPro.SemiAuto
```

或运行发布目录中的：

```powershell
.\ProjectARVRPro.SemiAuto.exe
```

窗口包含四个页签：

- `运行`：ARVR/PG 状态、连接操作、当前任务和通信日志。
- `配置`：连接参数、配置文件和指令映射。
- `结果`：测试摘要、测试项、原始 JSON 和 CSV 导出。
- `高级`：同步命令、直接确认、GECS 自定义指令和契约字段。

## 半自动流程

运行页的“PG 联动运行”是现场完整入口，执行顺序固定为：

```text
GECS POWER ON（携带 SN）
  -> 连接 ARVR 并发送 ProjectARVRInit
  -> ARVR SwitchPG
  -> EventName + ARVRTestType 匹配配置并发送 GECS 切图指令
  -> GECS END,OK 后发送 SwitchPGCompleted
  -> 重复切图，直到 ProjectARVRResult
  -> GECS POWER OFF
```

“内部运行全部”只发送 ARVR 原有的 `RunAll`，保留用于内部流程调试，不负责外部 GECS PG 切图。联动运行不依赖“自动执行”和“成功后确认”复选框，它会固定执行 PG 并仅在 PG 成功后确认 ARVR。

“暂停”在安全边界生效：已经发送的 PG 指令会完成，下一条 `SwitchPG` 会保持待处理，点击“继续”后再切图并确认。“取消”不再发送下一条确认，在当前 PG 指令或 ARVR 测试到达下一切图/最终结果边界后下电并断开。通信日志右上角“清除”只清空界面日志。

手动/可选自动流程仍可使用：

```text
ARVR SwitchPG
  -> EventName + ARVRTestType 匹配配置
  -> 首张图前 GECS POWER ON（携带 SN）
  -> 操作员点击“执行并确认”或启用自动执行
  -> GECS PG 返回 processing / END,OK / END,NG / ERROR
  -> 仅 END,OK 且成功匹配时确认 ARVR
  -> 收到最终 ProjectARVRResult 后 GECS POWER OFF
```

无映射、`END,NG`、`ERROR`、非法帧、断线、超时或 ARVR 确认发送失败时，不推进 ARVR 流程。

## 配置

首次启动读取 `Profiles\semi-auto-profile.sample.json`，默认提供 3 条禁用的 `SwitchPG` 切图映射。切图数量可在配置页新增或删除；保存后使用程序目录下的 `semi-auto-profile.json`。

主要字段：

| 字段 | 用途 |
| --- | --- |
| `ArvrHost` / `ArvrPort` | ARVR Socket 地址 |
| `PgHost` / `PgPort` | GECS PG Socket 地址 |
| `NetworkNumber` | GECS Network Number，范围 0–255 |
| `Channel` | PG Channel，范围 01–54 |
| `PgResponseTimeoutSeconds` | PG 单次回包等待时间 |
| `HeartbeatSeconds` | `ALIVE` 心跳间隔，0 表示禁用 |
| `AutoExecuteMappedPgCommand` | 收到请求后自动执行启用的映射 |
| `ConfirmArvrAfterPgSuccess` | PG 成功后自动确认 ARVR |
| `ManagePgPowerForRun` | 首次切图前自动开电、最终结果或异常结束后自动关电 |
| `Mappings` | `EventName + ARVRTestType` 到 GECS 指令模板的映射 |

指令模板支持 `{channel}`、`{testType}`、`{sn}`。`ARVRTestType` 必须按现场活动流程组核对；`*` 表示该事件的通配映射。

配置可以离线校验：

```powershell
.\ProjectARVRPro.SemiAuto.exe --validate-profile Profiles\semi-auto-profile.sample.json
```

## 协议

ARVR 使用无分隔 UTF-8 JSON。读取器处理 TCP 半包、粘包和连续 JSON 对象。

GECS 使用：

```text
STX(0x02) + Network Number(1 byte) + Message Length(4 byte HEX-ASCII)
+ Message Text(ASCII) + ETX(0x03)
```

`processing` 会继续等待最终结果；`,END,NG` 和 `ERROR` 直接判为失败。成功回包优先按 `SuccessContains` 连续文本匹配；为兼容现场回包中的图片编号，也支持逗号字段按顺序匹配，但回包开头必须与实际发送的 PG 指令一致。例如配置 `,PATTERN,INDEX,END,OK` 可以匹配 `PG,01,PATTERN,INDEX,1,END,OK`，但不会误收图片编号 2 的回包。

收到映射请求时，日志先以 `Prepared (not sent)` 显示变量展开后的命令、Network、4 位 HEX 长度、正文/整帧字节数和完整 HEX；真正执行后再以 `Sent` 记录实际发送帧，以 `Received` 记录原始回包，并用 `Result [OK]` / `Result [NG]` 显示最终判定。`ALIVE` 仍按配置发送并参与读帧，但正常收发不写日志；只有心跳失败保留错误日志。

## 结果

收到 `ProjectARVRResult` 后保存原始 JSON 并导出扁平 CSV。键化结果、动态 POI、屏幕缺陷和常用标准结果均保留可追溯 `Path`。

## 验证

```powershell
dotnet build Projects/ProjectARVRPro.SemiAuto/ProjectARVRPro.SemiAuto.csproj -c Release -p:Platform=x64
dotnet test Test/ProjectARVRPro.SemiAuto.Tests/ProjectARVRPro.SemiAuto.Tests.csproj -c Release -p:Platform=x64
cmd.exe /d /c Scripts\publish_project_arvrpro_semi_auto.bat --validate-only
```

`--validate-only` 会生成并校验临时 ZIP，但不会上传。

## 现场启用

1. 核对 ARVR 和 PG 地址。
2. 核对活动流程组中的 `ARVRTestType`。
3. 填写并单独验证 GECS 指令及成功回包特征。
4. 启用对应映射。
5. 先使用“执行并确认”逐项核对，再使用“PG 联动运行”完成整轮现场验收。
