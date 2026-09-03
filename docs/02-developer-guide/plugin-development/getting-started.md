---
knowledge_id: "plugins.getting-started"
knowledge_type: "topic"
status: "current"
summary: "插件项目构建、HostCopy、市场与本地安装、备份回退和提取插件；DLL目录替换、依赖补回及重启后加载的完成条件，正式打包会上传。"
aliases: ["新插件怎么创建","HostCopy 为什么没执行","插件安装失败","插件更新回滚","插件备份","导出插件依赖","提取插件","浏览商店","PluginProject.HostCopy.targets","package_plugin.bat","PluginUpdater","PluginExtractor","PluginRecoveryBackupService","stripped_files.json"]
code_paths: ["Plugins/Directory.Build.props","Plugins/SystemMonitor/SystemMonitor.csproj","PluginProject.HostCopy.targets","Scripts/package_cvxp.py","Scripts/package_plugin.bat","UI/ColorVision.UI/Plugins/PluginManifest.cs","UI/ColorVision.UI/Plugins/PluginUpdater.cs","UI/ColorVision.UI/Plugins/PluginDirectoryTransactionBatchScript.cs","UI/ColorVision.UI/Plugins/PluginRecoveryBackupService.cs","UI/ColorVision.UI/Plugins/PluginExtractor.cs","UI/ColorVision.UI/Update/ApplicationUpdateProcessCoordinator.cs","UI/ColorVision.UI/Update/ExternalUpdateBatchScript.cs","UI/ColorVision.UI.Desktop/Marketplace/MarketplacePackagePreflight.cs","UI/ColorVision.UI.Desktop/Marketplace/MarketplacePackageDownloadService.cs","UI/ColorVision.UI.Desktop/Marketplace/MarketplaceClient.cs","UI/ColorVision.UI.Desktop/Marketplace/MenuPluginManager.cs","UI/ColorVision.UI.Desktop/Marketplace/MarketplaceWindow.xaml","UI/ColorVision.UI.Desktop/Marketplace/MarketplaceManager.cs","UI/ColorVision.UI.Desktop/Marketplace/MarketplaceDetailContext.cs","UI/ColorVision.UI.Desktop/Marketplace/PluginInfoVM.cs","UI/ColorVision.UI.Desktop/Properties/Resources.resx","ColorVision/Update/UpdatePackageFileProcessors.cs"]
test_paths: ["Scripts/tests/test_package_cvxp.py","Test/ColorVision.UI.Tests/MarketplacePackageDownloadServiceTests.cs","Test/ColorVision.UI.Tests/PluginUpdaterBatchTests.cs","Test/ColorVision.UI.Tests/PluginRecoveryBackupServiceTests.cs","Test/ColorVision.UI.Tests/UpdaterBatchExecutionTests.cs"]
related: ["plugins.index","plugins.model","delivery.scripts","delivery.update"]
---

# 插件产物、安装与交付

插件以完整目录交付，正式包使用 ZIP 格式的 `.cvxp`。本页说明构建、安装、备份恢复和“提取插件”的操作，以及各阶段的完成条件。安装文件到位后，还需由新进程装载程序集并发现扩展；加载与启停规则见[插件装载契约](./overview.md)。

## 安装入口与操作顺序

安装前确认包来源、目标插件 ID、版本和宿主要求，并保存正在进行的工作。安装会写程序目录、保存配置、启动外部更新进程并请求退出；也可能强制结束同一安装位置的其他实例。程序目录权限不足时可能请求 Windows UAC。

| 任务 | 操作 | 预期结果 |
| --- | --- | --- |
| 从市场安装 | 打开 **帮助 → 浏览商店**，选择插件及版本，点击安装并确认 | 下载或复用缓存，完成适用的摘要检查和 manifest 预检后发起安装；菜单声明需要程序内 `Administrator` 权限 |
| 安装本地包 | 在同一窗口的 **更多操作 → 安装包** 中选择 `.cvxp` 或 `.zip`，确认文件选择 | 直接进入 `PluginUpdater` 准备安装，不经过市场请求 ID/hash 预检；确认文件选择后即开始准备 |
| 打开插件包文件 | 应用文件打开路由识别 `.cvxp`；`.zip` 需含根目录或一层包装目录中的 `manifest.json` | 包就绪后发起插件安装；无 manifest 的旧 `.zip` 可通过上面的“安装包”入口处理 |
| 更新已安装插件 | 使用[检查更新](../deployment/auto-update.md#检查并安装更新)选择主程序与插件更新项 | 按兼容版本生成更新计划；组合更新的主程序差异片段由更新主题负责 |
| 回退插件 | 在[启动恢复窗口](../deployment/auto-update.md#启动恢复)选择插件备份操作并确认 | 校验候选备份、暂存后退出并交接恢复；运行中的入口另执行任务状态与文档保存检查 |

安装或恢复后，重新打开应用并核对插件版本、实际加载位置与功能入口。应用重开和方法返回都不足以证明目录替换成功，具体判据见下文“安装与恢复完成判据”。

## 创建项目与本地构建

官方插件位于 `Plugins/`；`Plugins/Directory.Build.props` 指定 x64、`net10.0-windows`，关闭热重载支持，ARM64 没有对应完整交付链。具体版本和额外属性仍以实际 `.csproj` 为准。新增项目应按现有引用边界选择宿主项目，带 WPF UI 才添加 `UseWPF`，通过 `VersionPrefix` 管理编译版本，并使 manifest 和必要静态资源进入输出。

可参考实际存在的 `Plugins/SystemMonitor/SystemMonitor.csproj`：它声明宿主项目引用、`manifest.json` / README / CHANGELOG 的 `CopyToOutputDirectory=PreserveNewest`，并显式导入 `PluginProject.HostCopy.targets`。这些文件不会因为放在项目旁边就必然进入任意项目的构建输出。

以下是局部构建示例，写本地 bin/obj，可能还会构建项目引用；不打包上传。是否额外复制宿主目录取决于下述 HostCopy 条件：

```powershell
dotnet build .\Plugins\SystemMonitor\SystemMonitor.csproj -c Debug -p:Platform=x64
```

### HostCopy 的复制条件

项目导入 `PluginProject.HostCopy.targets` 后，`PostBuild` 挂在 `PostBuildEvent` 之后；其中复制命令只在 `SolutionDir` 非空、非 `*Undefined*` 时执行：

- 主 DLL 从 `$(OutDir)$(TargetName)$(TargetExt)` 取出，同时复制到宿主 `bin/x64/Debug/net10.0-windows/Plugins/$(TargetName)` 和 Release 对应目录。一次 Debug 构建也会写 Release 宿主目录，目录名不能证明 DLL 的构建配置。
- manifest、README、CHANGELOG 从 `ProjectDir` 取出，避免共享 `OutDir` 的同名元数据串包；这不是复制完整产物目录，不包含所有私有依赖、运行时文件或资源。
- 目标目录使用 `TargetName`，而正式包安装使用 manifest `id`。两者允许不同，因此本地 HostCopy 目录不自动证明正式包布局正确。

直接 `dotnet build <项目.csproj>` 不保证提供有效 `SolutionDir`。HostCopy 未执行时，检查 target 是否导入、`SolutionDir` 与 `OutDir` 的实际值，再检查两个宿主输出目录。

## manifest 身份、DLL 和包版本

`manifest.json` 声明稳定插件身份及主 DLL。项目名、插件 ID 和主 DLL 名不必相等：

| 值 | 实际责任 |
| --- | --- |
| 项目文件名 / `TargetName` | 构建输出与 HostCopy 目录；普通 wrapper 按 `Plugins/<PluginName>/<PluginName>.csproj` 定位项目 |
| manifest `id` | 正式包根目录、`<id>-<FileVersion>.cvxp` 文件名、上传插件标识和安装目录 |
| manifest `dllpath` | 相对插件根目录的主 DLL；可与项目名及 `id` 不同，打包器据此读取编译版本 |
| manifest `version` | 正式打包时由主 DLL `FileVersion` 同步；只改 JSON 不会改变 DLL ABI 或编译版本 |
| manifest `requires` | 应声明真实最低宿主要求；不能据此推断所有安装入口或启动加载器都会拦截不兼容包 |

打包器要求 manifest 为不超过 1 MiB 的 UTF-8 JSON 对象，按大小写无关字段处理：`id` 必须为 1–64 字符、ASCII 字母开头，其后允许字母、数字、点、下划线和连字符；`dllpath` 若提供必须为目录内相对 DLL 路径，不能含父目录跳转、绝对路径或冒号。它不会强制三个名字相同。

只做静态 manifest 校验、不构建、不同步版本、不打包上传，可运行：

```powershell
python .\Scripts\package_cvxp.py --project-file .\Plugins\SystemMonitor\SystemMonitor.csproj --validate-only
```

这个模式只执行 manifest 校验；缺少 manifest 时也会进入历史兼容提示后返回，并不证明新插件已正确配置。它不检查主 DLL 是否已生成，也不验收 `requires`、运行依赖或完整包可安装性。

## .cvxp 的生成与依赖所有权

`Scripts/package_cvxp.py` 的正常链路是：可选构建 → 找输出/共享文件清单 → 找主 DLL 并读 `FileVersion` → 同步项目目录 manifest → 生成 ZIP 格式 `.cvxp` → 上传包及 `LATEST_RELEASE`。显式项目路径默认先找 `bin/x64/<Configuration>/<Framework>`，再找不含 x64 的输出目录；仅传路径而不加 `--build` 可能使用旧 DLL。

正式包以 manifest `id` 为根目录，复制编译输出，再用项目目录中的 README、CHANGELOG、manifest、PackageIcon 补齐/覆盖元数据。按 `shared_files.json` 的相对路径剥离宿主共有文件并生成 `stripped_files.json`；不是按所有 `ColorVision.*` 或某个 DLL 前缀粗略删除。PDB 不入包，`runtimes/` 下仅保留 `win/` 和 `win-x64/` 分支。

仓库 `Plugins/` / `Projects/` 使用默认共享清单时，打包前核对它与当前 Release x64 宿主输出的一致性；宿主输出缺失或清单漂移会阻断。私有依赖不能误列为宿主共享文件，宿主 API 有变更也不能靠插件 manifest 改版本掩盖 ABI 不匹配。共享清单生成与正式发布入口见[构建与发布脚本](../scripts/README.md)。

## 下载与安装预检

市场下载链 `MarketplacePackageDownloadService` 先准备包，再由 `MarketplacePackagePreflightReader` 检查顶层 manifest 的路径、数量、大小/JSON，以及非空 manifest ID 与请求 ID 是否一致。预检接受无顶层 manifest 的历史包；它不是所有归档项的全面安全审计，也没有证明 DLL 来源可信。

`MarketplaceClient.VerifyFileHash` 先要求文件可用；只有提供 `ExpectedHash` 才比较 SHA-256，缺少 hash 时跳过摘要验证。摘要匹配只能说明与给定摘要一致，不能单独证明发布者身份；本地直接交给 `PluginUpdater` 的包也不能默认拥有市场请求身份/hash 校验。

`PluginUpdater.IsPluginPackageFileReady` 检查文件存在、非空、没有同名 `.aria2`、扩展名为 `.cvxp` 或 `.zip`，且 ZIP 内存在文件项。这是就绪检查，不是可执行代码可信认证。

## 安装目录替换与失败回退

`UpdatePluginWithRestartArguments` 为本次安装创建独立临时目录，经 `StagePluginPackagesForUpdate` 分开准备 manifest 包与 legacy overlay。提取使用 `ZipFile.ExtractToDirectory`；manifest 位于包根或一层包装目录时都可识别：

- manifest 包按 `id` 规范化到安装 `Plugins/<id>/`；多个顶层 manifest、重复 ID、不能解析成直接子目录的 ID 会拒绝。路径准备和目录事务还检查目标范围、目录重叠及相应 reparse point。
- 无 manifest 包保持历史相对目录布局，以 overlay 方式覆盖。这个兼容分支没有可靠的完整目录回滚，不应作为新包规范。
- 正常 manifest 更新为已有目标取得持久恢复备份，然后生成外部更新脚本。更新协调器按当前可执行文件完整路径定位其它实例；协调失败会记录日志并继续，不能保证所有占用一定已消失。

`PluginDirectoryTransactionBatchScript` 先把所有新内容复制到安装 Plugins 目录下、同卷的 `.ColorVisionUpdate-<GUID>/incoming`，随后逐个把旧目录移入 `rollback`，再把新目录移到目标。它替换整个插件目录，旧包中有而新包中没有的文件会消失，其它插件目录不应受影响。

检测到替换失败时，脚本逆序回退已切换目录；回退不完整会保留事务目录并记录日志，持久备份另行保留。这是带补偿回退的目录替换，不是多插件/断电场景下的全局原子事务；混入的 legacy overlay 也不在该目录事务保护范围内。

外部脚本通过 `ExternalUpdateBatchScript` 先尝试等待原进程退出；超时（15 秒）或等待异常时调用强制终止，再短暂等待并继续，不再次确认进程确实退出。因此“进入替换阶段”不保证旧进程和文件占用已经消失。安装不是热重载；更新方法发起交接不代表新版本已在当前进程中加载。

## 插件备份与恢复

`PluginRecoveryBackupService` 默认存放在 `Environments.DirLocalAppData/PluginRecovery`，按精确安装目录和插件身份隔离；不是主程序可选完整快照，也不是更新临时目录里的 rollback。

新建备份时，服务比较复制前后源目录和副本的内容目录，记录逐文件 SHA-256、文件数/总字节数及 manifest 元数据，通过校验后才把 `.creating` 目录变为完成备份。备份按 manifest 元数据复用，可能不包含同版本目录后续的文件改动。完成新备份后尝试保留最近三个已验证备份；删除失败只记录警告，因此实际数量可能更多。损坏或 `.creating` 条目不会作为普通旧备份自动删除。

### 备份复用与校验时机

| API / 阶段 | 实际校验 |
| --- | --- |
| `EnsureCurrentVersionBackup` 命中本进程已准备备份 | 检查备份目录仍存在、manifest 元数据匹配；不重新扫描源目录或校验备份 payload |
| `EnsureCurrentVersionBackup` 从磁盘查找备份 | 经 `GetAvailableBackup` 校验备份后，再按当前 manifest 元数据决定复用；没有合适备份才新建 |
| `GetRecoveryBackupCandidate` / `GetRecoveryBackupCandidates` | 只读取候选元数据；损坏或被占用的 payload 也可能出现在列表 |
| `GetAvailableBackup` / `GetAvailableBackups`、`ReadBackupMetadata` / `TryReadBackupMetadata` | 读取元数据并验证 payload 的内容与 manifest；后两个方法也不是仅仅读元数据 |
| `RestoreAsync` | 重新验证 payload、复制并复核暂存内容，只允许恢复到当前运行的同一安装位置，再交给外部目录替换脚本 |

### 安装与恢复完成判据

| 观察到的信号 | 可以确认什么，不能确认什么 |
| --- | --- |
| `InstallPackageAsync` 返回 `true` | 包准备/预检后调用过安装器；底层 void 更新方法会捕获错误，因此不证明交接已启动或目录替换成功 |
| 已出现恢复候选 | 有可展示的备份元数据，不证明 payload 校验通过 |
| `RestoreAsync` 正常返回 | 已完成校验、暂存和外部恢复交接，不是等待恢复进程执行完毕 |
| 应用重开或交接标记消失 | 成功和失败分支都会尝试重开并清交接标记，不证明替换成功；须核对更新日志、目标版本/文件和新进程加载，加载失败处理属于[装载契约](./overview.md) |

## 提取插件与补回依赖

“提取插件”将已安装的插件复制到选定文件夹，并尝试补回打包时剥离的宿主依赖，适合查看或转交当前文件集合。

1. 在 **浏览商店** 的已安装插件列表中，右键目标插件，选择 **提取插件**。
2. 选择目标文件夹。文件夹非空时，界面使用其下以插件 ID（缺失时使用名称）命名的子目录；该子目录可能已存在，选新空目录可避免旧文件混入。
3. 完成提示后查看自动打开的输出目录，并检查日志中补回和跳过的依赖。

`PluginExtractor.ExtractPlugin` 先复制插件目录，再从当前宿主根目录按 `stripped_files.json` 补依赖；两步都不覆盖已有目标文件。它没有重建 `.cvxp`，也没有验证补回的 DLL 与原始打包时版本一致。

当前实现存在明确缺口：`RestoreStrippedFiles` 直接对清单项使用 `Path.Combine`，未做完整路径约束；源目录递归复制没有独立 reparse-point 拒绝；依赖缺失被跳过，恢复异常在内部记录后吞掉，外层仍可能返回 `true`。提取前须确认源目录和清单可信、目标位置符合预期。“插件已成功提取”只表示外层复制流程返回成功，不证明依赖完整、路径安全或产物可独立运行。

## 发布插件

获准发布普通插件后，在仓库根目录运行 `Scripts\package_plugin.bat <PluginName>`。wrapper 选择仓库虚拟环境或系统 Python，传 `--build` 给打包器，上传产物并清理本地包；不支持 `--no-upload`。

正常打包还会改写项目 manifest 版本；包上传后继续上传 `LATEST_RELEASE`，上传尝试结束的 `finally` 会删除本地 `.cvxp`，即使上传失败也可能已经没有本地包。只有包上传与发布标记都成功才是脚本发布成功；本地文件消失不证明远端交付成功，完成报告还需明确实际远端包/元数据验证证据。

Spectrum 的独立 ZIP + `.cvxp` 有专用双通道发布入口，主程序使用 `Scripts\release.bat`；不能用普通插件 wrapper 替代。完整入口和交付验证见[构建与发布脚本](../scripts/README.md)。

## 常见问题

| 现象 | 检查顺序 |
| --- | --- |
| 构建成功，宿主找不到插件 | 先看项目输出，再查 HostCopy 条件；完整目录存在后转到装载契约检查发现与依赖 |
| 显示“插件包预检失败” | 按错误检查请求 ID 与 manifest ID、顶层 manifest 数量、JSON/大小/路径；预检失败不会调用市场安装器 |
| 更新后仍是旧版本 | 查[更新日志](../deployment/auto-update.md#更新失败与日志)确认是否交接、替换或回退，再查目标文件和新进程实际加载目录 |
| 列表有备份，回退却失败 | 候选列表不读 payload；检查备份完整性、读取权限/占用和安装位置是否匹配 |
| 提取后缺依赖或仍有旧文件 | 查看缺失依赖日志和 `stripped_files.json`，核对当前宿主依赖及非空目标目录；提取过程不会覆盖旧文件 |

## 验证入口与范围

- `Scripts/tests/test_package_cvxp.py`：manifest 身份/路径、DLL 版本同步、包根目录、私有依赖保留及默认共享清单漂移阻断。
- `MarketplacePackageDownloadServiceTests`：通过替身验证下载/摘要失败及 ID 预检阻止安装调用；不执行真实安装。
- `PluginUpdaterBatchTests`：暂存目录归一、重复 ID/坏包拒绝、manifest/legacy 分离和生成脚本文本；回滚分支存在不等于故障注入已覆盖。
- `PluginRecoveryBackupServiceTests`：备份内容、损坏识别、候选与已验证备份区别、安装隔离、保留策略和恢复脚本生成。
- `UpdaterBatchExecutionTests`：在隔离临时目录执行脚本，覆盖完整插件目录替换、旧文件移除、其它插件保留与 legacy 布局；不是正式安装目录验收。

这些测试分别覆盖包结构、流程调用和隔离目录中的脚本执行，不证明真实安装成功。HostCopy 双配置复制、真实文件占用/UAC、崩溃中断、回退失败及 `PluginExtractor` 的清单/路径风险仍需专项验证。
