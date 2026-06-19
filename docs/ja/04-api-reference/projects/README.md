# プロジェクトパッケージ概要

`Projects/` は顧客プロジェクト、業務パッケージ、連携デモを保持します。実行時には主プログラムの `Plugins/<Name>/` に配置されることがありますが、文書上は汎用プラグインと分けて扱います。プロジェクトパッケージでは、顧客ワークフロー、Recipe/Fix、Socket/MES/Serial、結果エクスポートが重要です。

まず [プロジェクト能力と引き継ぎマトリクス](./project-capability-matrix.md) を読み、具体的な現場問題は [プロジェクト実行と引き継ぎプレイブック](./project-package-playbook.md) で確認します。リリース、現地置換、rollback の証跡は [プロジェクトパッケージリリース証跡とバージョン確認表](./project-release-evidence.md) に残します。共通実行チェーンは [プロジェクト引き継ぎマニュアル](./project-handoff.md)、文書の対応状況は [現在のプロジェクト文書カバレッジ](./current-project-coverage.md) を参照します。

## 現在のプロジェクト

| プロジェクト | ソース | manifest Id | 位置づけ | 文書 |
| --- | --- | --- | --- | --- |
| ProjectARVR | `Projects/ProjectARVR/` | `ProjectARVR` | 固定 PG 切替、Socket、結果集約 | [詳細](./project-arvr.md) |
| ProjectARVRLite | `Projects/ProjectARVRLite/` | `ProjectARVRLite` | 設定可能な検査項目、前処理、CSV | [詳細](./project-arvr-lite.md) |
| ProjectARVRPro | `Projects/ProjectARVRPro/` | `ProjectARVRPro` | AR/VR ProcessGroup、Recipe、Socket、顧客出力 | [詳細](./project-arvr-pro.md) |
| ProjectARVRPro.IntegrationDemo | `Projects/ProjectARVRPro.IntegrationDemo/` | なし | 顧客 TCP/JSON デモ | [詳細](./project-arvr-pro-integration-demo.md) |
| ProjectBlackMura | `Projects/ProjectBlackMura/` | `ProjectBlackMura` | PG シリアル、5 色フロー、Excel | [詳細](./project-black-mura.md) |
| ProjectHeyuan | `Projects/ProjectHeyuan/` | `ProjectHeyuan` | STX/ETX、WBRO 4 点、CSV | [詳細](./project-heyuan.md) |
| ProjectKB | `Projects/ProjectKB/` | `ProjectKB` | Modbus、MES DLL、バックライト補正 | [詳細](./project-kb.md) |
| ProjectLUX | `Projects/ProjectLUX/` | `ProjectLUX` | 輝度、色、MTF、歪みの自動化 | [詳細](./project-lux.md) |
| ProjectShiyuan | `Projects/ProjectShiyuan/` | `ProjectShiyuan` | JND/POI 出力と画像後処理 | [詳細](./project-shiyuan.md) |

## パッケージ作成

```powershell
Scripts\package_project.bat ProjectLUX --no-upload
```

このバッチは `Scripts/package_cvxp.py` を呼び、出力 DLL、README、CHANGELOG、manifest、PackageIcon を集めて `.cvxp` を生成します。

リリース証跡は [プロジェクトパッケージリリース証跡とバージョン確認表](./project-release-evidence.md) に記録します。

## 保守ルール

- すべての `Projects/<Name>/` には README、docs ページ、カバレッジ行が必要です。
- manifest、メニュー、Socket/MES/Serial イベント、Recipe、結果フィールド、納入物を変えたら個別ページと [プロジェクトパッケージリリース証跡とバージョン確認表](./project-release-evidence.md) も更新します。
