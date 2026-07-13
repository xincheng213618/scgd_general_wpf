# 已停用项目

以下客户项目已于 2026-07-10 从 `develop` 分支移除。删除前的完整源码、文档和共享依赖保存在 Git Tag `archive/retired-projects-2026-07-10`。

| 项目 | 最后 manifest 版本 | 最低宿主版本 | 后续处理 |
| --- | --- | --- | --- |
| `ProjectARVR` | `1.0` | `1.3.9.10` | 新需求优先评估 `ProjectARVRLite` 或 `ProjectARVRPro` |
| `ProjectBlackMura` | `1.0` | `1.3.15.10` | 无指定替代项目，需要时从归档恢复并重新验证 |
| `ProjectHeyuan` | `1.0` | `1.3.15.10` | 无指定替代项目，需要时从归档恢复并重新验证 |
| `ProjectShiyuan` | `1.0` | `1.3.15.10` | 无指定替代项目，需要时从归档恢复并重新验证 |

恢复完整历史环境：

```powershell
git switch -c support/retired-projects archive/retired-projects-2026-07-10
```

只恢复单个项目到当前分支时，使用 `git restore --source archive/retired-projects-2026-07-10 -- Projects/<ProjectName> Projects/<ProjectName>.bat`，然后按当前宿主重新构建和验证。
