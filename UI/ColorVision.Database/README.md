# ColorVision.Database

数据库基础模块，提供实体驱动的通用查询、MySQL/DAO 基础，以及 SQLite 压缩正文和文件维护工具。它不拥有所有业务数据库，也不统一管理 Socket 服务、Flow 执行或连接生命周期。

目标框架为 `net8.0-windows7.0;net10.0-windows7.0`，使用 WPF；依赖和版本以 `ColorVision.Database.csproj` 为准。本 README 是源码入口，完整契约只维护在仓库知识主题中。

## 按代码责任定位

| 责任 | 实现入口 | 权威知识 |
| --- | --- | --- |
| 已发布插件的旧浏览器注册兼容 | `Compatibility/LegacyDatabaseBrowserRegistration.cs`（无操作，不访问数据库） | [数据库连接与兼容边界](../../docs/04-api-reference/ui-components/ColorVision.Database.md) |
| 实体字段条件、查询、结果集合与会话 | `GenericQueryWindow`、`GenericQueryConditionSupport`、`GenericQuerySessionStore` | [通用查询与整表操作](../../docs/04-api-reference/ui-components/database-query.md) |
| MySQL 配置、连接和 DAO | `MySqlControl`、`MySqlConnect`、`BaseTableDao`、`IEntity` | [连接和 DAO 边界](../../docs/04-api-reference/ui-components/ColorVision.Database.md) |
| gzip 正文读写、旧 TEXT 迁移、备份及空间回收 | `GzipTextPayloadCodec`、`SqliteGzipTextPayloadStore`、`SqliteGzipTextMigration`、`SqliteFileMaintenance` | [SQLite 存储与维护](../../docs/04-api-reference/ui-components/sqlite-storage.md) |
| 数据文件与业务所有者 | Socket、Engine、插件和项目各自的管理器 | [数据责任地图](../../docs/01-user-guide/data-management/README.md) |

不要互换这些入口的保证：查询窗口的 SQL 区在执行时更新，不是完整实时预览；其清空表/截断表不受筛选条件限制。SQLite 底层迁移不自动备份、停业务或恢复，逐批提交也不等于全库回滚。具体调用前先读所属契约和实际 owner。

## 本地构建

从仓库根目录执行，仅构建托管模块及其依赖，会生成本地输出；不连接业务库、不发布包，也不验证真实数据库操作。完整源码/检索验证入口见 `docs/AGENTS.md`。

```powershell
dotnet build .\UI\ColorVision.Database\ColorVision.Database.csproj -f net10.0-windows7.0 -p:Platform=x64
```
