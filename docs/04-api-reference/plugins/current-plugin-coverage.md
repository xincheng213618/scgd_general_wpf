# 当前插件文档覆盖清单

本页用来回答一个很直接的问题：当前 `Plugins/` 目录里的每个真实插件，是否都有对应的文档入口、交接说明和发版检查点。

当前有效插件只按源码目录认定。没有 `Plugins/<Name>/`、`.csproj` 和 `manifest.json` 的名称，不放入“现有插件能力说明”的主入口。

## 当前覆盖结论

| 插件目录 | 工程文件 | manifest Id / version | 当前能力页 | 交接与验收覆盖 | 打包命令 |
| --- | --- | --- | --- | --- | --- |
| `Plugins/Conoscope/` | `Conoscope.csproj` | `Conoscope` / `1.4.6.1` | [Conoscope 插件](./standard-plugins/conoscope.md) | [能力矩阵](./plugin-capability-matrix.md)、[场景手册](./plugin-handoff-playbook.md)、[现场验收](./plugin-field-acceptance.md) | `Scripts\package_plugin.bat Conoscope --no-upload` |
| `Plugins/Spectrum/` | `Spectrum.csproj` | `Spectrum` / `1.0` | [Spectrum 插件](./standard-plugins/spectrum.md) | [能力矩阵](./plugin-capability-matrix.md)、[场景手册](./plugin-handoff-playbook.md)、[现场验收](./plugin-field-acceptance.md) | `Scripts\package_plugin.bat Spectrum --no-upload` |
| `Plugins/SystemMonitor/` | `SystemMonitor.csproj` | `SystemMonitor` / `1.0.1` | [SystemMonitor 插件](./standard-plugins/system-monitor.md) | [能力矩阵](./plugin-capability-matrix.md)、[场景手册](./plugin-handoff-playbook.md)、[现场验收](./plugin-field-acceptance.md) | `Scripts\package_plugin.bat SystemMonitor --no-upload` |
| `Plugins/EventVWR/` | `EventVWR.csproj` | `EventVWR` / `1.0` | [EventVWR 插件](./standard-plugins/eventvwr.md) | [能力矩阵](./plugin-capability-matrix.md)、[场景手册](./plugin-handoff-playbook.md)、[现场验收](./plugin-field-acceptance.md) | `Scripts\package_plugin.bat EventVWR --no-upload` |
| `Plugins/WindowsServicePlugin/` | `WindowsServicePlugin.csproj` | `WindowsServicePlugin` / `1.0` | [WindowsServicePlugin 插件](./standard-plugins/windows-service.md) | [能力矩阵](./plugin-capability-matrix.md)、[场景手册](./plugin-handoff-playbook.md)、[现场验收](./plugin-field-acceptance.md) | `Scripts\package_plugin.bat WindowsServicePlugin --no-upload` |

## 当前仓库核查证据

2026-06-10 核查当前工作树时，`Plugins/` 下 5 个目录全部满足现有插件文档入口的最低证据：`.csproj`、`manifest.json`、运行时 `README.md`、运行时 `CHANGELOG.md`、docs 单插件页、矩阵/场景/验收页。

| 插件目录 | `.csproj` | `manifest.json` | 运行时 README | 运行时 CHANGELOG | docs 单插件页 | 结论 |
| --- | --- | --- | --- | --- | --- | --- |
| `Plugins/Conoscope/` | 有 | `Conoscope` / `1.4.6.1` | 有 | 有 | 有 | 覆盖完整 |
| `Plugins/EventVWR/` | 有 | `EventVWR` / `1.0` | 有 | 有 | 有 | 覆盖完整 |
| `Plugins/Spectrum/` | 有 | `Spectrum` / `1.0` | 有 | 有 | 有 | 覆盖完整 |
| `Plugins/SystemMonitor/` | 有 | `SystemMonitor` / `1.0.1` | 有 | 有 | 有 | 覆盖完整 |
| `Plugins/WindowsServicePlugin/` | 有 | `WindowsServicePlugin` / `1.0` | 有 | 有 | 有 | 覆盖完整 |

运行时 README/CHANGELOG 很重要：插件帮助窗口、插件包和现场交接往往先读插件目录里的文件；docs 站点页负责面向交接人员解释能力、边界、风险和验收。两边都要更新，不能只改其中一个。

## 外部边界覆盖

| 插件 | 必须说明的外部边界 | 当前文档落点 |
| --- | --- | --- |
| Conoscope | MVS 相机、`MvCameraControl.dll`、图像资源、关注点和 CSV 导出 | 单插件页、能力矩阵、现场验收 |
| Spectrum | 光谱仪 native DLL、串口、SMU/Shutter/CFW、许可证、SQLite 结果库、Socket JSON 指令 | 单插件页、能力矩阵、场景手册、现场验收 |
| SystemMonitor | Windows 性能计数器、CUDA 信息、磁盘/网络/进程、缓存目录权限 | 单插件页、能力矩阵、现场验收 |
| EventVWR | Windows EventLog、WER LocalDumps、HKLM 注册表、管理员权限 | 单插件页、能力矩阵、场景手册、现场验收 |
| WindowsServicePlugin | Windows 服务、MySQL、MQTT、服务包 ZIP、配置同步、管理员权限 | 单插件页、能力矩阵、场景手册、现场验收 |

## 不在当前插件清单里的名称

以下名称不再作为当前插件能力入口维护：

| 名称 | 当前状态 | 恢复前置条件 |
| --- | --- | --- |
| Pattern | 当前 `Plugins/` 下没有 `Pattern/` 插件工程 | 恢复源码目录、`.csproj`、`manifest.json`、README、CHANGELOG、构建复制和打包验证 |
| ImageProjector | 当前 `Plugins/` 下没有 `ImageProjector/` 插件工程 | 恢复源码目录、`.csproj`、`manifest.json`、README、CHANGELOG、构建复制和打包验证 |
| ScreenRecorder | 当前 `Plugins/` 下没有 `ScreenRecorder/` 插件工程 | 恢复源码目录、`.csproj`、`manifest.json`、README、CHANGELOG、构建复制和打包验证 |

如果这些名称后续重新变成真实插件，先按 [插件运行与交接场景手册](./plugin-handoff-playbook.md) 的“历史插件重新恢复”检查补齐源码和产物，再加入本页、[插件能力与交接矩阵](./plugin-capability-matrix.md)、[现有插件现场验收与交接清单](./plugin-field-acceptance.md) 和侧边栏导航。

## 覆盖率检查方法

新增、删除或恢复插件后，用下面的思路重新核对：

```powershell
Get-ChildItem Plugins -Directory | Sort-Object Name | Select-Object -ExpandProperty Name
Get-ChildItem docs/04-api-reference/plugins/standard-plugins -File | Sort-Object Name | Select-Object -ExpandProperty Name
Get-ChildItem Plugins -Directory | Sort-Object Name | ForEach-Object {
  "$($_.Name): csproj=$([bool](Get-ChildItem $_.FullName -Filter *.csproj -File)) manifest=$(Test-Path (Join-Path $_.FullName 'manifest.json')) readme=$(Test-Path (Join-Path $_.FullName 'README.md')) changelog=$(Test-Path (Join-Path $_.FullName 'CHANGELOG.md'))"
}
```

检查结果必须满足：

1. 每个当前 `Plugins/<Name>/` 都有单插件能力页。
2. 每个单插件能力页都能回到真实源码目录、`.csproj`、`manifest.json` 和关键类。
3. 每个运行时插件目录都保留 `README.md` 和 `CHANGELOG.md`，并和 docs 站点页描述一致。
4. 能力矩阵、场景手册、现场验收清单和侧边栏只把真实插件写成当前能力。
5. 历史名称只能出现在“恢复检查”语境中，不能作为当前功能入口。
