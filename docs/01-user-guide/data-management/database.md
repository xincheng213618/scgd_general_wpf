# 数据库操作

介绍 ColorVision 的数据库配置、实体浏览器和 CRUD 操作。

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

## 实体浏览器

> 菜单入口：**工具 → 实体浏览器**

实体浏览器是 ColorVision 提供的通用数据库管理工具，类似 Navicat，可以浏览和管理所有数据库实体。

### 界面布局

- **左侧**：实体列表 DataGrid
  - **表名**：实体类对应的数据库表名
  - **数据库**：MySQL 或 SQLite
  - **记录数**：该表的记录总数
- **右侧**：选中实体的 CRUD 界面（DataGrid）

### 使用方法

1. 点击菜单 **工具 → 实体浏览器**
2. 左侧面板显示所有已注册的实体类型
3. 使用搜索框按表名过滤
4. 点击某个实体，右侧显示该表的数据
5. 支持以下操作：
   - **查看数据**：分页浏览表数据
   - **编辑数据**：直接在 DataGrid 单元格内编辑，失焦自动保存
   - **新增记录**：点击工具栏"新增"按钮
   - **删除记录**：选中行后点击"删除"按钮
   - **搜索过滤**：输入关键字过滤数据
   - **分页导航**：上一页/下一页切换

### 数据库来源标注

实体浏览器通过 `[DatabaseSource]` 属性判断实体属于哪个数据库：

```csharp
// MySQL 实体（默认，无需标注）
[SugarTable("users")]
public class UserEntity : EntityBase { ... }

// SQLite 实体（需要标注）
[DatabaseSource(DatabaseType.Sqlite)]
[SugarTable("log_entries")]
public class LogEntry : EntityBase { ... }
```

未标注的实体默认归类为 MySQL。

## 数据表结构

ColorVision 使用以下主要数据表：

- `users` - 用户信息
- `test_results` - 测试结果
- `images` - 图像数据
- `devices` - 设备配置
- `log_entries` - 系统日志（SQLite）

所有实现了 `IEntity` 接口的实体类会自动被实体浏览器发现。

## 数据查询

### 使用实体浏览器

1. 打开"工具" → "实体浏览器"
2. 在左侧选择实体类型
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
