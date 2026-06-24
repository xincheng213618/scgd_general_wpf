# 扩展点概览

本章只保留当前能直接和代码对上的扩展点专题，不再维护“所有扩展机制一览”的旧式总表。

## 当前覆盖范围

目前这一分支只收束了一类稳定专题：

- [FlowEngineLib 节点扩展](./flow-node.md)

这意味着当前 `extensions/` 不是完整扩展百科，而是一个很窄的“已整理扩展点入口”。

## 先把边界分清

- 插件发现、装载和部署不属于这里，应该去看 [现有插件能力](../plugins/README.md) 和 [插件开发概览](../../02-developer-guide/plugin-development/overview.md)。
- 算法模板和流程模板不属于这里，应该去看 [算法与模板概览](../algorithms/README.md)。
- 运行时模块之间的依赖关系，也不在这里展开，应该回到 [架构设计](../../03-architecture/README.md)。

## 怎么使用这一章

1. 先确认你要扩的是“Flow 节点”还是“插件/模板/服务”。
2. 如果是 Flow 节点，再进入 [FlowEngineLib 节点扩展](./flow-node.md)。
3. 如果问题更偏运行时执行链，再结合 [FlowEngineLib 架构](../../03-architecture/components/engine/flow-engine.md) 一起读。

## 为什么这里只有一页

- 当前仓库里真正被文档收束成稳定专题的扩展点并不多。
- 与其继续维护一个表面完整、实际很快过期的扩展目录，不如只保留和代码能直接核对的入口。
