---
knowledge_id: "plugins.getting-started"
knowledge_type: "topic"
status: "current"
summary: "插件构建产物、HostCopy、manifest包身份、安装替换和恢复契约；发布会上传，安装器返回不等于替换或重启后加载成功。"
aliases: ["新插件怎么创建","HostCopy 为什么没执行","插件安装失败","插件更新回滚","导出插件依赖","PluginProject.HostCopy.targets","package_plugin.bat","PluginUpdater","PluginExtractor","stripped_files.json"]
code_paths: ["Plugins/Directory.Build.props","Plugins/SystemMonitor/SystemMonitor.csproj","PluginProject.HostCopy.targets","Scripts/package_cvxp.py","Scripts/package_plugin.bat","UI/ColorVision.UI/Plugins/PluginManifest.cs","UI/ColorVision.UI/Plugins/PluginUpdater.cs","UI/ColorVision.UI/Plugins/PluginDirectoryTransactionBatchScript.cs","UI/ColorVision.UI/Plugins/PluginRecoveryBackupService.cs","UI/ColorVision.UI/Plugins/PluginExtractor.cs","UI/ColorVision.UI/Update/ApplicationUpdateProcessCoordinator.cs","UI/ColorVision.UI/Update/ExternalUpdateBatchScript.cs","UI/ColorVision.UI.Desktop/Marketplace/MarketplacePackagePreflight.cs","UI/ColorVision.UI.Desktop/Marketplace/MarketplacePackageDownloadService.cs","UI/ColorVision.UI.Desktop/Marketplace/MarketplaceClient.cs"]
test_paths: ["Scripts/tests/test_package_cvxp.py","Test/ColorVision.UI.Tests/MarketplacePackageDownloadServiceTests.cs","Test/ColorVision.UI.Tests/PluginUpdaterBatchTests.cs","Test/ColorVision.UI.Tests/PluginRecoveryBackupServiceTests.cs","Test/ColorVision.UI.Tests/UpdaterBatchExecutionTests.cs"]
related: ["plugins.index","plugins.model","delivery.scripts"]
---

# 插件产物、安装与交付

本主题负责从插件项目输出到 `.cvxp`、安装目录替换、备份恢复和导出依赖的边界。程序集怎样发现、依赖怎样解析、provider 怎样进入宿主以及启停规则，统一见[插件装载契约](./overview.md)，不由“安装文件已经存在”推断运行可用。

插件包是将代码引入宿主的交付物，不是可信数据附件。构建、校验 manifest、下载、安装/恢复和发布是不同授权范围；安装或恢复会保存配置、写安装目录、交接到外部进程，并可能关闭同一安装的运行实例。不要为验证文档而执行这些动作。

## 项目输出与 HostCopy 不是同一件事

官方插件位于 `Plugins/`；`Plugins/Directory.Build.props` 指定 x64、`net10.0-windows`，关闭热重载支持，ARM64 没有对应完整交付链。具体版本和额外属性仍以实际 `.csproj` 为准。新增项目应按现有引用边界选择宿主项目，带 WPF UI 才添加 `UseWPF`，通过 `VersionPrefix` 管理编译版本，并使 manifest 和必要静态资源进入输出。

可参考实际存在的 `Plugins/SystemMonitor/SystemMonitor.csproj`：它声明宿主项目引用、`manifest.json` / README / CHANGELOG 的 `CopyToOutputDirectory=PreserveNewest`，并显式导入 `PluginProject.HostCopy.targets`。这些文件不会因为放在项目旁边就必然进入任意项目的构建输出。

以下是局部构建示例，写本地 bin/obj，可能还会构建项目引用；不打包上传。是否额外复制宿主目录取决于下述 HostCopy 条件：

```powershell
dotnet build .\Plugins\SystemMonitor\SystemMonitor.csproj -c Debug -p:Platform=x64
```

`PluginProject.HostCopy.targets` 的 `PostBuild` 只在导入该 target 且 `SolutionDir` 非空、非 `*Undefined*` 时执行。它有三个容易误判的行为：

- 主 DLL 从 `$(OutDir)$(TargetName)$(TargetExt)` 取出，同时复制到宿主 `bin/x64/Debug/net10.0-windows/Plugins/$(TargetName)` 和 Release 对应目录。一次 Debug 构建也会写 Release 宿主目录，目录名不能证明 DLL 的构建配置。
- manifest、README、CHANGELOG 从 `ProjectDir` 取出，避免共享 `OutDir` 的同名元数据串包；这不是复制完整产物目录，不包含所有私有依赖、运行时文件或资源。
- 目标目录使用 `TargetName`，而正式包安装使用 manifest `id`。两者允许不同，因此本地 HostCopy 目录不自动证明正式包布局正确。

直接 `dotnet build <项目.csproj>` 不保证提供有效 `SolutionDir`；HostCopy 未执行时先查实际输出和构建属性，不要通过发布或盲目覆盖宿主来“验证构建”。

## manifest 身份、DLL 和包版本

`manifest.json` 声明稳定插件身份及主 DLL。三种名字不必相等：

| 值 | 实际责任 |
| --- | --- |
| 项目文件名 / `TargetName` | 构建输出与 HostCopy 目录；普通 wrapper 按 `Plugins/<PluginName>/<PluginName>.csproj` 定位项目 |
| manifest `id` | 正式包根目录、`<id>-<FileVersion>.cvxp` 文件名、上传插件标识和安装目录 |
| manifest `dllpath` | 相对插件根目录的主 DLL；可与项目名及 `id` 不同，打包器据此读取编译版本 |
| manifest `version` | 正式打包时由主 DLL `FileVersion` 同步；只改 JSON 不会改变 DLL ABI 或编译版本 |
| manifest `requires` | 应声明真实最低宿主要求；不能据此推断所有安装入口或启动加载器都会拦截不兼容包 |

打包器的 manifest 校验按大小写无关字段处理：`id` 必须为 1–64 字符、ASCII 字母开头，其后允许字母、数字、点、下划线和连字符；`dllpath` 若提供必须为目录内相对 DLL 路径，不能含父目录跳转、绝对路径或冒号。它不会强制三个名字相同。

只做静态 manifest 校验、不构建、不同步版本、不打包上传，可运行：

```powershell
python .\Scripts\package_cvxp.py --project-file .\Plugins\SystemMonitor\SystemMonitor.csproj --validate-only
```

这个模式只执行 manifest 校验；缺少 manifest 时也会进入历史兼容提示后返回，并不证明新插件已正确配置。它不检查主 DLL 是否已生成，也不验收 `requires`、运行依赖或完整包可安装性。

## .cvxp 的生成与依赖所有权

`Scripts/package_cvxp.py` 的正常链路是：可选构建 → 找输出/共享文件清单 → 找主 DLL 并读 `FileVersion` → 同步项目目录 manifest → 生成 ZIP 格式 `.cvxp` → 上传包及 `LATEST_RELEASE`。显式项目路径默认先找 `bin/x64/<Configuration>/<Framework>`，再找不含 x64 的输出目录；仅传路径而不加 `--build` 可能使用旧 DLL。

正式包以 manifest `id` 为根目录，复制编译输出，再用项目目录中的 README、CHANGELOG、manifest、PackageIcon 补齐/覆盖元数据。按 `shared_files.json` 的相对路径剥离宿主共有文件并生成 `stripped_files.json`；不是按所有 `ColorVision.*` 或某个 DLL 前缀粗略删除。PDB 不入包，`runtimes/` 下仅保留 `win/` 和 `win-x64/` 分支。

仓库 `Plugins/` / `Projects/` 使用默认共享清单时，打包前核对它与当前 Release x64 宿主输出的一致性；宿主输出缺失或清单漂移会阻断。私有依赖不能误列为宿主共享文件，宿主 API 有变更也不能靠插件 manifest 改版本掩盖 ABI 不匹配。共享清单生成与正式发布入口见[构建与发布脚本](../scripts/README.md)。

## 下载预检与安装可信边界

市场下载链 `MarketplacePackageDownloadService` 先准备包，再由 `MarketplacePackagePreflightReader` 检查顶层 manifest 的路径、数量、大小/JSON，以及非空 manifest ID 与请求 ID 是否一致。预检接受无顶层 manifest 的历史包；它不是所有归档项的全面安全审计，也没有证明 DLL 来源可信。

`MarketplaceClient.VerifyFileHash` 先要求文件可用；只有提供 `ExpectedHash` 才比较 SHA-256，缺少 hash 时跳过摘要验证。摘要匹配只能说明与给定摘要一致，不能单独证明发布者身份；本地直接交给 `PluginUpdater` 的包也不能默认拥有市场请求身份/hash 校验。

`PluginUpdater.IsPluginPackageFileReady` 检查文件存在、非空、没有同名 `.aria2`、扩展名为 `.cvxp` 或 `.zip`，且 ZIP 内存在文件项。这是就绪检查，不是可执行代码可信认证。

## 安装布局、替换和原子性范围

`UpdatePluginWithRestartArguments` 为本次安装创建独立临时目录，经 `StagePluginPackagesForUpdate` 分开准备 manifest 包与 legacy overlay。提取使用 `ZipFile.ExtractToDirectory`；manifest 位于包根或一层包装目录时都可识别：

- manifest 包按 `id` 规范化到安装 `Plugins/<id>/`；多个顶层 manifest、重复 ID、不能解析成直接子目录的 ID 会拒绝。路径准备和目录事务还检查目标范围、目录重叠及相应 reparse point。
- 无 manifest 包保持历史相对目录布局，以 overlay 方式覆盖。这个兼容分支没有可靠的完整目录回滚，不应作为新包规范。
- 正常 manifest 更新为已有目标取得持久恢复备份，然后生成外部更新脚本。更新协调器按当前可执行文件完整路径定位其它实例；协调失败会记录日志并继续，不能保证所有占用一定已消失。

`PluginDirectoryTransactionBatchScript` 先把所有新内容复制到安装 Plugins 目录下、同卷的 `.ColorVisionUpdate-<GUID>/incoming`，随后逐个把旧目录移入 `rollback`，再把新目录移到目标。它替换整个插件目录，旧包中有而新包中没有的文件会消失，其它插件目录不应受影响。

检测到替换失败时，脚本逆序回退已切换目录；回退不完整会保留事务目录并记录日志，持久备份另行保留。这是带补偿回退的目录替换，不是多插件/断电场景下的全局原子事务；混入的 legacy overlay 也不在该目录事务保护范围内。

外部脚本通过 `ExternalUpdateBatchScript` 先尝试等待原进程退出；超时（15 秒）或等待异常时调用强制终止，再短暂等待并继续，不再次确认进程确实退出。因此“进入替换阶段”不保证旧进程和文件占用已经消失。安装不是热重载；更新方法发起交接不代表新版本已在当前进程中加载。

## 持久备份与恢复完成

`PluginRecoveryBackupService` 默认存放在 `Environments.DirLocalAppData/PluginRecovery`，按精确安装目录和插件身份隔离；不是主程序可选完整快照，也不是更新临时目录里的 rollback。

新建备份时，服务比较复制前后源目录和副本的内容目录，记录逐文件 SHA-256、文件数/总字节数及 manifest 元数据，通过校验后才把 `.creating` 目录变为完成备份。现有备份可按 manifest 元数据复用，因此不能宣称每次更新都会重新快照当前目录的所有改动。清理策略最多保留三个已验证完成备份，损坏或 `.creating` 条目不会作为普通旧备份自动删除。

`GetRecoveryBackupCandidate` / `GetRecoveryBackupCandidates` 只读取候选元数据，损坏或被占用的 payload 也可能出现在候选列表。`GetAvailableBackup` 和真正 `RestoreAsync` 会验证 payload；恢复还复核暂存内容，并只允许回到当前运行的同一安装位置，然后通过同类外部目录替换脚本完成。

| 观察到的信号 | 可以确认什么，不能确认什么 |
| --- | --- |
| `InstallPackageAsync` 返回 `true` | 包准备/预检后调用过安装器；底层 void 更新方法会捕获错误，因此不证明交接已启动或目录替换成功 |
| 已出现恢复候选 | 有可展示的备份元数据，不证明 payload 校验通过 |
| `RestoreAsync` 正常返回 | 已完成校验、暂存和外部恢复交接，不是等待恢复进程执行完毕 |
| 应用重开或交接标记消失 | 成功和失败分支都会尝试重开并清交接标记，不证明替换成功；须核对更新日志、目标版本/文件和新进程加载，加载失败处理属于[装载契约](./overview.md) |

## 导出插件不是安装，也不是已验证的独立分发包

`PluginExtractor.ExtractPlugin` 复制已安装插件目录，再从当前宿主根目录按 `stripped_files.json` 补依赖；已有目标文件不覆盖。它没有重建新的 `.cvxp`，也没有验证补回的 DLL 与原始打包时版本一致。

当前实现存在明确缺口：`RestoreStrippedFiles` 直接对清单项使用 `Path.Combine`，未做完整路径约束；源目录递归复制没有独立 reparse-point 拒绝；依赖缺失被跳过，恢复异常在内部记录后吞掉，外层仍可能返回 `true`。因此不要让 AI 对不可信插件/清单自动执行导出，也不能把“导出成功”当作依赖完整、无目录越界或产物可运行的证据。这些是当前实现风险，本页未修复产品代码。

## 发布授权与验收

普通插件正式发布使用 `Scripts\package_plugin.bat <PluginName>`；wrapper 选择仓库虚拟环境或系统 Python，传 `--build` 给打包器，拒绝 `--no-upload`。它不是只生成本地包的验证命令。

正常打包还会改写项目 manifest 版本；包上传后继续上传 `LATEST_RELEASE`，上传尝试结束的 `finally` 会删除本地 `.cvxp`，即使上传失败也可能已经没有本地包。只有包上传与发布标记都成功才是脚本发布成功；本地文件消失不证明远端交付成功，完成报告还需明确实际远端包/元数据验证证据。

Spectrum 的独立 ZIP + `.cvxp` 有专用双通道发布入口，主程序使用 `Scripts\release.bat`；不能用普通插件 wrapper 替代。完整入口和副作用统一见[构建与发布脚本](../scripts/README.md)，不要在普通安装排障中触发发布。

## 验证入口与缺口

- `Scripts/tests/test_package_cvxp.py`：manifest 身份/路径、DLL 版本同步、包根目录、私有依赖保留及默认共享清单漂移阻断。
- `MarketplacePackageDownloadServiceTests`：通过替身验证下载/摘要失败及 ID 预检阻止安装调用；不执行真实安装。
- `PluginUpdaterBatchTests`：暂存目录归一、重复 ID/坏包拒绝、manifest/legacy 分离和生成脚本文本；回滚分支存在不等于故障注入已覆盖。
- `PluginRecoveryBackupServiceTests`：备份内容、损坏识别、候选与已验证备份区别、安装隔离、保留策略和恢复脚本生成。
- `UpdaterBatchExecutionTests`：在隔离临时目录执行脚本，覆盖完整插件目录替换、旧文件移除、其它插件保留与 legacy 布局；不是正式安装目录验收。

测试引用不代表已经运行通过。HostCopy 双配置复制、真实文件占用/UAC、崩溃中断、回退失败和 `PluginExtractor` 的清单/路径风险仍需各自专项验证，不能由 manifest 校验或网站构建代替；实际安装、恢复、导出和发布须分别获得授权。
