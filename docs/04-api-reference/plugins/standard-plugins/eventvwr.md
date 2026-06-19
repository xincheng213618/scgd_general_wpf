# EventVWR 插件

本页只描述当前仓库里实际存在的 EventVWR 插件实现，不再继续维护“完整子系统手册 + 理想化 API 表”的旧稿。

## 先看这个插件现在做什么

从当前源码看，EventVWR 主要做两件事：

- 提供一个只读的 Windows Application 事件错误查看窗口。
- 提供一组 Dump 配置菜单，用于写入或清除 Windows Error Reporting 的 LocalDumps 注册表项。

因此它不是一个复杂的诊断平台，而是“事件窗口 + Dump 配置菜单”两条很直接的功能链。

## 当前最关键的文件

- `Plugins/EventVWR/EventVWRPlugins.cs`
- `Plugins/EventVWR/ExportEventWindow.cs`
- `Plugins/EventVWR/EventWindow.xaml(.cs)`
- `Plugins/EventVWR/Dump/DumpConfig.cs`
- `Plugins/EventVWR/Dump/MenuDump.cs`
- `Plugins/EventVWR/manifest.json`

如果只是想弄清插件如何进入宿主、如何打开事件窗口、如何修改 Dump 设置，这几处代码已经足够。

## 当前接入宿主的两条菜单链

### 事件窗口入口

`ExportEventWindow` 继承 `MenuItemBase`，当前挂在 `Help` 菜单下：

- `OwnerGuid = "Help"`
- `GuidId = "EventWindow"`
- `Order = 1000`

执行时会打开 `EventWindow` 对话框。

这个入口还有一个重要约束：`Execute()` 当前带有 `RequiresPermission(PermissionMode.Administrator)`，说明它不是纯本地辅助菜单，而是受宿主权限模式约束的。

### Dump 设置入口

`MenuDump` 也是 `Help` 菜单下的一个父级菜单项，`MenuThemeProvider` 则继续为它提供子菜单：

- 各 `DumpType` 枚举项
- 清空 Dmp
- 保存 Dmp

因此 EventVWR 当前不是只有一个窗口入口，而是帮助菜单下的两组独立能力。

## 事件窗口当前怎么工作

`EventWindow.xaml.cs` 的逻辑非常直接：

1. 窗口初始化时打开 Windows `Application` 事件日志。
2. 读取所有 `EventLogEntry`。
3. 只保留 `EntryType == Error` 的事件。
4. 按 `TimeGenerated` 倒序排列。
5. 把结果绑定到左侧列表。
6. 选择某条记录时，把 `Message` 显示到详情区域。

这意味着当前窗口并没有复杂的筛选器、搜索器或异步分页逻辑，本质上是一个“错误事件快速浏览器”。

## Dump 配置当前怎么落地

`DumpConfig` 负责真正的系统设置写入，当前核心点包括：

- 目标注册表路径是 `HKLM\SOFTWARE\Microsoft\Windows\Windows Error Reporting\LocalDumps`。
- 会优先读取默认 LocalDumps 配置，再覆盖到当前进程对应的 `LocalDumps\{Name}.exe`。
- 当前管理的关键字段有：
  - `DumpFolder`
  - `DumpCount`
  - `DumpType`
  - `CustomDumpFlags`

写入配置和清除配置都要求管理员权限；如果当前不是管理员，会直接弹窗提示而不继续执行。

除了注册表配置外，`SaveDump()` 还会调用 `DumpHelper.WriteMiniDump(...)`，把当前进程转储写到目标目录。

## 反馈包收集边界

`DumpFileCollector` 还实现了 `IFeedbackLogCollector` 风格的转储文件收集能力。它会读取 `DumpConfig` 当前解析到的 `DumpFolder`，把目录中的 `.dmp` 文件复制到临时目录，并在反馈包中以 `Dumps/<file>` 的名字提供。

这条链路只收集已经存在的 dump 文件，不会主动修改注册表，也不会触发新的进程转储。现场交接时要区分：

| 能力 | 入口 | 是否写系统配置 |
| --- | --- | --- |
| 设置 LocalDumps | Help -> Dump 设置子菜单 | 是，写 HKLM |
| 保存当前进程 Dump | Help -> Dump 设置 -> 保存 Dmp | 否，但会写 dump 文件 |
| 反馈包收集 Dump | 反馈/日志收集链 | 否，只复制现有 `.dmp` |

## 当前 manifest 信息

按 `manifest.json`，这个插件当前公开的基本信息是：

- `Id = "EventVWR"`
- `name = "事件插件"`
- `version = "1.0"`
- `dllpath = "EventVWR.dll"`
- `requires = "1.3.15.10"`

这比旧文档里那种“目标框架、依赖矩阵、完整 API 表”更接近当前插件装载模型真正关心的信息。

## 当前几个容易写错的点

### 它不是完整的事件诊断中心

当前实现只读取 Windows Application 日志中的错误项，并展示消息文本。不要把它继续写成带高级检索、导出和多日志源分析的平台。

### Dump 配置是系统级写入

`DumpConfig` 当前操作的是 HKLM 下的 LocalDumps 注册表项，不是应用内部配置文件。也正因为这样，写入和清理都要求管理员权限。

### 插件入口类本身很轻

`EventVWRPlugins` 现在只是一个很薄的 `IPluginBase` 壳，主要提供 Header 和 Description。真正的功能入口并不在这里，而在菜单项和对应窗口/配置类里。

### 权限边界分成两层

- 事件窗口菜单入口本身受 `RequiresPermission(PermissionMode.Administrator)` 约束。
- Dump 注册表写入和清理还会在运行时再次检查是否具备管理员权限。

如果只记录其中一层，文档就会把当前行为说得过于简单。

## 推荐阅读顺序

1. `Plugins/EventVWR/ExportEventWindow.cs`
2. `Plugins/EventVWR/EventWindow.xaml.cs`
3. `Plugins/EventVWR/Dump/DumpConfig.cs`
4. `Plugins/EventVWR/Dump/MenuDump.cs`
5. `Plugins/EventVWR/Dump/DumpFileCollector.cs`
6. `Plugins/EventVWR/manifest.json`

这样能先看到宿主入口，再看到窗口行为和系统级配置落点。

## 交接验收表

| 验收项 | 操作 | 通过标准 |
| --- | --- | --- |
| 插件加载 | 启动主程序并查看 Help 菜单 | 能看到事件窗口入口和 Dump 设置入口 |
| 权限约束 | 普通权限和管理员权限分别打开事件窗口 | 普通权限被权限链拦截，管理员权限能打开 |
| 事件读取 | 打开事件窗口 | 能读取 Windows `Application` 日志中的 Error 项，详情显示 `Message` |
| Dump 类型设置 | 在 Dump 子菜单选择 Mini/Full/Custom 等类型 | HKLM LocalDumps 对应进程项写入 `DumpType` 和必要字段 |
| 保存当前 Dump | 点击保存 Dmp | `DumpFolder` 下生成当前进程 `.dmp` |
| 清理设置 | 点击清空 Dmp | 当前进程 LocalDumps 注册表项被清理 |
| 反馈收集 | 触发反馈日志收集 | 已存在 `.dmp` 被复制到反馈包的 `Dumps/` 目录 |

## 故障首查

| 现象 | 首查点 | 处理 |
| --- | --- | --- |
| 菜单没有出现 | 插件是否加载、`manifest.json` 的 `dllpath` 是否正确 | 先查插件管理器和加载日志 |
| 打不开事件窗口 | 当前是否管理员、权限装饰是否生效 | 事件窗口入口本身需要管理员权限 |
| Dump 设置失败 | HKLM 写入权限、注册表路径 | 必须以管理员模式运行 |
| 保存 Dump 没文件 | `DumpFolder` 是否存在/可写，`DumpHelper.WriteMiniDump` 是否报错 | 先手动确认目录权限 |
| 反馈包没有 Dump | `DumpFolder` 是否已有 `.dmp` | 收集器不会主动生成 dump |

## 继续阅读

- [现有插件现场验收与交接清单](../plugin-field-acceptance.md)
- [插件能力与交接矩阵](../plugin-capability-matrix.md)
- [Plugins/README.md](../../../../Plugins/README.md)
- [docs/02-developer-guide/plugin-development/overview.md](../../../02-developer-guide/plugin-development/overview.md)
- [docs/04-api-reference/plugins/standard-plugins/system-monitor.md](./system-monitor.md)
