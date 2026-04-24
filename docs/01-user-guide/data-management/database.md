# 数据库操作

介绍 ColorVision 的数据库配置、数据库浏览器和基础 CRUD 操作。

## 数据库配置

### MySQL 配置

1. 打开"设置" → "数据库"
2. 选择"MySQL"
3. 输入连接信息：
   - 主机地址
   - 端口（默认 3306）
   - 数据库名
   - 用户名和密码
4. 点击"测试连接"
5. 保存配置

### SQLite 配置

1. 选择"SQLite"
2. 指定数据库文件路径
3. 保存配置

## 数据库浏览器

> 菜单入口：**工具 → 数据库浏览器**

数据库浏览器是 ColorVision 提供的通用数据库管理工具，类似 Navicat，可以先浏览数据源和库，再进入库中的表。它不依赖 C# 实体，适合同时查看 MySQL 和 SQLite 数据。

### 界面布局

- **左侧**：数据源树
   - **数据源**：MySQL、SQLite 日志或其他已注册 Provider
   - **库**：MySQL schema 或 SQLite 文件 catalog
   - **表**：数据库中的实际表
- **右侧**：选中表的数据网格、分页和维护工具栏

### 使用方法

1. 点击菜单 **工具 → 数据库浏览器**
2. 在左侧展开数据源、库和表
3. 点击某个表，右侧显示该表的数据
4. 支持以下操作：
   - **查看数据**：分页浏览表数据
   - **编辑数据**：直接在 DataGrid 单元格内编辑，然后点击保存
   - **新增记录**：点击工具栏"新增"按钮
   - **删除记录**：选中行后点击"删除"按钮
   - **搜索过滤**：输入关键字过滤数据
   - **分页导航**：上一页/下一页切换

### 数据源注册

数据库浏览器通过 `IDatabaseBrowserProvider` 接入数据源。默认已注册 MySQL 和 SQLite 日志库，其他 SQLite 文件可以在代码中注册：

```csharp
DatabaseBrowserProviderRegistry.Register(new SqliteDatabaseBrowserProvider(
    "sqlite.demo",
    "SQLite Demo",
    () => DemoDbManager.DbPath,
    DemoDbManager.CreateDbClient));
```

修改和删除依赖表主键；无主键表建议只做查询或新增。

## 数据表结构

ColorVision 使用以下主要数据表：

- `users` - 用户信息
- `test_results` - 测试结果
- `images` - 图像数据
- `devices` - 设备配置
- `log_entries` - 系统日志（SQLite）

浏览器直接读取数据库表结构，不要求表存在对应的 `IEntity` 实体类。

## 数据查询

### 使用数据库浏览器

1. 打开"工具" → "数据库浏览器"
2. 在左侧选择数据库表
3. 右侧 DataGrid 直接显示数据
4. 使用搜索框过滤

### 使用查询界面

1. 打开"数据" → "数据查询"
2. 选择查询条件
3. 点击"查询"
4. 查看结果

### SQL 查询

对于高级用户，支持直接执行 SQL：

```sql
SELECT * FROM test_results
WHERE test_date >= '2024-01-01'
ORDER BY test_date DESC
```

## 数据维护

### 数据清理

- 删除过期数据
- 清理重复记录
- 优化数据库

### 索引管理

- 创建索引提升查询性能
- 定期重建索引

## 数据安全

- 定期备份
- 访问控制
- 加密敏感数据

## 相关文档

- [数据导出与导入](./export-import.md)
- [ColorVision.Database API 文档](/04-api-reference/ui-components/ColorVision.Database.md)
