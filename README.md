# ColorVision

[![.NET](https://img.shields.io/badge/.NET-10.0-blue.svg)](https://dotnet.microsoft.com/)
[![Platform](https://img.shields.io/badge/platform-Windows-lightgrey.svg)](https://learn.microsoft.com/windows/)
[![UI](https://img.shields.io/badge/UI-WPF-blue.svg)](https://learn.microsoft.com/dotnet/desktop/wpf/)

ColorVision 是一个 Windows WPF 视觉检测平台，包含设备集成、可视化流程、图像分析、插件扩展和客户项目交付。

项目知识与代码一起维护，优先供 AI 按需检索、核对实现；网页展示同一份 Markdown，不另维护面向不同人群的手册。

## 用 AI 理解和维护仓库

拉取仓库后，在此目录打开 Codex 并直接提出问题。[AGENTS.md](AGENTS.md) 提供工作规则，[知识地图](docs/knowledge/index.md)按实际源码职责定位主题、实现和测试。不需要先构建项目或网站，也不依赖维护者的个人记忆。

例如：“新增属性编辑器从哪里扩展？先核对契约和测试，不修改代码。”或“首次构建缺什么环境？先检查，不执行发布。”

安装了 Node.js 时可只读查询，无需安装 npm 依赖：

```powershell
node docs/.vitepress/scripts/knowledge.mjs search "属性编辑器"
node docs/.vitepress/scripts/knowledge.mjs impact "UI/ColorVision.UI/PropertyEditor"
```

没有 Node.js 时直接读取 Markdown 或用 `rg` 搜索即可。工具只提供定位候选，回答前仍需阅读主题和实际代码；详细边界见[知识使用约定](docs/README.md)。

## 仓库结构

[生成的源码地图](docs/knowledge/index.md)覆盖主程序、UI、Engine、Native、插件、客户项目、Web、脚本和测试。它随主题的 `code_paths` 更新，不在 README 再维护一份模块目录。

跨模块职责与调用边界见[系统职责](docs/03-architecture/overview/system-overview.md)。目录归属、程序集引用、运行调用顺序不是同一件事。

## 环境要求

桌面宿主面向 Windows x64，工具链和运行依赖以当前项目文件及[环境与首次构建](docs/00-getting-started/prerequisites.md)为准。只读源码问答不需要安装设备驱动或启动应用；独立 FileIO 包的 AnyCPU 例外见[构建平台与制品边界](docs/02-developer-guide/README.md)，不能推广为宿主跨架构支持。

## 构建

按[环境与首次构建](docs/00-getting-started/prerequisites.md)选择已有 native DLL 或首次 C++ 构建路径。构建会生成本地产物，并可能还原依赖；启动产品是另一项动作，副作用与验证见[启动与最小运行验证](docs/00-getting-started/first-steps.md)。

## 测试

从改动主题的 `test_paths` 和[测试与验证](docs/02-developer-guide/testing.md)选择相关 managed、native、脚本或后端检查。一次构建或局部测试通过，不代表设备、数据库或正式交付已验收。

## 文档

- [本地知识地图](docs/knowledge/index.md)：生成的源码与能力检索入口。
- [知识使用约定](docs/README.md)：按需阅读、源码核对及标准资料的职责。
- [共同维护规范](docs/knowledge/maintenance.md)：正文、元数据、生成与验证命令。
- [在线知识库](https://xincheng213618.github.io/scgd_general_wpf/)：同一份资料的网页展示。

主题正文以简体中文为主；英文 `AGENTS.md` 与有用的原生英文模块说明保留，不维护中英重复镜像。网页构建不是本地知识查询的前提；知识和网站的本地生成不等于发布。

## 打包与发布

主程序、插件、客户项目包的入口和副作用由[构建与发布脚本](docs/02-developer-guide/scripts/README.md)统一维护。发布 wrapper 可能签名、上传或删除临时包，不要作为普通构建或文档验证命令运行；只有任务明确要求发布时才进入该流程。

制品种类、外部安装工程及历史安装器边界见[桌面交付责任](docs/02-developer-guide/deployment/overview.md)。

## 参与开发

提交 PR 前阅读 [CONTRIBUTING.md](CONTRIBUTING.md)。变更保持聚焦，同步受影响的知识与验证入口；提交、推送和发布分别以任务授权为准。

## 许可

见 [LICENSE.md](LICENSE.md) 和维护中的 [软件许可协议](docs/05-resources/legal/software-agreement.md)。
