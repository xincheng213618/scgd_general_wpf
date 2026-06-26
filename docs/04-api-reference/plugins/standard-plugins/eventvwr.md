# EventVWR 插件

EventVWR 是 `Plugins/EventVWR/` 下的诊断插件，当前只做两件事：查看 Windows Application 错误事件，以及配置/收集 Windows Error Reporting LocalDumps。

## manifest

| 字段 | 当前值 |
| --- | --- |
| `Id` | `EventVWR` |
| `name` | `事件插件` |
| `version` | `1.0` |
| `dllpath` | `EventVWR.dll` |
| `requires` | `1.3.15.10` |

## 先查什么

| 现象 | 第一检查点 |
| --- | --- |
| Help 菜单没有入口 | 插件是否加载、`manifest.json`、`dllpath`、加载日志 |
| 事件窗口打不开 | 宿主权限模式；入口带 `RequiresPermission(PermissionMode.Administrator)` |
| 事件列表为空 | Windows `Application` 日志是否有 `Error` 项 |
| Dump 设置失败 | 是否管理员运行、HKLM 注册表写权限 |
| 保存 Dump 没文件 | `DumpFolder` 是否存在/可写，`DumpHelper.WriteMiniDump` 是否报错 |
| 反馈包没有 Dump | `DumpFolder` 是否已有 `.dmp`；收集器不会主动生成 dump |

## 当前能力

| 能力 | 当前入口 | 说明 |
| --- | --- | --- |
| 插件壳 | `EventVWRPlugins.cs` | 很薄的 `IPluginBase`，只提供 Header/Description |
| 事件窗口菜单 | `ExportEventWindow.cs` | Help 菜单入口，管理员权限约束 |
| 事件窗口 | `EventWindow.xaml(.cs)` | 读取 Windows `Application` 日志中的 Error 项并显示 `Message` |
| Dump 菜单 | `Dump/MenuDump.cs` | Help 下的 Dump 父菜单和类型/清理/保存子项 |
| Dump 配置 | `Dump/DumpConfig.cs` | 写入/清理 `HKLM\...\Windows Error Reporting\LocalDumps` |
| Dump 收集 | `Dump/DumpFileCollector.cs` | 把已有 `.dmp` 复制到反馈包 `Dumps/` |

## Dump 边界

| 能力 | 是否写系统配置 | 说明 |
| --- | --- | --- |
| 设置 LocalDumps | 是 | 写 HKLM 下当前进程 `{Name}.exe` 的 LocalDumps 项 |
| 清理 LocalDumps | 是 | 删除当前进程对应注册表项 |
| 保存当前进程 Dump | 否 | 调用 `DumpHelper.WriteMiniDump(...)` 写文件 |
| 反馈包收集 Dump | 否 | 只复制 `DumpFolder` 中已有 `.dmp` |

`DumpConfig` 管理的字段包括 `DumpFolder`、`DumpCount`、`DumpType`、`CustomDumpFlags`。默认目录为用户 LocalAppData 下的 `CrashDumps`。

## 验收

| 验收项 | 通过标准 |
| --- | --- |
| 插件加载 | Help 菜单能看到事件窗口入口和 Dump 设置入口 |
| 权限约束 | 普通权限被拦截，管理员权限能打开事件窗口 |
| 事件读取 | 能读取 `Application` 日志 Error 项，详情显示 `Message` |
| Dump 类型设置 | 注册表当前进程项写入 `DumpType` 和必要字段 |
| 保存 Dump | `DumpFolder` 下生成当前进程 `.dmp` |
| 清理设置 | 当前进程 LocalDumps 注册表项被清理 |
| 反馈收集 | 已有 `.dmp` 被复制到反馈包 `Dumps/` |

## 边界

- 不是完整事件诊断中心；当前只读 Application 日志 Error 项。
- Dump 配置是系统级 HKLM 写入，必须按管理员权限处理。
- 权限有两层：菜单入口受宿主权限约束，注册表写入再检查 Windows 管理员权限。
- 反馈收集不会主动生成 dump。

## 关键文件

| 任务 | 先看 |
| --- | --- |
| 菜单入口 | `ExportEventWindow.cs`、`Dump/MenuDump.cs` |
| 事件查看 | `EventWindow.xaml.cs` |
| Dump 配置 | `Dump/DumpConfig.cs` |
| 反馈收集 | `Dump/DumpFileCollector.cs` |
| 装载信息 | `manifest.json` |
