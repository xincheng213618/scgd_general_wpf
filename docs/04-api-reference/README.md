# API 参考

本章节改成“从总览进入”的结构，优先保留仍在维护的模块总览和专题页，避免在侧边栏同时展开过多同级页面。

## 推荐入口

### UI 与客户端层

- [UI 组件总览](./ui-components/README.md)

### Engine 与核心处理层

- [Engine 组件总览](./engine-components/README.md)

### 算法与模板

- [算法总览](./algorithms/README.md)
- [算法概览](./algorithms/overview.md)
- [流程引擎](./algorithms/templates/flow-engine.md)
- [模板管理](./algorithms/templates/template-management.md)
- [JSON 模板](./algorithms/templates/json-templates.md)
- [POI 模板详解](./algorithms/templates/poi-template.md)
- [ARVR 模板详解](./algorithms/templates/arvr-template.md)
- [Templates API 参考](./algorithms/templates/api-reference.md)

### 标准插件

- [Pattern 插件](./plugins/standard-plugins/pattern.md)
- [Spectrum 插件](./plugins/standard-plugins/spectrum.md)
- [SystemMonitor 插件](./plugins/standard-plugins/system-monitor.md)
- [EventVwr 插件](./plugins/standard-plugins/eventvwr.md)
- [图像投影插件](./plugins/standard-plugins/image-projector.md)
- [录屏插件](./plugins/standard-plugins/screen-recorder.md)
- [Windows 服务插件](./plugins/standard-plugins/windows-service.md)

### 扩展点

- [FlowNode 开发](./extensions/flow-node.md)

## 使用建议

1. 先从 `README` 或 `overview` 页面进入，再跳到具体模块页。
2. 若文档与实现不一致，以源码、XML 注释和实际行为为准。
3. 复杂模块建议同时参考 [架构设计](../03-architecture/README.md)。

## 当前整理原则

- 章节首页只保留稳定入口，不再把所有组件页平铺到侧边栏。
- 深层页面仍然保留，但更多通过总览页和正文内链进入。
- 不再把覆盖度不足的小专题继续包装成完整手册。
