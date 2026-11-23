# ColorVision

[![.NET](https://img.shields.io/badge/.NET-10.0-blue.svg)](https://dotnet.microsoft.com/)
[![Platform](https://img.shields.io/badge/platform-Windows-lightgrey.svg)](https://learn.microsoft.com/windows/)
[![UI](https://img.shields.io/badge/UI-WPF-blue.svg)](https://learn.microsoft.com/dotnet/desktop/wpf/)

ColorVision 是一个 Windows WPF 视觉检测平台，包含设备集成、可视化流程、图像分析、插件扩展和客户项目交付。

`docs/` 是 [AGENTS.md](AGENTS.md) 的按需知识扩展，与代码一起维护。AI 按任务读取相关契约、理由和验证入口；网页展示同一份 Markdown。

## 用 AI 理解和维护仓库

拉取仓库后，在此目录打开 Codex 即可提问。代码定位从类名、界面文字、日志或协议字段入手，沿实现查调用和测试。需要设计理由、兼容规则和现场口径时再读专题。入口不明确时可查知识地图。 [AGENTS.md](AGENTS.md) · [知识地图](docs/knowledge/index.md) · [知识使用约定](docs/README.md)

## 仓库结构

[生成的源码地图](docs/knowledge/index.md)覆盖主程序、UI、Engine、Native、插件、客户项目、Web、脚本和测试。它随主题的 `code_paths` 更新，不在 README 再维护一份模块目录。

跨模块职责与调用边界见[系统职责](docs/03-architecture/overview/system-overview.md)。目录归属、程序集引用、运行调用顺序不是同一件事。

## 环境要求

桌面宿主面向 Windows x64，工具链和运行依赖以当前项目文件及[环境与首次构建](docs/00-getting-started/prerequisites.md)为准。只读源码问答不需要安装设备驱动或启动应用；独立 FileIO 包的 AnyCPU 例外见[构建平台与制品边界](docs/02-developer-guide/README.md)，不能推广为宿主跨架构支持。

## 构建

按[环境与首次构建](docs/00-getting-started/prerequisites.md)选择已有 native DLL 或首次 C++ 构建路径。构建会生成本地产物，并可能还原依赖；启动产品是另一项动作，副作用与验证见[启动与最小运行验证](docs/00-getting-started/first-steps.md)。

## 测试

从改动的实现和调用方定位相关测试。验证范围按改动和运行环境选择。 [测试与验证](docs/02-developer-guide/testing.md)

## 文档

- [本地知识地图](docs/knowledge/index.md)：生成的源码与能力检索入口。
- [知识使用约定](docs/README.md)：按需阅读、源码核对及标准资料的职责。
- [知识维护规范](docs/knowledge/maintenance.md)：修改知识时查阅的字段、生成与验证规则。
- [在线知识库](https://xincheng213618.github.io/scgd_general_wpf/)：同一份资料的网页展示。

主题正文以简体中文为主；英文 `AGENTS.md` 与有用的原生英文模块说明保留，不维护中英重复镜像。网页构建不是本地知识查询的前提；知识和网站的本地生成不等于发布。

## 打包与发布

主程序、插件、客户项目包的入口和副作用由[构建与发布脚本](docs/02-developer-guide/scripts/README.md)统一维护。发布 wrapper 可能签名、上传或删除临时包，不要作为普通构建或文档验证命令运行；只有任务明确要求发布时才进入该流程。

制品种类、外部安装工程及历史安装器边界见[桌面交付责任](docs/02-developer-guide/deployment/overview.md)。

## 参与开发

提交 PR 前阅读 [CONTRIBUTING.md](CONTRIBUTING.md)。变更保持聚焦，同步受影响的知识与验证入口；提交、推送和发布分别以任务授权为准。

## 许可

本仓库采用 **ColorVision 软件许可协议**，允许协议范围内的非商业源码学习、研究、本地编译、修改和测试；商业生产、商业研发或集成、客户交付和商业分发须事先取得独立书面授权。

许可入口见 [LICENSE.md](LICENSE.md)，完整条款见 [软件许可协议](docs/05-resources/legal/software-agreement.md)。第三方材料及此前已合法取得的独立许可保留原有权利和适用条件。
