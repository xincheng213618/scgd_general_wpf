# 数据库操作

介绍 ColorVision 的数据库配置和操作。

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

## 数据表结构

ColorVision 使用以下主要数据表：

- `projects` - 项目信息
- `test_results` - 测试结果
- `images` - 图像数据
- `devices` - 设备配置
- `users` - 用户信息

## 数据查询

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
- [数据存储](/05-resources/data-storage.md)
