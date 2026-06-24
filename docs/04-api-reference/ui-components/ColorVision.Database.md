# ColorVision.Database

`UI/ColorVision.Database/` 同时承担业务 DAO/实体基础和运行时数据库浏览器能力。当前数据库浏览主线是 Provider 驱动的“数据库优先”模型，而不是只靠 C# 实体扫描。

## 先查什么

| 现象 | 第一检查点 |
| --- | --- |
| 数据库浏览器没有数据源 | `DatabaseBrowserProviderRegistry.GetProviders()`、默认 MySQL Provider、MySQL 配置 |
| MySQL 连接失败 | `MySqlControl.GetConnectionString(...)`、账号权限、网络、数据库服务 |
| 表能看不能改 | 主键识别、数据库账号写权限、`CanWriteCurrentTable` |
| 保存时报 SQL 错误 | Provider 的 `Insert/Update/Delete` 参数映射和字段类型 |
| 运行时找不到 SqlSugar | 发布包和宿主输出目录中的 `SqlSugarCore` 及传递依赖 |
| SQLite/MySQL 文件被占用 | 数据库服务锁、文件权限、UI 窗口连接释放 |

## 当前能力

| 能力 | 当前入口 | 说明 |
| --- | --- | --- |
| 数据库浏览器 | `DatabaseBrowserWindow.xaml(.cs)` | 展示数据源、库、表，支持搜索、分页、排序、新增、更新、删除 |
| Provider 注册 | `DatabaseBrowserProviderRegistry` | 懒加载默认 Provider，并接收调用方注册的数据源 |
| Provider 契约 | `IDatabaseBrowserProvider` | 要求实现库/表/列/分页查询/插入/更新/删除 |
| 通用 Provider 基类 | `DatabaseBrowserProviderBase` | 提供 SQL 片段、关键字、排序、增删改构造辅助 |
| 浏览模型 | `DatabaseBrowserModels.cs` | 数据库、表、列、分页结果和行变更模型 |
| MySQL 接入 | `MySqlControl.cs` | 配置持久化、连接字符串、默认浏览 Provider 创建 |
| 业务 DAO | `BaseTableDao<T>`、`EntityBase`、`ViewEntity` | 服务已有业务代码的实体访问基础 |

## 运行链路

1. `DatabaseBrowserWindow` 向 `DatabaseBrowserProviderRegistry` 取 Provider。
2. Provider 返回库、表、列信息。
3. 浏览器按真实表结构动态展示 `DataTable`。
4. 搜索、分页、排序由 Provider 查询返回。
5. 新增、编辑、删除通过 Provider 通用写接口落回数据库。
6. 业务代码仍可并行使用 `BaseTableDao<T>`，但它不再控制浏览器 UI。

## 新增数据库 Provider

| 步骤 | 检查点 |
| --- | --- |
| 实现契约 | 实现 `IDatabaseBrowserProvider` |
| 元数据 | 能返回库、表、列和主键信息 |
| 查询 | 支持分页、关键字和排序 |
| 写入 | 实现插入、更新、删除；无主键或无权限时明确失败 |
| 注册 | 在初始化位置注册到 `DatabaseBrowserProviderRegistry` |
| 验证 | 打开 `DatabaseBrowserWindow` 检查库表树、分页、编辑和删除 |

## 发布验收

| 验收项 | 要查什么 |
| --- | --- |
| 目标框架 | `ColorVision.Database.csproj` 的 `net8.0-windows7.0;net10.0-windows7.0` |
| 包依赖 | `SqlSugarCore`、`SQLitePCLRaw.bundle_e_sqlite3`、`Newtonsoft.Json`、`log4net`、`ColorVision.UI` |
| 包内说明 | `README.md` 与当前数据库浏览器能力一致 |
| MySQL 配置 | 连接字符串、数据库名、超时、连接测试按现场配置工作 |
| Provider 注册 | 默认 MySQL Provider 和外部注册 Provider 都能出现 |
| 通用浏览 | 库、表、列、分页、搜索、排序展示真实结构 |
| 通用写入 | 有主键表能新增、修改、删除；失败信息清楚 |

## 边界

- 数据库浏览器主线已经是从真实数据库结构生成界面，不是“先有实体再有表格”。
- 扩展新数据库来源优先实现 Provider，而不是补一批实体类。
- `BaseTableDao<T>` 仍服务业务代码，但不是数据库能力的唯一入口。
- 该模块不携带数据库服务，现场问题要区分 DLL 加载、配置和目标数据库可访问性。

## 关键文件

| 任务 | 先看 |
| --- | --- |
| 浏览器主链 | `DatabaseBrowserWindow.xaml.cs`、`DatabaseBrowserProviderRegistry.cs`、`IDatabaseBrowserProvider.cs` |
| Provider 通用逻辑 | `DatabaseBrowserProviderBase.cs`、`DatabaseBrowserModels.cs` |
| MySQL 接入 | `MySqlControl.cs` |
| 业务实体访问 | `IEntity.cs`、`EntityBase.cs`、`ViewEntity.cs`、`BaseTableDao.cs` |
