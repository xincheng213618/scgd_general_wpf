# ColorVision Marketplace 运维说明

## 作用
本项目现在包含两类“整理并退出”的维护命令：

- `--reconcile-history`
  - 作用：整理 **主程序安装包**
  - 范围：`H:\ColorVision` 根目录下的 `ColorVision-*.exe/.zip/.rar`
  - 行为：只保留最近若干个版本，其余移动到 `History/<major.minor>/<major.minor.patch>/`

- `--reconcile-plugin-history`
  - 作用：整理 **插件版本包**
  - 范围：`H:\ColorVision\Plugins\<PluginId>\*.cvxp`
  - 行为：每个插件目录只保留最近若干个版本，其余移动到 `History/Plugins/<PluginId>/`

> 注意：这两个命令都会**移动文件**，不是只读扫描。

---

## 配置项
配置文件：`marketplace/config.json`

关键参数：

- `app_release_keep_count`
  - 根目录保留的主程序安装包数量
- `plugin_package_keep_count`
  - 每个插件目录保留的 `.cvxp` 数量

示例：

```json
{
  "app_release_keep_count": 5,
  "plugin_package_keep_count": 3
}
```

---

## 常用命令
在项目根目录执行：

```powershell
Set-Location "C:\Users\17917\Desktop\scgd_general_wpf\Backend"
python marketplace\app.py --reconcile-history
```

```powershell
Set-Location "C:\Users\17917\Desktop\scgd_general_wpf\Backend"
python marketplace\app.py --reconcile-plugin-history
```

正常启动服务：

```powershell
Set-Location "C:\Users\17917\Desktop\scgd_general_wpf\Backend"
python marketplace\app.py
```

---

## 建议的执行策略
推荐使用“两层策略”：

1. **发布时自动整理**
   - 当前系统已经支持主程序/插件在写入后自动整理一部分历史文件。
2. **计划任务补偿整理**
   - 因为真实环境里可能还有构建脚本、人工复制或其他工具写入 `H:\ColorVision`
   - 所以建议每天或每小时执行一次补偿整理

---

## Windows 计划任务建议
可以创建一个 `.ps1` 脚本，例如：

```powershell
Set-Location "C:\Users\17917\Desktop\scgd_general_wpf\Backend"
python marketplace\app.py --reconcile-history
python marketplace\app.py --reconcile-plugin-history
```

然后通过“任务计划程序”定时执行。

---

## 回滚思路
因为整理逻辑是“从当前目录移动到 History”，所以如果需要回滚：

- 主程序版本：从 `History/<major.minor>/<branch>/` 移回根目录
- 插件版本：从 `History/Plugins/<PluginId>/` 移回 `Plugins/<PluginId>/`

建议在首次大规模整理前先对 `H:\ColorVision` 做一次快照或备份。

