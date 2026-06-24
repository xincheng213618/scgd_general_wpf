# 贡献指南

本仓库是 Windows WPF/.NET 代码库，包含运行时插件、客户项目包、Native OpenCV helper、发布脚本和 VitePress 文档站。提交变更时请保持范围清晰、可验证，并遵守现有模块边界。

## 开发流程

1. 从当前集成分支创建工作分支。
2. 每个变更只解决一个明确问题。
3. 不要把无关重构和行为修改混在一起。
4. 当公开行为、构建步骤、发布步骤、插件打包或项目交付行为变化时，同步更新对应 README 或 `docs/` 页面。
5. 提交 PR 前运行与变更相关的验证命令。

## 构建与测试

恢复依赖并构建：

```powershell
dotnet restore
dotnet build build.sln -p:Platform=x64
```

CI 风格 Windows 构建：

```powershell
msbuild build.sln /p:Configuration=Release /p:Platform=x64
```

UI 测试：

```powershell
dotnet test Test/ColorVision.UI.Tests/ColorVision.UI.Tests.csproj -c Release -p:Platform=x64
```

文档站构建：

```powershell
npm install
npm run docs:build
```

已有构建产物时，可单独复查文档链接、导航、旧页面兼容入口和搜索索引：

```powershell
npm run docs:validate
```

后端和脚本测试：

```powershell
cd Web/Backend
python test_app.py
python test_app_releases.py

$env:PYTHONPATH='Scripts'
python -m unittest Scripts.test.test_backend_client Scripts.test.test_build Scripts.test.test_build_update Scripts.test.test_file_manager
```

运行与你的变更相关的最小验证集。如果本地无法运行某个命令，请在 PR 中说明。

## 代码规则

- 默认面向当前 Windows WPF 和 x64 交付路径。
- 沿用现有 MVVM、插件、服务、模板和 PropertyGrid 模式。
- UI 代码放在 UI 模块，Engine/业务行为放在 Engine 或项目包。
- 优先复用已有抽象，不轻易新增跨层依赖。
- 代码保持简洁直接；只有非显而易见的行为才加注释。
- `ColorVision.snk` 存在时不要关闭强名称签名。
- 保留运行时依赖，例如 `DLL/CVCommCore.dll`、`DLL/MQTTMessageLib.dll` 和 OpenCvSharp runtime 资产。

## 文档规则

文档按标准 Git 工程入口组织：

- 根目录 `README.md` 是仓库第一入口。
- `CONTRIBUTING.md` 说明贡献和验证规则。
- `CHANGELOG.md` 记录版本历史。
- `LICENSE.md` 指向许可协议。
- `docs/` 承载用户、开发、交付、项目、插件和源码参考材料。

语言策略：

- 简体中文是完整且维护中的文档。
- 英文、繁体中文、日文、韩文副本不在当前工作树中维护；如真实交付需要恢复，从 Git 历史找回后按当前结构重新维护，不要默认新增全量翻译。

修改文档导航时，请同步检查：

- `docs/index.md`
- `docs/README.md`
- `docs/.vitepress/i18n/navigation-data.json`
- 受影响章节的 README

删掉或合并旧页面时，不要让旧地址直接 404。若旧地址可能来自导航、搜索、外部书签或历史链接，请保留一个带 `redirect_from_deleted_page: true` 和 `search: false` 的兼容页，并跳转到新的正式页面。导航和正文入口应指向正式页面，不要指向兼容页。

修改导航、语言配置或 VitePress 配置后，运行 `npm run docs:build`。如果只是复查已有构建产物，可运行 `npm run docs:validate`。

## 插件和项目包变更

插件变更：

- 保持 `manifest.json`、插件 README、CHANGELOG、构建复制规则和 `.cvxp` 打包行为一致。
- 打包行为变化时运行 `Scripts\package_plugin.bat <PluginName> --no-upload`。
- 视情况更新 [插件开发](docs/02-developer-guide/plugin-development/README.md) 或 [现有插件能力](docs/04-api-reference/plugins/README.md)。

客户项目变更：

- 保持项目 README、CHANGELOG、manifest/配置、流程组、协议字段和结果导出文档一致。
- 交付打包变化时运行 `Scripts\package_project.bat <ProjectName> --no-upload`。
- 更新 [项目说明](docs/00-projects/README.md) 和受影响项目页。

## 发布规则

常规发布入口：

```powershell
Scripts\release.bat
```

不要为主安装包新增本地-only 发布捷径。发布脚本负责构建、打包、上传、更新元数据、更新包和完整 zip 产物。

修改 `Directory.Build.props` 版本元数据时，注意 GitHub Actions 会在推送到 `master` 后按 `v<VersionPrefix>` 创建标签。

## PR 检查清单

- 变更聚焦在一个目的上。
- 运行了与变更相关的构建/测试命令，或说明无法运行。
- 文档已更新，或确认无需更新。
- 插件/项目包的 manifest、README、CHANGELOG 在运行时打包行为变化时已同步。
- 没有提交生成产物、本地密钥、机器私有配置或无关格式化变更。
