# ARVRPRO TCP 流程组切换指令说明

本文提供给 ARVRPRO 上位机或产线控制程序的开发人员，用于通过 TCP 切换 ColorVision 中“当前组”下拉框所选择的流程组。

编写依据为 `ProjectARVRPro 1.1.8.28` 当前源码。现场接入前仍需确认已安装项目包支持 `SwitchGroup` 事件。

## 1. 连接条件

- ColorVision 已加载 `ProjectARVRPro`。
- 在 ColorVision“通信协议”设置中启用 Socket Server，并选择 **Json** 模式。
- 默认监听端口为 `6666`，实际 IP 和端口以现场配置为准。
- 报文编码为 UTF-8，直接发送一个完整 JSON 对象，不添加长度头。

## 2. 切换指令

事件名为 `SwitchGroup`，`Params` 填写界面“当前组”下拉框中的组名。

例如，将当前组切换为 `MD138`：

```json
{
  "Version": "1.0",
  "MsgID": "req-group-001",
  "EventName": "SwitchGroup",
  "Params": "MD138"
}
```

| 字段 | 必填 | 说明 |
| --- | --- | --- |
| `Version` | 建议填写 | 当前建议固定为 `1.0` |
| `MsgID` | 建议填写 | 客户端生成的请求标识，响应中会原样返回 |
| `EventName` | 是 | 必须为 `SwitchGroup`，大小写需完全一致 |
| `Params` | 是 | 要切换到的流程组名称，例如 `MD138` |

组名匹配不区分大小写，但名称前后不能多空格。现场流程组名称应以 ColorVision 界面中的实际配置为准。

## 3. 成功响应

```json
{
  "MsgID": "req-group-001",
  "EventName": "SwitchGroup",
  "Code": 0,
  "Msg": "Switched to MD138",
  "Data": {
    "GroupName": "MD138",
    "MetaCount": 5
  }
}
```

- `Code=0` 表示切换成功。
- ColorVision 界面的“当前组”会立即切换，并尝试将该选择保存为当前活动组；如需确认重启后仍保留，还应检查现场配置是否保存成功。
- `Data.GroupName` 是已切换到的组名。
- `Data.MetaCount` 是该组中的流程总数，包含已禁用流程。

## 4. 错误响应

| Code | Msg 示例 | 含义 |
| --- | --- | --- |
| `-1` | `GroupName is empty` | `Params` 为空 |
| `-2` | `Group not found: MD138` | 找不到指定流程组，请核对现场组名 |
| `-99` | 具体异常信息 | ColorVision 内部处理异常，需结合软件日志排查 |
| `404` | `Handler not found for event: SwitchGroup` | 项目未正确加载、事件名大小写错误，或当前软件版本不支持该事件 |

## 5. 切换后的确认

建议切换成功后发送 `GetProcessEnable`，读取返回的 `Data.ActiveGroupName`，同时取得该组当前的流程索引及启用状态：

```json
{
  "Version": "1.0",
  "MsgID": "req-group-check-001",
  "EventName": "GetProcessEnable"
}
```

确认活动组无误后，再发送 `RunAll` 或 `ProjectARVRInit` 开始测试。推荐顺序为：

```text
SwitchGroup -> GetProcessEnable -> RunAll（或 ProjectARVRInit）
```

## 6. 联调注意事项

- 不要在测试执行过程中切换流程组；应等待上一轮测试完成后再切换。
- TCP 服务当前没有长度头或分隔符。每个请求应单独、串行发送，避免把多条 JSON 拼在同一次发送中。
- 服务端响应同样没有结束符；客户端应按完整 JSON 对象解析接收缓存。
- 切换组会修改 ColorVision 的当前运行配置。正式联调前应确认目标设备、流程组和操作权限。
- 接入时应记录现场安装的 `ProjectARVRPro` 项目包版本，并使用与该版本匹配的协议说明。
