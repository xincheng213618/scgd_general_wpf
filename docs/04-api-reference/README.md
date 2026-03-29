# API 参考文档

ColorVision API 参考文档，包含所有公开的 API、组件、服务和扩展点。

## 📚 API 分类

### UI 组件 API
用户界面相关组件的 API 文档：
- [ColorVision.UI](./ui-components/ColorVision.UI.md) - UI 核心组件
- [ColorVision.Common](./ui-components/ColorVision.Common.md) - 通用组件
- [ColorVision.Core](./ui-components/ColorVision.Core.md) - 核心库
- [ColorVision.Themes](./ui-components/ColorVision.Themes.md) - 主题系统
- [ColorVision.ImageEditor](./ui-components/ColorVision.ImageEditor.md) - 图像编辑器
- [ColorVision.Solution](./ui-components/ColorVision.Solution.md) - 解决方案管理
- [ColorVision.Scheduler](./ui-components/ColorVision.Scheduler.md) - 调度器
- [ColorVision.Database](./ui-components/ColorVision.Database.md) - 数据库访问
- [ColorVision.SocketProtocol](./ui-components/ColorVision.SocketProtocol.md) - Socket 通信协议
- [UI 组件概览](./ui-components/README.md) - UI 组件总览

### Engine 组件 API
引擎和核心处理组件的 API：
- [ColorVision.Engine](./engine-components/ColorVision.Engine.md) - 核心引擎
- [ColorVision.FileIO](./engine-components/ColorVision.FileIO.md) - 文件输入输出
- [cvColorVision](./engine-components/cvColorVision.md) - 视觉处理库
- [FlowEngineLib](./engine-components/FlowEngineLib.md) - 流程引擎库
- [ST.Library.UI](./engine-components/ST.Library.UI.md) - UI 库
- [Engine 组件概览](./engine-components/README.md) - Engine 组件总览

### 算法 API
图像处理和分析算法：
- [算法概览](./algorithms/overview.md) - 算法系统介绍
- [Ghost 检测](./algorithms/detectors/ghost-detection.md) - Ghost 检测算法
- [ROI (感兴趣区域)](./algorithms/primitives/roi.md) - ROI 原语
- [POI (关注点)](./algorithms/primitives/poi.md) - POI 原语

### 插件 API
插件系统和标准插件：
- [Pattern 插件](./plugins/standard-plugins/pattern.md) - 图案分析插件
- [Spectrum 插件](./plugins/standard-plugins/spectrum.md) - 光谱仪测试插件
- [系统监控](./plugins/standard-plugins/system-monitor.md) - 系统监控插件

### 扩展点 API
系统扩展点和接口：
- [FlowEngineLib 节点开发](./extensions/flow-node.md) - 流程引擎节点扩展

## 🔍 使用指南

### 如何查找 API

1. **按模块查找** - 根据功能模块（UI、Engine、算法等）定位
2. **按类名查找** - 使用浏览器搜索功能查找特定类名
3. **按功能查找** - 根据需要实现的功能浏览相关章节

### API 文档结构

每个 API 文档通常包含：
- **概述** - 模块或类的基本介绍
- **主要类型** - 核心类、接口、枚举
- **使用示例** - 代码示例
- **注意事项** - 使用时需要注意的地方
- **相关链接** - 相关文档和资源

## 📖 待完善内容

以下 API 文档正在完善中：

- **服务 API** - 设备服务、相机服务、校准服务等
- **更多模板 API** - POI 模板、ARVR 模板等
- **插件基类 API** - 插件接口、插件基类
- **更多扩展点 API** - 属性编辑器扩展、结果处理器等

## 🔗 相关链接

- [开发指南](../02-developer-guide/README.md) - 开发教程和最佳实践
- [架构设计](../03-architecture/README.md) - 系统架构说明
- [项目结构](../05-resources/project-structure/README.md) - 代码组织结构

## 💡 API 使用提示

- 所有公开 API 都有 XML 文档注释
- 使用 Visual Studio 的 IntelliSense 查看 API 说明
- 查看单元测试了解 API 的使用方法
- 遵循 API 的设计模式和约定

---

发现 API 问题？请提交 [GitHub Issue](https://github.com/xincheng213618/scgd_general_wpf/issues)
