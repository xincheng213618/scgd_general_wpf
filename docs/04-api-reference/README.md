# API 参考

本章节现在只保留已经收束成“当前实现导读”的稳定入口，不再继续把所有专题页平铺成一层目录。

## 推荐入口

### UI 与客户端层

- [UI 组件总览](./ui-components/README.md)

### Engine 与运行时层

- [Engine 组件总览](./engine-components/README.md)

### 模板与算法接入层

- [算法与模板概览](./algorithms/README.md)
- [算法系统概览](./algorithms/overview.md)

## 当前整理原则

- 顶层首页只挂经过收束的总览页，不再把所有单页都当成首屏入口。
- 细分专题页仍保留在各自目录中，但默认需要和源码对照阅读。
- 如果文档与实现不一致，以源码、XML 注释和实际运行行为为准。

## 当前章节边界

- `ui-components/` 主要覆盖 WPF UI 侧模块与桌面壳层。
- `engine-components/` 主要覆盖 Engine 目录下的运行时模块，而不是完整算法百科。
- `algorithms/` 主要覆盖 Templates 系统和算法接入链，而不是所有底层图像算子目录。
- `plugins/` 主要覆盖当前工作区里仍能对上源码的标准插件，以及少量历史残留插件的现状说明页。
- `extensions/` 当前主要保留 Flow 节点扩展这一类和实际代码能直接对上的扩展点专题。

## 暂未作为首页入口的目录

`plugins/` 和 `extensions/` 下当前已经保留少量源码导向专题页与现状说明页，但仍不作为本章首页的推荐入口。阅读这些页面时，同样应与源码对照理解。

## 建议阅读顺序

1. 先看 [UI 组件总览](./ui-components/README.md)，理解客户端壳层和 UI 基础设施。
2. 再看 [Engine 组件总览](./engine-components/README.md)，理解服务、模板和流程运行时。
3. 最后看 [算法与模板概览](./algorithms/README.md) 与 [算法系统概览](./algorithms/overview.md)，把模板和算法接入链串起来。
