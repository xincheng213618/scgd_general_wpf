# Validate 判定ルールテンプレート

このページでは `Engine/ColorVision.Engine/Templates/Validate/` の二層ルール体系を説明します。Validate は単一テンプレートではなく、既定の合規辞書と実際の判定テンプレートの二層で構成されます。

## 対象範囲

| 項目 | 現在の実装 |
| --- | --- |
| 既定辞書テンプレート | `TemplateDicComply : ITemplate<DicComplyParam>` |
| 実判定テンプレート | `TemplateComplyParam : ITemplate<ValidateParam>` |
| 辞書編集 UI | `DicEditComply.xaml(.cs)` |
| ルール編集 UI | `ValidateControl.xaml(.cs)` |
| メニュー入口 | `ExportComply.cs`、`ExportDicComply.cs` |
| ルールマスターテーブル | `t_scgd_rule_validate_template_master` |
| ルール詳細テーブル | `t_scgd_rule_validate_template_detail` |
| 実行時キャッシュ | `TemplateComplyParam.CIEParams`、`TemplateComplyParam.JNDParams` |

## 二層モデル

`TemplateDicComply` は `SysDictionaryModMasterDao` と `SysDictionaryModItemValidateDao` から既定辞書と既定ルール項目を読み込みます。

| 辞書 `mod_type` | 現在の用途 |
| --- | --- |
| `110` | 点位 CIE/合規判定メニュー。 |
| `111` | 点位リスト合規判定メニュー。 |
| `120` | JND 合規判定メニュー。 |

`TemplateComplyParam(code, type)` は辞書 `Code` により実判定テンプレートを読み込みます。`t_scgd_rule_validate_template_master` を読み、続いて `t_scgd_rule_validate_template_detail` を読みます。

| テーブル | 主な列 | 目的 |
| --- | --- | --- |
| `t_scgd_rule_validate_template_master` | `dic_pid`、`code`、`name`、`is_enable`、`is_delete`、`tenant_id` | 辞書コード配下の一つの判定テンプレート。 |
| `t_scgd_rule_validate_template_detail` | `dic_pid`、`pid`、`code`、`val_max`、`val_min`、`val_equal`、`val_radix`、`val_type` | 個別判定項目と閾値。 |

## 動的メニュー

| ソース | メニュー動作 |
| --- | --- |
| `mod_type = 110` | `TemplateComplyParam(item.Code)` を開き、点位ルール入口になります。 |
| `mod_type = 111` | `TemplateComplyParam(item.Code)` を開き、点位リストルール入口になります。 |
| `mod_type = 120` | `TemplateComplyParam(item.Code, 1)` を開き、JND ルール入口になります。 |
| `ExportDicComply` | `TemplateDicComply` を開き、既定合規辞書を保守します。 |

## 作成と保存

`TemplateDicComply.Create(templateCode, templateName)` は `SysDictionaryModModel` を作成します。現在の既定 `ModType` は `111` です。

`TemplateComplyParam.Create(templateName)` はマスター行を作成し、`Code` に対応する辞書を取得し、有効な辞書検証項目をコピーして `ValMax`、`ValMin`、`ValEqual`、`ValRadix`、`ValType` を詳細行に保存します。

`TemplateComplyParam.Save()` は実判定テンプレート名と詳細ルールを保存します。`TemplateDicComply.Save()` は既定辞書と既定ルール詳細を保存します。

## 実行時キャッシュ

| キャッシュ | 説明 |
| --- | --- |
| `CIEParams` | CIE/一般合規判定テンプレート集合。BuzProduct が参照します。 |
| `JNDParams` | JND 判定テンプレート集合。 |

現在のコンストラクタは `type == 1` の場合に `JNDParams` へ追加し、その後 `CIEParams` にも追加します。引き継ぎではこの現行挙動として説明してください。

## インポート制限

`TemplateComplyParam.Import()` は現在インポート未対応です。現場移行では辞書テーブルと `t_scgd_rule_validate_template_*` をまとめて移行するか、専用インポート処理を追加してください。

## 他モジュールとの関係

| モジュール | 依存方法 |
| --- | --- |
| [BuzProduct 製品業務パラメータテンプレート](./buz-product-template.md) | 詳細の `val_rule_temp_id` が Validate テンプレートを参照します。 |
| [Compliance 結果引き継ぎ](./compliance-results.md) | `ValidateResult` を読み、`ValidateRuleResultType.M` で合否を見ます。 |
| [JND テンプレート](./jnd-template.md) | JND ルールは `mod_type = 120` から来ます。 |
| プロジェクトパッケージ | Validate/Compliance データを帳票や OK/NG に使う場合があります。 |

## トラブルシュート

| 現象 | 先に確認すること |
| --- | --- |
| メニュー入口がない | `SysDictionaryModMaster` の `mod_type` と `is_delete = false`。 |
| 新規テンプレートに詳細がない | 辞書配下の有効な検証項目。 |
| BuzProduct にルール候補がない | `TemplateComplyParam.CIEParams` が該当 `Code` を読み込んでいるか。 |
| JND ルールが CIE 一覧に出る | 現在のコンストラクタ挙動です。 |
| インポートできない | 実判定テンプレートは現在インポート未対応です。 |

## 引き継ぎチェックリスト

- 既定辞書層と実判定テンプレート層を分けて説明します。
- 判定フィールド追加時は、辞書、詳細、受入サンプル、結果説明を更新します。
- 閾値の意味を変える場合は、サービスが書く `ValidateResult` も検証します。
- 移行時は `SysDictionaryMod*` と `t_scgd_rule_validate_template_*` を一緒に移行します。
- メニュー変更時は `mod_type = 110/111/120` の三経路を確認します。

## 関連ページ

- [BuzProduct 製品業務パラメータテンプレート](./buz-product-template.md)
- [Compliance 結果引き継ぎ](./compliance-results.md)
- [テンプレート管理](./template-management.md)
- [Engine 結果表示とプロジェクト引き継ぎチェーン](../../engine-components/result-handoff-chain.md)
- [現在のアルゴリズムテンプレート網羅表](../current-algorithm-template-coverage.md)
