---
knowledge_id: "operations.device-configuration"
knowledge_type: "topic"
status: "current"
summary: "终端与设备配置引用、创建、保存、重启和删除清理；保存不保证远端已应用配置，未保存的活对象改动可影响运行，删除不保证显示项和通信对象一并释放。"
aliases: ["添加设备","保存设备","删除设备","设备配置引用","通信订阅清理","设备Code","设备配置保存失败","RestartRCService","SaveConfig","DeviceService","DeviceServiceConfig","DeviceServiceCreateContext","TryDeserializeConfig","txt_value","SQL修改设备配置"]
code_paths: ["Engine/ColorVision.Engine/Dao/SysResourceModel.cs","Engine/ColorVision.Engine/Services/DeviceService.cs","Engine/ColorVision.Engine/Services/Core/ServiceObjectBaseExtensions.cs","Engine/ColorVision.Engine/Services/Core/MQTTServiceBase.cs","Engine/ColorVision.Engine/Services/Devices/MQTTDeviceService.cs","Engine/ColorVision.Engine/Services/Devices/DeviceServiceConfig.cs","Engine/ColorVision.Engine/Services/Devices/DeviceServiceFactory.cs","Engine/ColorVision.Engine/Services/PhyCameras/Licenses/PhyLicenseDao.cs","Engine/ColorVision.Engine/Services/Devices/SMU/DeviceSMU.cs","Engine/ColorVision.Engine/Services/Devices/SMU/MQTTSMU.cs","Engine/ColorVision.Engine/Services/Type/CreateType.xaml.cs","Engine/ColorVision.Engine/Services/Terminal/CreateTerminal.xaml.cs","Engine/ColorVision.Engine/Services/Terminal/TerminalService.cs","Engine/ColorVision.Engine/Services/RC/MQTTRCService.cs"]
related: ["engine.devices","engine.mqtt","engine.rc-registration","ui.property-grid","operations.acceptance"]
---

# 设备资源配置、保存与重启

设备配置保存到设备加载时所属的 MySQL 或本地 SQLite 配置库，配置 JSON 存在 `SysResourceModel.Value`。保存本地设备不发送 RC 重启请求。

资源怎样进入列表和主显示区见[Engine 设备装配](../../04-api-reference/engine-components/device-service-chain.md)。本页覆盖通用终端/设备资源，不替代物理相机、客户项目或具体硬件参数契约。

设备与终端已接入的关键配置入口通过 `ServiceAuthorization` 使用 Common 动作策略，默认要求应用管理员。动作分别为设备配置 `device.configure`、终端名称配置 `service.configure`、创建终端 `service.create`、创建设备 `device.create` 和删除资源 `resource.delete`；设备和终端资源通常使用 Code，无 Code 的通用资源路径回退到 `resource:` 加资源 ID，创建终端使用服务类型身份。打开窗口和执行提交分别检查；事务属性窗口通过 `Submitting` 在复制回原配置前拒绝撤权后的提交，PG/Spectrum 的独立配置草稿在真正 `Apply` 前检查。

这些检查保护实际接入的界面/命令和提交入口，不把通用 `Save()`、DAO 或任意直接配置赋值变成全产品统一授权拦截。原有公开保存接口继续承担持久化与通知；宿主新增业务入口时仍应在副作用前请求对应动作，并保留数据库、远端服务、Windows 权限及设备许可各自的边界。

注册中心默认参数、节点令牌与设备状态见[RC 注册契约](../../04-api-reference/engine-components/rc-registration.md)。

## 无 MySQL 时创建相机与许可证

1. 点击设备控制面板标题栏的 **＋（添加/配置设备）**，进入管理员服务配置，在列表空白处右键并选择 Camera。可选已有终端，或选择 **新建服务配置并添加设备**，依次填写终端与设备配置；空本地库也有此入口。此操作沿用管理员权限要求，标题栏齿轮用于管理已有控制项的显示与分组。
2. 在物理相机管理中创建相机，或者扫描后从结果列表创建，并配置 Camera ID、型号、模式及物理相机绑定。
3. 从许可证管理导入已有许可证。许可证记录保存在本地；同一相机续期仍保留原有物理配置。SDK 需要的 `.lic` 文件继续通过原有导出入口写入程序使用的 `lincense` 目录，原生校验规则不变。
4. 新建的本地相机默认优先本地连接，打开相机后可在本机拍照；流程可使用现有 MQTT 开始节点连接“相机取图”和结束节点，见[离线执行边界](../workflow/execution.md)。

配置库不保存测试结果或图像，也不自动把远端设备搬到本地。校准模板、算法模板和需要远端服务的硬件功能不因设备配置可离线编辑而自动具备离线执行能力。

物理光谱仪管理也使用本地资源和许可证存储：未连接 MySQL 时可以注册、查看和续期本地记录，相同 SN 的相机许可证不会被光谱仪许可证覆盖。管理窗口与设备编辑窗口固定使用打开时的存储，导入或续期过程中连接变化不会改写另一库。在线下载许可证本身仍需要访问许可证服务。

## 资源与配置身份

设备树右侧的属性操作由[通用命令生成器](../../04-api-reference/ui-components/property-grid.md#命令属性页自动生成)依据命令元数据生成，按设备与连接、校准与校正、采集与显示、数据与日志、服务与维护归类。不同设备使用同一套紧凑按钮布局和更新窗口主题规则，仅显示自身拥有的命令。按钮显示操作名称，说明保留在工具提示和无障碍帮助文本中。修改配置和删除来自基类元数据，重启服务通过设备右键菜单执行。点击设备配置窗口顶部 **创建设备** 后面的 **配置路径**，打开统一编辑 **数据基础路径** 的窗口。没有命令的分类不显示。页面不会因展示按钮而自动执行设备操作。

设备卡片或上下文菜单中的 **属性** 会把同一设备页放入专用窗口外壳，显示设备名称、Code 和带留白的内容边界；管理员服务配置中的右侧详情仍使用其自身面板外壳。两种入口复用命令与设备行为，但不会把独立窗口的留白写进设备控件后再重复叠加。

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

设备配置窗口默认显示设备平铺列表，可切换到“服务配置 → 设备”视图。旧配置中的类型视图会转为服务配置视图，已保存的服务或设备视图偏好继续生效。顶部依次提供 **创建设备**、**配置路径** 和 **物理相机管理**。**创建设备** 与列表空白处的右键菜单共用创建流程和管理员权限检查。按服务类型选择 **在某服务添加设备**，也可选择 **新建服务配置并添加设备**。已有服务配置的右键 **创建** 直接添加其下设备。没有已有设备的类型也保留创建入口。右侧服务详情显示服务名称、代码和 **修改配置** 按钮。点击 **修改配置** 可编辑服务名称。从左侧树或设备平铺列表选择设备可查看设备详情，删除通过对应项的右键菜单执行。类型菜单使用程序的多语言资源，已有服务和设备名称沿用用户配置，类型字典、资源 Type、Code 和通信主题不会因显示语言或列表层级变化而改写。

- `CreateType` 根据类型字典设置 Type，构造带服务类型、终端 Code 和 RCName 的 CMD/STATUS 主题，插入根资源后加入终端集合，再请求按类型重启。
- `CreateTerminal` 固定窗口打开时的父终端与配置库。提交时复核父记录、检查设备标识并写入子资源 JSON。新建相机、光谱、算法和校准设备时，将本机选定的数据基础路径填入设备的 `FileServerCfg.DataBasePath`，随原有创建流程保存。提交成功后才创建运行实例、初始化显示配置并加入集合。
- 新建支持本地后端的设备优先使用本地。已有本机显示配置的设备 Code 沿用保存的选择；新建 PG 和独立滤色轮的本地偏好也保存在本机显示配置中。支持本地后端且有自动连接选项的新设备默认关闭自动连接，既有设备按原配置加载。
- 单独新建设备在保存前查找可用的相机许可证并默认填入 SN；相机、校准设备和光谱仪不自动填写，由用户自行配置。候选必须是相机许可证（`LiceType=0`）、SN 和许可证内容非空，且许可证内容可解析、型号非空、内容中的到期时间晚于当前时间；不只依据数据库 `expired` 元数据。默认选择到期时间最晚的一份，到期时间相同则按 SN 忽略大小写排序，以保持选择稳定；不依赖物理相机是否已配置或选中。本地终端只查询本地许可证。没有可用许可证时保持原配置，已有显式 SN 不覆盖。该许可证默认值只在单独创建设备时写入。首次创建唯一物理相机的初始化另行覆盖数据库预置设备：直接用物理相机 Code 补齐普通设备的空 SN，以及相机/校正的空 CameraCode，不依赖许可证是否已导入；已有绑定和光谱仪保持不变。签名与硬件授权仍由原生驱动在运行时验证。
- 新建设备默认按类型命名，例如 `Camera`，名称已被当前已加载设备使用时依次取首个可用的 `Camera1`、`Camera2`。设备名称可以手工填写，设备 Code 使用独立的标识规则。子设备写入时，在原配置库的同一事务中检查同租户有效设备已有的 Code。
- 提交前原父终端或数据库失效时，创建事务回滚。提交后若运行实例未注册，界面提示从原库刷新服务列表，已写入的记录可在重新加载后使用。

创建设备成功后，从原库解析终端类型和 Code，向登记中心发送带新设备 Code 的定向重载请求。本地设备跳过远端请求。

## 编辑、保存与远端应用

`DeviceService<T>.Config` 是运行对象持有的配置，不是天然的待保存副本。`MQTTDeviceService<T>` 的 `DeviceCode`、收发 Topic 和 `ServiceToken` 直接读取它持有的 Config；例如 `MQTTSMU` 构造时接收 `DeviceSMU.Config` 的同一引用。直接修改这个共享对象，后续取值即可变化，不以 `Save()` 为内存生效开关。但字段变化不证明新主题已经订阅、配置已持久化或远端设备已经应用，通信状态仍按[消息契约](../../02-developer-guide/engine-development/mqtt.md)核对。

`Save()` 的基类流程不调用 `LoadServices()` 或重建显示区，默认 `OnConfigChanged()` 为空；具体设备可覆盖或订阅通知。不能承诺保存后所有运行对象自动重建，也不能统一建议“再重载一次”——重载对旧对象及集合的影响见[运行装配](../../04-api-reference/engine-components/device-service-chain.md#重载、旧对象与集合引用)。事务属性窗口的工作副本与提交边界仍只在[属性契约](../../04-api-reference/ui-components/property-grid.md)维护，不把直接改 Config 的语义套到所有编辑窗口。

设备配置窗口在确认时核对打开时的设备、运行配置对象和配置库。切库、重载、更换配置对象后，原窗口拒绝提交，需从当前设备重新打开。直接保存也验证设备及其配置库。

相机和校准设备保存时，从同一配置库读取物理相机参数。设备配置写入成功后才同步许可证关联，更新范围限于对应的相机或校准设备 ID。光谱的暗校正与 SP100 参数窗口沿用即时编辑，关闭时复核原设备后保存。

| 阶段 | 代码行为 | 不能据此推断 |
| --- | --- | --- |
| 编辑对象 | 控件修改传入 Config；具体窗口决定直接编辑或事务副本 | 关闭一定撤销、字段变化已经落盘 |
| `SaveConfig()` | 验证原记录的 ID、Code、Type、Tenant 和 Pid，再更新 Code、Name 和 JSON。找不到原行或归属变更时拒绝写入 | 与写库同时发生的其它业务动作已完成 |
| `RestartRCService()` | 保存成功后从原库解析设备所属终端和服务类型，使用该次请求的 MQTT 连接。排队期间设备或连接失效时停止发送 | 远端已经重启或应用新配置 |
| 本地通知 | `OnConfigChanged()`，再发 `ConfigChanged` | 各设备全部对象已经重建或硬件已健康 |

直接 SQL 修改 `t_scgd_sys_resource` 不调用上述保存、重启和通知流程，也不会自动更新已经载入的 Config。已存在的设备对象稍后执行 `SaveConfig()`，还可能把旧 Code/Name/JSON 覆盖回数据库。核验 SQL 修改时应分别确认目标行与实际使用该配置的运行对象；不能把数据库写入成功当成界面刷新或远端生效。

上述步骤不是 MySQL 与远端服务的分布式事务。后续阶段出错不会自动回滚已经成功的数据库更新；顺序中抛异常会阻止更后的通知。

RC 未连接或无有效令牌时跳过请求。命令发送后通过原连接刷新服务列表。需要确认远端参数生效时，核对设备端的应用结果。

设备右键“重启服务”的 `RefreshCommand` 实际调用 `Save()`，也会尝试把当前 Config 写入数据库，不是只读刷新。不要把设备级与终端级操作视为相同范围。

服务配置右键 **编辑** 只提供 Name 的工作副本。确认时验证原终端和配置库，读取当前已保存的 JSON，更新其中的 Name 和资源名称。成功后同步列表与详情标题。其它配置按原库最新值保留。

`TerminalService.Save()` 在原配置库更新该终端的 Name、Code 和配置 JSON。终端右键 **重新加载终端设备** 从原库解析类型字典、终端 Code 和启用的直属设备，为每台设备发送带终端 Code 和设备 Code 的定向配置重载请求。登记中心收到缺少设备 Code 的请求时会按类型重启，因此此入口总是发送完整标识。重载无数据库写入，本地终端跳过远端请求。

## 导入、导出与删除

| 操作 | 通用实现及限制 |
| --- | --- |
| 导出 `.config` | 将当前 Config JSON 写入选定文件；不包含设备树、数据库关系或硬件校准全量备份，分享前检查敏感字段 |
| 导入 `.config` | 文件选择完成后复核原设备和配置库，再复制到现有 Config 并调用 `Save()`。导入可改变设备 Code 和通信主题，并请求远端重启 |
| 数据基础路径 | 从设备配置顶部的 **配置路径** 打开独立窗口。**应用设置** 将路径保存到本机，并同步到当前设备的 `FileServerCfg.DataBasePath`。保存和重载行为见[文件保存配置](./file-server.md#文件保存配置与输出) |
| 删除设备 | 确认后复核权限和原配置库，以 ID、Code、Type、Tenant 和 Pid 核对目标。删除该行提交成功后，移出设备树、注销原显示控件并释放设备 |
| 删除终端 | 在原配置库的同一事务中删除终端和直属资源。提交后移出终端树与直属设备，注销原显示控件并释放运行对象与终端连接 |

确认期间原数据库连接失效、设备或终端被重载替换、权限被撤销时，取消删除。本地记录始终使用其原 SQLite 库。缺少原记录或写库失败时保留树和显示。终端删除只覆盖一层直属资源。相机单独删除后，再从原库更新对应许可证关联。

具体设备的会话、订阅及打开的视图由其 `Dispose()` 实现释放。`MQTTServiceBase.Dispose()` 负责退订该通信对象、释放计时器并结束待处理记录。数据库提交成功后，运行时释放或许可证更新失败会另行记录。

具体 `Device*` 可以覆盖上述方法，操作前应核对实际类型。PropertyGrid 的编辑会话、确定与关闭语义只在[属性契约](../../04-api-reference/ui-components/property-grid.md)维护；按钮是否可用不扩大 AI 的执行授权。

## 定位与验证

保存/创建失败时，分别记录：当前数据库和目标资源 ID/Type/Pid/Code、旧 Value 备份、编辑模式、数据库阶段结果、RC 连接与重启请求、设备端最终状态。只读诊断可以审查代码、脱敏日志与既有记录；不要通过改 Code、清库、重新导入或点击“重启服务”试探。

创建、保存与重启需在隔离环境核对旧 JSON 恢复、坏 JSON 保留证据、缺失目标行、RC 离线和后续阶段失败。分别确认数据库已提交、远端已生效和真实设备动作。

不访问真实数据库或操作设备。

验证共享 Config 的即时取值，以及删除设备时显示实例和通信订阅释放。
