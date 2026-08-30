---
knowledge_id: "engine.mysql-recovery"
knowledge_type: "topic"
status: "current"
summary: "MySQL手动SQL恢复、数据库重置与资源保留：导入后才同步配置和重启注册中心，失败不回滚；迁移备份不含结果，配置更新计数不证明键完整。"
aliases: ["MySQL数据库恢复", "加载SQL备份", "数据库重置", "资源迁移备份", "恢复失败但数据已改变", "恢复后重启", "Sensor字典ID重映射", "MySqlDatabaseMaintenanceService", "RestoreAndRestartAsync", "RestoreSqlFileAsync", "RestoreMysql", "ResetDatabaseFromSqlFileAsync", "SynchronizeInstalledServiceConfigs", "UpdateConfigFile", "MigrationBackupTableNames", "BuildMigrationDictionaryDependencyStatements", "SensorTemplateMigrationSqlBuilder", "MySqlRestoreProgressWindow"]
code_paths: ["Engine/ColorVision.Engine/Mysql/MySqlDatabaseMaintenanceService.cs", "Engine/ColorVision.Engine/Mysql/MySqlLocalServicesManager.cs", "Engine/ColorVision.Engine/Mysql/MySqlRestoreProgressWindow.xaml", "Engine/ColorVision.Engine/Mysql/MySqlRestoreProgressWindow.xaml.cs", "Engine/ColorVision.Engine/Mysql/MySqlToolWindow.xaml", "Engine/ColorVision.Engine/Mysql/SensorTemplateMigrationSqlBuilder.cs", "Engine/ColorVision.Engine/Services/RC/RCInitializer.cs", "UI/ColorVision.Database/MySqlControl.cs", "UI/ColorVision.Database/MySqlSetting.cs", "UI/ColorVision.Database/MySqlProtocolDefaults.cs", "UI/ColorVision.UI/ServiceHost/IColorVisionServiceHostClient.cs"]
test_paths: ["Test/ColorVision.UI.Tests/MySqlBackupRestoreSafetyTests.cs", "Test/ColorVision.UI.Tests/MySqlMigrationBackupTableTests.cs", "Test/ColorVision.UI.Tests/SensorTemplateMigrationTests.cs"]
related: ["engine.mysql-maintenance", "engine.database-maintenance", "plugins.windows-service", "platform.service-host", "operations.data", "engine.template-design"]
---

# MySQL SQL 恢复、重置与资源保留

`MySqlDatabaseMaintenanceService` 是 Engine 的 SQL 恢复、数据库重置和服务 MySQL 配置同步实现，虽在 `ColorVision.Database` 命名空间，文件实际位于 `Engine/ColorVision.Engine/Mysql/`。本主题区分它的底层接口与桌面编排；[结果清理](./mysql-maintenance.md)不负责重置，[WindowsServicePlugin](../plugins/standard-plugins/windows-service.md)负责服务版本到库名的选择、安装文件替换及业务账号授权。

恢复不是查看备份或撤销操作：SQL 可以修改数据库，后续还可能改服务配置、重启服务和应用。必须另行确认脚本来源、实际主机/库/账号、业务停写、可恢复副本和操作权限；文档核对不得调用这些接口、启动应用或连接用户数据库。这里没有统一 dry-run、恢复演练或全链回滚保证。

## 入口决定执行范围

| 入口 | 当前行为 | 不包含的保证 |
| --- | --- | --- |
| 工具窗口“加载备份”、备份项“还原” | 均进入 `RestoreAndRestartAsync`：SQL导入 → 服务配置同步 → 重启注册中心 | 不先自动备份/停写，不自动重启ColorVision，不代表全部服务健康 |
| `MySqlLocalServicesManager.RestoreMysql` | 在管理器维护门内同步等待 SQL 导入，成功返回文件完整路径 | 不同步服务配置、不重启服务，没有桌面确认或进度协议 |
| `MySqlDatabaseMaintenanceService.RestoreSqlFileAsync` | 直接委托 `ExecuteSqlFileCoreAsync`，可选择是否在mysql参数中指定库 | 自身不取得管理器维护门、不提权、不验证脚本的业务范围 |
| `ResetDatabaseFromSqlFileAsync` | 保留源资源数据 → 执行安装SQL → 检查目标连接 → 回写资源 | 不推导目标版本、不保证完整迁移，也不同步配置或启停服务 |

桌面恢复使用当次 `MySqlSetting.Instance.MySqlConfig` 和 `Config.MysqlPath`，不自动切换root账号。重置使用调用者传入的 `rootConfig`，方法名和日志中的“root”不证明账号身份/权限已校验。安装插件如何构造配置和选择源/目标库，以插件专题为准。

## 手动恢复的阶段与完成含义

`RestoreAndRestartAsync` 首先非阻塞尝试进入进程内维护门，已有其它维护任务时提示并返回；同调用链允许嵌套。取得门后打开进度窗口，执行 SQL，然后同步配置；同步返回的文件数为0时抛错，提示“数据库已导入，但已停止服务重启”。只有配置同步通过才调用 ServiceHost 重启固定的 `RegistrationCenterService`（服务超时60秒、请求等待90秒）。请求等待超时不代表代理动作取消，见[本机权限代理](../../03-architecture/components/service-host.md)。

这个顺序没有导入前自动完整备份、停止采集/流程或停服务步骤，也没有额外“确认后再导入”的弹窗。管理器维护门只约束使用该门的进程内动作，不阻止其它 DAO、直接 SQL 或外部进程写入；门的机制与完整备份边界见[结果维护](./mysql-maintenance.md)。底层恢复和重置接口不会自动加入该门。

进度窗口的库名是创建时显示值，导入及之后配置同步会读取当前设置，没有整链不可变目标快照。维护期间不能假定更改连接设置不影响后续阶段。进度百分比表示阶段，不是已导入行数或完成比例。

SQL导入、配置写入和注册中心重启不在同一事务中。执行阶段异常被捕获、记日志并将窗口标为失败，没有恢复原数据/配置的补偿；因此方法的 `Task` 正常完成也可能是忙碌拒绝或界面已报告失败，不能当成业务成功返回值。SQL已导入但配置/重启失败时，先核对最后完成阶段，不自行重跑可能破坏数据的脚本。

运行中的 `MySqlRestoreProgressWindow` 拒绝关闭，没有取消令牌或取消按钮；它与“关窗不取消后台动作”的[数据库清理窗口](./database-maintenance.md)不同。执行结束后可关闭；只有成功时启用“重启ColorVision”，由用户点击启动新进程 `-r`，创建进程成功后才关闭当前应用。窗口显示成功表示导入、配置同步和注册中心重启响应通过，不验证全部业务服务、模板/结果语义或新应用就绪。

## 重置前保留什么

重置不是“完整备份后安全覆盖”。`MigrationBackupTableNames` 当前恰为六张服务设置表与三张配置表，和结果清理白名单分离：

| 定义 | 表 |
| --- | --- |
| `ServiceSettingTableNames` | `t_scgd_algorithm_poi_template_detail/master`、`t_scgd_buz_product_detail/master`、`t_scgd_mod_param_detail/master`；这里的detail/master表示各两张表 |
| `ServiceConfigurationTableNames` | `t_scgd_camera_license`、`t_scgd_sys_resource`、`t_scgd_sys_resource_group` |

源码数组是准确表名的权威位置。它不含算法结果、测量批次/结果、外部图片、其它库或项目SQLite。重置先查源库 `BASE TABLE` 并取上述白名单交集；没有匹配表就跳过资源导出，不要求九张表全部存在，也不验证每张表有行。普通“迁移备份”按钮的 `BackupMysqlResource` 则直接使用完整白名单，不能把两种入口视为同一实现。

重置的私有备份使用唯一 `color_vision_resources_<时间>_<GUID>.sql.part`，调用 `RunMysqlDumpAsync(dataOnly: true, replaceExistingRows: true)`：显式加 `--single-transaction`、`--quick`、`--skip-triggers`、`--skip-lock-tables`、`--skip-add-locks`、`--no-create-info`、`--complete-insert`、`--replace`。这不是schema或触发器备份，也不证明跨表引擎、跨阶段的一致快照。普通 `BackupMysqlResource` 没有传 `dataOnly: true`，不能套用这些参数。

导出后追加字典依赖再将part改名为SQL；失败尝试删除part，已完成的SQL文件不因后续重置失败而自动删除。这条私有备份链不加入管理器 `Backups` 列表，也没有普通 `CreateMySqlBackupFile` 的显式非空文件门禁；外部工具正常退出不等于内容完整或可恢复。

`BuildMigrationDictionaryDependencyStatements` 补充普通模板实际引用的字典主档/项，排除 `mod_type=5` 的通用Sensor字典，生成 `INSERT IGNORE`；这不是全部字典备份，同ID目标行已存在时也不是覆盖更新。Sensor另由 `SensorTemplateMigrationSqlBuilder` 按业务code和命令symbol生成引用重映射，兼顾旧 `Sensor.*` 名称推导及缺失命令补位，不直接照抄旧字典ID。

依赖SQL是在dump之后另行查询，不和dump共享事务。依赖构建异常会记录并返回空字符串，重置可继续使用没有补充依赖的资源SQL；不能将“资源备份完成”解释为全部模板依赖已验证。Sensor的默认命令补位也不是恢复丢失命令的原始内容。

## 重置执行与失败位置

`ResetDatabaseFromSqlFileAsync` 的顺序是：

1. 拒绝空源/目标库名及不存在的脚本。`rootConfig` 空值、路径解析等发生在内部try之前，不能承诺所有错误都返回false。
2. 跨库名时先测试源库连接；源库不可连接即返回false，不执行重置SQL。同库名没有这项独立预检，但资源表查询仍可能失败。
3. 备份实际存在的资源表；此阶段抛出的异常阻止执行重置SQL，但前述依赖构建内部吞错不属于这项门禁。
4. 调用底层导入，`selectDatabase: false`，不把源库作为mysql的默认数据库参数；实际创建、删除或选库由脚本内容决定，并非helper自动替换脚本中的库名。
5. 脚本执行后，对传入目标库执行连接/`SELECT 1` 检查。失败返回false，**不撤销已经执行的脚本**；检查通过只说明能连接，甚至不证明该库由本次脚本新建或schema正确。
6. 有保留SQL时以 `selectDatabase: true` 回写目标库；没有则直接返回true。导入过程异常返回false，可能已有部分写入，没有整体事务、自动重试或回滚。

`true` 表示这条代码路径的检查通过，不证明所有源数据、表关系、业务账号权限或模板可用。源与目标的主机/凭据由传入配置克隆，方法不支持通过两个独立连接配置声明跨服务器迁移。

## 服务配置同步的实际保证

`SynchronizeInstalledServiceConfigs` 从 `ServiceConfig.Instance` 取注册中心、x64主服务、dev主服务的路径，按顺序定位各自 `cfg/MySql.config`。未配置路径或文件不存在只记录并跳过，不创建文件；已有文件解析/写入异常向上传播，之前写过的文件不回滚。返回列表只统计本次调用了 `UpdateConfigFile` 并正常返回的路径，不是三个服务配置齐全的证明。

`UpdateConfigFile` 要求已有 `configuration/appSettings`，仅更新已存在的、大小写精确为 `Host`、`Port`、`User`、`Password`、`Database` 的add项，其它项保留。它不补缺失键；空appSettings、缺部分键也可以保存并算一次更新。保存直接写原文件，没有临时文件替换或自动备份。不能从“更新1个文件”推出连接参数完整、所有服务已切库或写入失败时原文件完整。

这里同步的是服务XML配置，不是模板迁移，也不负责保存应用设置或重载数据库连接。实际内容含凭据，不要把原文件或未经脱敏的日志写入文档/诊断回答。插件的其它MQTT/WinService/旧App.config同步仍归插件专题，不由本接口覆盖。

## SQL执行与验证范围

底层导入只检查路径、`.sql`扩展名、存在和非空，按流输入 `mysql.exe`；默认库参数不会限制脚本中的 `USE`、DDL或跨库语句。成功返回脚本完整路径，不返回业务行数校验结果；进程非零/流失败可能发生在部分SQL已经执行之后。字符集、密码传递、两小时预算及尝试终止子进程树的规则见[外部工具边界](./mysql-maintenance.md#外部工具与错误边界)，终止客户端不构成数据库回滚。

- `MySqlBackupRestoreSafetyTests` 包括进程参数、维护门、入口源码检查及临时XML更新；返回路径用例实际只检查方法返回类型，不能按测试名推断做过真实恢复。
- `MySqlMigrationBackupTableTests` 检查九张保留表组合及其与结果清理表分离，不验证真库存在、导出内容或schema兼容。
- `SensorTemplateMigrationTests` 对合成数据生成SQL，检查业务code推导、ID冲突重映射和健康命令保留的字符串；没有在MySQL中执行生成脚本。

现存这些测试不覆盖真实dump/import、缺依赖仍成功、配置缺键/部分写失败、并发切配置或服务重启失败后的恢复验收。本次只核对源码与测试内容并验证文档，没有执行产品测试、连接数据库、调用ServiceHost或重启应用。
