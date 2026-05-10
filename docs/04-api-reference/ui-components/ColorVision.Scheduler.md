# ColorVision.Scheduler

本页只描述 `UI/ColorVision.Scheduler/` 当前已经落地的调度能力，不再继续维护旧文档里那种“通用 Quartz 教程 + 想象中的任务平台功能清单”。

## 模块定位

`ColorVision.Scheduler` 当前是桌面侧的任务调度与监控模块，核心不是“抽象任务类型大全”，而是这三条真实链：

- `QuartzSchedulerManager` 管理 Quartz 调度器和任务恢复
- `scheduler_tasks.json` 保存任务配置
- `SchedulerHistory.db` 保存执行历史和统计恢复数据

所以它既不是纯 UI 控件，也不是只有 Quartz 包装层。

## 当前最关键的文件

从项目目录看，最值得先认识的是：

- `QuartzSchedulerManager.cs`：调度器主入口
- `TaskViewerWindow.xaml(.cs)`：任务查看、过滤和右键操作窗口
- `CreateTask.xaml(.cs)`：新建和编辑任务窗口
- `TaskExecutionListener.cs`：执行监听与统计更新
- `Data/SchedulerDbManager.cs`：历史记录 SQLite 持久化
- `MenuTaskViewer.cs`：菜单入口和初始化器
- `SchedulerInfo.cs`：任务展示与持久化模型

## 关键入口类型

### `QuartzSchedulerManager`

`QuartzSchedulerManager` 是当前调度模块的中心对象。它负责：

- 启动 Quartz 调度器
- 扫描已加载程序集中的 `IJob` 类型
- 维护 `TaskInfos`
- 从 JSON 文件加载任务配置
- 在启动后恢复历史任务
- 提供暂停、恢复、删除、更新和创建任务的方法

当前任务配置文件默认放在：

- `%AppData%/ColorVision/scheduler_tasks.json`

这说明当前任务定义并不是完全存在数据库里，而是以 JSON 配置为主、SQLite 历史为辅。

### `TaskViewerWindow`

`TaskViewerWindow` 是当前任务管理主窗口。它负责：

- 绑定 `TaskInfos`
- 按名称、分组、状态过滤
- 从调度器读取已注册任务的下一次和上一次执行时间
- 通过右键菜单执行编辑、查看属性、暂停、继续、立即执行、删除、查看历史

这页旧文档里那些“大而全的监控面板设计图”都不如这里的实际窗口更有参考价值。

### `CreateTask`

`CreateTask` 窗口承担新建和编辑任务。它和 `SchedulerInfo` 配合，决定一个任务最终如何被序列化、恢复和更新。

### `SchedulerDbManager`

执行历史不是存在同一个 JSON 文件里，而是单独存在 SQLite 数据库中。`SchedulerDbManager` 当前负责：

- 初始化 `%AppData%/ColorVision/SchedulerHistory.db`
- 写入执行记录
- 查询单任务或全量执行历史
- 计算统计数据用于重启后恢复
- 清理旧记录

这也是当前“运行次数、成功失败数、平均耗时”这类数据能在重启后延续的原因。

### `TaskExecutionListener`

运行时统计更新和执行反馈，并不是窗口自己轮询拿到的，而是通过监听器回写任务状态和执行历史。

## 当前运行时主链

调度模块当前更接近下面这条链：

1. `TaskViewerInitializer` 或菜单入口触发 `QuartzSchedulerManager.GetInstance()`。
2. `QuartzSchedulerManager` 启动 Quartz 调度器。
3. 它扫描当前已加载程序集里的 `IJob` 类型，建立任务类型字典。
4. 读取 `%AppData%/ColorVision/scheduler_tasks.json`。
5. 启动后延迟恢复已有任务。
6. `TaskExecutionListener` 在任务执行时更新状态与统计。
7. `SchedulerDbManager` 把执行记录写入 `SchedulerHistory.db`。
8. `TaskViewerWindow` 再把这些状态、历史和统计展示给用户。

这个链路比旧文档里那种“任务编辑器/监控面板/日志查看器三层架构”更贴近现有实现。

## 当前实现有哪些边界

### 任务类型来自已加载程序集

当前 `QuartzSchedulerManager` 会遍历 `AssemblyService.Instance.GetAssemblies()`，收集实现 `IJob` 的类型，并优先用 `DisplayNameAttribute` 作为显示名。

所以新增任务类型，本质上是新增可被程序集扫描到的 `IJob` 实现，而不是往某张任务类型表里登记。

### 配置恢复和执行历史是两套存储

当前任务定义和恢复主要靠 JSON；执行历史和统计恢复主要靠 SQLite。不要把这两者混写成单一数据库调度中心。

### 任务窗口是真实管理入口，不是示意图

当前最重要的用户入口就是 `TaskViewerWindow` 和 `CreateTask`。很多旧文档里编造的“批量导出、统计报告、复杂面板分区”并没有必要继续作为既有能力列出，除非代码里能直接对应到具体实现。

## 当前更适合怎样读这个项目

### 想看调度器怎么启动和恢复

先看：

- `QuartzSchedulerManager.cs`
- `MenuTaskViewer.cs`

### 想看任务界面和操作入口

先看：

- `TaskViewerWindow.xaml(.cs)`
- `CreateTask.xaml(.cs)`

### 想看执行历史和统计

先看：

- `Data/SchedulerDbManager.cs`
- `TaskExecutionListener.cs`
- `ExecutionHistoryWindow.xaml(.cs)`

## 这页不再做什么

本页不再继续维护这些高风险内容：

- 通用 Quartz 示例代码大全
- 未经核实的系统任务/业务任务/维护任务分类表
- 想象中的统一任务平台功能矩阵
- 过时版本号和目标框架清单

如果后续要补某个具体任务类型，应直接落到实际任务实现或窗口页，而不是在这里继续写教程式内容。

## 继续阅读

- [UI组件概览](./README.md)
- [ColorVision.UI](./ColorVision.UI.md)
- [ColorVision.Database](./ColorVision.Database.md)