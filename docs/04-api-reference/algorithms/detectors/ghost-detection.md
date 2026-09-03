---
knowledge_id: "algorithms.ghost"
knowledge_type: "topic"
status: "current"
summary: "Ghost1.0 鬼影检测的模板、颜色和请求入口；说明数据库明细、首条结果叠图、全部明细 CSV 追加导出及读取失败边界。"
aliases: ["Ghost检测入口和结果在哪里","TemplateGhost","AlgorithmGhost","ViewHandleGhost","Ghost1.0","鬼影模板管理","Ghost模板","请先选择Ghost模板","鬼影灰度"]
code_paths: ["Engine/ColorVision.Engine/Templates/ARVR/Ghost/TemplateGhost.cs","Engine/ColorVision.Engine/Templates/ARVR/Ghost/GhostParam.cs","Engine/ColorVision.Engine/Templates/ARVR/Ghost/AlgorithmGhost.cs","Engine/ColorVision.Engine/Templates/ARVR/Ghost/ViewHandleGhost.cs","Engine/ColorVision.Engine/Templates/ARVR/Ghost/AlgResultGhostDao.cs","Engine/ColorVision.Engine/Abstractions/IDisplayAlgorithm.cs","Engine/ColorVision.Engine/Abstractions/IResultHandlers.cs","Engine/ColorVision.Engine/Abstractions/IViewResult.cs","Engine/ColorVision.Engine/Services/Core/MQTTServiceBase.cs","UI/ColorVision.Database/BaseTableDao.cs"]
test_paths: ["Test/ColorVision.UI.Tests/VectorizedSelectVisualTests.cs"]
related: ["algorithms.arvr","algorithms.json-templates","engine.results","algorithms.local-native-analysis","algorithms.template-menus","engine.template-design"]
---

# Ghost1.0 鬼影检测

本页描述 Engine 的 ARVR 传统 Ghost 模板：手动界面发送算法服务请求，结果通过数据库明细、图像叠加和 CSV 呈现。ImageEditor 的 `GhostLocalAnalysis` / `M_DetectGhosts` 属于[本地原生分析](../local-native-analysis.md)；其它 Ghost 版本的入口见 [ARVR 模板](../templates/arvr-template.md)。

## 配置并执行

1. 在算法设备的通用手动面板中选择 **ARVR → Ghost1.0**。
2. 选择 **Ghost模板**，需要修改参数时打开模板旁的编辑命令并保存。模板加载依赖字典 `7`、编码 `ghost`；具体[模板编辑入口](../templates/template-menu-entries.md)共用通用宿主。
3. 选择 **颜色**：`BLUE`、`GREEN` 或 `RED`。新配置默认为 `BLUE`，其枚举值依次为 `0`、`1`、`2`。
4. 设置算法服务能读取的图像路径，点击 **计算**。输入助手检查有效模板及非空路径，并按扩展名确定文件类型；不检查图像是否存在或远端是否可读。
5. 核对本次请求、服务返回和对应历史结果。`MsgRecord` 只表示请求记录已建立并发起发送，不代表网络发送、算法计算或结果落库成功。

## 模板参数

`TemplateGhost : ITemplate<GhostParam>` 使用静态 `Params` 集合。下表列的是无明细的新空参数对象初值；已保存模板和新建预览分别受模板明细与系统字典默认值影响，见[模板持久化](../../../03-architecture/components/templates/design.md)。

| 字段 | 类型 | 新空对象初值 | 含义 |
| --- | --- | --- | --- |
| `Ghost_radius` | `int` | `65` | 待检测点阵的半径，像素 |
| `Ghost_cols` | `int` | `3` | 点阵列数 |
| `Ghost_rows` | `int` | `3` | 点阵行数 |
| `Ghost_ratioH` | `float` | `0.4` | 中心灰度百分比上限，保留参数原值 |
| `Ghost_ratioL` | `float` | `0.2` | 中心灰度百分比下限，保留参数原值 |

参数类没有范围、行列数或上下限关系校验；这些初值不是推荐测量配置，服务端接受的范围和比例解释需与实际算法一致。

## 手动请求

`AlgorithmGhost.SendCommand()` 发布 `EventName = "Ghost"`，参数包括：

| 字段 | 本入口发送的内容 |
| --- | --- |
| `ImgFileName`、`FileType` | 输入路径及扩展名对应的类型 |
| `TemplateParam` | 所选 `GhostParam` 的 `ID`、`Name`；不内联发送点阵数值 |
| `Color` | 当前颜色配置 |
| `Params.DeviceCode`、`Params.DeviceType` | 空字符串 |
| `SerialNumber` | 空字符串 |

消息外层的设备、服务名和 Token 为 `null` 时，由 `MQTTServiceBase.PublishAsyncClient` 补入服务值；和表中的内层设备字段分别处理。`AlgorithmGhost` 负责配置与消息组装，实际检测由算法服务完成。

## 结果明细与叠图

`ViewHandleGhost` 处理 `ViewResultAlgType.Ghost`。`Load()` 仅在 `result.ViewResults == null` 时，通过 `AlgResultGhostDao.GetAllByPid(result.Id)` 查询 `t_scgd_algorithm_result_detail_ghost`，将明细放入结果集合；已有非空引用的集合不会在该方法中重新查询。

DAO 在数据库未连接或查询异常时记录日志并返回空集合。因此空列表可能表示无匹配记录，也可能是读取失败；需结合数据库日志和主结果 ID 判断。结果菜单的 **调试** 会选择 `AlgorithmGhost` 并带入结果图像路径，不会自动还原本次模板和颜色。

| 呈现 | 当前范围 |
| --- | --- |
| 明细列表 | 展示结果集合中的 `LEDCenters`、`LEDBlobGray`、`GhostAverageGray`，对应“质心坐标”“光斑灰度”“鬼影灰度” |
| 图像叠加 | 只取第一条 `AlgResultGhostModel`，合并其 `GhostPixel` 与 `LedPixel` 点集；不是对全部明细逐条叠图 |
| 点位图形 | 每个点构造 `1×1` 几何矩形，整体使用一份冻结的 `StreamGeometry`，透明填充、红色轮廓 |
| 图形边界 | 可取得原图尺寸时使用图像范围，否则使用点集边界；空点集不创建图形 |

`GhostPixel` / `LedPixel` 分别反序列化 `GhostPixels` / `LEDPixels` 中的 JSON 点集；格式错误会使本次叠图失败并显示异常消息。打开源图也依赖结果路径上的文件存在。显示完整链见[结果交接](../../engine-components/result-handoff-chain.md)。

## CSV 导出

`ViewHandleGhost.SideSave()` 从当前 `ViewResults` 筛选全部 `AlgResultGhostModel`，按一条明细一行输出四列：`id`、质心坐标、光斑灰度、鬼影灰度。后三列直接使用存储的集合文本，不展开为“一个点一行”，也不包含完整轮廓坐标或导出的叠图。

文件以 UTF-8 **追加**写入；每次调用都会写列头和尾部空行，重复导出到同一文件会累积多个区块。含逗号、双引号或换行的字段会按 CSV 引号规则转义。导出基于已加载的内存明细，不会为此重新查询数据库。

## 排查与验证

| 现象 | 优先检查 |
| --- | --- |
| 提示“请先选择Ghost模板” | 当前模板选择、参数对象、字典 `7` 与编码 `ghost` 是否加载 |
| 结果与颜色或点阵不符 | 实际 `Color`、模板引用及保存的参数；初始颜色是 `BLUE` |
| 列表为空 | 主结果 ID、数据库连接与查询日志，以及结果集合是否已初始化；不把空列表直接当作检测结论 |
| 列表有多条但叠图不完整 | 叠图只使用第一条明细；再查该条轮廓 JSON 和源图路径 |
| CSV 重复列头或内容 | 同一路径采用追加写入；核对导出次数和文件是否已有内容 |

`VectorizedSelectVisualTests` 的两个 Ghost 用例验证图像边界、无图时点集边界、冻结矢量图形及空点集处理。它们不覆盖算法服务、DAO 查询或 CSV 导出；这些路径仍需用已知图像、颜色与点阵参数分别核对请求、明细、叠图和文件内容。
