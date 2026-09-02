---
knowledge_id: "plugins.sdk-packaging"
knowledge_type: "topic"
status: "current"
summary: "独立 PluginKit 的项目命名、CLI/config 参数、构建与发布模式、包内容和失败排查；显式 config 的上传行为与无参数运行不同。"
aliases: ["ColorVision.PluginKit","cvplugin","cvplugin.exe","pluginkit.config.json","插件 SDK","外部插件 SDK","PluginKit SDK打包","auto_mode","keepPackageAfterUpload","buildCommand","buildWorkingDir","pluginName","srcDir","sharedFiles","Plugin DLL not found","Cannot read version from","No .csproj file found under"]
code_paths: ["SDK/ColorVision.Plugin.SDK.md","SDK/ColorVision.PluginKit/README.md","SDK/ColorVision.PluginKit/docs/ColorVision.Plugin.SDK.md","SDK/ColorVision.PluginKit/examples/YoloWpfDemo.Commands.md","SDK/ColorVision.PluginKit/scripts/package_cvxp.py","SDK/ColorVision.PluginKit/cvplugin.spec","SDK/ColorVision.PluginKit/build.bat"]
test_paths: []
related: ["plugins.getting-started","delivery.scripts"]
---

# PluginKit SDK 打包器

`SDK/ColorVision.PluginKit/` 向独立插件作者提供 `cvplugin.exe` 和 Python 源码入口。包内 `README.md` 与 `docs/ColorVision.Plugin.SDK.md` 保留独立分发所需的项目模板、依赖和使用说明；本主题负责易混淆的实际打包/发布契约。

SDK 的 `scripts/package_cvxp.py` 与仓库根 `Scripts/package_cvxp.py` 是不同实现。根打包器的 manifest 身份、版本同步、验证模式和正式 wrapper 规则见[插件产物、安装与交付](./getting-started.md)，不能因为脚本同名或共享清单相同就套用到 SDK。

## project_name 决定什么

`infer_project_name` 的当前推导顺序是：

1. `--plugin-name`，否则配置中的 `pluginName`。
2. 提供项目文件时使用 `.csproj` 文件名，不读取其 `TargetName` 作为替代。
3. 没有上述值时，尝试编译输出 `manifest.json` 中 `dllpath` 的文件名 stem。
4. 再取排序后第一份 `.deps.json` 的 `Path.stem`。当前实现只移除最后一个扩展名：`DemoPlugin.deps.json` 得到 `DemoPlugin.deps`，随后会查找 `DemoPlugin.deps.dll`，不能当作可靠的程序集命名推断。这是未修复的回退缺陷；提供正确的 `projectFile` 或 `pluginName` 可避免走到该分支。
5. 没有 `.deps.json` 时取排序后第一份非 `.resources.dll` 的 DLL 文件名；也没有 DLL 时返回输出目录名。多个 DLL 不会按插件接口自动筛选。

同一个 `project_name` 随后用于查找输出根目录 `<project_name>.dll`、读取其 `FileVersion`、生成 `<project_name>-<version>.cvxp`、建立 ZIP 根目录 `<project_name>/`，以及上传到 `Plugins/<project_name>`。

SDK 不以 manifest `id` 独立决定这些值，也不按嵌套 `dllpath` 定位主 DLL；`dllpath` 仅在上述特定兜底步骤参与取名。通常有项目文件的调用会先选项目名。项目名、程序集名、manifest `id` 不同的场景，不能从根打包器“允许分离”的契约推断 SDK 可用。

`package_plugin` 复制编译输出，排除 PDB，`runtimes/` 下仅保留 `win/` 和 `win-x64/` 分支，再按共享清单中的相对路径剥离文件并写 `stripped_files.json`。不是按 `ColorVision.*` 前缀一律删除。项目目录的 README、CHANGELOG、manifest 和 PackageIcon 随后补齐/覆盖包内同名文件；配置中的 `pluginRoot` 因此会影响最终 metadata。

SDK 不执行根打包器的 manifest 校验、版本同步或宿主共享清单新鲜度检查。输出 metadata 被复制、打包成功均不证明身份、版本、依赖或宿主兼容性正确；维护者须核对实际产物。

## 无参数与显式 config 不是同一模式

`main` 仅在没有任何命令行参数、且当前目录已有 `pluginkit.config.json` 时设置 `auto_mode=true`。配置字段内的相对路径以配置文件目录解析；`--config` 文件路径与默认配置发现则以进程当前工作目录为准，不以 exe 所在目录为通用保证。

| 调用方式 | 当前行为与副作用 |
| --- | --- |
| 无参数、没有默认配置 | 交互生成当前目录配置后退出；首次不继续构建/上传 |
| 无参数、已有默认配置 | 按 `buildEnabled` 决定构建，打包后按 `uploadEnabled` 决定上传 |
| 显式 `--config <file>` | 不因 `buildEnabled=true` 自动构建；除 `--build-only` 提前退出外，打包后会上传，即使 `uploadEnabled=false` |
| `--build` | 显式执行构建后继续打包/上传；不能当成仅构建参数 |
| `--build-only` | 执行构建后退出，不打包/上传；构建本身仍会执行项目或配置中的命令 |
| `--init-config` | 写配置后退出；不是对已有配置的只读校验 |

`should_build = args.build or args.build_only or (auto_mode and config_build_enabled)`；`should_upload = not auto_mode or config_upload_enabled`。SDK 没有 `--validate-only` 和 `--no-upload` 参数，不能把根脚本的静态验证示例移来运行。上述差异是当前实现限制，不是推荐的安全默认设计。

无参数已有配置的构建可能执行 `buildCommand`：`run_custom_build_command` 使用 shell 执行配置文本。配置还可保存上传地址和凭据，因此只执行可信配置；文档检索不授权执行任意命令或发布。仓库内使用 SDK 脚本时须明确 `SDK/ColorVision.PluginKit` 的路径，避免在 Windows 下把根 `Scripts/` 当成 SDK `scripts/`。

上传地址与凭据优先取非空显式参数，其次非空配置值，再读取环境变量；只有环境变量未定义时才取脚本默认值，显式空环境值不会继续回退。设置 `COLORVISION_UPLOAD_USERNAME` / `COLORVISION_UPLOAD_PASSWORD` 不会覆盖配置中的非空同名值；执行前核对实际配置与目标，不能把演示环境变量当作安全切换账户。`examples/YoloWpfDemo.Commands.md` 的无参数命令均依赖明确的插件工作目录，重新构建 exe 后停留在 SDK 目录不能直接假定仍会读取插件配置。

## 配置与命令行参数

配置是 UTF-8 JSON 对象。多数值按“非空 CLI 参数 → 非空配置值 → 默认值”选择；`buildCommand` 仅来自配置，优先于项目的默认 `dotnet build`。配置布尔字段必须使用 JSON 的 `true` / `false`，不要写字符串 `"false"`：脚本用 Python `bool(...)` 转换，非空字符串仍会启用该项。

| 配置键 | CLI 参数 | 缺省值或作用 |
| --- | --- | --- |
| `projectFile`（兼容 `projectPath`） | `--project-file` | `.csproj` 或可发现唯一项目的目录；没有时须提供 `srcDir` 才能打包，构建还须提供 `buildCommand` |
| `pluginName` | `--plugin-name` | 覆盖前述 `project_name` 推导；必须对应输出根目录中的同名 DLL |
| `srcDir` | `--src-dir` | 默认先找项目的 `bin/<platform>/<configuration>/<framework>`，不存在再找不含 platform 的路径；只选存在目录，不判断内容是否最新 |
| `pluginRoot` | `--plugin-root` | 默认项目文件所在目录；无项目时取输出路径中名为 `bin` 的目录的父目录，否则用输出目录 |
| `outputDir` | `--output-dir` | 配置向导写 `packages`；未提供值时直接调用的默认是脚本目录，并非总是当前工作目录 |
| `sharedFiles` | `--shared-files` | 依次检查指定文件、脚本旁 `shared_files.json`、当前目录同名文件；指定文件不存在时仍会继续回退 |
| `configuration` | `-c` / `--configuration` | `Release` |
| `framework` | `-f` / `--framework` | `net10.0-windows` |
| `platform` | `--platform` | `x64`；传给默认构建命令及输出目录推导 |
| `dotnet` | `--dotnet` | 环境变量 `DOTNET_EXE`，未定义则 `dotnet` |
| `buildCommand` | 无 | 按 shell 文本执行自定义构建；有值时优先于默认 .NET 构建 |
| `buildWorkingDir` | 无 | 自定义命令工作目录；默认项目目录，无项目时用进程当前目录 |
| `buildEnabled` | `--build` / `--build-only` | 缺省 `false`；向导/初始化配置默认 `true`。仅无参数模式自动读取，显式开关行为见上表 |
| `uploadEnabled` | 无关闭上传参数 | 缺省 `true`；只控制无参数模式，不能阻止显式参数调用上传 |
| `keepPackageAfterUpload` | `--keep-package` | 非空配置对象缺少该键时默认保留；无配置或空对象时默认不保留。向导选择上传及 `--init-config` 生成值默认 `false`；CLI 可强制保留 |
| `uploadUrl` | `--upload-url` | 市场基址；回退环境变量 `COLORVISION_UPLOAD_URL`，再取脚本默认值，运行前确认实际目标 |
| `username` / `password` | `--username` / `--password` | 回退对应 `COLORVISION_UPLOAD_USERNAME` / `COLORVISION_UPLOAD_PASSWORD`；不能把非空配置凭据误认为会被环境变量覆盖 |

`--config <file>` 选择配置，`--init-config <project>` 写初始化配置后退出，`--help` 显示参数后退出。`--init-config` 会覆盖指定目标配置，没有覆盖确认，也不会检查产物能否发布。读取 CLI 帮助需要源码依赖可导入，但不会加载已有配置并执行发布。

### 路径从哪里开始解析

- `--config` 和 `--init-config` 的项目参数相对于进程当前目录。
- 已选择配置文件时，项目、源目录、插件根、共享清单、输出目录和自定义构建工作目录的相对路径都基于**配置文件目录**，包括 CLI 显式覆盖的 `--src-dir` 等参数。没有配置文件时才基于当前目录。
- 默认项目发现先检查指定目录直属的 `.csproj`；只有直属没有候选时才扫描可发现的子目录，忽略隐藏、依赖和构建目录。多候选必须明确选择，不能保证任意目录中都会自动找到想要的工程。
- `dotnet` 与 `buildCommand` 是可执行命令文本，不按配置目录自动拼成文件路径；自定义命令的相对文件由其工作目录解释。

需要固定发布位置时显式配置 `outputDir`，并从日志核对 `Source directory`、`Plugin root`、`Shared files manifest` 和 `Packaged`。这些值帮助定位选错文件的问题，不代替后续上传或宿主加载结果。

## 上传与成功清理

交互配置默认启用构建和上传；选择上传时，“保留本地包”默认否。`--init-config` 生成的配置同样默认 `buildEnabled=true`、`uploadEnabled=true`、`keepPackageAfterUpload=false`。这些是有副作用的发布默认值，不是校验默认值。

SDK 先上传 `.cvxp`，再上传 `LATEST_RELEASE`。任一步失败会抛错，不进入后续成功清理；包上传成功但标记上传失败也不能报告完整发布成功，且已有远端写入不会自动回滚。

只有两次上传均成功，且既未传 `--keep-package`、配置也未要求保留时，才删除本地 `.cvxp`。输出目录变空且运行前不存在或原本为空时，还会删除该空目录；原先非空的目录不做此清理。这里没有根打包器的上传尝试 `finally` 清理，不能套用“失败也删除”的结论。显式 `--config` 仍会读取 `keepPackageAfterUpload`，不要把上传开关的模式差异误推广到全部配置键。

上传与本地清理不是一个事务；文件消失不能单独证明远端包、发布标记及市场索引都正确。未执行发布与远端验收时，不声明市场可用。

## 单文件 exe 与源码运行前提

`cvplugin.spec` 将 SDK 脚本、共享清单及 Python 运行依赖构建为控制台单文件 exe，并显式收集上传所需包；`shared_files.json` 作为资源嵌入，不要求正常分发额外放一份。提供上传能力的 exe 仍须在装有所需依赖的构建环境生成，产物可运行性要单独验收。

- exe 使用者需要 Windows；启用默认 .NET 构建时需要对应 .NET SDK，自定义命令则需要自己的工具链，不要求另装 Python。
- 源码运行需要 Python 3.10+、`pefile`，上传再需要 `requests`；安装依赖会联网并修改 Python 环境。
- `build.bat` 优先使用仓库 `.venv`，缺少 PyInstaller 时会尝试 `pip install pyinstaller`，然后调用 spec 写本地构建产物；它不是插件发布，也不是只读检查。

共享清单仍由根 `Scripts/generate_shared_files.py` 同步生成，维护规则见[构建与发布脚本](../scripts/README.md)；不要手工编辑 SDK 镜像。

## 常见问题

| 现象 | 检查顺序与处理 |
| --- | --- |
| `No .csproj file found under` / `Multiple .csproj files found under` | 核对配置文件目录和项目发现范围；直接给出要构建的 `.csproj`，避免依赖目录猜测 |
| `Plugin output directory not found` | 核对 `srcDir`、configuration/platform/framework 和实际输出；显式 `--config` 不自动构建，输出目录存在也可能只是旧产物 |
| `Plugin DLL not found` | 核对 `project_name` 与根目录 DLL 名；特别检查 `.deps.json` 回退产生的 `.deps` 后缀和自定义 AssemblyName |
| `Cannot read version from` | DLL 必须有可读取的 `StringFileInfo/FileVersion`；manifest `version` 不会作为回退版本。无效 PE 也可能更早抛异常 |
| 改了环境变量仍上传到旧账户/地址 | 检查显式参数及配置中非空的 `uploadUrl`、`username`、`password`，它们优先于环境变量 |
| 输出 `Packaged` 后失败 | 该行只证明本地 ZIP 已写入；继续看预检、包上传和 `LATEST_RELEASE` 结果，任一步失败都不能当完整发布成功 |
| 代理环境已设置但仍直连 | 上传 Session 设置 `trust_env=false` 并清空代理配置，不读取环境代理；核对直接到目标服务的网络通路 |

上传会依次探测 `/api/health` 和 `/api/ready`；任一返回 404 会直接转入兼容上传，不证明另一探针就绪。上传以 HTTP 201 为成功，401 立即停止；网络异常、5xx、408 和 429 最多尝试 3 次。其它失败响应不重试。重试针对同一路径的 PUT，不提供两个文件的远端事务。

## 验证范围

本主题未声明 SDK 专属自动化测试；根 `Scripts/tests/test_package_cvxp.py` 不等于覆盖这份 SDK 脚本。当前可先只读核对 `main`、`infer_project_name`、`package_plugin` 和 spec；构建 exe、执行配置、打包、上传与清理验收须分别取得授权，不为验证 Markdown 自动运行。

实际 exe 依赖打入情况、不同项目/程序集命名、配置模式组合、两段上传部分失败与清理结果，仍需隔离环境专项验收。文档构建与静态核对不证明插件已在目标宿主加载。
