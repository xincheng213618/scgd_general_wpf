# ColorVision.Scheduler

`UI/ColorVision.Scheduler/` 是桌面侧任务调度与监控模块。核心链路是 Quartz 调度器、`scheduler_tasks.json` 任务配置、`SchedulerHistory.db` 执行历史和任务管理窗口。

## 先查什么

| 现象 | 第一检查点 |
| --- | --- |
| 创建任务窗口没有任务类型 | 任务程序集是否被 `AssemblyService` 加载，类型是否实现 `Quartz.IJob` |
| 升级后任务丢失 | `%AppData%/ColorVision/scheduler_tasks.json` 是否存在且能反序列化 |
| 历史记录为空 | `%AppData%/ColorVision/SchedulerHistory.db` 是否存在、路径/权限是否变化 |
| 到时间不执行 | Quartz 是否启动、任务是否暂停、Cron 表达式和时区是否正确 |
| 状态栏不显示 | `SchedulerStatusBarProvider` 是否被宿主加载，UI 初始化是否正常 |
| 包缺 README | 打开 `.nupkg` 看根目录 README，注意 `.csproj` 打包项路径 |

## 当前能力

| 能力 | 当前入口 | 说明 |
| --- | --- | --- |
| 调度器 | `QuartzSchedulerManager` | 启动 Quartz，扫描 `IJob`，维护 `TaskInfos`，创建/更新/暂停/恢复/删除任务 |
| 任务配置 | `%AppData%/ColorVision/scheduler_tasks.json` | 保存任务定义和恢复配置 |
| 执行历史 | `SchedulerDbManager`、`SchedulerHistory.db` | 保存执行记录、统计恢复和清理旧记录 |
| 执行监听 | `TaskExecutionListener` | 更新运行状态、成功失败、耗时和历史记录 |
| 管理窗口 | `TaskViewerWindow` | 过滤、右键操作、立即执行、查看历史 |
| 编辑窗口 | `CreateTask` | 新建/编辑任务，支持 `IConfigurableJob` 配置面板 |
| 菜单/状态栏 | `MenuTaskViewer`、`SchedulerStatusBarProvider` | 提供入口和调度状态展示 |

## 运行链路

1. `TaskViewerInitializer` 或菜单入口触发 `QuartzSchedulerManager.GetInstance()`。
2. 管理器启动 Quartz 调度器，并扫描已加载程序集里的 `IJob` 类型。
3. 读取 `%AppData%/ColorVision/scheduler_tasks.json`。
4. 启动后延迟恢复已有任务。
5. `TaskExecutionListener` 在任务执行前后更新状态和统计。
6. `SchedulerDbManager` 写入 `SchedulerHistory.db`。
7. `TaskViewerWindow` 展示任务状态、历史和统计。

## 新增任务类型

| 步骤 | 检查点 |
| --- | --- |
| 实现任务 | 新类型实现 `Quartz.IJob` |
| 显示名称 | 需要友好名称时加 `DisplayNameAttribute` |
| 配置面板 | 有配置时实现 `IConfigurableJob` 或配套 `IJobConfig` |
| 加载程序集 | 任务所在程序集能被 `AssemblyService` 收集 |
| 验证闭环 | 在 `TaskViewerWindow` 新建任务，检查 JSON 配置和 SQLite 历史 |

## 发布验收

| 验收项 | 要查什么 |
| --- | --- |
| 目标框架 | `ColorVision.Scheduler.csproj` 的 `net8.0-windows7.0;net10.0-windows7.0` |
| 包依赖 | `Quartz`、`SqlSugarCore`、`SQLitePCLRaw.bundle_e_sqlite3`、`Newtonsoft.Json`、`ColorVision.UI` |
| README | `.nupkg` 根目录是否真的包含 README |
| 菜单和状态栏 | 菜单能打开任务窗口，状态栏能反映任务数量/状态 |
| 任务类型扫描 | 已加载程序集里的 `IJob` 能出现在创建窗口 |
| 配置恢复 | 升级后 JSON 任务不被空配置覆盖 |
| 历史恢复 | 执行次数、成功失败数、耗时统计能跨重启保留 |
| 基础操作 | 新建、编辑、暂停、恢复、立即执行、删除、查看历史都能闭环 |

## 边界

- 任务类型来自已加载程序集扫描，不是数据库任务类型表。
- 任务定义主要在 JSON；执行历史和统计恢复在 SQLite，不要混成单一数据库中心。
- `TaskViewerWindow` 和 `CreateTask` 是当前真实用户入口；不要写未落地的复杂报表/批量导出能力。

## 关键文件

| 任务 | 先看 |
| --- | --- |
| 调度器启动和恢复 | `QuartzSchedulerManager.cs`、`MenuTaskViewer.cs` |
| 任务界面和操作 | `TaskViewerWindow.xaml(.cs)`、`CreateTask.xaml(.cs)` |
| 执行历史和统计 | `Data/SchedulerDbManager.cs`、`TaskExecutionListener.cs`、`ExecutionHistoryWindow.xaml(.cs)` |
| 任务配置模型 | `SchedulerInfo.cs`、`IJobConfig.cs` |
