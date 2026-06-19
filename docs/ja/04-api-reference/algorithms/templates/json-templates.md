# JSONテンプレート

このページでは、現在のウェアハウスで実際に利用可能な JSON テンプレート ホスト チェーンのみを説明しており、「ユニバーサル アルゴリズム DSL プラットフォーム + クロスプロジェクト構成フレームワーク」の古いドラフトは維持されません。

## まず、このモジュールが現在どのようなものであるかを見てみましょう

現在のソース コードの状況によると、JSON テンプレート システムはデータベースから独立して存在する構成プラットフォームではなく、`ColorVision.Engine` テンプレート システムの特定のブランチです。現在の中心的な目標は次のとおりです。

- JSON コンテンツを `ModMasterModel.JsonVal` でテンプレート アイテムとしてホストします。
- ユニバーサル エディタ `EditTemplateJson` によるテキスト編集と属性編集の 2 つのモードを提供します。
- 特定のテンプレート タイプで、`ITemplateJson<T>` の形式で同じロード、保存、インポート、エクスポート ロジックのセットを再利用できるようにします。
- `PoiAnalysis`、`SFRFindROI` などの JSON ドライバー テンプレートに統合ホストを提供します。

したがって、完全に独立した構成サブシステムというよりは、「データベース内の JSON テンプレート フォーク」に似ています。

## 現時点で最も重要なファイル

- `Engine/ColorVision.Engine/Templates/Jsons/ITemplateJson.cs`
- `Engine/ColorVision.Engine/Templates/Jsons/TemplateJsonParam.cs`
- `Engine/ColorVision.Engine/Templates/Jsons/EditTemplateJson.xaml`
- `Engine/ColorVision.Engine/Templates/Jsons/EditTemplateJson.xaml.cs`
- `Engine/ColorVision.Engine/Templates/Jsons/PoiAnalysis/TemplatePoiAnalysis.cs`
- `Engine/ColorVision.Engine/Templates/Jsons/SFRFindROI/TemplateSFRFindROI.cs`

「JSON テンプレートを今すぐ保存する方法、編集する方法、テンプレート ウィンドウにハングアップする方法」だけを知りたい場合は、これらのファイルで本体はすでにカバーされています。

## 現在の JSON サブテンプレート一覧

`Jsons/` 配下は 1 種類のテンプレートではなく、同じ JSON ホストを共有する複数の具体的なアルゴリズムテンプレートです。現在のソースでは次のように整理できます。

| ディレクトリ | テンプレート/辞書 | アルゴリズムイベント | 結果/メニュー | 引き継ぎポイント |
| --- | --- | --- | --- | --- |
| `LedCheck2/` | `TemplateLedCheck2`、`TemplateDicId = 18`、`Code = FindLED` | `Event_OLED_FindDotsArrayMem_GetData` | 専用 handler なし | LED ドット配列 V2 JSON テンプレート。schema は `FindLED.schema.json`。 |
| `LEDStripDetectionV2/` | `TemplateLEDStripDetectionV2`、`TemplateDicId = 26`、`Code = LEDStripDetection` | `LEDStripDetection`、`Version = 2.0` | `ViewHandleLEDStripDetectionV2`、`MenuLEDStripDetectionV2` | LED ストリップ V2 経路。旧強型 `LEDStripDetection/` と区別する。 |
| `OLEDAOI/` | `TemplateOLEDAOI`、`TemplateDicId = 28`、`Code = OLED.AOI` | `OLEDAOI`、`Version = 2.0` | `ViewHandleOLEDAOI`、`MenuOLEDAOI` | OLED AOI 主テンプレート。黒画面、四合一、再判定の子テンプレートもある。 |
| `BinocularFusion/` | `TemplateBinocularFusion`、`TemplateDicId = 35`、`Code = ARVR.BinocularFusion` | `ARVR.BinocularFusion` | `ViewHandleBinocularFusion` | ARVR 双眼融合 JSON テンプレート。 |
| `SFRFindROI/` | `TemplateSFRFindROI`、`TemplateDicId = 36`、`Code = ARVR.SFR.FindROI` | `ARVR.SFR.FindROI` | `ViewHandleSFRFindROI` | SFR ROI 検出。ARVR/SFR 系と一緒に確認することが多い。 |
| `BlackMura/` | `TemplateBlackMura`、`TemplateDicId = 37`、`Code = BlackMura.Caculate` | `BlackMura.Caculate` | `ViewHandleBlackMura` | BlackMura 計算テンプレートと結果表示。 |
| `Ghost2/` | `TemplateGhostQK`、`TemplateDicId = 38`、`Code = ghost` | `Ghost`、`Version = 2.0` | `ViewHandleGhostQK`、`MenuGhost2` | Ghost V2。handler は結果バージョン `2.0` に依存する。 |
| `FOV2/` | `TemplateDFOV`、`TemplateDicId = 39`、`Code = FOV` | `FOV`、`Version = 2.0` | `ViewHandleDFOV` | DFOV/FOV V2 JSON 経路。 |
| `Distortion2/` | `TemplateDistortion2`、`TemplateDicId = 40`、`Code = distortion` | `Distortion`、`Version = 2.0` | `ViewHandleDistortion2` | 歪み V2。handler は結果バージョン `2.0` に依存する。 |
| `BuildPOIAA/` | `TemplateBuildPOIAA`、`TemplateDicId = 41`、`Code = BuildPOI` | `ARVR.AA.FindPoints`、`Version = 2.0` | 専用 handler なし | AA 検出結果から POI を構築する JSON テンプレート。 |
| `AAFindPoints/` | `TemplateAAFindPoints`、`TemplateDicId = 42`、`Code = FindLightArea` | `ARVR.AA.FindPoints`、`Version = 2.0` | `ViewHandleAAFindPoints` | AA 検出/発光領域 V2。handler も結果バージョンを見る。 |
| `PoiAnalysis/` | `TemplatePoiAnalysis`、`TemplateDicId = 44`、`Code = PoiAnalysis` | `PoiAnalysis`、`Version = 1.0` | `ViewHandlePoiAnalysis` | POI 分析 JSON テンプレート。バージョンはまだ `1.0`。 |
| `FindCross/` | `TemplateFindCross`、`TemplateDicId = 45`、`Code = FindCross` | `FindCross` | `ViewHandleFindCross` | 十字検出テンプレート。handler は現在結果バージョン `1.0` を確認する。 |
| `MTF2/` | `TemplateMTF2`、`TemplateDicId = 48`、`Code = MTF` | `MTF`、`Version = 2.0` | `ViewHandleMTF2` | MTF V2。旧 ARVR/MTF テンプレートと区別する。 |
| `SFR2/` | `TemplateSFR2`、`TemplateDicId = 49`、`Code = SFR` | `SFR`、`Version = 2.0` | `ViewHandleSFR2` | SFR V2。旧 ARVR/SFR テンプレートと区別する。 |
| `ImageROI/` | `TemplateImageROI`、`TemplateDicId = 52`、`Code = Image.ROI` | `Image.ROI` | 専用 handler なし | JSON 画像 ROI。強型の [ImageCropping 画像切り抜きテンプレート](./image-cropping-template.md) とは別経路。 |
| `KB/` | `TemplateKB`、`TemplateDicId = 150`、`Code = KB` | `KB` | `ViewHandleKB` | KB プロジェクト/アルゴリズム関連の JSON テンプレート。 |
| `Deprecated/` | `TemplateCaliAngleShift`、`TemplateCompoundImg` | `CaliAngleShift`、`CompoundImg` | 旧 handler | 互換用の履歴ディレクトリ。新規機能では優先して拡張しない。 |

`Schemas/schema-index.json` は現在の schema インデックスです。`FindLED.schema.json`、`LEDStripDetection.schema.json`、`OLED.AOI.schema.json`、`ARVR.SFR.FindROI.schema.json`、`SFR.schema.json`、`Image.ROI.schema.json` などを参照します。JSON テンプレートを追加するときは、対応する schema を配置し、必要に応じて schema index へ登録します。

## V2 と旧強型テンプレートの境界

多くのディレクトリ名に `2` または `V2` が含まれますが、結果 handler に本当に影響するのはディレクトリ名ではなく、リクエストパラメータと結果バージョンです。

| テンプレート群 | 現在の JSON 経路 | 旧/強型経路 | 引き継ぎ境界 |
| --- | --- | --- | --- |
| LED 点/ストリップ | `LedCheck2/`、`LEDStripDetectionV2/` | `LedCheck/`、`LEDStripDetection/` | V2 は主に JSON schema と `Version = 2.0` を使う。旧テンプレート項目と混ぜない。 |
| MTF/SFR/FOV/Ghost/Distortion | `MTF2/`、`SFR2/`、`FOV2/`、`Ghost2/`、`Distortion2/` | `ARVR/MTF`、`ARVR/SFR`、`ARVR/FOV`、`ARVR/Ghost`、旧歪みテンプレート | handler は多くの場合 `result.Version` で分岐する。結果表示を調べるときは必ずバージョンを見る。 |
| ROI/切り抜き | `ImageROI/`、`SFRFindROI/` | `ImageCropping/`、`FindLightArea/`、`POI/` | JSON ROI と強型切り抜きは別チェーンで、パラメータ元と結果テーブルも異なる。 |
| OLED AOI | `OLEDAOI/` と子ディレクトリ | プロジェクトパッケージまたは旧 OLED ノード | 主テンプレートと黒画面/四合一/再判定子テンプレートは AOI 領域を共有するが、イベント名と schema は異なる。 |

同じアルゴリズム名に旧テンプレートと JSON テンプレートの両方がある場合は、「テンプレート種別 -> MQTT イベント -> Version -> ViewHandle」の順に現在の経路を確認してください。

## 現在のメインチェーンを実行する方法

### ホスト基本クラス

`ITemplateJson<T>` は、JSON テンプレート ブランチのユニバーサル ホストです。現在、次のことを担当しています。

- `TemplateDicId` を使用して MySQL から `ModMasterModel` を読み取ります
- 各レコードを `TemplateModel<T>` にラップします
- 保存、削除、コピー、インポート、エクスポートを提供します
- 新しいテンプレートを作成するときに、辞書テンプレートのデフォルト JSON から初期コンテンツを生成します

これは、JSON テンプレートはプレーン テキスト編集のように見えますが、現時点では依然としてテンプレート ディクショナリとデータベース レコードに大きく依存していることを意味します。

### パラメータオブジェクト

`TemplateJsonParam` は、現在最も基本的な JSON テンプレート パラメーター オブジェクトです。それは以下を保持します:

- `TemplateJsonModel`
- `ResetCommand`
- `CheckCommand`
- `JsonValueChanged` イベント

ここで、`JsonValue` の実際のセマンティクスは次のとおりです。

- 読み込み時に`JsonHelper.BeautifyJson(...)`でフォーマット
- JSON が有効な場合のみライトバックします `TemplateJsonModel.JsonVal`

`ResetValue()` は、ローカル テキストを単にクリアするのではなく、辞書テンプレートに記録されているデフォルトの JSON に戻ります。

### エディター コントロール

`EditTemplateJson` は、現在の実際の編集エントリです。現在は両方をサポートしています。

- AvalonEditテキストモード
- `JsonPropertyEditorControl` 属性モード
- 説明アノテーションビュー切り替え
- 確認ボタン
- 外部JSON Webサイト補助入口

右下隅にある `json` ボタンの現在の実際の動作は非常に明確です。

- `https://www.json.cn/` を開きます
- 現在の JSON をクリップボードにコピーします

これは、他の隠しコマンドではなく、現在アクティブなファイルの `Button_Click_1` の実際の機能です。

### モードの切り替えと同期

`EditTemplateJson` は現在、単純なテキストボックス ラッパーではありません。それは次のことを行います:

- テキストの変更をデバウンスタイマーと同期します
- `IEditTemplateJson.JsonValueChanged` を介したリバース リフレッシュ インターフェイス
- テキストモードと属性モードを切り替えるときに JSON コンテンツを同期します
- `EditTemplateJsonConfig` を使用して幅とデフォルトの編集モードを記憶します

したがって、ここでの複雑さは主に、アルゴリズム自体ではなく、「両方の編集側で同じ JSON の一貫性を保つ」ことにあります。

## 現在、最もよくある間違いのいくつかが犯されています

### これはユニバーサル ファイル テンプレート プラットフォームではありません

JSON テンプレートの現在のプライマリ ストレージは、ウェアハウス内の任意の JSON ファイルのセットではなく、MySQL の `ModMasterModel.JsonVal` です。そのまま「ディスク構成ディレクトリを読み込む」と書き続けると、実際の実装から外れてしまいます。

### すべての JSON テンプレートが同じビジネス スキーマを共有するわけではありません

`ITemplateJson<T>` はホスト チェーンのみを提供します。それぞれの特定のテンプレートに必要な実際のフィールドは、それぞれの JSON 規約によって決まります。ドキュメントでは、特定のタイプの JSON 構造をシステム全体の統一仕様に書き込むことができなくなりました。

### エディターは単なるテキストエディターではなくなりました

現在、`EditTemplateJson` は属性モードと説明モードの間の切り替えをすでにサポートしています。 AvalonEdit テキスト ボックスについて説明するだけでは、ユーザーが実際に見る機能の半分が失われます。

### 「検証」は現在主にイベントトリガーであり、完全なコンパイラーではありません

`CheckCommand` は `JsonValueChanged` イベント チェーンをトリガーし、特定の応答は呼び出し元によって異なります。スタンドアロンの JSON ルール エンジンとして記述しないでください。

### Deprecated ディレクトリは新機能の入口ではありません

`Deprecated/` には `CaliAngleShift`、`CompoundImg` などの旧テンプレートと handler が残っています。履歴データ互換のための場所なので、新機能、現場引き継ぎ、新プロジェクト説明では、旧フロー保守であることが明確な場合を除き優先して参照しません。

## 受け入れ確認

| 場面 | 必須確認 |
| --- | --- |
| JSON 編集 | テキストモードと属性モードを切り替えても JSON フィールドが失われない |
| schema 保守 | schema を追加または変更した後、`Schemas/schema-index.json` から該当ファイルをたどれる |
| V2 アルゴリズム実行 | MQTT パラメータの `TemplateParam`、`Version`、イベント名がサーバー側の期待と一致する |
| 結果表示 | `ViewHandle*.cs` の `CanHandle1` またはバージョン判定が実結果に一致する |
| インポート/エクスポート | JSON テンプレートをエクスポートして再インポートしたとき、名前、`Code`、既定値、JSON 内容が正しい |

## 推奨される読む順序

1. `Engine/ColorVision.Engine/Templates/Jsons/ITemplateJson.cs`
2. `Engine/ColorVision.Engine/Templates/Jsons/TemplateJsonParam.cs`
3. `Engine/ColorVision.Engine/Templates/Jsons/EditTemplateJson.xaml.cs`
4. `Engine/ColorVision.Engine/Templates/Jsons/PoiAnalysis/TemplatePoiAnalysis.cs`
5. `Engine/ColorVision.Engine/Templates/Jsons/SFRFindROI/TemplateSFRFindROI.cs`

## 続きを読む

- [テンプレート API リファレンス](./api-reference.md)
- [テンプレート管理](./template-management.md)
- [ColorVision.Engine](../../engine-components/ColorVision.Engine.md)
