# 已停用项目

以下客户项目已从主开发分支移除。删除前的完整源码和文档保存在对应的 Git Tag 中；已交付环境可继续使用，但不再从这些旧项目承接新版本。

| 项目 | 退役日期 | 归档标签 | 最后版本 | 最低宿主版本 | 后续处理 |
| --- | --- | --- | --- | --- | --- |
| `ProjectARVR` | 2026-07-10 | `archive/retired-projects-2026-07-10` | manifest `1.0` | `1.3.9.10` | 新需求迁移到 `ProjectARVRPro` |
| `ProjectBlackMura` | 2026-07-10 | `archive/retired-projects-2026-07-10` | manifest `1.0` | `1.3.15.10` | 无指定替代项目，需要时从归档恢复并重新验证 |
| `ProjectHeyuan` | 2026-07-10 | `archive/retired-projects-2026-07-10` | manifest `1.0` | `1.3.15.10` | 无指定替代项目，需要时从归档恢复并重新验证 |
| `ProjectShiyuan` | 2026-07-10 | `archive/retired-projects-2026-07-10` | manifest `1.0` | `1.3.15.10` | 无指定替代项目，需要时从归档恢复并重新验证 |
| `ProjectARVRLite` | 2026-07-22 | `archive/retired-projects-2026-07-22` | 项目 `1.2.5.18` / manifest `1.0` | `1.3.15.6` | 现有交付冻结维护，后续更新和迁移统一进入 `ProjectARVRPro` |

恢复完整历史环境时，先选择项目对应的归档标签：

```powershell
$archiveTag = 'archive/retired-projects-2026-07-22'
git switch -c support/retired-project $archiveTag
```

只恢复单个项目到当前分支时：

```powershell
$archiveTag = 'archive/retired-projects-2026-07-22'
$projectName = 'ProjectARVRLite'
git restore --source $archiveTag -- "Projects/$projectName" "Projects/$projectName.bat"
```

恢复后必须按当前宿主重新构建和验证；归档标签只保证可追溯，不代表旧插件可直接兼容当前宿主。
