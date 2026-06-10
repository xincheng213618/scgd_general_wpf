# 插件运行与交接场景手册

这页面向接手现有插件、排查插件不加载、打包 `.cvxp`、验证现场插件包的人。它不替代单插件详情页，而是把 `PluginLoader`、`manifest.json`、`.deps.json`、`.cvxp`、管理员权限、native DLL 和 Socket 这些运行时链路串成可执行步骤。

如果你要横向比较插件能力，先看 [插件能力与交接矩阵](./plugin-capability-matrix.md)。如果你已经准备发版、现场替换或交接现有插件，继续看 [现有插件现场验收与交接清单](./plugin-field-acceptance.md)，并按 [插件发布证据与版本核查表](./plugin-release-evidence.md) 保存版本和包内容证据。如果你要开发新插件，先看 [插件开发手册](../../02-developer-guide/plugin-development/README.md)。

## 使用方法

1. 先看 [场景入口](#场景入口)，判断这是加载、菜单、打包、设备依赖、权限还是 Socket 问题。
2. 按对应场景检查源码、输出目录和运行时日志。
3. 最后填写 [插件交接记录](#插件交接记录)，把 manifest、DLL 版本、`.cvxp`、烟测结果和已知限制写清楚。

## 场景入口

| 接到的问题 | 先看场景 | 相关插件 |
| --- | --- | --- |
| 插件目录存在，但插件管理器或菜单看不到 | [场景 A](#场景-a：插件目录存在但没有加载) | 全部插件 |
| 插件已加载，但菜单、状态栏或设置页没有出现 | [场景 B](#场景-b：插件加载了但入口没有出现) | Conoscope、Spectrum、SystemMonitor、EventVWR、WindowsServicePlugin |
| 现场提示 `ColorVision.*.dll` 版本不足或缺依赖 | [场景 C](#场景-c：依赖版本不足或缺-colorvision-dll) | 全部插件 |
| 要发布 `.cvxp` 插件包 | [场景 D](#场景-d：打包并发布-cvxp) | 全部插件 |
| 插件涉及设备、native DLL 或驱动 | [场景 E](#场景-e：设备或-native-dll-相关插件) | Spectrum、Conoscope |
| 插件需要管理员权限 | [场景 F](#场景-f：管理员权限插件) | EventVWR、WindowsServicePlugin |
| Socket 指令进不来或没有返回 | [场景 G](#场景-g：socket-插件指令不工作) | Spectrum |
| 曾经出现过的插件名称要恢复为当前插件 | [场景 H](#场景-h：历史插件重新恢复) | Pattern、ImageProjector、ScreenRecorder |

## 插件运行模型

宿主通过 `UI/ColorVision.UI/Plugins/PluginLoader.cs` 加载插件：

1. 扫描主程序输出目录 `Plugins/` 下的一级子目录。
2. 优先读取 `manifest.json`。
3. 用 `manifest.id` 更新插件配置缓存。
4. 根据 `manifest.dllpath` 找主 DLL；没有 `dllpath` 时使用“目录名 + `.dll`”。
5. 如果目录里恰好有一个 `.deps.json`，检查其中 `ColorVision.*` 依赖版本。
6. 依赖满足后通过 `Assembly.LoadFrom(...)` 加载主 DLL。
7. 没有 `manifest.json` 时进入兼容加载：尝试加载目录同名 DLL，但不会按正常插件记录到配置缓存。

推荐现场目录形态：

```text
ColorVision/bin/x64/<Config>/net10.0-windows/Plugins/<PluginName>/
  <PluginName>.dll
  manifest.json
  README.md
  CHANGELOG.md
  PackageIcon.png        # 可选
```

## 场景 A：插件目录存在但没有加载

处理步骤：

1. 确认插件目录位于主程序输出目录的一级 `Plugins/<PluginName>/`，不要放成 `Plugins/<PluginName>/<PluginName>/`。
2. 打开 `manifest.json`，检查 `id`、`name`、`version`、`dllpath`。
3. 确认 `dllpath` 指向的 DLL 在同一目录存在。
4. 如果有 `.deps.json`，确认目录内只有一个，并检查主程序根目录的 `ColorVision.*.dll` 版本。
5. 查看主程序日志中的 `PluginDllNotFound`、`DependencyVersionInsufficient`、`PluginLoadError`。

快速检查：

```powershell
$plugin = "ColorVision/bin/x64/Release/net10.0-windows/Plugins/Spectrum"
Get-ChildItem $plugin
Get-Content "$plugin/manifest.json"
Get-ChildItem $plugin -Filter "*.deps.json"
```

如果没有 `manifest.json`，宿主会尝试兼容加载目录同名 DLL，但这种插件不会有完整 manifest 元数据。当前通用插件不建议用这种形态交付。

## 场景 B：插件加载了但入口没有出现

插件加载 DLL 只代表程序集进入进程，不代表菜单、状态栏、设置页、Socket handler 都已注册。

| 插件 | 入口检查 |
| --- | --- |
| Conoscope | Tool 菜单 `VAM`，`ConoscopeWindow` Ribbon，ImageEditor 右键菜单 |
| Spectrum | Tool 菜单光谱窗口，Spectrum 窗口菜单，状态栏，Socket handlers |
| SystemMonitor | Tool 菜单、设置页、主程序状态栏 |
| EventVWR | Help 菜单事件窗口、Dump 子菜单 |
| WindowsServicePlugin | Help 菜单服务管理器、安装向导入口 |

处理步骤：

1. 先确认插件 DLL 已加载，没有依赖版本报错。
2. 查对应插件页，确认它的菜单 Provider、状态栏 Provider、设置 Provider 或 Socket handler 名称。
3. 如果菜单缺失，看插件是否实现了宿主扫描的 Provider 接口或初始化器。
4. 如果状态栏缺失，看插件配置是否关闭了状态栏项。
5. 如果只是某个窗口内菜单缺失，确认窗口是否已经创建，窗口级菜单通常不是主菜单。

## 场景 C：依赖版本不足或缺 ColorVision DLL

当插件目录里有唯一 `.deps.json` 时，`PluginLoader` 会读取其中 `ColorVision.*` 依赖，并到主程序根目录寻找同名 DLL。如果实际版本低于 `.deps.json` 要求，插件会被跳过。

处理步骤：

1. 不要只替换插件目录；同时检查主程序根目录的 `ColorVision.*.dll`。
2. 对比 `.deps.json` 需要的版本和 DLL 实际版本。
3. 如果插件是用新版 UI DLL 构建的，现场主程序也要同步对应 UI DLL。
4. 若缺的是 native DLL，转到 [场景 E](#场景-e：设备或-native-dll-相关插件)。

版本检查示例：

```powershell
$out = "ColorVision/bin/x64/Release/net10.0-windows"
Get-ChildItem $out -Filter "ColorVision*.dll" |
  Select-Object Name, @{Name="AssemblyVersion";Expression={[Reflection.AssemblyName]::GetAssemblyName($_.FullName).Version}}, LastWriteTime
```

## 场景 D：打包并发布 cvxp

通用命令：

```powershell
Scripts\package_plugin.bat Spectrum --no-upload
```

`package_plugin.bat` 会调用 `Scripts/package_cvxp.py`，默认执行构建、收集输出目录、剔除 `Scripts/shared_files.json` 中的宿主共享文件、复制 `README.md`、`CHANGELOG.md`、`manifest.json`、`PackageIcon.png`，并读取主 DLL `FileVersion` 生成 `<PluginName>-<FileVersion>.cvxp`。

发布前必须确认：

| 检查项 | 说明 |
| --- | --- |
| manifest 版本 | 插件市场或插件管理器展示可能看 `manifest.version` |
| DLL 文件版本 | `.cvxp` 文件名来自主 DLL `FileVersion` |
| CHANGELOG | 必须对应这次 DLL，而不是只对应 manifest |
| README | 运行时帮助和市场说明通常读取插件 README |
| shared files | 不应把宿主已有的 `ColorVision.*.dll` 打进插件包，但现场主程序必须有兼容版本 |
| native DLL | Spectrum、Conoscope 等不能只检查托管 DLL |

抽检 `.cvxp`：

```powershell
$pkg = Get-ChildItem Scripts -Filter "Spectrum-*.cvxp" | Sort-Object LastWriteTime -Descending | Select-Object -First 1
$tmp = Join-Path $env:TEMP "cv-plugin-cvxp"
Remove-Item $tmp -Recurse -Force -ErrorAction SilentlyContinue
New-Item $tmp -ItemType Directory | Out-Null
Copy-Item $pkg.FullName "$tmp/plugin.zip"
Expand-Archive "$tmp/plugin.zip" "$tmp/plugin"
Get-ChildItem "$tmp/plugin" -Recurse
```

## 场景 E：设备或 native DLL 相关插件

| 插件 | 外部边界 | 先查什么 |
| --- | --- | --- |
| Spectrum | 光谱仪 native DLL、串口、SMU、Shutter、CFW、许可证、SQLite 结果库 | 设备连接日志、许可证、标定分组、`Magiude.dat`、`WavaLength.dat`、CIE 图片 |
| Conoscope | MVS 相机、`MvCameraControl.dll`、图像资源、CSV 导出 | MVS SDK、相机驱动、图像能否打开、关注点/参考轴配置 |

处理步骤：

1. 先区分“插件没有加载”和“插件加载后设备不可用”。
2. 设备不可用时先查驱动、native DLL、许可证和配置文件，不要直接改插件逻辑。
3. 打包后展开 `.cvxp`，确认 native DLL 或数据文件被带上，或者明确要求现场环境预装。
4. 写交接记录时说明哪些依赖来自插件包，哪些来自现场机器。

## 场景 F：管理员权限插件

EventVWR 和 WindowsServicePlugin 涉及注册表、Dump、Windows 服务、MySQL、MQTT、本机目录和配置同步。普通用户下验证这些能力容易得到误判。

处理步骤：

1. 标注需要管理员模式的操作，不把权限失败当成功能缺陷。
2. EventVWR 重点确认 Windows Application 日志、WER LocalDumps 注册表、Dump 输出目录。
3. WindowsServicePlugin 重点确认服务根目录、服务状态、MySQL/MQTT 安装包、`cfg/*.config` 同步。
4. 安装、卸载、服务启动停止只能在测试环境或明确授权的现场环境执行。

## 场景 G：Socket 插件指令不工作

当前真实插件里 Spectrum 有 Socket JSON 指令。它依赖 `ColorVision.SocketProtocol` 的 TCP 服务、协议模式、端口配置和 Spectrum 插件程序集加载。

处理步骤：

1. 确认主程序 Socket 服务已启用，端口没有被占用。
2. 确认协议模式是 JSON，而不是 Text。
3. 确认 Spectrum 插件 DLL 已加载，没有 `.deps.json` 版本问题。
4. 打开 Spectrum 窗口，确认设备状态和标定组状态满足指令前置条件。
5. 对照 [Spectrum 插件](./standard-plugins/spectrum.md) 的 Socket 指令和 `Plugins/Spectrum/Socket/` handler。

## 场景 H：历史插件重新恢复

Pattern、ImageProjector、ScreenRecorder 当前不在 `Plugins/` 目录的真实插件清单里，也不作为“现有插件能力说明”的入口。

恢复为当前插件前必须补齐：

1. `Plugins/<Name>/` 源码目录。
2. `<Name>.csproj`，目标框架和 WPF 设置。
3. `manifest.json`，至少包含 `id`、`name`、`version`、`dllpath`。
4. `README.md`、`CHANGELOG.md`。
5. PostBuild 或打包脚本规则。
6. docs 站点中的当前插件页、[当前插件文档覆盖清单](./current-plugin-coverage.md)、能力矩阵、导航入口。
7. 至少一次 `Scripts\package_plugin.bat <Name> --no-upload` 和主程序加载烟测。

## 插件交接记录

每次插件发布或交接至少记录：

| 项 | 内容 |
| --- | --- |
| 插件 | 名称、源码目录、manifest id |
| 版本 | `manifest.version`、`.csproj VersionPrefix`、输出 DLL `FileVersion`、`.cvxp` 文件名 |
| 构建命令 | `dotnet build` 和 `Scripts\package_plugin.bat ... --no-upload` |
| 必带文件 | DLL、manifest、README、CHANGELOG、PackageIcon、native DLL、设备数据文件 |
| 宿主依赖 | 主程序根目录 `ColorVision.*.dll` 是否满足 `.deps.json` |
| 入口验收 | 菜单、状态栏、设置页、窗口级菜单、Socket handler |
| 外部边界 | 设备、驱动、许可证、数据库、注册表、Windows 服务、管理员权限 |
| 烟测结果 | 对应插件页中的最小烟测是否通过 |
| 回退方式 | 上一版 `.cvxp`、插件目录备份、主程序 DLL 版本 |
| 已知限制 | 当前未验证的设备、权限、协议或现场环境差异 |

## 继续阅读

- [插件能力与交接矩阵](./plugin-capability-matrix.md)
- [当前插件文档覆盖清单](./current-plugin-coverage.md)
- [现有插件现场验收与交接清单](./plugin-field-acceptance.md)
- [插件发布证据与版本核查表](./plugin-release-evidence.md)
- [Conoscope 插件](./standard-plugins/conoscope.md)
- [Spectrum 插件](./standard-plugins/spectrum.md)
- [SystemMonitor 插件](./standard-plugins/system-monitor.md)
- [EventVWR 插件](./standard-plugins/eventvwr.md)
- [WindowsServicePlugin 插件](./standard-plugins/windows-service.md)
- [插件开发手册](../../02-developer-guide/plugin-development/README.md)
