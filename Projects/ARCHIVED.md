# 已停用项目

以下客户项目已从主开发分支移除。对应版本的源码和仓库内文档保存在 Git 归档标签中；已交付环境继续按原版本维护，新需求按表中后续处理方向推进。

| 项目 | 退役日期 | 归档标签 | 最后版本 | 最低宿主版本 | 后续处理 |
| --- | --- | --- | --- | --- | --- |
| `ProjectARVR` | 2026-07-10 | `archive/retired-projects-2026-07-10` | manifest `1.0` | `1.3.9.10` | 新需求迁移到 `ProjectARVRPro` |
| `ProjectBlackMura` | 2026-07-10 | `archive/retired-projects-2026-07-10` | manifest `1.0` | `1.3.15.10` | 无指定替代项目，需要时从归档恢复并重新验证 |
| `ProjectHeyuan` | 2026-07-10 | `archive/retired-projects-2026-07-10` | manifest `1.0` | `1.3.15.10` | 无指定替代项目，需要时从归档恢复并重新验证 |
| `ProjectShiyuan` | 2026-07-10 | `archive/retired-projects-2026-07-10` | manifest `1.0` | `1.3.15.10` | 无指定替代项目，需要时从归档恢复并重新验证 |
| `ProjectARVRLite` | 2026-07-22 | `archive/retired-projects-2026-07-22` | 项目 `1.2.5.18` / manifest `1.0` | `1.3.15.6` | 现有交付冻结维护，后续更新和迁移统一进入 `ProjectARVRPro` |

## 查看或维护归档版本

从仓库根目录操作，先提交或另行保存工作区中需要保留的改动。选择项目对应的归档标签，再创建维护分支；`git switch` 会切换当前工作区的源码版本：

```powershell
$archiveTag = 'archive/retired-projects-2026-07-22'
git switch -c support/retired-project $archiveTag
```

该分支保留归档时的项目文件。构建和运行仍需准备对应版本的工具链、外部依赖和配置；切换 Git 版本不会恢复这些环境。

## 将单个项目源码取回当前分支

确认目标路径没有需要保留的改动后执行。以下命令会覆盖指定工作区路径，索引保持不变：

```powershell
$archiveTag = 'archive/retired-projects-2026-07-22'
$projectName = 'ProjectARVRLite'
git restore --source $archiveTag -- "Projects/$projectName" "Projects/$projectName.bat"
```

取回源码不会自动把项目加入当前解决方案，也不会补齐外部依赖或迁移接口。需要按当前宿主重新集成、构建和验证；归档标签提供历史定位，不代表旧插件可直接兼容当前宿主。
