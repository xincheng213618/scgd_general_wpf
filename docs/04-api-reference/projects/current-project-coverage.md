# 当前项目文档覆盖清单

这页用来确认 `Projects/` 下每个真实项目目录都有对应文档、交接入口和维护边界。新增或删除项目包时，先更新这张表，再更新导航和 [项目包总览](./README.md)。

## 覆盖结果

| 项目目录 | 项目文件 | manifest Id / version | 当前文档页 | 交接覆盖 |
| --- | --- | --- | --- | --- |
| `Projects/ProjectARVR/` | `ProjectARVR.csproj` | `ProjectARVR` / `1.0` | [ProjectARVR](./project-arvr.md) | [矩阵](./project-capability-matrix.md)、[场景手册](./project-package-playbook.md)、[交接手册](./project-handoff.md) |
| `Projects/ProjectARVRLite/` | `ProjectARVRLite.csproj` | `ProjectARVRLite` / `1.0` | [ProjectARVRLite](./project-arvr-lite.md) | [矩阵](./project-capability-matrix.md)、[场景手册](./project-package-playbook.md)、[交接手册](./project-handoff.md) |
| `Projects/ProjectARVRPro/` | `ProjectARVRPro.csproj` | `ProjectARVRPro` / `1.1.7.7` | [ProjectARVRPro](./project-arvr-pro.md) | [矩阵](./project-capability-matrix.md)、[场景手册](./project-package-playbook.md)、[交接手册](./project-handoff.md) |
| `Projects/ProjectARVRPro.IntegrationDemo/` | `ProjectARVRPro.IntegrationDemo.csproj` | 无 manifest | [ARVRPro 对接 Demo](./project-arvr-pro-integration-demo.md) | [矩阵](./project-capability-matrix.md)、[场景手册](./project-package-playbook.md) |
| `Projects/ProjectBlackMura/` | `ProjectBlackMura.csproj` | `ProjectBlackMura` / `1.0` | [ProjectBlackMura](./project-black-mura.md) | [矩阵](./project-capability-matrix.md)、[场景手册](./project-package-playbook.md)、[交接手册](./project-handoff.md) |
| `Projects/ProjectHeyuan/` | `ProjectHeyuan.csproj` | `ProjectHeyuan` / `1.0` | [ProjectHeyuan](./project-heyuan.md) | [矩阵](./project-capability-matrix.md)、[场景手册](./project-package-playbook.md)、[交接手册](./project-handoff.md) |
| `Projects/ProjectKB/` | `ProjectKB.csproj` | `ProjectKB` / `1.0` | [ProjectKB](./project-kb.md) | [矩阵](./project-capability-matrix.md)、[场景手册](./project-package-playbook.md)、[交接手册](./project-handoff.md) |
| `Projects/ProjectLUX/` | `ProjectLUX.csproj` | `ProjectLUX` / `1.0` | [ProjectLUX](./project-lux.md) | [矩阵](./project-capability-matrix.md)、[场景手册](./project-package-playbook.md)、[交接手册](./project-handoff.md) |
| `Projects/ProjectShiyuan/` | `ProjectShiyuan.csproj` | `ProjectShiyuan` / `1.0` | [ProjectShiyuan](./project-shiyuan.md) | [矩阵](./project-capability-matrix.md)、[场景手册](./project-package-playbook.md)、[交接手册](./project-handoff.md) |

## 当前仓库核查证据

2026-06-10 核查当前工作树时，`Projects/` 下共有 9 个目录。8 个正式运行时项目包具备 `.csproj`、`manifest.json`、`README.md`、`CHANGELOG.md` 和 docs 项目页；`ProjectARVRPro.IntegrationDemo` 是客户侧对接 Demo，工程文件声明 `OutputType=Exe`、`TargetFrameworks=net48`、`IsPackable=false`，当前没有 manifest 和 CHANGELOG 是已知边界。

| 项目目录 | `.csproj` | `manifest.json` | README | CHANGELOG | docs 项目页 | 结论 |
| --- | --- | --- | --- | --- | --- | --- |
| `Projects/ProjectARVR/` | 有 | `ProjectARVR` / `1.0` | 有 | 有 | 有 | 正式项目包覆盖完整 |
| `Projects/ProjectARVRLite/` | 有 | `ProjectARVRLite` / `1.0` | 有 | 有 | 有 | 正式项目包覆盖完整 |
| `Projects/ProjectARVRPro/` | 有 | `ProjectARVRPro` / `1.1.7.7` | 有 | 有 | 有 | 正式项目包覆盖完整 |
| `Projects/ProjectARVRPro.IntegrationDemo/` | 有 | 无 | 有 | 无 | 有 | 客户对接 Demo，不按项目包 manifest 验收 |
| `Projects/ProjectBlackMura/` | 有 | `ProjectBlackMura` / `1.0` | 有 | 有 | 有 | 正式项目包覆盖完整 |
| `Projects/ProjectHeyuan/` | 有 | `ProjectHeyuan` / `1.0` | 有 | 有 | 有 | 正式项目包覆盖完整 |
| `Projects/ProjectKB/` | 有 | `ProjectKB` / `1.0` | 有 | 有 | 有 | 正式项目包覆盖完整 |
| `Projects/ProjectLUX/` | 有 | `ProjectLUX` / `1.0` | 有 | 有 | 有 | 正式项目包覆盖完整 |
| `Projects/ProjectShiyuan/` | 有 | `ProjectShiyuan` / `1.0` | 有 | 有 | 有 | 正式项目包覆盖完整 |

如果 `ProjectARVRPro.IntegrationDemo` 后续变成要随主程序打包交付的正式项目包，应补 `manifest.json`、`CHANGELOG.md`、PostBuild 复制规则、打包脚本验证和发布证据；在那之前，它只作为客户侧协议 Demo 进入覆盖表。

## 必须保留的交接边界

| 边界 | 项目 |
| --- | --- |
| JSON Socket 切图流程 | ProjectARVR、ProjectARVRLite、ProjectARVRPro |
| 文本 Socket 命令流程 | ProjectLUX |
| 串口/MES 或 PG 控制 | ProjectBlackMura、ProjectHeyuan |
| Modbus/MES DLL 产线集成 | ProjectKB |
| 手动/离线客户文件导出 | ProjectShiyuan |
| 客户侧协议 Demo | ProjectARVRPro.IntegrationDemo |

## 覆盖检查命令

```powershell
Get-ChildItem Projects -Directory | Sort-Object Name | Select-Object -ExpandProperty Name
Get-ChildItem docs/04-api-reference/projects -File | Sort-Object Name | Select-Object -ExpandProperty Name
Get-ChildItem Projects -Directory | Sort-Object Name | ForEach-Object {
  "$($_.Name): csproj=$([bool](Get-ChildItem $_.FullName -Filter *.csproj -File)) manifest=$(Test-Path (Join-Path $_.FullName 'manifest.json')) readme=$(Test-Path (Join-Path $_.FullName 'README.md')) changelog=$(Test-Path (Join-Path $_.FullName 'CHANGELOG.md'))"
}
```

每个当前项目目录都必须有项目页。项目协议、结果字段、打包内容或验收方式变化时，要同步更新对应项目页、[项目包能力与交接矩阵](./project-capability-matrix.md)、[项目包运行与交接场景手册](./project-package-playbook.md) 和 [项目包发布证据与版本核查表](./project-release-evidence.md)。
