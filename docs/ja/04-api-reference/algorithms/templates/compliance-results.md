# Compliance 結果引き継ぎ

このページでは `Engine/ColorVision.Engine/Templates/Compliance/` の結果モデルと表示チェーンを説明します。このディレクトリはルール作成層ではなく、合規結果を読み込み、表示し、`ValidateResult` を解釈する層です。

## 対象範囲

| 項目 | 現在の実装 |
| --- | --- |
| Y 結果 | `ComplianceYModel`、`ComplianceYDao`、`ViewHandleComplianceY` |
| XYZ 結果 | `ComplianceXYZModel`、`ComplianceXYZDao`、`ViewHandleComplianceXYZ` |
| JND 結果 | `ComplianceJNDModel`、`ComplianceJNDDao`、`ViewHandleComplianceJND` |
| 判定ソース | `ValidateResult` JSON |
| 反シリアライズ型 | `ObservableCollection<ValidateRuleResult>` |
| 合格条件 | すべてのルールが `Result == ValidateRuleResultType.M` |
| 実行入口 | `IResultHandleBase` handler |

## 結果タイプ対応

| Handler | 対応結果タイプ | テーブル |
| --- | --- | --- |
| `ViewHandleComplianceY` | `Compliance_Contrast`、`Compliance_Math`、`Compliance_Contrast_CIE_Y`、`Compliance_Math_CIE_Y` | `t_scgd_algorithm_result_detail_compliance_y` |
| `ViewHandleComplianceXYZ` | `Compliance_Contrast_CIE_XYZ`、`Compliance_Math_CIE_XYZ` | `t_scgd_algorithm_result_detail_compliance_xyz` |
| `ViewHandleComplianceJND` | `Compliance_Math_JND` | `t_scgd_algorithm_result_detail_compliance_jnd` |

## データモデル

`ComplianceYModel` は単一値の輝度またはコントラスト結果を保存し、`pid`、`name`、`data_type`、`data_value`、`validate_result` を持ちます。

`ComplianceXYZModel` は `data_value_x/y/z`、`data_value_u/v`、`data_value_cct`、`data_value_wave` などの色/光学分量と `validate_result` を保存します。

`ComplianceJNDModel` は `data_val_h`、`data_val_v`、`validate_result` を保存します。

## 判定ロジック

三つのモデルは同じ `Validate` ロジックを使います。

1. `ValidateResult` が空なら `false`。
2. JSON は `ObservableCollection<ValidateRuleResult>` に反シリアライズされます。
3. すべての `Result` が `ValidateRuleResultType.M` の場合だけ合格です。
4. 一つでも `M` 以外なら不合格です。

Compliance ページは閾値を再計算せず、上流が書き込んだ判定 JSON を解釈します。

## 表示フロー

1. 結果ページが `ViewResultAlgType` により `ViewHandleCompliance*` を選びます。
2. `ResultImagFile` が存在すれば画像を開きます。
3. handler は主結果 `id` で詳細テーブルを検索します。
4. 行を `IViewResult` に変換し、ListView にバインドします。
5. 表では名前、値、判定状態、判定 JSON を表示します。

現在 `ViewHandleComplianceXYZ` は `DataValue` 列をバインドしていますが、モデルは `DataValuex/y/z/u/v/...` を主に公開しています。XYZ 値が空の場合は、バインド名とモデルを先に確認してください。

## 他モジュールとの関係

| モジュール | 判定チェーンでの役割 |
| --- | --- |
| [Validate 判定ルールテンプレート](./validate-rules.md) | フィールド、閾値、比較方法を定義します。 |
| [BuzProduct 製品業務パラメータテンプレート](./buz-product-template.md) | `val_rule_temp_id` でルールテンプレートを選びます。 |
| Compliance 結果 | `ValidateResult` を読み、合否を表示します。 |
| プロジェクトパッケージ | Compliance/JND/POI 結果を集計またはエクスポートします。 |

## トラブルシュート

| 現象 | 先に確認すること |
| --- | --- |
| 詳細が表示されない | 結果タイプと詳細テーブルの `pid` を確認します。 |
| 画像が開かない | `ResultImagFile` の存在と移行後のパスを確認します。 |
| `Validate` が失敗 | `validate_result` が空、または非 `M` のルールがないか確認します。 |
| XYZ 値が空 | ListView バインド名と `ComplianceXYZModel` を確認します。 |
| プロジェクト帳票と違う | プロジェクト側のフィルター、並び順、集計を確認します。 |

## 引き継ぎチェックリスト

- Compliance は結果表示/解釈層であり、ルール編集層ではないと説明します。
- 結果タイプ追加時は handler、DAO、テーブル、文書を合わせて更新します。
- `ValidateResult` JSON 変更時は Y、XYZ、JND の三系統を検証します。
- 受入時は主結果、詳細行、画像パス、Validate テンプレート、プロジェクト出力を保存します。

## 関連ページ

- [Validate 判定ルールテンプレート](./validate-rules.md)
- [BuzProduct 製品業務パラメータテンプレート](./buz-product-template.md)
- [JND テンプレート](./jnd-template.md)
- [Engine 結果表示とプロジェクト引き継ぎチェーン](../../engine-components/result-handoff-chain.md)
- [現在のアルゴリズムテンプレート網羅表](../current-algorithm-template-coverage.md)
