# BuzProduct 产品业务参数模板

本页说明 `Engine/ColorVision.Engine/Templates/BuzProduct/` 的业务边界。`BuzProduct` 不是一个独立算法执行入口，它更像“产品型号/业务点位/判定规则”的装配模板，用来把产品级配置、POI 点位和 Validate 判定模板绑在一起，供项目包或现场模板复用。

## 适用范围

| 事项 | 当前实现 |
| --- | --- |
| 模板代码 | `BuzProduc`，源码中当前就是这个拼写 |
| 模板类 | `TemplateBuzProduc : ITemplateBuzProduc<TemplateBuzProductParam>, IITemplateLoad` |
| 参数类 | `TemplateBuzProductParam` |
| 编辑控件 | `EditTemplateBuzProduct.xaml(.cs)` |
| MySQL 恢复入口 | `MysqlBuzProduct` |
| 主表 | `t_scgd_buz_product_master` |
| 明细表 | `t_scgd_buz_product_detail` |
| 关键依赖 | `TemplateComplyParam.CIEParams`、POI 模板、Validate 规则模板 |

## 源码入口

| 文件 | 交接用途 |
| --- | --- |
| `TemplateBuzProduc.cs` | 注册模板标题、模板代码、用户控件和 MySQL 恢复命令。 |
| `ITemplateBuzProduc.cs` | 实现加载、保存、创建、复制、导入、导出和删除的通用模板生命周期。 |
| `TemplateBuzProductParam.cs` | 把主表模型、明细集合和“新增明细”命令暴露给编辑器。 |
| `BuzProductMasterDao.cs` | `t_scgd_buz_product_master` 的 SqlSugar 模型与 DAO。 |
| `BuzProductDetailDao.cs` | `t_scgd_buz_product_detail` 的 SqlSugar 模型与 DAO。 |
| `EditTemplateBuzProduct.xaml(.cs)` | 编辑产品业务参数，并从 Validate 的 CIE 模板池加载可选判定模板。 |
| `MysqlBuzProduct.cs` | 恢复主表和明细表结构。 |

## 数据表结构

### 主表

`t_scgd_buz_product_master` 保存一个产品或业务模板的主档。

| 字段 | 说明 |
| --- | --- |
| `code` | 产品或业务模板代码。 |
| `name` | 模板显示名称。 |
| `buz_type` | 业务类型，当前作为整数保存。 |
| `cfg_json` | 产品级 JSON 配置。 |
| `img_file` | 产品图片文件路径。 |
| `create_date` | 创建时间。 |
| `is_enable` | 是否启用。 |
| `is_delete` | 是否逻辑删除。 |
| `tenant_id` | 租户标识。 |
| `remark` | 备注。 |

### 明细表

`t_scgd_buz_product_detail` 保存主档下的检测项或业务点位配置。

| 字段 | 说明 |
| --- | --- |
| `code` | 明细代码。 |
| `name` | 明细名称。 |
| `pid` | 所属主表 `id`。 |
| `poi_id` | 关联的 POI 模板或点位 ID。 |
| `order_index` | 明细排序。 |
| `cfg_json` | 明细级 JSON 配置。 |
| `val_rule_temp_id` | 合规/判定模板 ID，指向 Validate 生成的规则模板。 |

## 生命周期

1. `TemplateBuzProduc` 被模板系统发现后，调用 `Load()`。
2. `Load()` 读取 `t_scgd_buz_product_master` 中 `is_delete = 0` 的主档。
3. 每个主档再通过 `BuzProductDetailDao.GetAllByPid(...)` 加载明细。
4. 编辑器绑定 `TemplateBuzProductParam.BuzProductDetailModels`，现场人员可以新增、调整或保存明细。
5. `Save()` 会保存主档名称和每条明细。
6. `Create()` 会新建主档，并从当前导入或复制的模板中复制明细，明细 `id` 会重置为 `-1` 后重新入库。
7. `Delete()` 会删除主档和对应明细。

## 与 Validate 的关系

`EditTemplateBuzProduct` 初始化判定规则下拉框时，会读取：

```csharp
TemplateComplyParam.CIEParams.SelectMany(kvp => kvp.Value).ToList()
```

因此，产品明细里的 `val_rule_temp_id` 不是手写阈值，而是引用 Validate 模板已经创建好的判定规则。交接时要把这条关系讲清楚：

| BuzProduct 明细 | Validate 规则 | 结果侧影响 |
| --- | --- | --- |
| `poi_id` | 指定检测点位或区域来源。 | 决定算法结果对应哪个业务点。 |
| `val_rule_temp_id` | 指定该点位使用哪套合规判定。 | 影响 Compliance 或项目侧最终 OK/NG。 |
| `cfg_json` | 保存业务点位的额外配置。 | 可能被项目包二次解释。 |

## 导入导出

| 操作 | 当前行为 |
| --- | --- |
| 单模板导出 | 导出为 `.cfg`。 |
| 多模板导出 | 打包为 `.zip`。 |
| 导入 | 读取配置后进入创建或复制流程。 |
| 复制 | 复制主档和明细，但入库时重新生成主键。 |

导入导出适合现场迁移模板，但不等于已经迁移了所有依赖。迁移到另一台机器时，还要确认 POI 模板、Validate 字典和 Validate 规则模板是否也存在。

## 常见排查

| 现象 | 优先排查 |
| --- | --- |
| 模板找不到 | 模板代码是 `BuzProduc`，不要按修正后的 `BuzProduct` 搜注册项。 |
| 明细下拉里没有判定模板 | `TemplateComplyParam` 是否已按字典代码加载，`CIEParams` 是否有数据。 |
| 产品模板保存后结果判定没变 | 明细 `val_rule_temp_id` 是否真的指向期望的 Validate 规则模板。 |
| 换项目包后业务点不对 | `poi_id` 是否指向当前项目使用的 POI 模板或点位集合。 |
| 导入模板后 ID 错乱 | 复制流程会重置明细 `id`，应按新库的主键重新检查引用。 |

## 交接清单

- 说明主表和明细表的用途，不要只说“产品模板表”。
- 记录每个产品模板对应的 POI 模板、Validate 规则模板和项目包。
- 变更 `val_rule_temp_id` 时，同步更新验收样例和项目说明。
- 做现场迁移时，同时检查 BuzProduct、POI、Validate 字典和 Validate 规则。
- 如果要修正 `BuzProduc` 拼写，必须同步迁移模板代码、菜单、历史数据和文档，不能只改类名。

## 继续阅读

- [Validate 判定规则模板](./validate-rules.md)
- [Compliance 结果交接](./compliance-results.md)
- [POI 模板](./poi-template.md)
- [模板管理](./template-management.md)
- [当前算法模板覆盖清单](../current-algorithm-template-coverage.md)
