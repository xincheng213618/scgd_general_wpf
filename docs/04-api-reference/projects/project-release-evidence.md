# 项目包发布证据与版本核查表

这页给发布项目 `.cvxp`、现场替换 `Plugins/{ProjectName}/`、排查“项目包装上但菜单/协议/结果不对”的维护人员使用。它补足 [项目包运行与交接场景手册](./project-package-playbook.md) 里偏运行排障的一面，重点留下可追溯证据：manifest、DLL 文件版本、`.cvxp` 包名、项目配置、外部协议、输出样例和回退包。

## 当前版本差异

当前项目包里，`manifest.version` 和 `.csproj VersionPrefix` 大多不是同一个数字。发布记录不要只写“项目版本”，至少同时记录 manifest、DLL FileVersion、`.cvxp` 文件名和 CHANGELOG。

| 项目 | manifest version | `.csproj VersionPrefix` | 说明 |
| --- | --- | --- | --- |
| ProjectARVR | `1.0` | `1.6.1.11` | 现场看到的包名通常来自 DLL FileVersion，不是 manifest |
| ProjectARVRLite | `1.0` | `1.2.5.17` | 轻量项目要同时记录启用项配置版本 |
| ProjectARVRPro | `1.1.7.7` | `1.1.7.7` | 当前 manifest 和项目版本一致，但仍要核对 DLL FileVersion |
| ProjectBlackMura | `1.0` | `1.2.6.3` | Excel/EPPlus 输出和串口 PG 配置要随版本记录 |
| ProjectHeyuan | `1.0` | `1.1.6.3` | 项目文件未显式写 `TargetFramework`，交付时同时确认继承的全局 TFM |
| ProjectKB | `1.0` | `1.4.2.19` | MES DLL、Modbus 地址和背光修正配置必须入证据 |
| ProjectLUX | `1.0` | `1.1.4.25` | `ProcessGroups.json`、Recipe/Fix、`SocketCode` 是版本证据的一部分 |
| ProjectShiyuan | `1.0` | `1.3.5.3` | 当前显式目标是 `net8.0-windows`，和其他 net10 项目不同 |
| ProjectARVRPro.IntegrationDemo | 无 manifest | 无 `VersionPrefix` | 这是客户对接 Demo，使用 `dotnet publish` 证据，不按项目 `.cvxp` 验收 |

## 发布证据清单

| 证据 | 获取方式 | 通过标准 |
| --- | --- | --- |
| 项目源目录 | `Projects/{Name}/` | 源码、README、CHANGELOG、manifest 都来自同一目录 |
| manifest | `Get-Content Projects/{Name}/manifest.json` | `Id`、`dllpath`、`version`、`requires` 与项目页一致 |
| 项目版本 | `Select-String Projects/{Name}/{Name}.csproj -Pattern "TargetFramework|VersionPrefix"` | 目标框架、平台和版本来源明确 |
| 输出 DLL | 构建后的 `Projects/{Name}/bin/x64/Release/.../{Name}.dll` | DLL FileVersion 能和 `.cvxp` 包名对上 |
| `.cvxp` 包 | `Scripts\package_project.bat {Name} --no-upload` | 包名、主 DLL、manifest、README、CHANGELOG、PackageIcon 存在 |
| 配置文件 | `ProcessGroups.json`、Recipe/Fix、项目配置类、输出路径配置 | 能说明哪些配置随包，哪些来自现场 `%APPDATA%` |
| 外部协议 | Socket/MES/串口/Modbus 命令和返回码 | 有一组当前客户实际使用的命令样例和响应样例 |
| 输出样例 | SQLite、CSV、XLSX、PDF、MES/Socket 返回 | 至少有一个最小成功样例，字段和客户版本一致 |
| 外部依赖 | NPOI/EPPlus/NModbus、MES DLL、`FunTestDll.dll`、串口/PG/设备 SDK | 包内或现场预装边界写清楚 |
| 回退证据 | 上一版 `.cvxp`、项目目录备份、配置备份、数据库备份 | 现场可以退回到上一套可运行项目包 |

## 核查命令

### 1. 查看源码版本

```powershell
$name = "ProjectLUX"
Get-Content "Projects/$name/manifest.json"
Select-String "Projects/$name/$name.csproj" -Pattern "TargetFramework|VersionPrefix|ProjectReference|PackageReference|CopyToOutputDirectory"
```

### 2. 生成并展开项目包

```powershell
Scripts\package_project.bat ProjectLUX --no-upload

$pkg = Get-ChildItem Scripts -Filter "ProjectLUX-*.cvxp" |
  Sort-Object LastWriteTime -Descending |
  Select-Object -First 1

$tmp = Join-Path $env:TEMP "cv-project-cvxp"
Remove-Item $tmp -Recurse -Force -ErrorAction SilentlyContinue
New-Item -ItemType Directory -Path $tmp | Out-Null
Expand-Archive $pkg.FullName $tmp
Get-ChildItem $tmp -Recurse | Select-Object FullName
```

`.cvxp` 里至少要能看到主 DLL、`manifest.json`、`README.md`、`CHANGELOG.md` 和 `PackageIcon.png`。项目包如果依赖配置、模板、native DLL 或客户 DLL，要么进入包内，要么在发布记录里明确“现场必须预装/导入”。

### 3. 检查现场替换目录

```powershell
$app = "ColorVision/bin/x64/Release/net10.0-windows"
$name = "ProjectLUX"
Get-ChildItem "$app/Plugins/$name"
Get-Item "$app/Plugins/$name/$name.dll" | Select-Object FullName, Length, LastWriteTime, VersionInfo
Get-Content "$app/Plugins/$name/manifest.json"
```

如果项目包能复制但菜单不出现，先看 manifest `dllpath`、`.deps.json`、主程序目录的共享 `ColorVision.*.dll` 版本，再看项目 `PluginConfig/` 或菜单注册。

## 项目专项证据

| 项目 | 发布时必须额外留证 |
| --- | --- |
| ProjectARVR | `ProjectARVRInit`、`SwitchPGCompleted`、`ProjectARVRResult` 样例，固定 PG 顺序，整机 CSV |
| ProjectARVRLite | 启用项配置、预处理开关、Socket 初始化和 CSV 输出样例 |
| ProjectARVRPro | `ProcessGroups.json`、Recipe、PictureSwitchConfig、Legacy/新版 CSV、客户 XLSX、SocketRelay/AOI 样例 |
| ProjectBlackMura | PG 串口命令、五色流程、Excel 报告样例、EPPlus 依赖、POI overlay 截图或结果图 |
| ProjectHeyuan | STX/ETX 原始报文、WBRO 四点结果、CSV 上传文件、`HYMesConfig` |
| ProjectKB | `FunTestDll.dll`、`FunTestDllConfig.INI`、NModbus 配置、MES `Collect_test` 返回、背光自动修正记录 |
| ProjectLUX | `ProcessGroups.json`、Recipe/Fix、`T00XX,SN;` 命令样例、`SocketCode` 对照、SQLite/CSV/PDF 输出 |
| ProjectShiyuan | `DataPath`、JND/POI CSV、固定输入图路径、伪彩图输出、串口链路是否仍未启用 |
| IntegrationDemo | `dotnet publish` 输出目录、样例 JSON、连接端口、半包/粘包读取验证、导出 CSV |

## 发布记录模板

```text
project:
source path:
manifest id/version:
csproj VersionPrefix:
target framework/platform:
dll FileVersion:
cvxp file:
host version/requires:
changed workflow/process group:
changed protocol/event/command:
changed result fields:
config files to migrate:
external DLL/device/MES dependency:
smoke test evidence:
rollback package/config:
known limitations:
owner/date:
```

## 继续阅读

- [当前项目文档覆盖清单](./current-project-coverage.md)
- [项目包能力与交接矩阵](./project-capability-matrix.md)
- [项目包运行与交接场景手册](./project-package-playbook.md)
- [项目包交接手册](./project-handoff.md)
- [构建与发布脚本](../../02-developer-guide/scripts/README.md)
