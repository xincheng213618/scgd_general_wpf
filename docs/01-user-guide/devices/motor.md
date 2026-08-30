---
knowledge_id: "operations.motor"
knowledge_type: "topic"
status: "current"
summary: "电机设备配置、MQTT运动命令与位置读回契约；移动回包不会刷新位置，客户端参数不能代替现场限位与急停。"
aliases: ["电机不动","回原点","位置没有刷新","绝对相对移动","DeviceMotor","MQTTMotor","GetPosition","MoveDiaphragm"]
code_paths: ["Engine/ColorVision.Engine/Services/Devices/Motor/DeviceMotor.cs","Engine/ColorVision.Engine/Services/Devices/Motor/ConfigMotor.cs","Engine/ColorVision.Engine/Services/Devices/Motor/MQTTMotor.cs","Engine/ColorVision.Engine/Services/Devices/Motor/DisplayMotor.xaml.cs","Engine/ColorVision.Engine/Services/PhyCameras/Configs/MotorConfig.cs","Engine/FlowEngineLib/MotorNode.cs","Engine/FlowEngineLib/CamMotorNode.cs"]
test_paths: []
related: ["engine.devices","operations.device-configuration","flow.session"]
---

# 电机命令与位置读回

`DeviceMotor` 把独立电机服务接入设备资源体系；`MQTTMotor` 发送打开、移动、光圈移动、回原点和读位置请求，`DisplayMotor` 提供手动入口。这里描述的是客户端请求与回包处理，不是电机控制器的运动完成或安全保证。

移动和回原点会产生真实机械运动。执行前必须由现场操作者确认行程、方向、限位/急停、负载及危险区域，并取得动作授权。配置里有 `MinPosition` / `MaxPosition` 不代表手动发送路径已校验目标范围；当前 `MQTTMotor` 也没有独立的急停方法，`Close` 不能当作已验证的急停。位置异常不授权 AI 自动回零或试走。

## 配置、显示与持久化

`ConfigMotor.MotorConfig` 保存 `MotorConfigBase` 的通信、速度、加减速、回零和行程参数。设备设置采用事务式属性编辑，提交后调用设备 `Save`；数据库保存、远端重配与完成确认的公共边界见[设备资源配置](./configuration.md)，不要把保存配置等同于远端已应用。

`ConfigMotor.Position` 是带 `JsonIgnore` 的运行期读回值，不是持久化目标。界面位置框绑定它；应用中保留的旧值不能证明当前物理位置。目标位置、绝对/相对模式则来自 `DisplayMotor` 当前控件。

## 手动请求的实际契约

| 操作 | 发出的关键参数 | 本客户端收到 `Code=0` 后的处理 |
| --- | --- | --- |
| `Open` | 无显式设备参数；消息跟踪超时设为 1000 ms | 标记 `DeviceStatus=Opened` |
| `Close` | 空参数；消息跟踪超时设为 1000 ms | 标记 `DeviceStatus=Closed` |
| `Move` | `nPosition`、`bAbs`、`dwTimeOut` | 不更新位置，也不自动发 `GetPosition` |
| `MoveDiaphragm` | `dPosition`、`dwTimeOut` | 不更新位置 |
| `GoHome` | `dwTimeOut` | 没有专门的回包更新分支 |
| `GetPosition` | `dwTimeOut` | 把 `Data.nPosition` 写入 `Config.Position` |

`Move` / `MoveDiaphragm` 签名虽然接受 `dwTimeOut` 实参，当前实现没有使用它；上述运动、回原点和读位置消息的 `dwTimeOut` 都来自 `Config.MotorConfig.DwTimeOut`，并非 `HomeTimeout`。它是消息内传给服务的参数，和 `PublishAsyncClient` 的本地回包等待计时不是同一个设置。

`Move` 的 API 默认绝对移动（`IsbAbs=true`），但手动按钮显式传入 `CheckBoxIsAbs.IsChecked`；判断绝对或相对移动应看这次实际参数，不按方法默认值推断。手动移动只先做整数解析，光圈移动做浮点解析；当前回原点按钮也要求位置文本能解析为整数，尽管该数值不会传给 `GoHome`。因此“点了回原点但无消息”可能是输入框拦截，不一定是设备拒绝。

## 回包、位置与失败分流

公共 `MQTTServiceBase` 按订阅主题和待处理 `MsgID` 关联回包，把 `Code=0` 标为 `MsgRecordState.Success`，其余代码标为 `Fail`；超时会结束本地追踪。这只描述请求回包，不独立证明电机停稳、到位或已安全断开。

`MQTTMotor` 的附加处理在 `Code=1` 时把设备标为 `Closed`；其它非零代码不会在此处更新设备状态。不要因此把状态文字当作控制器真实电源或运动状态。

| 现象 | 无需运动即可先核对的证据 |
| --- | --- |
| 打不开或无回包 | 设备 Code、收发主题、通信配置、对应 `MsgID` 的失败码/超时 |
| 移动后位置未变 | 是否只有 `Move` 回包；只有 `GetPosition` 成功分支会更新界面位置 |
| 回原点按钮没有发消息 | 位置文本是否通过整数解析；是否真的产生本次 `MsgID` |
| 超时参数与预期不符 | 检查实际消息里的 `dwTimeOut`，不要只看调用实参或 `HomeTimeout` |
| 读回位置不可信 | 核对目标设备和最近读回；需要进一步硬件检查时单独取得现场授权 |

需要确认运动后位置时，读位置请求与运动请求应分别关联，不能以界面没刷新推断运动失败，也不能以成功回包推断到位。

## Flow 不是手动按钮的复用

`Engine/FlowEngineLib/MotorNode.cs` 面向 `Motor` 服务，当前按运行类型发送焦距 `Move` 或光圈 `MoveDiaphragm`，使用自己的位置和绝对/相对参数。不要由手动服务存在 `GoHome` 推断该节点也提供回原点。

`CamMotorNode` 面向 `Camera` 服务，包含相机电机移动、光圈和自动对焦，是另一条设备路由。排查时先确定节点的服务类型、设备 Code 和实际载荷，再核对[Flow 执行会话](../workflow/execution.md)；不把“先手动移动一次”作为无需授权的排障步骤。

## 验证边界

本主题没有声明独立电机自动化测试。源码核对可确认参数来源、消息事件和位置赋值，但不能证明真实控制器的单位、方向、限位、回零、停止或到位行为；这些仍需隔离现场、确认授权后的设备验收。文档维护不执行运动、设备打开/关闭或产品测试。
