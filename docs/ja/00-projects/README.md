# プロジェクト説明

この章は `Projects/` 配下の顧客プロジェクトパッケージを説明します。これらは実行時にはプラグインのようにホストへ読み込まれますが、中心は汎用ツールではなく、顧客ごとの検査順序、Recipe/Fix、外部プロトコル、結果出力、現場納入です。

初回引き継ぎでは、まずこのページで全体像を確認し、次に [プロジェクト能力と引き継ぎマトリクス](../04-api-reference/projects/project-capability-matrix.md)、[プロジェクト実行と引き継ぎプレイブック](../04-api-reference/projects/project-package-playbook.md)、[プロジェクトパッケージリリース証跡とバージョン確認表](../04-api-reference/projects/project-release-evidence.md) を読みます。共通実行チェーンは [プロジェクト引き継ぎマニュアル](../04-api-reference/projects/project-handoff.md) を確認し、最後に対象プロジェクトページへ進みます。

## 現在のプロジェクト一覧

| プロジェクト | 役割 | 詳細 |
| --- | --- | --- |
| ProjectARVR | 初期 AR/VR 光学検査。固定 PG 切替、Socket イベント、`ObjectiveTestResult` 集約 | [ProjectARVR](../04-api-reference/projects/project-arvr.md) |
| ProjectARVRLite | 軽量 AR/VR クイック検査。測定項目設定、前処理、Socket 切替、CSV | [ProjectARVRLite](../04-api-reference/projects/project-arvr-lite.md) |
| ProjectARVRPro | 現行の主要 AR/VR パッケージ。ProcessGroup、Recipe、画像切替、Socket、多形式出力 | [ProjectARVRPro](../04-api-reference/projects/project-arvr-pro.md) |
| ProjectARVRPro.IntegrationDemo | 顧客、MES、PLC、上位機向け TCP/JSON 連携サンプル | [ARVRPro Integration Demo](../04-api-reference/projects/project-arvr-pro-integration-demo.md) |
| ProjectBlackMura | 表示パネル Black Mura 検査。PG シリアル切替、5 色フロー、Excel レポート | [ProjectBlackMura](../04-api-reference/projects/project-black-mura.md) |
| ProjectHeyuan | Heyuan 顧客向け 4 点 WBRO 色/輝度検査。STX/ETX シリアルと CSV | [ProjectHeyuan](../04-api-reference/projects/project-heyuan.md) |
| ProjectKB | キーボードバックライト輝度/均一性。Modbus、MES DLL、自動補正、CSV/summary | [ProjectKB](../04-api-reference/projects/project-kb.md) |
| ProjectLUX | LUX 光学自動化。輝度、色、MTF、歪み、光学中心、VID、光束 | [ProjectLUX](../04-api-reference/projects/project-lux.md) |
| ProjectShiyuan | Shiyuan 顧客向け JND/POI エクスポートと固定画像後処理 | [ProjectShiyuan](../04-api-reference/projects/project-shiyuan.md) |

## 推奨読書順

1. [プロジェクト能力と引き継ぎマトリクス](../04-api-reference/projects/project-capability-matrix.md)
2. [現在のプロジェクト文書カバレッジ](../04-api-reference/projects/current-project-coverage.md)
3. [プロジェクト実行と引き継ぎプレイブック](../04-api-reference/projects/project-package-playbook.md)
4. [プロジェクトパッケージリリース証跡とバージョン確認表](../04-api-reference/projects/project-release-evidence.md)
5. [プロジェクト引き継ぎマニュアル](../04-api-reference/projects/project-handoff.md)
6. 対象プロジェクトページ

## 保守ルール

- 新しい `Projects/<Name>/` を追加したら、このページ、プロジェクト概要、カバレッジ表、個別ページを更新します。
- 外部プロトコル、結果出力、ProcessGroup、Recipe/Fix、受入条件を変更したら、個別ページ、マトリクス、プレイブック、リリース証跡ページを同時に更新します。
- 文書には現在のソースコードで確認できる挙動だけを書きます。
