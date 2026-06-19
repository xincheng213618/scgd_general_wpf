# 插件发布证据与版本核查表

这页给发布 `.cvxp`、现场替换插件目录、排查“插件已复制但宿主不加载”的维护人员使用。它补足 [现有插件现场验收与交接清单](./plugin-field-acceptance.md) 里偏运行验收的一面，重点留下可追溯证据：manifest、DLL 文件版本、`.cvxp` 文件名、依赖、native 文件、权限和回退包。

## 为什么要单独记录证据

当前真实插件里，`manifest.version` 和 `.csproj VersionPrefix` 并不总是一致：

| 插件 | manifest version | `.csproj VersionPrefix` | 风险 |
| --- | --- | --- | --- |
| Conoscope | `1.4.6.1` | `1.4.6.9` | 插件管理器显示、DLL 文件版本、`.cvxp` 文件名可能不是同一个数字 |
| Spectrum | `1.0` | `2.3.3.1` | 市场说明和交付包名如果只写 manifest 会误导现场 |
| SystemMonitor | `1.0.1` | `1.4.3.3` | 状态栏问题排查时需要确认实际 DLL |
| EventVWR | `1.0` | `1.1.8.1` | 管理员权限问题容易和版本问题混淆 |
| WindowsServicePlugin | `1.0` | `1.4.3.17` | 服务安装类插件必须能回退到明确 DLL 版本 |

因此每次发布不要只写“插件版本”。至少同时记录 manifest、DLL FileVersion、`.cvxp` 文件名和 CHANGELOG。

## 必留证据

| 证据 | 命令或来源 | 要证明什么 |
| --- | --- | --- |
| 插件源码清单 | `Get-ChildItem Plugins -Directory` | 当前发布的是仓库真实插件 |
| manifest | `Get-Content Plugins/<Name>/manifest.json` | `Id`、`version`、`dllpath`、`requires` 正确 |
| 项目版本 | `Select-String Plugins/<Name>/<Name>.csproj -Pattern "VersionPrefix"` | DLL 版本来源明确 |
| 输出 DLL | 主程序 `Plugins/<Name>/<Name>.dll` 文件属性 | 现场加载的是预期 DLL |
| `.cvxp` 包名 | `Scripts\package_plugin.bat <Name> --no-upload` 输出 | 包名和 DLL `FileVersion` 对得上 |
| README/CHANGELOG | 插件根目录和 `.cvxp` 内容 | 运行时帮助和变更记录对应本次 DLL |
| 宿主共享 DLL | 主程序根目录 `ColorVision.*.dll` | `.deps.json` 依赖版本满足 |
| native/数据文件 | 展开 `.cvxp` 或检查插件目录 | Spectrum/Conoscope 的设备依赖没有漏 |
| 权限证据 | 管理员模式标记、UAC、注册表/服务操作记录 | EventVWR/WindowsServicePlugin 的权限边界清楚 |
| 回退证据 | 上一版 `.cvxp`、插件目录备份、主程序 DLL 版本 | 现场可退回 |

## 统一核查命令

### 1. 当前源码和文档是否一致

```powershell
Get-ChildItem Plugins -Directory | Sort-Object Name | Select-Object -ExpandProperty Name
Get-ChildItem docs/04-api-reference/plugins/standard-plugins -File |
  Sort-Object Name |
  Select-Object -ExpandProperty Name
```

当前有效插件应只包含 Conoscope、Spectrum、SystemMonitor、EventVWR、WindowsServicePlugin。Pattern、ImageProjector、ScreenRecorder 只能出现在历史恢复说明里。

### 2. manifest 和项目版本

```powershell
$name = "Spectrum"
Get-Content "Plugins/$name/manifest.json"
Select-String "Plugins/$name/$name.csproj" -Pattern "TargetFramework|VersionPrefix|ProjectReference|PackageReference|CopyToOutputDirectory"
```

记录时不要只抄 manifest。发布记录至少写：

```text
manifest Id:
manifest version:
manifest requires:
csproj VersionPrefix:
output DLL FileVersion:
cvxp file name:
CHANGELOG date/entry:
```

### 3. 生成并展开 `.cvxp`

```powershell
Scripts\package_plugin.bat Spectrum --no-upload

$pkg = Get-ChildItem Scripts -Filter "Spectrum-*.cvxp" |
  Sort-Object LastWriteTime -Descending |
  Select-Object -First 1
$tmp = Join-Path $env:TEMP "cv-plugin-evidence"
Remove-Item $tmp -Recurse -Force -ErrorAction SilentlyContinue
New-Item $tmp -ItemType Directory | Out-Null
Copy-Item $pkg.FullName "$tmp/plugin.zip"
Expand-Archive "$tmp/plugin.zip" "$tmp/plugin"
Get-ChildItem "$tmp/plugin" -Recurse | Select-Object FullName, Length
```

`.cvxp` 里至少要能看到主 DLL、`manifest.json`、`README.md`、`CHANGELOG.md`。有设备或 native 依赖的插件还要检查数据文件和 native DLL。

### 4. 现场插件目录

```powershell
$root = "ColorVision/bin/x64/Release/net10.0-windows"
$name = "Spectrum"
$plugin = Join-Path $root "Plugins/$name"
Get-ChildItem $plugin
Get-Content (Join-Path $plugin "manifest.json")
Get-ChildItem $plugin -Filter "*.deps.json"
Get-ChildItem $root -Filter "ColorVision*.dll" |
  Select-Object Name, @{Name="FileVersion";Expression={$_.VersionInfo.FileVersion}}, LastWriteTime
```

现场问题常见原因是只替换插件目录，主程序根目录的 `ColorVision.*.dll` 仍然是旧版本。

## 插件专项证据

| 插件 | 必须额外保存 |
| --- | --- |
| Conoscope | `MvCameraControl.dll` / MVS SDK 状态、测试图、关注点/参考轴配置、CSV 导出样例 |
| Spectrum | 光谱仪 native DLL、`Magiude.dat`、`WavaLength.dat`、CIE 图片、许可证目录、`Spectrum.db` 或结果库、Socket `SpectrumStatus` 返回 |
| SystemMonitor | 状态栏配置截图或记录、CPU/RAM/磁盘/网络刷新结果、缓存清理范围 |
| EventVWR | 管理员模式、Windows Application Error 示例、HKLM LocalDumps 注册表项、Dump 文件路径 |
| WindowsServicePlugin | 管理员模式、服务根目录、服务状态、MySQL/MQTT 配置、安装包 ZIP、配置同步日志 |

## 发布记录模板

```text
插件名称：
源码目录：
manifest Id/version/requires：
csproj VersionPrefix：
输出 DLL FileVersion：
cvxp 文件：
构建命令：
打包命令：
包内容抽检：
主程序根目录 ColorVision.*.dll 版本：
入口验收：
业务烟测：
设备/native/权限证据：
README/CHANGELOG 是否同步：
已知限制：
回退包和目录：
```

## 继续阅读

- [现有插件现场验收与交接清单](./plugin-field-acceptance.md)
- [插件运行与交接场景手册](./plugin-handoff-playbook.md)
- [插件能力与交接矩阵](./plugin-capability-matrix.md)
- [当前插件文档覆盖清单](./current-plugin-coverage.md)

