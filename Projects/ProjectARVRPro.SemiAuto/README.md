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

```text
ARVR SwitchPG
  -> EventName + ARVRTestType 匹配配置
  -> 操作员点击“执行并确认”或启用自动执行
  -> GECS PG 返回 processing / END,OK / END,NG / ERROR
  -> 仅 END,OK 且成功匹配时确认 ARVR
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

`processing` 会继续等待最终结果；`,END,NG` 和 `ERROR` 直接判为失败；成功回包还必须包含映射中的 `SuccessContains`。

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
5. 先使用“执行并确认”完成一轮半自动验收，再决定是否开启自动执行。
