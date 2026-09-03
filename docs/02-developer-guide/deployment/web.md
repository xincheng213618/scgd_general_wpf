---
knowledge_id: "delivery.web-deployment"
knowledge_type: "guide"
status: "current"
summary: "Web 本地启动与 Windows NAS 部署的前提、参数、构建和健康检查、Git bundle 交付、备份保留及失败恢复；SkipTests 仍运行前端测试，失败不保证回退代码或数据库。"
aliases: ["Web 部署", "NAS 部署", "本地启动 Web", "Run-Web", "Deploy-Nas", "SkipInstall", "SkipBuild", "SkipDocsBuild", "SkipTests", "RemoteGitBundle", "GitNetworkTimeoutSeconds", "DRY_RUN_ERROR", "DEPLOYMENT_ERROR", "already_current", "web-deploy-backups", "web-deploy-bundles", "ColorVisionWeb.log", "Invoke-WebDeployBackupRetention", "Invoke-WebGitBundleRetention", "New-WebRemotePowerShellTransport"]
code_paths: ["Web/Run-Web.ps1", "Web/Run-Web.bat", "Web/Deploy-Nas.ps1", "Web/Deploy-Nas.bat", "Web/RemotePowerShellTransport.psm1", "Web/NativeProcess.psm1", "Web/DeploymentRetention.psm1", "Web/GitBundleRetention.psm1", "Web/DeploymentHistory.psm1", "Web/Backend/services/runtime_logging.py", "Web/Frontend/package.json", "Web/Frontend/scripts/precompress-static.mjs", "Web/Frontend/scripts/check-dashboard-bundle.mjs"]
test_paths: ["Web/Test-NativeProcess.ps1", "Web/Test-RemotePowerShellTransport.ps1", "Web/Test-DeploymentRetention.ps1", "Web/Test-GitBundleRetention.ps1", "Web/Test-DeploymentHistory.ps1", "Web/Backend/test_runtime_logging.py", "Web/Backend/test_frontend_spa.py", "Web/Backend/test_docs_site.py"]
related: ["delivery.backend", "delivery.backend-records", "delivery.backend-observability", "delivery.deployment", "delivery.artifact-delivery", "platform.web-architecture", "delivery.web-performance-baseline"]
---

# Web 本地启动与 NAS 部署

`Web/Run-Web.ps1` 在当前 Windows 工作区构建前端并启动 Flask；`Web/Deploy-Nas.ps1` 通过 SSH 更新已有 Windows NAS 服务。后者依赖现成的仓库、配置、数据库、运行时和计划任务，不负责从空机器安装服务。桌面程序和插件的打包发布见[构建与发布脚本](../scripts/README.md)。

下面的启动、上传和部署命令用于已获授权的目标环境。启动会使用当前 Backend 的配置和数据库；正式部署会同步远端代码、安装依赖、替换前端、重启服务并清理符合保留规则的旧文件。不要运行这些命令来检查文档是否正确。

## 本地启动

准备 PATH 中可执行的 `node`、`python` 和可用的 `npm.cmd`，以及 `Web/Backend/requirements.txt` 所需依赖。先按[Backend 启动与配置](../backend/README.md#启动与配置)准备专用 `config.json`、制品目录、账号/数据库和监听设置。`-Storage` 仅覆盖制品目录；它不会隔离 `config.json` 或 `Web/Backend/marketplace.db`。默认后端 host 为 `0.0.0.0`，脚本打印本机 URL 不表示仅监听本机。

从仓库根运行：

```powershell
# 可能联网安装依赖、写构建输出，并启动后端和浏览器；Ctrl+C 停止前台后端
.\Web\Run-Web.ps1
```

| 参数 | 默认值 | 行为 |
| --- | --- | --- |
| `-Port` | `9998` | 传入后端 `--port`，浏览器访问 `http://127.0.0.1:<端口>/` |
| `-Storage` | 空 | 非空时传入 `--storage`；相对路径按 Backend 工作目录解释 |
| `-SkipInstall` | 关闭 | 跳过前端、文档与后端依赖安装，需自行准备依赖 |
| `-SkipBuild` | 关闭 | 只跳过 Web 前端构建，不跳过文档准备或后端启动 |
| `-SkipDocsBuild` | 关闭 | 文档产物不存在时也不构建；不影响 Web 前端构建 |

脚本按以下顺序执行，命令非零退出会中断后续步骤：

1. 前端没有 `node_modules` 且未指定 `-SkipInstall` 时运行 `npm install`。目录已存在就不安装，不会自动依据 lockfile 更新旧依赖。
2. 构建前端：优先直接调用本地 `tsc -b`、`vite build --manifest`，再生成压缩文件并检查管理首页包体预算；本地工具不齐时调用 `npm run build`。此入口不运行前端测试。
3. 准备文档站点：已有 `docs/.vitepress/dist/index.html` 就直接使用，不检查是否落后于源文档。没有产物且未跳过时，按需安装仓库根依赖；优先直接执行 VitePress 和搜索索引生成脚本，找不到本地 VitePress 才调用 `npm run docs:build`。因此“Documentation site ready”不等于完整文档校验刚通过。
4. 未指定 `-SkipInstall` 时，每次都运行 `python -m pip install -r requirements.txt`，随后以前台方式启动 `app.py`。
5. 后台浏览器任务最多尝试 45 次请求首页，每次超时 1 秒、失败间隔 1 秒；成功就打开 URL，全部失败后也会打开。浏览器打开本身不证明后端启动成功。

已有依赖与产物时可以使用下面的启动方式；修改源文件后，应先重建对应产物：

```powershell
.\Web\Run-Web.ps1 -SkipInstall -SkipBuild -SkipDocsBuild
```

若报 `npm cannot be started`，脚本已经依次尝试 Node 所在目录、Program Files 下的 Node.js 和 PATH 中的 `npm.cmd`。先检查这些实际可执行文件及 PATH，不能只凭 `node --version` 成功判断 npm 可用。参数、配置校验与 CLI 退出语义见 [Backend 命令行参数](../backend/README.md#命令行参数)。

## NAS 部署前提与参数

本机须能运行 OpenSSH `ssh`，目标须能执行 Windows PowerShell 和计划任务管理命令。部署账号需要访问仓库、制品目录、配置、数据库，以及停止/启动目标任务和确认监听进程的权限。

NAS 端须已有 `Web/Backend/config.json`、`marketplace.db`、对应分支和计划任务。脚本检查已暂存及未暂存的受跟踪改动，只允许 `Web/Backend/config.json` 有本地改动；不把未跟踪文件当作完整环境检查。它不切换分支、不覆盖其它受跟踪改动、不自动创建计划任务。

| 参数 | 脚本默认值 / 允许范围 |
| --- | --- |
| `-SshTarget` | `cv-publish`，SSH 配置别名 |
| `-RepoPath` | `C:\Users\Administrator\Desktop\scgd_general_wpf` |
| `-StoragePath` | `D:\ColorVision`，部署备份、历史及日志检查的根目录 |
| `-Branch` | `develop`；NAS 当前分支必须匹配 |
| `-TaskPath` / `-TaskName` | `\ColorVision\` / `ColorVisionWeb` |
| `-Port` | `9998` |
| `-GitNetworkTimeoutSeconds` | `45` 秒，5–600 |
| `-RemoteGitBundle` | 空；非空时使用已上传到 NAS 的 bundle 路径，替代 origin |
| `-Force` | 关闭；同一提交也重新构建并重启 |
| `-SkipTests` | 关闭；跳过部署器列出的 PowerShell 和 Backend 定向测试，**仍运行前端 `npm run test`** |
| `-DryRun` | 关闭；只检查部署前提及目标提交，不 fetch、备份、构建、重启或写部署历史 |
| `-KeepSuccessfulBackups` | `10`，2–1000 |
| `-KeepFailedBackups` | `3`，1–1000 |
| `-KeepGitBundles` | `3`，1–1000 |
| `-KeepHistoryRecords` | `500`，20–100000 |

这些是代码默认值，不是对目标机器当前配置的探测结果。`-StoragePath` 和 `-Port` 用于部署检查，不会替你改写 Backend 配置或计划任务动作。两者必须与实际服务匹配。

远端模板还固定使用以下运行时位置，没有对应命令行覆盖参数：

```text
C:\Users\Administrator\AppData\Local\Programs\Python\Python310\python.exe
C:\Program Files\nodejs\node.exe
C:\Program Files\nodejs\npm.cmd
C:\Program Files\Git\cmd\git.exe
```

其它机器仅调整 `-RepoPath` 并不足够；需要先核对模板的运行时路径和既有任务配置。进程验证要求监听端口的进程是上述 Python 路径、名称为 `python.exe`，且命令行匹配 `app.py --port <端口>`。

## 检查、部署与成功结果

先检查目标，再在同一套参数下正式执行；DryRun 成功不是对未来部署成功的承诺：

```powershell
# 通过 SSH 查询；不修改目标部署文件或历史
.\Web\Deploy-Nas.ps1 -DryRun

# 更新已有 NAS 服务；会联网、写文件、重启并执行保留清理
.\Web\Deploy-Nas.ps1
```

DryRun 输出 `status=dry_run`、当前/目标提交、`update_required`、source、监听 PID、任务状态和允许的受跟踪改动；不会探测 `/api/ready` 或确认监听进程归属。失败输出 `DRY_RUN_ERROR=...`，远端退出 1，不进入正式部署的恢复分支。

正式部署的顺序如下：

1. 检查前提后，从 origin fetch 一次并读取 `origin/<Branch>`；使用 bundle 时验证并 fetch 其 `HEAD`。origin 查询/fetch 关闭交互凭据提示，采用配置的超时，超时时尝试终止该 Git 进程及其子进程。
2. 当前提交等于目标且未指定 `-Force` 时，检查 health/ready。健康则返回 `already_current`，执行 bundle 保留并写历史，不重新构建、执行测试、验证新 PID 或运行时日志。若健康检查失败，继续完整构建和重启流程。
3. 创建时间戳备份，复制生产配置、已有前端 `dist` 和前端 lockfile，并用 SQLite backup API 复制数据库。它不是停止所有写入后的多文件统一时间点快照。
4. 提交不同时临时恢复受跟踪的 `config.json`，对已 fetch 的目标做 `merge --ff-only`，在 finally 中复制回备份的生产配置。确认最终 HEAD 等于目标；不再执行另一次 `git pull`。
5. 两个提交间 Backend `requirements.txt` 有变化才执行 pip 安装；前端每次执行 `npm ci --no-audit --no-fund`、测试、TypeScript 编译、Vite 构建到 `dist.deploy-<时间戳>`，再生成压缩变体和检查包体预算。NAS 部署不构建仓库文档站点。
6. 未指定 `-SkipTests` 时运行脚本中列出的 5 个 PowerShell 测试及 Backend 定向 unittest 集合；它不是整个仓库测试。随后确认旧监听进程归属，停止计划任务，必要时结束仍在监听的目标 Python 进程。
7. 将旧 `dist` 移到 `dist.rollback-<时间戳>`，切换暂存前端为正式 `dist`，启动计划任务并重新验证监听进程。代码同步和依赖安装在停服务前完成，整条链不是原子切换。
8. 检查 `/api/health` 的 status 为 ok、`/api/ready` 的 ready 为 true；确认活动日志包含新 PID 的 `process_start`。请求 compact 首页、版本及变更日志的普通和 gzip 响应并记录大小，检查指定管理页的构建文件和访问统计表可查询。
9. 移除临时 rollback 前端目录，写 success 标记，执行备份/bundle 保留和历史写入，最后输出结果 JSON。

正常完成输出 `status=success`，包含前后提交、测试状态、health/ready、新旧 PID、日志及响应大小等证据。`backend_targeted_tests=skipped` 表示显式跳过该组测试。compact 响应大小是记录值，脚本没有据此实施通用 JSON 大小上限；实际构建预算见 [Web 架构](../../03-architecture/components/web.md#性能预算与现有检查)。

`already_current` 和 `success` 的证据范围不同，二者都不证明反向代理、公网访问、所有管理页面交互或全部业务链已验收。计划任务启动监听的等待上限为 60 秒；health/ready 循环使用 90 秒截止时间，每个请求另有 10 秒超时；新 PID 日志检查使用 20 秒截止时间。这些不是整个 SSH 部署的总超时。

## origin 不可用时使用 Git bundle

`-RemoteGitBundle` 不负责在本机创建或上传文件。准备一个包含已选定提交 `HEAD` 的 Git bundle，在 NAS 仓库验证其依赖可满足，再传入远端路径。部署仍要求 fast-forward；bundle 不会绕过分支、受跟踪改动、测试或服务检查。

本地从确认过的仓库 HEAD 生成完整 bundle，以下步骤只写本机临时文件，不部署：

```powershell
$bundleCommit = (git rev-parse HEAD).Trim()
if ($LASTEXITCODE -ne 0) { throw 'Cannot resolve the selected HEAD.' }
$bundleName = "ColorVision-Web-$bundleCommit.bundle"
$bundlePath = Join-Path $env:TEMP $bundleName
if (Test-Path -LiteralPath $bundlePath) { throw "Bundle already exists: $bundlePath" }
git bundle create $bundlePath HEAD
if ($LASTEXITCODE -ne 0) { throw 'Bundle creation failed.' }
git bundle verify $bundlePath
if ($LASTEXITCODE -ne 0) { throw 'Bundle verification failed.' }
```

随后将该文件上传到已确认的 NAS 目录。上传前核对目标文件不存在或允许替换；上传和正式部署都属于远端写入。下面示例要求远端 `D:\ColorVision\web-deploy-bundles` 已存在：

```powershell
scp $bundlePath "cv-publish:D:/ColorVision/web-deploy-bundles/$bundleName"
if ($LASTEXITCODE -ne 0) { throw 'Bundle upload failed.' }
$nasBundlePath = "D:\ColorVision\web-deploy-bundles\$bundleName"
.\Web\Deploy-Nas.ps1 -RemoteGitBundle $nasBundlePath -DryRun
```

确认 DryRun 成功且目标提交正确，再执行正式部署：

```powershell
.\Web\Deploy-Nas.ps1 -RemoteGitBundle $nasBundlePath
```

部署器使用 `git bundle verify` 和暴露的 `HEAD` 选择目标，不依据文件名决定部署哪个提交；命名模式主要用于保留清理。SSH 脚本载荷以不超过 4096 字符的分行 Base64 和显式终止标记传输，发送完即关闭 stdin；无需等待 stdin EOF 才开始执行。部署器没有传入 SSH 进程的总超时，`GitNetworkTimeoutSeconds` 只约束特定 Git 网络命令，不约束依赖安装、构建或整条部署。

## 失败处理与恢复范围

正式部署异常输出 `DEPLOYMENT_ERROR=...`，远端退出 1，并尝试记录 failed 结果；本地包装把非零 SSH 退出作为错误抛出。先保留错误、提交、备份和 `recovery` 字段，再按失败阶段判断，不能仅凭有备份就认定已经还原。

| 失败时状态 | 自动处理及检查重点 |
| --- | --- |
| 尚未切换前端且存在暂存目录 | 尝试删除本次 `dist.deploy-*`；仍需确认代码同步、配置恢复和依赖安装进行到哪一步 |
| 没有监听进程，已标记前端切换成功，且 rollback 目录存在 | 尝试恢复旧前端，再启动计划任务；恢复分支没有再次完成 health/ready 验证 |
| 没有监听进程，但不满足旧前端恢复条件 | 尝试启动既有计划任务；这也可能发生在正式部署的前提检查失败后 |
| 有监听进程，但 health/ready、日志、响应或其它验收失败 | 恢复分支不会因此自动停止该进程或换回旧前端；需要检查仍在运行的实际版本和失败项目 |
| 保留清理或部署历史写入失败 | 通常记录 warning/error 后继续保留已健康的服务；历史页可能没有新记录。其它未被包装捕获的文件写入错误仍可进入部署失败分支 |

`distSwapped` 在旧目录移动和新目录就位都完成后才置为 true；若两个移动之间失败，不满足上述自动恢复旧前端的条件。检查 `dist`、`dist.deploy-*`、`dist.rollback-*` 的实际状态，不要直接删除仍可能需要恢复的目录。

自动恢复不回退 Git HEAD、Python/npm 依赖或 SQLite 数据库，也不保证服务已恢复健康。人工恢复时应先确认目标提交和备份完整性、实际监听进程以及数据库/制品的一致性，制定对应恢复范围，再执行已批准的停止、替换与启动操作。恢复后重新检查进程、health/ready、日志和受影响业务；仅有 `service_restart_attempted` 不能作为完成证据。

## 备份、历史、bundle 与日志

| 内容 | 相对 `StoragePath` 的位置 | 保留方式 |
| --- | --- | --- |
| 部署前备份 | `web-deploy-backups/<yyyyMMdd-HHmmss>/` | 成功部署后分类裁剪，默认成功 10 份、失败 3 份 |
| 部署历史 | `web-deploy-history.jsonl` | 每次写入默认保留最新 500 条有效 JSON 对象，包含不同状态 |
| 传输 bundle | `web-deploy-bundles/` | 成功部署或健康的 already_current 检查后保留最新 3 个合格 bundle，并保护本次使用的文件 |
| 运行时日志 | `Logs/Web/ColorVisionWeb.log` | 默认 10 MiB 轮转阈值，5 个备份文件 |

部署备份含 `config.json`、SQLite backup 生成的 `marketplace.db`、存在时的 `dist-old` 和 `package-lock.json.before`，以及过程/结果标记。配置和数据库可能含敏感信息，不能把这些备份当成可公开下载的制品。

备份清理按符合时间戳格式的目录名倒序排序，依据 `deployment-after.json` 或 `deployment-failed.json` 的存在分类，failed 优先；不验证标记正文或备份能否实际还原。本次备份、保留窗口内的失败记录、异常名称和未分类目录受保护。清理会检查候选的父目录及内部 reparse point，遇到错误停止本次处理；此前已删的候选不会恢复。10/3 是分类保留参数，不是整个目录的绝对文件数上限。

bundle 只扫描根目录的直接 `.bundle` 文件，要求名称匹配 `ColorVision-Web-<8至40位十六进制提交>[-head][-yyyyMMdd-HHmmss].bundle`、不是 reparse point、通过 Git 验证、恰好暴露一个 HEAD，且 HEAD 是已部署提交的祖先。按文件修改时间和名称倒序保留；本次文件始终保护，未识别、未验证、无 HEAD、非祖先等文件留待人工检查。删除前重新核验大小、修改时间和 Git 分类；部分删除失败通过 `git_bundle_retention` 报告，不撤销此前删除。

历史 writer 遇到空行、坏 JSON 或非对象会拒绝覆盖已有文件；原子替换不能代替跨进程写锁。历史写入失败不保证随后能把失败本身写入文件。查询、管理页面的字段投影和证据边界统一见[审计与部署记录](../backend/management-records.md)。

运行时日志接收安装捕获器后的 stdout/stderr，包含启动标记及通过这些流输出的错误；它不保证所有组成阶段或所有日志系统的消息都被捕获。轮转阈值按单次写入前判断，超大单次消息可能使文件超过 10 MiB；日志写入也可能失败。部署器要求活动日志最后 200 行中出现新 PID 的启动标记，不能以旧日志存在代替该检查。请求性能与访问统计见[后端观测](../backend/observability.md)。

## 实现与验证

部署顺序和恢复条件以 `Web/Deploy-Nas.ps1` 内嵌远端模板为准；它会读取本机的传输模块和 NativeProcess 源码，再在远端使用相应仓库模块。`Run-Web.ps1` 是另一条本地入口，不能把 NAS 的测试或健康检查推广到本地启动。

`Test-NativeProcess.ps1` 检查参数、超时与子进程处理；`Test-RemotePowerShellTransport.ps1` 检查分块、终止标记和 Windows PowerShell 5.1 的 Unicode 传输；保留/历史测试使用临时数据检查候选保护、裁剪、坏记录拒绝及失败 DryRun 不写历史。Backend 的 runtime_logging、frontend_spa 和 docs_site 测试分别覆盖日志与服务静态产物的部分契约。

这些测试不构成真实 NAS 完整部署、并发部署、任意阶段断电或数据库恢复验收。文档构建和检索检查只验证文档产物；历史性能数据见[Web 历史性能基线](../backend/performance-baseline.md)，不能作为本次部署结果。
