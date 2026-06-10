# BuzProduct 製品業務パラメータテンプレート

このページでは `Engine/ColorVision.Engine/Templates/BuzProduct/` の業務境界を説明します。`BuzProduct` は単独のアルゴリズム実行入口ではなく、製品設定、POI 参照、Validate 判定テンプレートをまとめる製品/業務テンプレートです。

## 対象範囲

| 項目 | 現在の実装 |
| --- | --- |
| テンプレートコード | `BuzProduc`、ソース上の綴りをそのまま記載 |
| テンプレートクラス | `TemplateBuzProduc : ITemplateBuzProduc<TemplateBuzProductParam>, IITemplateLoad` |
| パラメータクラス | `TemplateBuzProductParam` |
| 編集 UI | `EditTemplateBuzProduct.xaml(.cs)` |
| MySQL 復旧入口 | `MysqlBuzProduct` |
| マスターテーブル | `t_scgd_buz_product_master` |
| 詳細テーブル | `t_scgd_buz_product_detail` |
| 主な依存 | `TemplateComplyParam.CIEParams`、POI テンプレート、Validate ルールテンプレート |

## ソース入口

| ファイル | 引き継ぎ用途 |
| --- | --- |
| `TemplateBuzProduc.cs` | タイトル、コード、編集コントロール、MySQL 復旧コマンドを登録します。 |
| `ITemplateBuzProduc.cs` | 読み込み、保存、作成、コピー、インポート、エクスポート、削除を実装します。 |
| `TemplateBuzProductParam.cs` | マスターモデル、詳細リスト、詳細追加コマンドを編集 UI に公開します。 |
| `BuzProductMasterDao.cs` | `t_scgd_buz_product_master` の SqlSugar モデルと DAO。 |
| `BuzProductDetailDao.cs` | `t_scgd_buz_product_detail` の SqlSugar モデルと DAO。 |
| `EditTemplateBuzProduct.xaml(.cs)` | 製品業務項目を編集し、`CIEParams` から Validate 候補を読み込みます。 |
| `MysqlBuzProduct.cs` | マスター/詳細テーブル構造を復旧します。 |

## データテーブル

`t_scgd_buz_product_master` は製品または業務テンプレートのマスターを保存します。主な列は `code`、`name`、`buz_type`、`cfg_json`、`img_file`、`is_enable`、`is_delete`、`tenant_id`、`remark` です。

`t_scgd_buz_product_detail` はマスター配下の検査項目または業務点位を保存します。主な列は `code`、`name`、`pid`、`poi_id`、`order_index`、`cfg_json`、`val_rule_temp_id` です。

`val_rule_temp_id` は Validate ルールテンプレートへの参照です。ここを変更すると、その製品項目の Compliance またはプロジェクト側 OK/NG に影響します。

## ライフサイクル

1. テンプレートシステムが `TemplateBuzProduc` を検出し、`Load()` を呼びます。
2. `Load()` は `is_delete = 0` のマスター行を読み込みます。
3. 各マスターは `BuzProductDetailDao.GetAllByPid(...)` で詳細を読み込みます。
4. 編集 UI は `TemplateBuzProductParam.BuzProductDetailModels` にバインドします。
5. `Save()` はマスター名と各詳細行を保存します。
6. `Create()` は新しいマスターを作成し、インポート/コピー元の詳細を新しい ID で保存します。
7. `Delete()` はマスターと対応する詳細を削除します。

## Validate との関係

`EditTemplateBuzProduct` は判定ルールのドロップダウンを次のリストから作ります。

```csharp
TemplateComplyParam.CIEParams.SelectMany(kvp => kvp.Value).ToList()
```

| BuzProduct 詳細 | Validate ルール | 結果への影響 |
| --- | --- | --- |
| `poi_id` | 検査点または領域を指定します。 | 結果がどの業務点に属するかを決めます。 |
| `val_rule_temp_id` | 使用するルールテンプレートを指定します。 | Compliance またはプロジェクト側 OK/NG に影響します。 |
| `cfg_json` | 詳細項目の追加設定を保存します。 | プロジェクトパッケージで再解釈される場合があります。 |

## インポートとエクスポート

単一テンプレートは `.cfg`、複数テンプレートは `.zip` として出力されます。インポート/コピー時はデータベース ID が再生成されます。別環境へ移す場合は、POI テンプレート、Validate 辞書、Validate ルールテンプレートも確認してください。

## トラブルシュート

| 現象 | 先に確認すること |
| --- | --- |
| テンプレートが見つからない | 修正後の `BuzProduct` ではなく `BuzProduc` を検索します。 |
| ルールドロップダウンが空 | `TemplateComplyParam.CIEParams` が読み込まれているか確認します。 |
| 保存後も判定が変わらない | 詳細行の `val_rule_temp_id` が期待するテンプレートを指しているか確認します。 |
| プロジェクト変更後に点位がずれる | `poi_id` が現在のプロジェクトの POI と一致するか確認します。 |
| インポート後の ID が合わない | コピー/インポートでは ID が再生成されます。参照を再確認します。 |

## 引き継ぎチェックリスト

- マスター表と詳細表の役割を分けて説明します。
- 製品テンプレートごとに POI、Validate ルール、プロジェクトパッケージを記録します。
- `val_rule_temp_id` を変更したら、受入サンプルとプロジェクト説明も更新します。
- 移行時は BuzProduct、POI、Validate 辞書、Validate ルールをまとめて確認します。
- `BuzProduc` の綴りは永続化コードなので、安易に変更しません。

## 関連ページ

- [Validate 判定ルールテンプレート](./validate-rules.md)
- [Compliance 結果引き継ぎ](./compliance-results.md)
- [POI テンプレート](./poi-template.md)
- [テンプレート管理](./template-management.md)
- [現在のアルゴリズムテンプレート網羅表](../current-algorithm-template-coverage.md)
