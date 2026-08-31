# 贡献指南

本仓库是 Windows WPF/.NET 代码库，包含运行时插件、客户项目包、Native OpenCV helper、发布脚本和 VitePress 文档站。提交变更时请保持范围清晰、可验证，并遵守现有模块边界。

## 开发流程

1. 从当前集成分支创建工作分支。
2. 每个变更只解决一个明确问题。
3. 不要把无关重构和行为修改混在一起。
4. 当公开行为、构建步骤、发布步骤、插件打包或项目交付行为变化时，用 `knowledge.mjs impact <源码路径>` 找到候选主题，并在同一变更中更新对应知识和验证入口；没有候选不等于无需更新。
5. 提交 PR 前运行与变更相关的验证命令。

## 构建与测试

恢复依赖并构建：

```powershell
dotnet restore .\ColorVision\ColorVision.csproj
dotnet build .\ColorVision\ColorVision.csproj -p:Platform=x64
```

CI 风格 Windows 构建：

```powershell
dotnet restore .\build.sln
msbuild .\build.sln /m /p:Configuration=Release /p:Platform=x64
```

.NET 测试：

```powershell
dotnet test Test/ColorVision.UI.Tests/ColorVision.UI.Tests.csproj -c Release -p:Platform=x64
dotnet test Test/ColorVision.Copilot.Tests/ColorVision.Copilot.Tests.csproj -c Release -p:Platform=x64
```

文档站构建（Node.js 22+；安装依赖会联网，本地构建不会发布）：

```powershell
npm ci
npm run docs:build
```

已有构建产物时，可单独复查文档链接、导航、旧页面兼容入口和搜索索引：

```powershell
npm run docs:validate:dist
```

后端和脚本测试：

```powershell
Push-Location .\Web\Backend
python -m unittest discover -p "test_*.py"
Pop-Location

python -m unittest discover -s .\Scripts\tests -p "test_*.py" -v
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

文档是供AI按需读取的项目知识，网站是派生视图。维护规则以 [docs/AGENTS.md](docs/AGENTS.md) 和[知识维护规范](docs/knowledge/maintenance.md)为准。

- 根目录 `README.md` 是仓库第一入口。
- `CONTRIBUTING.md` 说明贡献和验证规则。
- `CHANGELOG.md` 记录版本历史。
- `LICENSE.md` 指向许可协议。
- `docs/` 按能力和任务组织知识，每个活动主题声明稳定ID、摘要、状态、源码、测试与相关主题。
- `docs/knowledge/index.md`、`catalog.json` 和站点导航从元数据生成，不手工维护第二份事实。

简体中文是当前维护中的文档语言。

更新后运行 `npm run docs:knowledge`、`npm run docs:check`，涉及网页时运行 `npm run docs:build`。正文按完整语义组织，不按固定行数拆分。测试引用仅表示定位，不得伪造已验证结果；未来设计和默认关闭的实验能力必须明确标注。

删掉或合并旧页面时，不要让旧地址直接 404。若旧地址可能来自导航、搜索、外部书签或历史链接，请保留一个带 `redirect_from_deleted_page: true` 和 `search: false` 的兼容页，并跳转到新的正式页面。导航和正文入口应指向正式页面，不要指向兼容页。

修改导航或 VitePress 配置后，运行 `npm run docs:build`。如果只是复查已有构建产物，可运行 `npm run docs:validate:dist`。

## 插件和项目包变更

插件变更：

- 保持 `manifest.json`、插件 README、CHANGELOG、构建复制规则和 `.cvxp` 打包行为一致。
- 打包行为变化时先运行相关脚本测试和不上传的清单校验；`Scripts\package_plugin.bat <PluginName>` 会上传，只在任务明确授权发布/远端验收时执行。
- 按改动责任更新[插件装配与模块知识](docs/04-api-reference/plugins/README.md)所指向的规范主题；不再分别维护开发手册与使用手册。

客户项目变更：

- 保持项目 README、CHANGELOG、manifest/配置、流程组、协议字段和结果导出文档一致。
- 交付打包变化时先验证脚本与清单；`Scripts\package_project.bat <ProjectName>` 会上传，执行前必须有任务范围内的发布授权。
- 更新 [项目说明](docs/04-api-reference/projects/README.md) 和受影响项目页。

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
- 受影响知识已核对和更新，或说明无需变更的依据；派生目录已同步，知识检查通过。
- 插件/项目包的 manifest、README、CHANGELOG 在运行时打包行为变化时已同步。
- 没有提交编译输出、网站 `dist`、本地密钥、机器私有配置或无关格式化变更；知识地图、领域索引和catalog属于必须同步提交的派生资料。
