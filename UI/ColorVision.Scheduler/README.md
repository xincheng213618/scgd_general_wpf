# ColorVision.Scheduler

> 版本: 1.5.5.1 | 目标框架: .NET 8.0 / .NET 10.0 Windows

## 功能定位

基于 Quartz.NET 的任务调度系统，提供定时任务的创建、管理和监控功能。

## 主要功能

### 任务管理
- **创建任务** (`CreateTask`) — 可视化创建定时任务（Cron 表达式、立即执行、延迟执行）
- **任务配置** (`IJobConfig`) — 任务配置接口，支持自定义任务类型
- **任务监控** — 实时查看任务执行状态和历史记录
- **批量操作** — 批量启动、暂停、删除任务

### 数据持久化
- 使用 SqlSugar + SQLite 存储任务配置和执行日志
- 启动时自动恢复历史任务

### 增强功能
- **任务优先级** — 1-10 级优先级
- **执行统计** — 成功/失败次数、执行时间统计
- **搜索过滤** — 按名称、分组、状态过滤
- **数据导出** — CSV/JSON 导出
- **右键菜单** — 编辑、暂停、继续、删除、立即执行

## 文件清单

| 文件 | 说明 |
|------|------|
| `CreateTask.xaml` + `.cs` | 创建任务窗口 |
| `IJobConfig.cs` | 任务配置接口 |
| `QuartzSchedulerManager.cs` | 调度器管理器 |
| `TaskViewerWindow.xaml` + `.cs` | 任务查看窗口 |

## 依赖关系

- **引用**: ColorVision.Common, ColorVision.Themes, ColorVision.UI, Quartz 3.18.0, SqlSugarCore
- **被引用**: ColorVision.UI.Desktop（通过菜单集成）

## 构建

```bash
dotnet build UI/ColorVision.Scheduler/ColorVision.Scheduler.csproj
```
