---
knowledge_id: "operations.device-configuration"
knowledge_type: "topic"
status: "current"
summary: "终端与设备配置引用、创建、保存、重启和删除清理；未保存的活对象改动可影响运行，删除不保证显示项和通信对象一并释放。"
aliases: ["添加设备","保存设备","删除设备","设备配置引用","通信订阅清理","设备Code","设备配置保存失败","RestartRCService","SaveConfig","DeviceService","DeviceServiceConfig","DeviceServiceCreateContext","TryDeserializeConfig","txt_value","SQL修改设备配置"]
code_paths: ["Engine/ColorVision.Engine/Dao/SysResourceModel.cs","Engine/ColorVision.Engine/Services/DeviceService.cs","Engine/ColorVision.Engine/Services/Core/ServiceObjectBaseExtensions.cs","Engine/ColorVision.Engine/Services/Core/MQTTServiceBase.cs","Engine/ColorVision.Engine/Services/Devices/MQTTDeviceService.cs","Engine/ColorVision.Engine/Services/Devices/DeviceServiceConfig.cs","Engine/ColorVision.Engine/Services/Devices/DeviceServiceFactory.cs","Engine/ColorVision.Engine/Services/Devices/SMU/DeviceSMU.cs","Engine/ColorVision.Engine/Services/Devices/SMU/MQTTSMU.cs","Engine/ColorVision.Engine/Services/Type/CreateType.xaml.cs","Engine/ColorVision.Engine/Services/Terminal/CreateTerminal.xaml.cs","Engine/ColorVision.Engine/Services/Terminal/TerminalService.cs","Engine/ColorVision.Engine/Services/RC/MQTTRCService.cs"]
test_paths: []
related: ["engine.devices","engine.mqtt","engine.rc-registration","ui.property-grid","operations.acceptance"]
---

# 设备资源配置、保存与重启

设备配置主要保存到 MySQL 的 `SysResourceModel.Value`，不是主程序通用设置文件。`DeviceService<T>.Save()` 除了写入配置，还请求 RC 重启服务。查询“怎么配置”不授权新增资源、导入覆盖、保存、重置、删除或远端重启；先确认当前任务允许的设备、数据库和外部影响范围。

资源怎样进入列表和主显示区见[Engine 设备装配](../../04-api-reference/engine-components/device-service-chain.md)。本页覆盖通用终端/设备资源，不替代物理相机、客户项目或具体硬件参数契约。

RCName/AppId 等客户端注册配置不是这里的 MySQL 设备参数；其连接测试、取消、节点令牌与设备状态的区别见[RC 注册契约](../../04-api-reference/engine-components/rc-registration.md)。

## 资源与配置身份

| 对象/字段 | 含义 |
| --- | --- |
| `SysResourceModel.Id` / `Pid` | 数据库资源身份及父终端关系，不是服务类型编号 |
| `SysResourceModel.Type` | `ServiceTypes` 编号，用于查工厂和服务类型字典 |
| `Code` / `Name` | 运行身份与显示名称；不要只按相同名称判断同一设备 |
| `Value` | 具体 `Config*` 的 JSON |
| `SendTopic` / `SubscribeTopic` | 通信主题；资源可见并不证明主题、token或远端服务有效 |

`DeviceService<T>` 构造时用 `TryDeserializeConfig<T>` 建立默认对象，再用 JSON 填充，JSON 的 null 值被忽略。空文本或解析异常会返回默认配置；异常写日志，不阻止对象构造。随后以资源列覆盖 `Config.Code` / `Config.Name`。因此“配置页能打开”不能证明旧 JSON 成功恢复，看到默认值时先保留原始 Value 并查反序列化日志，不要直接保存覆盖证据。

## 创建终端与创建设备

`TypeService` 的创建入口打开 `CreateType`，实际创建的是根终端资源；`TerminalService` 的创建入口打开 `CreateTerminal`，实际创建的是终端下的设备。不要按窗口类名猜数据库层级。

- `CreateType` 根据类型字典设置 Type，构造带服务类型、终端 Code 和 RCName 的 CMD/STATUS 主题，插入根资源后加入终端集合，再请求按类型重启。
- `CreateTerminal` 以终端 Type 找工厂，传入 `DeviceServiceCreateContext(Code, Name, SendTopic, SubscribeTopic)`；工厂建立 Config，保存子资源 JSON，再创建运行实例并加入集合。
- 默认命名辅助方法会查询资源是否重名，但手工提交的检查路径不同：`CreateType` 检查当前类型的终端 Code，`CreateTerminal` 检查当前已加载设备 Code。不能把界面检查当成跨进程、并发或全部数据库记录的唯一性保证。
- 数据库插入、构建设备、加入集合和请求远端重启没有统一事务回滚。创建后异常时先定位失败阶段，不能直接重复创建。

**现有实现冲突：** `CreateTerminal.Button_Click` 的创建后重启查询使用 `sysDevModel.Pid` 匹配字典 Value、使用 `sysDevModel.Type` 查资源主键；而通用 `RestartRCService` 使用 Type 匹配字典、Pid 查父资源。前者与本页的字段职责不一致，可能在资源已经插入后查错重启目标或抛异常。这里保留源码冲突供修复定位，不将它写成正确配置规则，也不宣称已经修复或真机复现。

## 编辑、保存与远端应用

`DeviceService<T>.Config` 是运行对象持有的配置，不是天然的待保存副本。`MQTTDeviceService<T>` 的 `DeviceCode`、收发 Topic 和 `ServiceToken` 直接读取它持有的 Config；例如 `MQTTSMU` 构造时接收 `DeviceSMU.Config` 的同一引用。直接修改这个共享对象，后续取值即可变化，不以 `Save()` 为内存生效开关。但字段变化不证明新主题已经订阅、配置已持久化或远端设备已经应用，通信状态仍按[消息契约](../../02-developer-guide/engine-development/mqtt.md)核对。

修改同一对象与替换引用也不同：通用重置直接 `Config = new T()`，不会自动重绑其它对象已保存的旧 Config。`Save()` 的基类流程不调用 `LoadServices()` 或重建显示区，默认 `OnConfigChanged()` 为空；具体设备可覆盖或订阅通知。不能承诺保存后所有运行对象自动重建，也不能统一建议“再重载一次”——重载对旧对象及集合的影响见[运行装配](../../04-api-reference/engine-components/device-service-chain.md#重载、旧对象与集合引用)。事务属性窗口的工作副本与提交边界仍只在[属性契约](../../04-api-reference/ui-components/property-grid.md)维护，不把直接改 Config 的语义套到所有编辑窗口。

通用 `DeviceService<T>` 的顺序是：

| 阶段 | 代码行为 | 不能据此推断 |
| --- | --- | --- |
| 编辑对象 | 控件修改传入 Config；具体窗口决定直接编辑或事务副本 | 关闭一定撤销、字段变化已经落盘 |
| `SaveConfig()` | 将 Config 的 Code/Name/JSON 写入资源并执行 MySQL Update；返回的影响行数未检查 | 没抛异常就一定更新了目标行 |
| `RestartRCService()` | 按 Type 查服务类型、按 Pid 查终端 Code，再请求 RC 重启该设备 | 远端已经重启或应用新配置 |
| 本地通知 | `OnConfigChanged()`，再发 `ConfigChanged` | 各设备全部对象已经重建或硬件已健康 |

直接 SQL 修改 `t_scgd_sys_resource` 不调用上述保存、重启和通知流程，也不会自动更新已经载入的 Config。已存在的设备对象稍后执行 `SaveConfig()`，还可能把旧 Code/Name/JSON 覆盖回数据库。核验 SQL 修改时应分别确认目标行与实际使用该配置的运行对象；不能把数据库写入成功当成界面刷新或远端生效。

上述步骤不是 MySQL 与远端服务的分布式事务。后续阶段出错不会自动回滚已经成功的数据库更新；顺序中抛异常会阻止更后的通知。

RC 的三参数 `RestartServices` 是 void 包装，丢弃 `TryRestartServices` 的结果。RC 未连接或无可用 token 时内部返回 false；可以发送时也是异步发布并稍后查询，不等待设备应用完成。因此保存方法返回、通知发生、设备在线和参数生效必须分别取证。

设备右键“重启服务”的 `RefreshCommand` 实际调用 `Save()`，也会尝试把当前 Config 写入数据库，不是只读刷新。不要把设备级与终端级操作视为相同范围。

**终端保存仍有实现缺口：** `TerminalService.Save()` 先修改内存 `SysResourceModel`，但使用的是未传实体、未指定条件的 `Db.Updateable<SysResourceModel>().ExecuteCommand()`，不同于设备的 `Updateable(SysResourceModel)`。不能据此宣称目标终端行已正确持久化；实际 ORM 行为和修复需单独验证，不猜测它一定更新全部行或一定失败。随后重启仅传 `Config.ServiceType.ToString()`，未传终端 Code；`CreateType` 新建 Config 没有设置该 ServiceType，加载终端也只覆盖 Code/Name，不以资源 Type 同步它。需要核对实际配置和请求目标，不能声称只重启当前终端。

## 导入、导出、重置与删除

| 操作 | 通用实现及限制 |
| --- | --- |
| 导出 `.config` | 将当前 Config JSON 写入选定文件；不包含设备树、数据库关系或硬件校准全量备份，分享前检查敏感字段 |
| 导入 `.config` | 读取并反序列化为具体 T，复制到现有 Config 后调用 `Save()`；可能改变身份/主题并请求远端重启，不是预览 |
| 重置 | 确认后仅 `Config = new T()`；本身没有保存或重建其它持有旧 Config 的对象，不等于恢复出厂硬件状态 |
| 文件存储配置 | 仅 Config 实现 `IFileServerCfg` 时可用；`UpdateFilecfg` 用事务属性窗口，关闭时比较值，有变化才调用 Save |
| 删除设备 | 确认后移出树，物理删除该资源行，再移出 `DeviceServices`；尝试按本次 `GetDisplayControl()` 返回值移除显示项，最后调用 Dispose。没有通用软删除、子资源级联或整轮回滚保证 |
| 删除终端 | 删除直接子资源行和终端行并移出终端集合；不能推断递归删除全部后代或立即清理所有旧设备/窗口引用 |

删除显示项依赖返回同一个已登记实例，并非按设备 Code 查找。例如 `DeviceSMU.GetDisplayControl()` 每次创建新的 `DisplaySMU(this)`，删除时拿到新实例不能据此保证原显示项已移除。删除中途任一步抛异常也会阻止后续清理，不恢复先前已完成的移树或数据库删除。

调用 Dispose 不等于释放通信：通用 `DeviceService.Dispose()` 只调用 `GC.SuppressFinalize`，不会自动执行 `GetMQTTService()?.Dispose()`；`DeviceSMU` 也没有覆盖这个方法。`MQTTServiceBase.Dispose()` 才负责自身 `Processing` 退订、计时器及待处理记录清理，派生通信类另加的事件仍要核对其解绑实现。因此不能从“设备已删”推断所有通信回调、显示缓存或已打开窗口均已失效。

具体 `Device*` 可以覆盖上述方法，操作前应核对实际类型。PropertyGrid 的编辑会话、确定与关闭语义只在[属性契约](../../04-api-reference/ui-components/property-grid.md)维护；按钮是否可用不扩大 AI 的执行授权。

## 定位与验证

保存/创建失败时，分别记录：当前数据库和目标资源 ID/Type/Pid/Code、旧 Value 备份、编辑模式、数据库阶段结果、RC 连接与重启请求、设备端最终状态。只读诊断可以审查代码、脱敏日志与既有记录；不要通过改 Code、清库、重新导入或点击“重启服务”试探。

本页未声明创建/保存/重启的自动化集成测试。`ServiceConfigTests` 只覆盖 RC 配置信息属性通知，不能证明本页契约。受授权的隔离验证应覆盖旧 JSON 恢复、无效 JSON 保留证据、保存后重开、目标行不存在、RC 离线和后阶段失败，并检查数据库已提交但远端未生效的分离状态；真实设备动作另外验收。

共享 Config 的即时取值、重置后的新旧引用、删除前后显示实例与通信事件解绑也尚无本页声明的自动化覆盖；应使用隔离对象/替身分别验证，不能以知识检索命中替代生命周期测试。
