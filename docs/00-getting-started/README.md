---
knowledge_id: "delivery.start"
knowledge_type: "index"
status: "current"
summary: "克隆代码后的源码问答、本地构建、安装和运行分流；只问Codex不需要先启动程序。"
aliases: ["安装入口","源码问答前提","构建还是运行","克隆仓库","拉取代码","Codex源码问答"]
code_paths: ["ColorVision/ColorVision.csproj","Directory.Build.props"]
test_paths: []
related: ["delivery.prerequisites","operations.first-run","governance.knowledge"]
---

# 安装、构建与运行入口

按实际动作选择入口，不把理解代码、构建产物和运行应用串成必经流程。

| 要完成的动作 | 入口与边界 |
| --- | --- |
| 对检出的源码提问、定位实现 | 从根 `AGENTS.md` 和[知识地图](../knowledge/index.md)定位主题及源码；不要求安装产品、构建网站或连接设备 |
| 配置工具链、构建主程序 | [环境与构建前提](./prerequisites.md)；生成本地产物，不等于启动或发布 |
| 部署安装包 | [安装指南](./installation.md)；安装会修改本机环境，先确认目标机器和权限 |
| 启动应用、验证本地图片显示 | [主程序启动与最小图像验证](./first-steps.md)；启动可能写配置、连接服务和替换旧实例，先确认运行授权 |

产品范围见 [ColorVision 概览](./what-is-colorvision.md)。具体能力直接从知识地图进入对应源码主题；本页不另设一套完整阅读路线。
