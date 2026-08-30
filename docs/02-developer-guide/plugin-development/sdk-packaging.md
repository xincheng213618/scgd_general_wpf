---
knowledge_id: "plugins.sdk-packaging"
knowledge_type: "topic"
status: "current"
summary: "独立PluginKit的project_name包身份、无参数与显式config发布差异、成功清理及单文件exe边界；不是根打包器的同契约镜像。"
aliases: ["ColorVision.PluginKit","cvplugin","cvplugin.exe","pluginkit.config.json","PluginKit SDK打包","auto_mode","keepPackageAfterUpload"]
code_paths: ["SDK/ColorVision.PluginKit/README.md","SDK/ColorVision.PluginKit/docs/ColorVision.Plugin.SDK.md","SDK/ColorVision.PluginKit/examples/YoloWpfDemo.Commands.md","SDK/ColorVision.PluginKit/scripts/package_cvxp.py","SDK/ColorVision.PluginKit/cvplugin.spec","SDK/ColorVision.PluginKit/build.bat"]
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
4. 再按排序后的 `.deps.json`、非 `.resources.dll` 的 DLL 文件名兜底。

同一个 `project_name` 随后用于查找输出根目录 `<project_name>.dll`、读取其 `FileVersion`、生成 `<project_name>-<version>.cvxp`、建立 ZIP 根目录 `<project_name>/`，以及上传到 `Plugins/<project_name>`。

SDK 不以 manifest `id` 独立决定这些值，也不按嵌套 `dllpath` 定位主 DLL；`dllpath` 仅在上述特定兜底步骤参与取名。通常有项目文件的调用会先选项目名。项目名、程序集名、manifest `id` 不同的场景，不能从根打包器“允许分离”的契约推断 SDK 可用。

`package_plugin` 复制编译输出，按共享清单剥离文件并写 `stripped_files.json`，再用项目目录 README、CHANGELOG、manifest 和 PackageIcon 补齐/覆盖。SDK 不执行根打包器的 manifest 校验、版本同步或宿主共享清单新鲜度检查。输出 metadata 被复制、打包成功均不证明身份、版本、依赖或宿主兼容性正确；维护者须核对实际产物。

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

## 验证缺口

本主题未声明 SDK 专属自动化测试；根 `Scripts/tests/test_package_cvxp.py` 不等于覆盖这份 SDK 脚本。当前可先只读核对 `main`、`infer_project_name`、`package_plugin` 和 spec；构建 exe、执行配置、打包、上传与清理验收须分别取得授权，不为验证 Markdown 自动运行。

实际 exe 依赖打入情况、不同项目/程序集命名、配置模式组合、两段上传部分失败与清理结果，仍需隔离环境专项验收。本页记录当前限制，没有修复产品或把打包成功当成插件成功安装/加载。
