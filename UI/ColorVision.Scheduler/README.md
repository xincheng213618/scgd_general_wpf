# ColorVision.Scheduler

桌面侧 Quartz 调度组件：`QuartzSchedulerManager` 管理任务定义，JSON 保存调度意图，SQLite 保存执行历史；真正的设备与流程动作由各 `IJob` 实现负责。目标框架、依赖及版本以 [ColorVision.Scheduler.csproj](./ColorVision.Scheduler.csproj) 为准。

当前行为、恢复、并发、结果和验证边界统一见[Quartz 任务定义、恢复与执行历史](../../docs/04-api-reference/ui-components/ColorVision.Scheduler.md)（`ui.scheduler`）。

- 首次 `GetInstance()` 会加载定义、初始化历史库并开始异步恢复；窗口或状态栏也可能触发它。未获运行授权时不要把这些入口当作只读检查。
- 暂停只限制后续触发，不终止已经开始的 Job；超时或取消是否结束真实设备操作，必须核对具体 Job，不能仅看列表状态。

从仓库根目录在 Windows 上构建：会还原依赖并写入输出，项目启用了 `GeneratePackageOnBuild`；这不是发布，也不代表运行验证。

```powershell
dotnet build .\UI\ColorVision.Scheduler\ColorVision.Scheduler.csproj -p:Platform=x64
```
