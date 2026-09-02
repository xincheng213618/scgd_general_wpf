---
knowledge_id: "delivery.scripts"
knowledge_type: "guide"
status: "current"
summary: "主程序、插件和项目包的正式发布入口、只读校验与上传清理副作用。"
aliases: ["发布","打包","release.bat","package_project.bat","只做本地构建"]
code_paths: ["Scripts/release.bat","Scripts/build.py","Scripts/build_update.py","Scripts/generate_shared_files.py","Scripts/package_project.bat","Scripts/package_plugin.bat","Scripts/package_cvxp.py","Scripts/build_spectrum.py"]
test_paths: ["Scripts/tests"]
related: ["delivery.index","delivery.testing","delivery.backend"]
---

# 构建与发布脚本

本页只回答四件事：正式发布走哪个入口，插件和项目包怎么打包，上传凭据怎么提供，失败时先查哪里。脚本参数以源码和 `--help` 为准；不要把这里写成每个 Python 函数的说明书。

## 先记住

| 场景 | 使用入口 | 说明 |
| --- | --- | --- |
| 主程序正式发布 | `Scripts\release.bat` | 唯一正常入口，会通过后端 HTTP 接口上传主包和 `CHANGELOG.md`，最后更新 `LATEST_RELEASE`，随后上传增量包并生成全量 zip |
| 发布插件包 | `Scripts\package_plugin.bat <PluginName>` | 面向 `Plugins/<PluginName>/`，上传尝试结束后删除本地 `.cvxp` |
| 发布项目包 | `Scripts\package_project.bat <ProjectName>` | 面向 `Projects/<ProjectName>/`，上传尝试结束后删除本地 `.cvxp` |
| 发布 Spectrum 独立包和插件包 | `Scripts\Spectrum.bat --release-notes "<说明>"` | 同时维护独立更新源和 ColorVision 插件更新源，完整远程验收后才删除本地 `.cvxp` |
| 发布外部编译产物 | `py Scripts\package_cvxp.py --src-dir <输出目录>` | 适合只拿到插件输出目录的场景 |
| 只校验插件清单 | `py Scripts\package_cvxp.py --project-file <插件.csproj> --validate-only` | 不构建、不打包、不上传 |
| 刷新两份共享文件表 | `py Scripts\generate_shared_files.py` | 从当前 Release x64 宿主输出一次扫描，同时更新仓库与 Plugin Kit 镜像 |
| 校验共享文件表 | `py Scripts\generate_shared_files.py --check` | 只比较 `shared_files` 集合，忽略时间戳、顺序和重复项 |

`build.py`、`build_update.py` 和 `verify_release.py` 是 `release.bat` 的内部步骤。正式发布不要绕过 `release.bat` 单独跑它们；`build_update.py` 没有安全的 `--help` 查询模式，直接执行会进入增量包生成和上传流程。

## 正式发布

以下命令会签名、打包并修改远端发布状态，只在用户明确要求发布时执行；文档或代码审阅不授予发布权限。

主程序和 ServiceHost 共用仓库根目录 `Directory.Build.props` 中的 `VersionPrefix`。每个增量包都必须携带完整的 `ServiceHost/` 运行时，确保 ZIP 部署机器可从空的 ProgramData 目录完成首次安装。发布前提升这个版本号并更新根 `CHANGELOG.md`，然后运行：

```powershell
Scripts\release.bat
```

主程序发布不携带输出根目录的 `CHANGELOG.md`：`build_update.py` 在全量 ZIP 和增量 CVX 中排除该路径，`generate_shared_files.py` 也忽略该文件，避免旧输出副本重新进入共享清单。外部 `ColorVision.aip` 不应包含对应文件行及 `AI_ViewReadme` 对该本地日志的引用。仓库根目录原稿继续由 `build.py` 独立上传，插件自己的日志照常随插件包交付；这些规则不清理历史包或已有安装目录。

发布成功时，控制台应依次看到主包上传、`CHANGELOG.md` 上传、`LATEST_RELEASE` 更新和增量包上传成功。最后 `verify_release.py` 会并行验证安装包 Authenticode 签名、远端 latest/changelog、安装包与更新包 Range 下载大小，并报告 Git 状态；只有这一阶段也返回零，wrapper 才算成功。后端 HTTP 接口是唯一发布通道，不再同步企业微信 WeDrive 或百度云；任一元数据上传失败都会阻止版本号更新。本地安装包、全量 zip、增量包是正常构建产物，不代表“本地-only 发布”。其中桌面 `History` 目录用于生成增量差分，不是额外分发渠道。客户端会合并启动检查和手动检查中同时进行的 `LATEST_RELEASE` 读取；插件详情在单次 2 秒空响应后新建连接重试，候选插件元数据仍不完整时整轮更新延期。这些客户端容错不改变发布脚本的上传顺序或成功判定。发布失败时先修复失败原因，再重新走 `release.bat`。

## 插件和项目包

| 场景 | 命令 |
| --- | --- |
| 仓库内普通插件 | `Scripts\package_plugin.bat Conoscope` |
| 仓库内项目包 | `Scripts\package_project.bat ProjectLUX` |
| 外部编译产物 | `py Scripts\package_cvxp.py --src-dir C:\path\to\MyPlugin\bin\x64\Release\net10.0-windows` |

插件和项目包默认上传，并在上传尝试结束后删除本地 `.cvxp`，包括上传或 `LATEST_RELEASE` 提交失败的路径；失败后应修复原因并重新执行 wrapper，不能把本地包是否残留当成成功依据。构建和上传前会先校验 `manifest.json` 的插件 ID、DLL 路径和文件大小；需要在 CI 或发布前单独检查时使用 `--validate-only`。校验通过后，打包再读取 `Scripts/shared_files.json`，剔除宿主已共享文件和 `.pdb`，生成 `.cvxp`。仓库内 `Plugins/`、`Projects/` 的默认打包路径会先将该集合与当前 Release x64 宿主输出比较；发生双向漂移就拒绝打包。外部 `--src-dir` 和显式 `--shared-files` 仍保留离线兼容行为。

带有效清单的 `.cvxp` 是完整插件目录包，不是相对旧版本的差异包。客户端安装前会为现有 `Plugins/<manifest.id>/` 创建校验备份，然后精确替换整个目录；发布脚本不得省略仍由插件运行时需要、但本次源码未变化的私有文件。宿主共享文件仍按 `shared_files.json` 排除。`Scripts/shared_files.json` 与 `SDK/ColorVision.PluginKit/scripts/shared_files.json` 是同一份派生集合的两个消费镜像，不要手工编辑。生成器一次扫描后同步两份文件；集合未变化时不会仅因 `generated_at` 重写。CI 和主程序发布都会基于刚构建的宿主输出执行 `--check`，因此宿主文件集合变化必须先刷新并提交两份镜像。

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

## 脚本速查

| 脚本 | 是否日常入口 | 用途 |
| --- | --- | --- |
| `release.bat` | 是 | 主程序正式发布入口 |
| `package_cvxp.py` | 是 | `.cvxp` 打包、上传和本地包清理 |
| `package_plugin.bat` | 是 | 仓库内普通插件构建、上传快捷入口 |
| `package_project.bat` | 是 | 仓库内项目包构建、上传快捷入口 |
| `clear-bin.ps1`、`clear-artifacts.ps1` | 是 | 清理本地构建产物 |
| `build.py`、`build_update.py` | 否 | `release.bat` 内部构建、上传和增量更新步骤 |
| `verify_release.py` | 否 | `release.bat` 最后的并行签名、远端元数据、下载大小和 Git 状态验收 |
| `backend_client.py` | 否 | 统一处理上传认证、预检、重试、流式上传和路径编码 |
| `generate_shared_files.py` | 宿主输出集合变化时 | 同步生成两份共享文件清单，或用 `--check` 做集合门禁 |
| `build_spectrum.py` | 特殊 | Spectrum 独立 ZIP + 插件 `.cvxp` 双通道构建、签名、发布和远程验收 |

如果某个脚本不在 `Scripts/` 目录里，就不要在文档里继续引用它。改正式发布路径时先运行相关脚本测试；测试环境发布演练也需要明确的远端写入授权。快速发布/完整发布的范围以根 `AGENTS.md` 为准，不自行添加额外发布轮次。

## 常见失败

| 现象 | 先查 |
| --- | --- |
| 主程序发布没有完整成功证据 | 后端预检、上传地址、账号密码、网络代理，以及 `verify_release.py` 六项验收输出 |
| 增量包失败 | 历史版本 zip 是否存在、当前 `ColorVision.exe` 文件版本、上传返回码 |
| `.cvxp` 打包报告共享清单漂移 | 先做干净的 Release x64 宿主构建，再运行 `py Scripts\generate_shared_files.py`，审查并提交两份清单 |
| 插件/项目包找不到项目 | 名称是否等于 `Plugins/<Name>/<Name>.csproj` 或 `Projects/<Name>/<Name>.csproj` |
| 上传 401 或连接失败 | 环境变量、后端是否运行、URL 是否正确、代理是否需要启用 |
| Spectrum 发布保留了本地 `.cvxp` | 检查签名证书、独立发布接口响应、两个 latest 以及 Range 下载的大小/SHA-256 验证输出；修复后重新完整发布 |
| 构建失败 | 先单独跑对应 `dotnet build`，再看 MSBuild、Advanced Installer 或外部 DLL |
