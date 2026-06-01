# Flow Engine 结点文档汇总

## 文档说明

本目录包含以下自动生成的结点文档：

1. **`flow_nodes_reference.md`** (已配置结点参考)
   - 基于 `NodeConfigurator` 目录中的配置器文件
   - 包含 **42 个已配置结点** 的详细属性
   - 每个结点包含：
     - 配置面板属性（来自 NodeConfigurator）
     - 类级别属性（来自 FlowEngineLib 实现）
     - 基类、实现文件等信息

2. **`flow_nodes_complete.md`** (完整结点清单)
   - 基于 `FlowEngineLib` 目录中所有结点类定义
   - 包含 **90 个结点类** 的完整清单
   - 按类型分类统计
   - 每个结点包含基类、实现文件、属性数量

## 统计概览

### 已配置结点 (42 个)

| 类型 | 数量 |
|------|------|
| Algorithm | 17 |
| Camera | 8 |
| POI | 5 |
| OLED | 2 |
| SMU | 3 |
| Sensor | 2 |
| Spectrum | 3 |
| PG | 1 |
| FW | 1 |

### 所有结点类 (90 个)

| 类型 | 数量 |
|------|------|
| Algorithm | 25 |
| Camera | 14 |
| POI | 8 |
| OLED | 7 |
| SMU | 7 |
| MQTT | 5 |
| Sensor | 3 |
| Start | 3 |
| Other | 8 |
| Loop | 2 |
| End | 2 |
| Spectrum | 2 |
| FW | 1 |
| Manual | 1 |
| Device | 1 |
| PG | 1 |

## 使用建议

- **开发人员**：参考 `flow_nodes_complete.md` 了解所有可用的结点类及其属性
- **配置人员**：参考 `flow_nodes_reference.md` 了解已配置结点的面板属性和配置方式
- **维护人员**：两个文档结合使用，了解结点配置与实现的对应关系

## 数据来源

- **结点配置**：`Engine\ColorVision.Engine\Templates\Flow\NodeConfigurator\`
- **结点实现**：`Engine\FlowEngineLib\`

## 更新说明

文档基于源码自动生成，当结点配置或实现发生变化时，需要重新运行生成脚本。

生成时间：2026-05-22