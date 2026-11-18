---
knowledge_id: "delivery.testing"
knowledge_type: "reference"
status: "current"
summary: "从实际代码选择验证范围，区分离线检查、硬件联调与交付验收。"
aliases: ["怎么测试","验证命令","dotnet test","测试入口","PerformanceProbe","测试维护","测试瘦身"]
code_paths: ["package.json",".github/workflows/dotnet.yml"]
related: ["delivery.index","delivery.prerequisites","delivery.native-testing","governance.retrieval","copilot.configuration"]
---

# 测试与验证

从改动的实现和调用方定位测试。项目文件、脚本和 CI 定义实际构建配置、筛选和运行入口。 [构建前提](../00-getting-started/prerequisites.md)

## 按变更选择验证

UI 变更核对 Windows、WPF 和宿主配置。性能数据附带实际硬件、配置、输入和运行条件。设备、网络、安装及交付使用目标环境的独立证据。文档改动按维护规范检查。 [知识维护规范](../knowledge/maintenance.md#按变更选择验证)

临时测试可用于复现、探索和迭代。长期回归测试用于保护现有覆盖遗漏的稳定共享行为、兼容性要求或故障与生命周期边界，并在任务报告中注明保留理由。优先复用所属行为测试或参数化真正不同的边界。避免按每个界面、属性、语言、主题组合和任意次数重复覆盖。

测试与当前生产行为或明确的公开契约不一致时，先核实测试是否过时。只有已确认的产品契约才适合作为时间、像素、迭代次数或调度容差。业务行为调整后，失效的临时阈值或 Dispatcher 时序测试应删除或改成确定性检查，避免通过放宽阈值或修改生产代码迁就测试。

## native 和后端

原生调试核对工具集、架构和实际加载的 DLL。后端验证核对依赖、配置、数据库与外部服务隔离。 [原生调试](./engine-development/native-testing.md) · [后端验证边界](./backend/README.md#测试与边界)

## 验证记录

随任务记录命令、环境、结果、受阻原因和待验收范围。
