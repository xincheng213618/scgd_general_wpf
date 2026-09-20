---
knowledge_id: "projects.arvr-pro-semi-auto"
knowledge_type: "reference"
status: "current"
summary: "独立 ProjectARVRPro SemiAuto 软件的 ARVR/GECS 双 Socket、可配置指令映射、PG 成功门禁、四页签操作界面、结果解析及客户 ZIP 验证边界。"
aliases: ["ProjectARVRPro.SemiAuto","ARVR 半自动软件","GECS 半自动","SemiAutomaticWorkflow","semi-auto-profile.json","GecsPacketCodec","GecsClient"]
code_paths: ["Projects/ProjectARVRPro.SemiAuto/","Scripts/publish_project_arvrpro_semi_auto.py","Scripts/publish_project_arvrpro_semi_auto.bat"]
test_paths: ["Test/ProjectARVRPro.SemiAuto.Tests/"]
related: ["projects.arvr-pro-demo","projects.arvr-pro","projects.arvr-pro-protocol","projects.index"]
---

# ProjectARVRPro SemiAuto

## 产品边界

`ProjectARVRPro.SemiAuto` 是独立的 .NET Framework 4.8 WPF 操作软件，不是对外协议样例 `ProjectARVRPro.IntegrationDemo`。两者可以复用公开契约，但版本、配置、发布包和使用目的保持独立。

- `IntegrationDemo`：轻量协议调用、结果解析和客户复制示例。
- `SemiAuto`：真实 PG 指令、操作员确认、自动执行选项和执行门禁。

程序集和输出文件名为 `ProjectARVRPro.SemiAuto`，独立版本从其项目文件 `VersionPrefix` 读取。

## 界面

主窗口使用四个页签：

| 页签 | 内容 |
| --- | --- |
| 运行 | ARVR/PG 连接状态、PG 联动运行、暂停/取消、当前任务和可清除通信日志 |
| 配置 | 配置文件、连接参数和指令映射 |
| 结果 | 结果摘要、测试项、原始 JSON 和 CSV 导出 |
| 高级 | ARVR 同步命令、直接确认、GECS 自定义指令和契约字段 |

运行页是默认页。地址输入、映射表、结果明细和调试入口不占用主操作区。

## Socket 与门禁

ARVR 使用无分隔 UTF-8 JSON；读取器按字符串和转义感知的大括号配平处理半包、粘包和连续对象。PG 使用 GECS 帧：

```text
STX + Network Number + 4 位 HEX-ASCII Message Length + ASCII Message Text + ETX
```

默认现场完整入口是“PG 联动运行”，它不发送 ARVR 内部 `RunAll`，而是显式编排外部 PG 和 ARVR：

```text
POWER ON（携带 SN） -> ProjectARVRInit -> SwitchPG
-> GECS 切图指令 -> END,OK -> SwitchPGCompleted
-> 重复切图 -> ProjectARVRResult -> POWER OFF
```

“内部运行全部”保留原有 `RunAll` 行为，仅用于 ARVR 内部流程调试，不承诺触发外部 `SwitchPG`。完整联动固定执行 PG 并在成功后确认 ARVR，不受手动流程的“自动执行”和“成功后确认”选项影响。

暂停在协议安全边界生效：已发送的 PG 指令正常完成，下一条切图请求保持未确认，继续后再执行。取消不强制打断已发送的 PG 指令或正在执行的 ARVR Flow；它停止后续确认，在下一切图或最终结果边界下电并断开。日志“清除”只清空当前界面显示。

手动/可选自动流程收到 `SwitchPG` 后，以 `EventName + ARVRTestType` 查找启用映射，执行顺序是：

```text
ARVR 首次切图请求 -> POWER ON（携带 SN） -> GECS 切图指令
-> processing（可重复） -> END,OK -> ARVR 确认
-> ProjectARVRResult -> POWER OFF
```

`ManagePgPowerForRun` 默认启用，同一次 ARVR 运行只在首张图前开电一次；最终结果、接收异常或显式断开会尝试关电。开电失败时不发送切图指令，也不确认 ARVR。只有回包匹配映射的 `SuccessContains` 且没有 `ERROR` / `END,NG` 时，PG 动作才成功。匹配优先使用连续文本；兼容模式允许逗号字段按顺序出现额外值，但同时要求回包开头与实际发送指令一致。因此 `,PATTERN,INDEX,END,OK` 可匹配带图片编号的 `PG,01,PATTERN,INDEX,1,END,OK`，而图片编号不一致不会误判成功。无映射、非法帧、超时、断线、NG 或 ARVR 确认未发送都不能报告完整成功。

收到映射请求时，业务日志先用 `Prepared (not sent)` 显示变量展开后的命令、Network、4 位 HEX-ASCII 正文长度、正文/整帧字节数和完整 HEX；实际执行后用 `Sent` 记录真实发送帧，以 `Received` 记录原始回包，并用 `Result [OK]` / `Result [NG]` 显示最终判定。长度只计算 ASCII Message Text，不包含 STX、Network、长度字段和 ETX；整帧固定比正文多 7 字节。正常 `ALIVE` 收发不记日志，心跳失败仍记录。

同一可识别请求按 `EventName + MsgID` 去重；缺 MsgID 时回退 SN 和测试类型。全部身份字段都缺失时，每次到达均视为新请求，避免漏掉连续 AOI 切图。

## 配置

首次启动读取 `Profiles/semi-auto-profile.sample.json`；现场保存到程序目录下的 `semi-auto-profile.json`。默认配置提供 3 条禁用的 `SwitchPG` 切图映射，对应测试类型 0、1、2 和 PG 图 1、2、3；数量可在配置页增删，不包含 AOI 示例。

配置包含 ARVR/PG 地址、Network Number、Channel、响应超时、`ALIVE` 心跳、自动电源时序、自动执行、成功后确认和映射列表。模板变量为 `{channel}`、`{testType}`、`{sn}`；`*` 是事件级通配。

`ARVRTestType` 是活动流程组中的外部索引，不是固定业务枚举。流程组或 Legacy 偏移变化后必须重新核对映射。

## 验证

```powershell
dotnet build Projects/ProjectARVRPro.SemiAuto/ProjectARVRPro.SemiAuto.csproj -c Release -p:Platform=x64
dotnet test Test/ProjectARVRPro.SemiAuto.Tests/ProjectARVRPro.SemiAuto.Tests.csproj -c Release -p:Platform=x64
cmd.exe /d /c Scripts\publish_project_arvrpro_semi_auto.bat --validate-only
```

测试覆盖 GECS 组包/拆包、半包/粘包、ASCII 和长度校验、静默 `ALIVE`、电源开关顺序、`processing`、成功/失败回包，以及“PG 成功后确认、失败不确认”的工作流门禁。发布自检还验证配置解析、样例结果解析、CSV 产物和 ZIP 内容。

这些检查不等于现场硬件验收。真实交付仍需记录 ARVR 版本、PG 固件、目标地址、流程组、`ARVRTestType` 映射、实际指令和回包。
