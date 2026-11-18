---
knowledge_id: "delivery.scripts"
knowledge_type: "guide"
status: "current"
summary: "主程序、插件和项目包的正式发布入口、只读校验与上传清理副作用。"
aliases: ["发布","打包","release.bat","package_project.bat","只做本地构建","版本标签","auto-tag.yml"]
code_paths: [".github/workflows/auto-tag.yml","Directory.Build.props","Scripts/release.bat","Scripts/build.py","Scripts/build_update.py","Scripts/generate_shared_files.py","Scripts/installer_shared_files.py","Scripts/package_project.bat","Scripts/package_plugin.bat","Scripts/package_cvxp.py","Scripts/build_spectrum.py","Scripts/export_offline_updates.py"]
related: ["delivery.index","delivery.testing","delivery.backend","delivery.update","delivery.code-statistics","ui.publishing"]
---

# 构建与发布脚本

本页说明主程序、插件和项目包的发布入口、上传配置与失败处理。源码规模与历史图表使用独立的[代码统计工具](./code-statistics.md)，UI NuGet 发布见[包构建与发布](../../04-api-reference/ui-components/publishing.md)。

## 选择入口

用户明确指定产物时按指定范围发布。直接说“发布”时，按本次交付改动涉及的产物选择入口。只改插件或客户项目时，只发布受影响的独立包。只改主程序或随主程序交付的共享代码时，只发布主程序。两者都改时，分别走各自的发布入口。

主程序发布使用当前 Git 分支和整个工作区，构建和发布说明涵盖其他对话已保存到同一工作区的待发布主程序改动。主程序和独立插件、客户项目各自发布。只有用户明确要求时才收窄范围、切换或创建分支、创建发布工作区，或抽取当前对话的改动单独发布。

| 场景 | 使用入口 | 说明 |
| --- | --- | --- |
| 主程序正式发布 | `Scripts\release.bat` | 唯一正常入口，发布并验收签名安装包。有历史基线时同时发布增量包，无历史基线时用本版全量 ZIP 建立基线 |
| 发布插件包 | `Scripts\package_plugin.bat <PluginName>` | 面向 `Plugins/<PluginName>/`，上传尝试结束后删除本地 `.cvxp` |
| 发布项目包 | `Scripts\package_project.bat <ProjectName>` | 面向 `Projects/<ProjectName>/`，上传尝试结束后删除本地 `.cvxp` |
| 发布 Spectrum 独立包和插件包 | `Scripts\Spectrum.bat --release-notes "<说明>"` | 同时维护独立更新源和 ColorVision 插件更新源，完整远程验收后才删除本地 `.cvxp` |
| 导出离线更新文件夹 | `py Scripts\export_offline_updates.py --out <空目录> --from-version <现场版本> [--plugins <ID,...>]` | 只读取更新服务，导出主程序增量链、完整安装包和指定插件及 SHA-256 清单，供断网现场作为更新来源；用法见[离线更新文件夹](../deployment/auto-update.md#离线更新文件夹) |
| 发布外部编译产物 | `py Scripts\package_cvxp.py --src-dir <输出目录>` | 适合只拿到插件输出目录的场景 |
| 只校验插件清单 | `py Scripts\package_cvxp.py --project-file <插件.csproj> --validate-only` | 不构建、不打包、不上传 |
| 刷新两份共享文件表 | `py Scripts\generate_shared_files.py` | 从当前 Release x64 宿主输出一次扫描，同时更新仓库与 Plugin Kit 镜像 |
| 校验共享文件表 | `py Scripts\generate_shared_files.py --check` | 只比较 `shared_files` 集合，忽略时间戳、顺序和重复项 |

`release.bat` 调用 `build.py` 上传主包，再调用 `build_update.py` 打包并把本次实际产物交给 `verify_release.py` 验收。正式发布统一执行 `release.bat`。`build_update.py` 直接执行会进入打包和上传流程，包括传入 `--help` 时。

## 正式发布

以下命令会签名、打包并修改远端发布状态，只在用户明确要求发布时执行；文档或代码审阅不授予发布权限。

对于主程序或仓库级的「发布」「打包发布」「直接打包」「快速发布」，按以下快速发布流程操作。

1. 更新根目录 `Directory.Build.props` 的 `VersionPrefix`。用户指定版本时使用指定值，否则将本地现有版本的最后一段加一。版本从本地读取，无需先查远端。
2. 在根 `CHANGELOG.md` 顶部新增本次版本的一至三条用户可感知变化。发布说明省略测试、文档、文件名、类名、重构过程及逐提交记录。同一三段版本系列的条目全部保留。切换系列时，将旧系列完整合并到 `docs/_history/CHANGELOG.md`，保留已有历史并避免重复，根日志从新系列开始。同系列发布只改根日志。有记录缺失时从 Git 历史补回现有条目，缺乏记录的版本无需补写。
3. 从原工作区执行一次 `Scripts\release.bat` 并报告结果。由脚本完成构建、安装器校验、上传和并行验收。准备工作限于版本和日志，沿用脚本提供的验收结果。

```powershell
Scripts\release.bat
```

快速发布省略单独测试、预构建、知识检索或 impact 分析、深度 diff 或历史审查、重复远端与下载校验。仅在补齐缺失的版本日志时读取对应历史。脚本失败时只检查失败阶段，按证据做一次修正并重跑一次。扩大排查范围由用户决定。

只有显式要求「完整发布」才在快速验收之外增加独立测试、深入的改动范围审查、完整制品下载和哈希校验，以及远端分支与标签同步。创建提交或标签、推送均按用户明确要求执行。发布后如需提交并推送，将 `Directory.Build.props` 和根 `CHANGELOG.md` 一起提交，先同步再推送，随后核对远端分支。公开日志只记录用户可感知变化。

主程序、ServiceHost 和 OperationsWatchdog 共用根目录 `Directory.Build.props` 的 `VersionPrefix`。增量包携带完整的 `ServiceHost/` 和 `OperationsWatchdog/`，支持 ZIP 部署后的首次安装，以及下一次增量更新对旧安装的修复。

`docs/_history/CHANGELOG.md` 保存既有旧版记录和已经结束的版本系列，仅供仓库内回顾。只在三段版本系列切换时合并上一系列，同系列的日常发布不修改历史文件。历史文件不作为主程序 changelog 上传；下划线目录会从公开文档构建中排除。

发布前会按 SHA-256 校验 OpenCV 原生运行库：OpenCvSharp 的源文件由主程序 `project.assets.json` 中实际解析的 NuGet 版本定位，本地 OpenCV 由 `packages/OpenCV.Release.x64.props` 定位。安装器构建前自动修复输出副本，复制后再次校验；缺少源文件、复制失败或校验不符会停止发布。增量打包前只读复查，包内包含的原生库还会在上传前再次校验，文件大小和修改时间相同不能代替内容一致性。

OpenCV 原生运行库遵循普通内容差分：与所选历史基准内容相同的 DLL 不进入增量包；新增或内容变化的 DLL 正常进入增量包。文件完整性校验只保证内容正确，不会强制把未变化的库加入更新包，也不因历史版本异常增加额外修复载荷。

主程序发布不携带输出根目录的 `CHANGELOG.md`：`build_update.py` 在全量 ZIP 和增量 CVX 中排除该路径，`generate_shared_files.py` 也忽略该文件，避免旧输出副本重新进入共享清单。运行时 `Config/` 和旧版窗口尺寸诊断可能留下的根目录 `window-resize-diagnostics.mode`、`window-resize-traces/` 属于本机产物，不进入主程序全量 ZIP、增量 CVX 或插件共享清单。外部 `ColorVision.aip` 不应包含这些文件；仓库根目录的变更日志原稿继续由 `build.py` 独立上传，插件自己的日志照常随插件包交付。这些规则不清理历史包或已有安装目录。

发布按顺序上传主包、`CHANGELOG.md` 和版本化宿主共享清单，再更新 `LATEST_RELEASE`。主包或其元数据上传失败会阻止版本号更新。随后从桌面 `History` 选择旧版基线。找到基线时生成并上传增量包。找不到时，脚本自动把本版全量 ZIP 留在 `History`，供后续同主次版本的发布作为差分基线。基线 ZIP 解压到系统临时目录，打包结束后自动清理。

两种情况都要生成本版全量 ZIP，并验收安装包 Authenticode 签名、远端 latest/changelog、安装包 Range 下载大小和 Git 状态。生成增量包时还要验收该包的远端下载大小。后端 HTTP 接口是唯一发布通道，脚本全部阶段返回零才算发布成功。 [检查更新](../deployment/auto-update.md#检查复用与元数据新鲜度)

提交 `LATEST_RELEASE` 前还必须上传该宿主版本的 PluginKit 共享文件清单，目的路径为 `Tool/PluginKit/shared-files/<宿主版本>/net10.0-windows-x64.json`。清单失败会阻止更新版本标记。文件复用现有公开 `/download/Tool/...` 下载通道，不需要启动新服务；客户端按精确目标版本获取并缓存，见 [PluginKit SDK 打包器](../plugin-development/sdk-packaging.md#远端版本化共享清单)。这不是每次重建 `cvplugin.exe`：共享清单随宿主发布，打包器逻辑可以独立升级。

远端清单是“宿主共享集合、全量 ZIP 交付集合、安装器可证明的同路径交付集合”的交集，不直接公开整个编译目录。`installer_shared_files.py` 从 AIP 文件行、组件和目录树还原 APPDIR 下的安装位置；仅认可来源位于当前宿主输出且源/安装相对路径相同的文件。宏、重命名、目录循环或无法确定的映射不声明共享，插件保留这些依赖。原两份本地 `shared_files.json` 的生成/漂移门禁保持原有集合契约，不能把远端较小集合覆盖回本地镜像。元数据校验仍不是实际安装与运行验收。

### Git 版本标签

`.github/workflows/auto-tag.yml` 在推送到 `master` 且触及 `Directory.Build.props` 时触发。它读取当前提交及 `HEAD~1` 的 `VersionPrefix`，版本变化且 `v<VersionPrefix>` 标签尚不存在时，创建并推送 annotated tag；无法读取前一版本时按首次版本处理。比较的是最后两个提交，不是本次推送范围内所有版本变更。

自动标签与 `release.bat` 的制品构建/上传分别执行。标签存在不证明安装源已更新，创建标签也不等于创建 GitHub Release；提交或推送普通文档不授权发布产品。

## 插件和项目包

| 场景 | 命令 |
| --- | --- |
| 仓库内普通插件 | `Scripts\package_plugin.bat Conoscope` |
| 仓库内项目包 | `Scripts\package_project.bat ProjectARVRPro` |
| 外部编译产物 | `py Scripts\package_cvxp.py --src-dir C:\path\to\MyPlugin\bin\x64\Release\net10.0-windows` |

插件和项目包默认上传，并在上传尝试结束后删除本地 `.cvxp`，包括上传或 `LATEST_RELEASE` 提交失败的路径；失败后应修复原因并重新执行 wrapper，不能把本地包是否残留当成成功依据。构建和上传前会先校验 `manifest.json` 的插件 ID、DLL 路径和文件大小；需要在 CI 或发布前单独检查时使用 `--validate-only`。校验通过后，打包再读取 `Scripts/shared_files.json`，剔除宿主已共享文件和 `.pdb`，生成 `.cvxp`。仓库内 `Plugins/`、`Projects/` 的默认打包路径会将该集合与当前 Release x64 宿主输出比较；发生双向漂移时自动从当前宿主输出刷新仓库与 Plugin Kit 两份清单，再继续打包。外部 `--src-dir` 和显式 `--shared-files` 仍保留离线兼容行为，不改写调用者提供的清单。

带有效清单的 `.cvxp` 是完整插件目录包，不是相对旧版本的差异包。客户端安装前会为现有 `Plugins/<manifest.id>/` 创建校验备份，然后精确替换整个目录；发布脚本不得省略仍由插件运行时需要、但本次源码未变化的私有文件。宿主共享文件仍按 `shared_files.json` 排除。`Scripts/shared_files.json` 与 `SDK/ColorVision.PluginKit/scripts/shared_files.json` 是同一份派生集合的两个消费镜像，不要手工编辑。生成器和仓库内正式打包器都会在集合变化时同步两份文件；集合未变化时不会仅因 `generated_at` 重写。共享集合在所有层级排除本机调试产生的 `*.runtimeconfig.dev.json`，保留正式 `*.runtimeconfig.json`，避免可选开发产物造成干净 CI 构建的清单漂移。CI 可继续用 `--check` 做只读漂移检查。主程序的全量/增量发布不以插件静态清单漂移作为门禁；它按本次运行目录、全量 ZIP 和安装器交集现场生成并上传版本化宿主清单。

存在清单时，`manifest.id` 是唯一的发布身份：它决定服务器目录、`.cvxp` 文件名前缀、包内根目录和最终的 `Plugins/<id>/` 安装目录；`dllpath` 只决定用于读取版本并启动插件的主 DLL。因此第三方插件不需要让项目名、程序集名和插件 ID 完全相同。

## Spectrum 双通道发布

Spectrum 同时提供无需安装 ColorVision 主程序的独立 ZIP，以及可随主程序更新的 `.cvxp` 插件包。正式发布使用 `Scripts\Spectrum.bat --release-notes "本次变更说明"`。

`build_spectrum.py` 从 `Spectrum.exe` 读取四段 PE 版本，并沿用 `package_cvxp.py` 的清单版本同步规则。独立清单使用 canonical UTF-8 JSON，包含版本、UTC 发布时间、发布说明和 ZIP 的文件名、大小、SHA-256；脚本通过 `Cert:\CurrentUser\My` 中 `CN=xincheng`、指纹 `0AFB92F7CF8B33F13C931B327B1BE5DC773F30FA` 的 RSA 私钥生成 PKCS#1 SHA-256 签名，私钥不会导出或上传。

远端写入顺序是先上传尚未对用户可见的 `.cvxp` 文件，再调用独立包原子发布接口，最后提交插件 `LATEST_RELEASE`。随后脚本会校验插件 latest、独立 latest 和 latest-version，重新下载插件包比对长度及 SHA-256，并以 Range 请求重新下载独立 ZIP 做相同校验。任一步骤失败都会以非零状态结束并保留本地 ZIP 和 `.cvxp`；只有全部远程验证通过才删除本地 `.cvxp`。

只生成本地包、不签名也不上传时，使用 `py Scripts\build_spectrum.py --release-notes "本地打包"`。正式 `--upload` 不允许配合 `--no-zip` 或 `--no-cvxp`，避免两个更新源出现人为缺口。

## 上传配置

远程上传优先使用环境变量，不要在文档或脚本调用里写真实账号密码：

```powershell
$env:COLORVISION_UPLOAD_URL = "http://<host>:<port>"
$env:COLORVISION_UPLOAD_FOLDER = "ColorVision"
$env:COLORVISION_UPLOAD_USERNAME = "<user>"
$env:COLORVISION_UPLOAD_PASSWORD = "<password>"
$env:COLORVISION_UPLOAD_USE_SYSTEM_PROXY = "1"
```

上传脚本会先做后端预检。新后端走 `/api/health` 和 `/api/ready`；旧后端没有这些接口时会按兼容模式继续上传。

## 内部步骤与其他工具

| 脚本 | 用途 |
| --- | --- |
| `clear-bin.ps1`、`clear-artifacts.ps1` | 删除本地构建产物；执行前确认目标目录和所需产物 |
| `build.py`、`build_update.py` | `release.bat` 内部构建、上传和增量更新步骤 |
| `verify_release.py` | wrapper 的签名、远端元数据、下载大小和 Git 状态验收 |
| `backend_client.py` | 发布脚本共用的认证、预检、重试、流式上传和路径编码 |

修改正式发布路径时先运行相关脚本测试。测试环境发布演练也需要明确的远端写入授权。验收范围按本页「正式发布」执行。

## 常见失败

| 现象 | 先查 |
| --- | --- |
| 主程序发布没有完整成功证据 | 后端预检、上传地址、账号密码、网络代理，以及 `verify_release.py` 验收输出 |
| 有历史基线时增量包失败 | 基线 ZIP 完整性、当前 `ColorVision.exe` 文件版本、上传返回码 |
| `.cvxp` 打包无法刷新共享清单 | 确认 Release x64 宿主输出存在且可写；仓库内正式打包会自动同步两份清单，显式 `--shared-files` 仍由调用者维护 |
| 插件/项目包找不到项目 | 名称是否等于 `Plugins/<Name>/<Name>.csproj` 或 `Projects/<Name>/<Name>.csproj` |
| 上传 401 或连接失败 | 环境变量、后端是否运行、URL 是否正确、代理是否需要启用 |
| Spectrum 发布保留了本地 `.cvxp` | 检查签名证书、独立发布接口响应、两个 latest 以及 Range 下载的大小/SHA-256 验证输出；修复后重新完整发布 |
| 构建失败 | 根据 wrapper 报告定位 MSBuild、Advanced Installer 或外部依赖。快速发布只处理失败阶段 |
