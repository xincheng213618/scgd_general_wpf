# SysDictionary システム辞書テンプレート

このページでは `Engine/ColorVision.Engine/Templates/SysDictionary/` を説明します。ここではアルゴリズムテンプレートの既定辞書を管理します。主要データは `t_scgd_sys_dictionary_mod_master` と `t_scgd_sys_dictionary_mod_item` にあり、現在 `TemplateModParam` は `mod_type = 7` を読みます。

## 対象範囲

| 項目 | 現在の実装 |
| --- | --- |
| テンプレートクラス | `TemplateModParam : ITemplate<DicModParam>` |
| パラメータクラス | `DicModParam : ParamModBase` |
| 編集 UI | `EditDictionaryMode.xaml(.cs)` |
| マスター作成 | `CreateDicTemplate.xaml(.cs)` |
| 詳細作成 | `CreateDicModeDetail.xaml(.cs)` |
| メニュー入口 | `MenuDefalutDicAlg` |
| マスターテーブル | `t_scgd_sys_dictionary_mod_master` |
| 詳細テーブル | `t_scgd_sys_dictionary_mod_item` |
| 現在の範囲 | `tenant_id = 0`、`mod_type = 7` |

## データモデル

`SysDictionaryModModel` は辞書マスターを保存します。主な列は `code`、`name`、`pid`、`p_type`、`mod_type`、`cfg_json`、`version`、`is_enable`、`is_delete`、`tenant_id` です。

`SysDictionaryModDetaiModel` は辞書項目を保存します。主な列は `pid`、`address_code`、`symbol`、`name`、`default_val`、`val_type`、`is_enable`、`is_delete` です。`val_type` は `Integer`、`Float`、`Bool`、`String`、`Enum` です。

## ライフサイクル

1. `MenuDefalutDicAlg` が `TemplateEditorWindow(new TemplateModParam())` を開きます。
2. `TemplateModParam.Load()` が `tenant_id = 0`、`mod_type = 7` のマスターを読みます。
3. 詳細は `SysDictionaryModDetailDao.GetAllByPid(model.Id)` で読みます。
4. `EditDictionaryMode` で既定値と有効状態を編集します。
5. `CreateDicTemplate` は `ModType = 7` のマスターを作成します。
6. `CreateDicModeDetail` は `ValueType = String`、`IsEnable = true` の詳細を作成します。
7. `Save()` は詳細行を保存します。

現在 `Save()` は詳細だけを保存し、マスター項目は保存しません。削除経路は `SysResourceModel` を呼んでいるため、期待する辞書表から消えない場合は DAO とテーブルを確認してください。

## 関係

| モジュール | 関係 |
| --- | --- |
| 強型テンプレート | `TemplateDicId` で既定辞書項目を読みます。 |
| JSON テンプレート | 多くの JSON テンプレートマスターも `mod_type = 7` で、内容は `cfg_json` にあります。 |
| Flow テンプレート | Flow は辞書項目を読んでノード/テンプレートパラメータを構成します。 |
| Validate | Validate は `mod_type = 110/111/120` を使うため、ここ混ぜないでください。 |

## トラブルシュート

| 現象 | 先に確認すること |
| --- | --- |
| テンプレート項目が出ない | `TemplateDicId` と辞書マスター。 |
| 新規項目が効かない | 詳細の `pid`、`symbol`、`address_code`、`is_enable`。 |
| 既定値が効かない | `default_val` と `val_type` の整合。 |
| メニューがない | `MenuDefalutDicAlg` のスキャンと権限。 |
| 削除後も見える | 削除先テーブル、キャッシュ、メニュー/テンプレート更新。 |

## 関連ページ

- [テンプレート管理](./template-management.md)
- [Templates API 参考](./api-reference.md)
- [Validate 判定ルールテンプレート](./validate-rules.md)
- [Engine テンプレートと Flow チェーン](../../engine-components/template-flow-chain.md)
- [現在のアルゴリズムテンプレート網羅表](../current-algorithm-template-coverage.md)
