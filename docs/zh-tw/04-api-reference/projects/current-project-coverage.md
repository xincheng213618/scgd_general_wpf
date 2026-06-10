# 目前專案文件覆蓋清單

本頁用來確認 `Projects/` 下的真實專案包都有對應文件、交接入口和運行時元資料。

## 覆蓋結果

| 專案目錄 | 專案檔 | manifest Id / version | 目前文件頁 | 交接入口 |
| --- | --- | --- | --- | --- |
| `Projects/ProjectARVR/` | `ProjectARVR.csproj` | `ProjectARVR` / `1.0` | [ProjectARVR](./project-arvr.md) | [矩陣](./project-capability-matrix.md)、[場景手冊](./project-package-playbook.md)、[交接](./project-handoff.md) |
| `Projects/ProjectARVRLite/` | `ProjectARVRLite.csproj` | `ProjectARVRLite` / `1.0` | [ProjectARVRLite](./project-arvr-lite.md) | [矩陣](./project-capability-matrix.md)、[場景手冊](./project-package-playbook.md)、[交接](./project-handoff.md) |
| `Projects/ProjectARVRPro/` | `ProjectARVRPro.csproj` | `ProjectARVRPro` / `1.1.7.7` | [ProjectARVRPro](./project-arvr-pro.md) | [矩陣](./project-capability-matrix.md)、[場景手冊](./project-package-playbook.md)、[交接](./project-handoff.md) |
| `Projects/ProjectARVRPro.IntegrationDemo/` | `ProjectARVRPro.IntegrationDemo.csproj` | 無 manifest | [ARVRPro 對接 Demo](./project-arvr-pro-integration-demo.md) | [矩陣](./project-capability-matrix.md)、[場景手冊](./project-package-playbook.md) |
| `Projects/ProjectBlackMura/` | `ProjectBlackMura.csproj` | `ProjectBlackMura` / `1.0` | [ProjectBlackMura](./project-black-mura.md) | [矩陣](./project-capability-matrix.md)、[場景手冊](./project-package-playbook.md)、[交接](./project-handoff.md) |
| `Projects/ProjectHeyuan/` | `ProjectHeyuan.csproj` | `ProjectHeyuan` / `1.0` | [ProjectHeyuan](./project-heyuan.md) | [矩陣](./project-capability-matrix.md)、[場景手冊](./project-package-playbook.md)、[交接](./project-handoff.md) |
| `Projects/ProjectKB/` | `ProjectKB.csproj` | `ProjectKB` / `1.0` | [ProjectKB](./project-kb.md) | [矩陣](./project-capability-matrix.md)、[場景手冊](./project-package-playbook.md)、[交接](./project-handoff.md) |
| `Projects/ProjectLUX/` | `ProjectLUX.csproj` | `ProjectLUX` / `1.0` | [ProjectLUX](./project-lux.md) | [矩陣](./project-capability-matrix.md)、[場景手冊](./project-package-playbook.md)、[交接](./project-handoff.md) |
| `Projects/ProjectShiyuan/` | `ProjectShiyuan.csproj` | `ProjectShiyuan` / `1.0` | [ProjectShiyuan](./project-shiyuan.md) | [矩陣](./project-capability-matrix.md)、[場景手冊](./project-package-playbook.md)、[交接](./project-handoff.md) |

## 目前工作樹核查證據

2026-06-10 核查目前工作樹時，`Projects/` 下共有 9 個目錄。8 個正式運行時專案包具備 `.csproj`、`manifest.json`、`README.md`、`CHANGELOG.md` 和 docs 專案頁；`ProjectARVRPro.IntegrationDemo` 是客戶側對接 Demo，工程檔宣告 `OutputType=Exe`、`TargetFrameworks=net48`、`IsPackable=false`，因此沒有 manifest 和 CHANGELOG 是已知邊界。

| 專案目錄 | `.csproj` | `manifest.json` | README | CHANGELOG | 結論 |
| --- | --- | --- | --- | --- | --- |
| `Projects/ProjectARVR/` | 有 | `ProjectARVR` / `1.0` | 有 | 有 | 正式專案包覆蓋完整 |
| `Projects/ProjectARVRLite/` | 有 | `ProjectARVRLite` / `1.0` | 有 | 有 | 正式專案包覆蓋完整 |
| `Projects/ProjectARVRPro/` | 有 | `ProjectARVRPro` / `1.1.7.7` | 有 | 有 | 正式專案包覆蓋完整 |
| `Projects/ProjectARVRPro.IntegrationDemo/` | 有 | 無 | 有 | 無 | 客戶對接 Demo，不按專案包 manifest 驗收 |
| `Projects/ProjectBlackMura/` | 有 | `ProjectBlackMura` / `1.0` | 有 | 有 | 正式專案包覆蓋完整 |
| `Projects/ProjectHeyuan/` | 有 | `ProjectHeyuan` / `1.0` | 有 | 有 | 正式專案包覆蓋完整 |
| `Projects/ProjectKB/` | 有 | `ProjectKB` / `1.0` | 有 | 有 | 正式專案包覆蓋完整 |
| `Projects/ProjectLUX/` | 有 | `ProjectLUX` / `1.0` | 有 | 有 | 正式專案包覆蓋完整 |
| `Projects/ProjectShiyuan/` | 有 | `ProjectShiyuan` / `1.0` | 有 | 有 | 正式專案包覆蓋完整 |

如果 `ProjectARVRPro.IntegrationDemo` 後續變成要隨主程式打包交付的正式專案包，應先補 `manifest.json`、`CHANGELOG.md`、PostBuild 複製規則、打包驗證和發布證據。

## 必須保留的交接邊界

| 邊界 | 專案 |
| --- | --- |
| JSON Socket 切圖流程 | ProjectARVR、ProjectARVRLite、ProjectARVRPro |
| 文字 Socket 命令流程 | ProjectLUX |
| 串口/MES 或 PG 控制 | ProjectBlackMura、ProjectHeyuan |
| Modbus/MES DLL 產線整合 | ProjectKB |
| 手動/離線客戶檔案匯出 | ProjectShiyuan |
| 客戶側協議 Demo | ProjectARVRPro.IntegrationDemo |

新增、刪除或改名 `Projects/<Name>/` 時，必須同步更新本頁、專案包總覽、矩陣、場景手冊和發布證據頁。
