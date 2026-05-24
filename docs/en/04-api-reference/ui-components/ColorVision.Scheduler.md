# ColorVision.Scheduler

This page only describes the scheduling capabilities currently implemented in `UI/ColorVision.Scheduler/`, no longer continuing the old documentation's "general Quartz tutorial + imagined task platform feature checklist."

## Module Positioning

`ColorVision.Scheduler` is currently the desktop-side task scheduling and monitoring module. Its core is not an "encyclopedia of abstract task types," but these three real chains:

- `QuartzSchedulerManager` manages the Quartz scheduler and task recovery
- `scheduler_tasks.json` stores task configurations
- `SchedulerHistory.db` stores execution history and statistical recovery data

So it is neither a pure UI control nor just a Quartz wrapper layer.

## Most Critical Files

From the project directory, the most worthwhile to recognize first are:

- `QuartzSchedulerManager.cs`: Scheduler main entry point
- `TaskViewerWindow.xaml(.cs)`: Task viewing, filtering, and right-click operation window
- `CreateTask.xaml(.cs)`: New and edit task window
- `TaskExecutionListener.cs`: Execution listener and statistics update
- `Data/SchedulerDbManager.cs`: History record SQLite persistence
- `MenuTaskViewer.cs`: Menu entry point and initializer
- `SchedulerInfo.cs`: Task display and persistence model

## Key Entry Point Types

### `QuartzSchedulerManager`

`QuartzSchedulerManager` is the central object of the current scheduling module. It is responsible for:

- Starting the Quartz scheduler
- Scanning loaded assemblies for `IJob` types
- Maintaining `TaskInfos`
- Loading task configurations from JSON file
- Recovering historical tasks after startup
- Providing pause, resume, delete, update, and create task methods

The current task configuration file is placed by default at:

- `%AppData%/ColorVision/scheduler_tasks.json`

This indicates that current task definitions are not entirely stored in a database, but primarily use JSON configuration with SQLite history as a supplement.

### `TaskViewerWindow`

`TaskViewerWindow` is the current task management main window. It is responsible for:

- Binding `TaskInfos`
- Filtering by name, group, status
- Reading registered tasks' next and last execution times from the scheduler
- Executing edit, view properties, pause, resume, execute immediately, delete, view history via right-click menu

The "comprehensive monitoring panel design diagrams" in the old documentation of this page are less valuable references than the actual window here.

### `CreateTask`

The `CreateTask` window handles creating and editing tasks. It works with `SchedulerInfo` to determine how a task is ultimately serialized, recovered, and updated.

### `SchedulerDbManager`

Execution history is not stored in the same JSON file, but separately in a SQLite database. `SchedulerDbManager` is currently responsible for:

- Initializing `%AppData%/ColorVision/SchedulerHistory.db`
- Writing execution records
- Querying single-task or full execution history
- Calculating statistical data for post-restart recovery
- Cleaning old records

This is also why data like "run count, success/failure count, average duration" can persist after restart.

### `TaskExecutionListener`

Runtime statistical updates and execution feedback are not obtained by the window polling itself, but through listener write-back of task status and execution history.

## Current Runtime Main Chain

The scheduling module is currently closer to the following chain:

1. `TaskViewerInitializer` or menu entry triggers `QuartzSchedulerManager.GetInstance()`.
2. `QuartzSchedulerManager` starts the Quartz scheduler.
3. It scans `IJob` types in currently loaded assemblies and builds a task type dictionary.
4. Reads `%AppData%/ColorVision/scheduler_tasks.json`.
5. Recovers existing tasks after startup with a delay.
6. `TaskExecutionListener` updates status and statistics during task execution.
7. `SchedulerDbManager` writes execution records to `SchedulerHistory.db`.
8. `TaskViewerWindow` then displays these statuses, histories, and statistics to the user.

This chain is closer to the existing implementation than the old documentation's "task editor/monitor panel/log viewer three-layer architecture."

## What Boundaries the Current Implementation Has

### Task Types Come from Loaded Assemblies

Currently, `QuartzSchedulerManager` iterates through `AssemblyService.Instance.GetAssemblies()`, collects types implementing `IJob`, and preferentially uses `DisplayNameAttribute` as the display name.

So adding a new task type essentially means adding an `IJob` implementation that can be scanned by assemblies, rather than registering in some task type table.

### Configuration Recovery and Execution History Are Two Storage Systems

Current task definition and recovery mainly rely on JSON; execution history and statistical recovery mainly rely on SQLite. Do not conflate these two into a single database scheduling center.

### The Task Window Is a Real Management Entry Point, Not a Diagram

The most important user entry points currently are `TaskViewerWindow` and `CreateTask`. Many fabricated features in old documentation such as "batch export, statistical reports, complex panel partitions" do not need to continue being listed as existing capabilities unless the code can directly correspond to specific implementations.

## How to Better Read This Project Currently

### To View How the Scheduler Starts and Recovers

Read first:

- `QuartzSchedulerManager.cs`
- `MenuTaskViewer.cs`

### To View Task Interface and Operation Entry Points

Read first:

- `TaskViewerWindow.xaml(.cs)`
- `CreateTask.xaml(.cs)`

### To View Execution History and Statistics

Read first:

- `Data/SchedulerDbManager.cs`
- `TaskExecutionListener.cs`
- `ExecutionHistoryWindow.xaml(.cs)`

## What This Page No Longer Does

This page no longer continues to maintain these high-risk contents:

- General Quartz sample code encyclopedia
- Unverified system task/business task/maintenance task classification tables
- Imagined unified task platform feature matrix
- Outdated version numbers and target framework lists

If a specific task type needs to be supplemented later, it should directly land on the actual task implementation or window page, rather than continuing to write tutorial-style content here.

## Continue Reading

- [UI Components Overview](./README.md)
- [ColorVision.UI](./ColorVision.UI.md)
- [ColorVision.Database](./ColorVision.Database.md)