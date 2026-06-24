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

| 文件 | 用途 |
| --- | --- |
| `TemplateBuzProduc.cs` | 注册模板标题、模板代码、用户控件和 MySQL 恢复命令。 |
| `ITemplateBuzProduc.cs` | 实现加载、保存、创建、复制、导入、导出和删除的通用模板生命周期。 |
| `TemplateBuzProductParam.cs` | 把主表模型、明细集合和“新增明细”命令暴露给编辑器。 |
| `BuzProductMasterDao.cs` | `t_scgd_buz_product_master` 的 SqlSugar 模型与 DAO。 |
| `BuzProductDetailDao.cs` | `t_scgd_buz_product_detail` 的 SqlSugar 模型与 DAO。 |
| `EditTemplateBuzProduct.xaml(.cs)` | 编辑产品业务参数，并从 Validate 的 CIE 模板池加载可选判定模板。 |
| `MysqlBuzProduct.cs` | 恢复主表和明细表结构。 |

## 数据表结构

| 表 | 关键字段 | 说明 |
| --- | --- | --- |
| `t_scgd_buz_product_master` | `code`、`name`、`buz_type`、`cfg_json`、`img_file`、`create_date`、`is_enable`、`is_delete`、`tenant_id`、`remark` | 产品或业务模板主档 |
| `t_scgd_buz_product_detail` | `code`、`name`、`pid`、`poi_id`、`order_index`、`cfg_json`、`val_rule_temp_id` | 主档下的检测项或业务点位配置 |

## 生命周期

`TemplateBuzProduc.Load()` 读取 `is_delete = 0` 的主档，再按 `pid` 加载明细。编辑器绑定 `TemplateBuzProductParam.BuzProductDetailModels`；`Save()` 保存主档名称和明细，`Create()` 复制明细并重置 `id = -1` 后入库，`Delete()` 删除主档和对应明细。

## 与 Validate 的关系

`EditTemplateBuzProduct` 初始化判定规则下拉框时读取 `TemplateComplyParam.CIEParams`。因此，产品明细里的 `val_rule_temp_id` 不是手写阈值，而是引用 Validate 模板已经创建好的判定规则。

| BuzProduct 明细 | Validate 规则 | 结果侧影响 |
| --- | --- | --- |
| `poi_id` | 指定检测点位或区域来源。 | 决定算法结果对应哪个业务点。 |
| `val_rule_temp_id` | 指定该点位使用哪套合规判定。 | 影响 Compliance 或项目侧最终 OK/NG。 |
| `cfg_json` | 保存业务点位的额外配置。 | 可能被项目包二次解释。 |

## 导入导出

单模板导出为 `.cfg`，多模板打包为 `.zip`；导入后进入创建或复制流程，主档和明细会重新生成主键。迁移到另一台机器时，还要确认 POI 模板、Validate 字典和 Validate 规则模板也存在。

## 常见排查

| 现象 | 优先排查 |
| --- | --- |
| 模板找不到 | 模板代码是 `BuzProduc`，不要按修正后的 `BuzProduct` 搜注册项。 |
| 明细下拉里没有判定模板 | `TemplateComplyParam` 是否已按字典代码加载，`CIEParams` 是否有数据。 |
| 产品模板保存后结果判定没变 | 明细 `val_rule_temp_id` 是否真的指向期望的 Validate 规则模板。 |
| 换项目包后业务点不对 | `poi_id` 是否指向当前项目使用的 POI 模板或点位集合。 |
| 导入模板后 ID 错乱 | 复制流程会重置明细 `id`，应按新库的主键重新检查引用。 |

## 检查清单

- 说明主表和明细表的用途，不要只说“产品模板表”。
- 记录每个产品模板对应的 POI 模板、Validate 规则模板和项目包。
- 变更 `val_rule_temp_id` 时，同步更新验收样例和项目说明。
- 做现场迁移时，同时检查 BuzProduct、POI、Validate 字典和 Validate 规则。
- 如果要修正 `BuzProduc` 拼写，必须同步迁移模板代码、菜单、历史数据和文档，不能只改类名。
