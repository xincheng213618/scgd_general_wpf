# CHANGELOG

## [1.0.4.2] 2025.12.09

### 文档更新

1. 全面更新README.md
   - 增强功能描述和分类
   - 添加快速开始指南
   - 补充常见问题解答
   - 新增性能优化建议
   - 添加最佳实践说明

2. 新增文档
   - `Process/README_IsEnabled_Feature_CN.md` - IsEnabled功能中文详解
   - `PERFORMANCE_OPTIMIZATION.md` - 性能优化指南

3. 优化文档结构
   - 更新相关文档链接
   - 完善使用示例
   - 增加配置说明

## [1.0.4.1] 2025.12.01

1. 优化选项，统一结构

## [1.0.3.4] 2025.11.17

1. 增加ProcessMeta的IsEnabled属性，支持选择性执行测试步骤

2. 优化流程管理：
   - ProcessManagerWindow新增"是否启用"列，支持通过复选框控制步骤启用/禁用
   - 执行逻辑自动跳过禁用的步骤
   - 完成判断仅基于启用的步骤
   - 初始化时动态查找第一个启用的步骤

3. 持久化支持：IsEnabled状态自动保存到ProcessMetas.json

## [1.0.0.15] 2025.10.21

1. ARVR优化项目

2. 增加ProcessManagerWindow，拆分Process项目

3. 增加Red,Green,Blue的解析