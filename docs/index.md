---
knowledge_id: "governance.home"
knowledge_type: "index"
status: "current"
summary: "ColorVision AI优先知识入口：按问题定位能力、代码、测试与维护约束。"
aliases: ["ColorVision", "从哪里开始", "知识入口"]
code_paths: ["AGENTS.md"]
test_paths: []
related: ["governance.knowledge", "platform.system"]
layout: home

hero:
  name: "ColorVision"
  text: "项目知识库"
  tagline: 先定位问题，再读取必要知识与代码。AI 优先使用，同一份资料生成网页。
  image:
    src: /images/ColorVision.png
    alt: ColorVision
  actions:
    - theme: brand
      text: 按源码定位知识
      link: /knowledge/
    - theme: alt
      text: 系统职责与边界
      link: /03-architecture/overview/system-overview
    - theme: alt
      text: 知识维护约定
      link: /knowledge/maintenance

features:
  - title: 按需上下文
    details: 从问题、界面名称或代码符号进入主题，不要求扫描全仓或通读章节。
    link: /knowledge/
  - title: 可核对的事实
    details: 主题关联实现与测试，明确当前能力、未来设计和验证缺口。
    link: /README
  - title: 代码与知识同步
    details: 按变更路径查找受影响说明，更新同一正文，派生检索和网页。
    link: /knowledge/maintenance
---

## 拉取仓库后直接提问

在仓库目录打开 Codex，遵守已有 `AGENTS.md`，例如询问：

- “我要增加一个属性编辑器，先找扩展契约和测试，不修改代码。”
- “历史结果的原图被清理了，还能怎样显示？请核对当前实现。”
- “这份代码首次构建需要什么？先做环境检查，不运行发布脚本。”

本地[知识地图](./knowledge/index.md)随源码提供；不需要先启动网站，也不依赖维护者的个人记忆。问答不会自动授权构建、设备操作或发布。

## 运行行为也是代码知识

环境前提、可见行为、失败条件和验证方法与实现契约一起维护，不按读者身份另写手册：[构建前提](./00-getting-started/prerequisites.md)、[启动验证](./00-getting-started/first-steps.md)、[跨模块故障定位](./01-user-guide/README.md)、[现场证据](./01-user-guide/field-operation-acceptance.md)。网页只是这些知识的展示视图。
