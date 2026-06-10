# Engine テンプレートと Flow チェーン

このページは、テンプレートがどのように編集可能、保存可能、実行可能な Flow になるかを説明します。

## メインチェーン

```text
TemplateControl / TemplateModel<T>
  -> TemplateFlow
  -> FlowEngineControl / FlowControl
  -> NodeConfiguratorRegistry
  -> Flow node execution
  -> FlowCompleted
  -> batch / result / Projects
```

## テンプレート種別

| 種別 | 主な場所 | 引き継ぎポイント |
| --- | --- | --- |
| JSON テンプレート | `Templates/Jsons/` | パラメータ、既定値、互換性 |
| POI / アルゴリズムテンプレート | `Templates/POI/`, `Templates/ARVR/` | 結果 key、表示モデル、DAO |
| Flow テンプレート | `Templates/Flow/` | ノード、接続、`.cvflow` 保存/インポート |
| デバイス動作テンプレート | `Services/Devices/**` またはテンプレート配下 | デバイス命令、MQTT、タイムアウト |

## NodeConfigurator の役割

`NodeConfiguratorRegistry` は、ノードが UI で何を設定できるかを決めます。実行ノードだけを追加して設定器を追加しないと、ノードは存在してもテンプレートやデバイスを正しく選べません。

確認項目:

- ノード種別が登録されている。
- 選択可能なテンプレート種別が正しい。
- 選択可能なデバイス種別が正しい。
- 保存後にパラメータを再読み込みできる。
- 旧 `.cvflow` のインポート互換性がある。

## よく使うテンプレート接続点

| テンプレート/入口 | Flow 引き継ぎ点 |
| --- | --- |
| [FocusPoints フォーカスポイントテンプレート](../algorithms/templates/focus-points-template.md) | `AlgorithmNode` の発光領域検出が `operatorCode = "FocusPoints"` に対応します。 |
| [ImageCropping 画像クロッピングテンプレート](../algorithms/templates/image-cropping-template.md) | `AlgorithmType.图像裁剪` と `OLEDImageCroppingNode` が `TemplateImageCropping` をバインドします。 |
| [テンプレートメニュー入口](../algorithms/templates/template-menu-entries.md) | メニューは編集画面を開くだけで、Flow ノードで選べるかは `NodeConfigurator` が決めます。 |

## Flow 実行の受け入れ

1. Flow を新規作成し、ノードを追加して保存します。
2. 閉じて再度開き、ノードとパラメータを確認します。
3. 既存 `.cvflow` をインポートします。
4. 実行し、開始/終了ノードの状態を確認します。
5. `FlowCompleted` を確認します。
6. バッチ、結果表、プロジェクトが結果を読めることを確認します。

## よくある問題

| 現象 | 可能性 |
| --- | --- |
| 保存後にノードパラメータが消える | モデル項目未シリアライズ、プロパティ名変更 |
| ノードは動くが結果が無い | 結果 key 不一致、DAO 未保存 |
| Flow インポート失敗 | ノード型名、テンプレート ID、バージョン互換性 |
| デバイスノードを選べない | `NodeConfigurator` フィルタ、サービス未生成 |
