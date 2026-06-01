# Flow Engine Node Documentation Summary

## Document Description

This directory contains the following auto-generated node documentation:

1. **`flow_nodes_reference.md`** (Configured Node Reference)
   - Based on configurator files in the `NodeConfigurator` directory
   - Contains detailed properties for **42 configured nodes**
   - Each node includes:
     - Configuration panel properties (from NodeConfigurator)
     - Class-level properties (from FlowEngineLib implementation)
     - Base class, implementation file, and other info

2. **`flow_nodes_complete.md`** (Complete Node Inventory)
   - Based on all node class definitions in the `FlowEngineLib` directory
   - Contains a complete inventory of **90 node classes**
   - Grouped and counted by type
   - Each node includes base class, implementation file, property count

## Statistical Overview

### Configured Nodes (42)

| Type | Count |
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

### All Node Classes (90)

| Type | Count |
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

## Usage Recommendations

- **Developers**: Refer to `flow_nodes_complete.md` to understand all available node classes and their properties
- **Configurators**: Refer to `flow_nodes_reference.md` to understand panel properties and configuration methods for configured nodes
- **Maintainers**: Use both documents together to understand the mapping between node configuration and implementation

## Data Sources

- **Node Configuration**: `Engine\ColorVision.Engine\Templates\Flow\NodeConfigurator\`
- **Node Implementation**: `Engine\FlowEngineLib\`

## Update Notes

Documents are auto-generated from source code. When node configuration or implementation changes, the generation script needs to be re-run.

Generated on: 2026-05-22