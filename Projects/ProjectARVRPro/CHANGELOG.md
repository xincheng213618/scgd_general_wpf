# CHANGELOG

## [1.0.4.2] 2025.12.04

1. 新增MTFHV058Process模块，支持058产品的MTF测试

2. MTFHVProcess和MTFHV058Process优化：
   - 将解析Key从硬编码移动到ProcessConfig中
   - 支持用户自定义配置解析使用的变量名
   - 默认值保持不变，确保向后兼容

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