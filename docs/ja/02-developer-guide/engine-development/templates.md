# Engine テンプレートシステム開発引き継ぎ

このページは `Engine/ColorVision.Engine/Templates/` の現在のテンプレートモデルを説明します。テンプレートはパラメータ、編集、保存、インポート/エクスポート、アルゴリズム命令パラメータを扱います。顧客判定や帳票形式はプロジェクトパッケージ側で扱います。

## 実行時チェーン

| 段階 | 主要オブジェクト | 説明 |
| --- | --- | --- |
| 登録 | `ITemplate`、`TemplateControl.AddITemplateInstance` | テンプレートインスタンスをグローバル表に登録 |
| 検出 | `IITemplateLoad`、`TemplateControl` | 起動時にロード可能なテンプレートをスキャン |
| パラメータ集合 | `TemplateModel<T>`、`TemplateParams` | UI のコンボボックスと編集画面が参照する集合 |
| MySQL テンプレート | `ITemplate<T>`、`ParamModBase` | `TemplateDicId` で `ModMasterModel` / `ModDetailModel` を読む |
| JSON テンプレート | `ITemplateJson<T>`、`TemplateJsonParam` | 複雑なアルゴリズムパラメータを JSON で保持 |
| Flow 連携 | `Templates/Flow/`、`NodeConfigurator` | ノード設定画面からテンプレートを選択 |

## モデル選択

| 場面 | 推奨モデル |
| --- | --- |
| 安定した辞書ベースのパラメータ | `ITemplate<T>` + `ParamModBase` |
| 複雑でバージョン変更が多いパラメータ | `ITemplateJson<T>` + `TemplateJsonParam` |
| デバイス実行パラメータ | デバイスフォルダ内の `Templates/` |
| Flow テンプレート | `Templates/Flow/TemplateFlow` |
| 顧客出力形式 | プロジェクト `Process` / exporter |

## 追加手順

1. パラメータの所属を、共通アルゴリズム、デバイス、Flow、顧客プロジェクトのどれかに決めます。
2. MySQL テンプレートは `ParamModBase`、JSON テンプレートは `TemplateJsonParam` を継承します。
3. `Template*` を作成し、`ITemplate<T>` または `ITemplateJson<T>` を継承します。
4. 自動ロードする場合は `IITemplateLoad` を実装します。
5. 静的 `Params` を用意し、`TemplateParams` に代入します。
6. DB 復元が必要なら `GetMysqlCommand()` と `TemplateDicId` を確認します。
7. Flow または `Algorithm*` が参照するテンプレート ID/名前を正しく渡します。

## よくある問題

| 現象 | 最初に確認するもの |
| --- | --- |
| コンボボックスに出ない | `IITemplateLoad`、`TemplateParams`、`TemplateControl` |
| 再起動後に消える | `GetMysqlCommand()`、`TemplateDicId`、`SaveIndex`、MySQL 接続 |
| Flow が古い値を使う | ノード保存フィールド、テンプレート ID、テンプレート名 |
| アルゴリズムにパラメータが届かない | `TemplateParam` / `POITemplateParam` の書き込み |

## 受け入れ確認

- 作成、コピー、リネーム、インポート、エクスポート、削除ができる。
- 再起動後も保存値が残る。
- 新しいテンプレートで最小 Flow を実行できる。
- 履歴結果、overlay、表、プロジェクト出力が新しい結果を読める。
- 旧テンプレートも実行できる。

## 関連ドキュメント

- [Engine テンプレートと Flow チェーン](../../04-api-reference/engine-components/template-flow-chain.md)
- [Engine 結果表示とプロジェクト引き継ぎ](../../04-api-reference/engine-components/result-handoff-chain.md)
- [FlowEngineLib](../../04-api-reference/engine-components/FlowEngineLib.md)
- [テストと検証の引き継ぎ](../testing.md)
