# LED 検出テンプレート

このページは LED 検出関連テンプレートの引き継ぎ境界を説明します。主に `LEDStripDetection/` と `LedCheck/` の強型テンプレートを扱い、`Jsons/LEDStripDetectionV2/`、`Jsons/LedCheck2/` との関係も示します。

## 4 つの入口

| 入口 | 種類 | コード/イベント | 用途 |
| --- | --- | --- | --- |
| `LEDStripDetection/` | 強型テンプレート | `Code = LEDStripDetection`、`Event_LED_StripDetection` | 旧 LED ストリップ定位。 |
| `LedCheck/` | 強型テンプレート | `Code = FindLED`、`Event_LED_Check_GetData` | LED 点検出。POI に依存し、円を描画する。 |
| `Jsons/LEDStripDetectionV2/` | JSON テンプレート | `Code = LEDStripDetection`、イベント名 `LEDStripDetection`、`Version = 2.0` | 新しい LED ストリップ/POI 中心計算。 |
| `Jsons/LedCheck2/` | JSON テンプレート | `Code = FindLED`、`Event_OLED_FindDotsArrayMem_GetData` | サブピクセル OLED 点配列検出。 |

`Code` だけで実装を一意に判断しないでください。`LEDStripDetection` と `FindLED` は旧強型実装と新 JSON 実装が併存します。

## 強型 LEDStripDetection

| ファイル | 引き継ぎ用途 |
| --- | --- |
| `TemplateLEDStripDetection.cs` | テンプレート登録。`TemplateDicId = 21`、`IsUserControl = true`。 |
| `LEDStripDetectionParam.cs` | 点数、点距離、開始位置、二値化割合、デバッグ、保存先を保持する。 |
| `EditLEDStripDetection.xaml(.cs)` | カスタム パラメータ エディタ。 |
| `AlgorithmLEDStripDetection.cs` | `Event_LED_StripDetection` 要求を作る。 |
| `DisplayLEDStripDetection.xaml(.cs)` | テンプレート、画像、バッチ/Raw/ローカルファイルを選択し実行する。 |

要求には `ImgFileName`、`FileType`、`DeviceCode`、`DeviceType`、`TemplateParam`、`IsInversion` が含まれます。

## 強型 LedCheck

| ファイル | 引き継ぎ用途 |
| --- | --- |
| `TemplateLedCheck.cs` | LED 点検出を登録し、`Code = FindLED`。 |
| `LedCheckParam.cs` | チャンネル、固定半径、輪郭面積、二値化補正、グリッド数などを保持する。 |
| `AlgorithmLedCheck.cs` | LED テンプレートと POI テンプレートを集め、`Event_LED_Check_GetData` を送る。 |
| `DisplayLedCheck.xaml(.cs)` | LED テンプレート、POI テンプレート、画像を選ぶ。 |
| `ViewHandleMTF.cs` | POI 結果から点を復元し、円を描画する。 |
| `ViewResultLedCheck.cs` | 点と半径を保存する。 |

`LedCheck` は `TemplateParam` に加えて `POITemplateParam` を送ります。UI は `TemplatePoi.Params.CreateEmpty()` を使うため、現場で空 POI を許すか確認してください。

`ViewHandleLedCheck.CanHandle` は現在空です。実行成功後に結果表示が接管されない場合、まず結果タイプ登録を確認します。

## JSON V2 入口

- `TemplateLEDStripDetectionV2`: `TemplateDicId = 26`、`Name = LedStripDetectionV2`、`debugCfg`、`mathMaskRect`、`nV1`、`threshold`、`dRatio`、`pattern`、`CalcMethod` を含む JSON。
- `AlgorithmLEDStripDetectionV2`: イベント名 `LEDStripDetection`、`Version = 2.0`、必要に応じて `POITemplateParam` を送る。
- `TemplateLedCheck2`: `TemplateDicId = 18`、`Code = FindLED`。
- `AlgorithmLedCheck2`: `Event_OLED_FindDotsArrayMem_GetData`、`Color`、`FDAType`、4 つの `FixedLEDPoint` を送る。

## どの入口を使うか

| 目的 | 推奨入口 |
| --- | --- |
| 旧 LED ストリップ定位を保守 | `LEDStripDetection/` |
| 複雑な LED ストリップ JSON パラメータを追加 | `Jsons/LEDStripDetectionV2/` |
| 従来 LED 点検出と POI 半径表示を保守 | `LedCheck/` |
| サブピクセル OLED 点配列検出 | `Jsons/LedCheck2/` |
| 結果表示の調査 | handler の結果タイプ登録を先に確認する。 |

## トラブルシュート

| 現象 | 先に確認するもの |
| --- | --- |
| テンプレートが空 | `TemplateLEDStripDetection.Params` と `TemplateDicId = 21`。 |
| V2 テンプレートが空 | `TemplateLEDStripDetectionV2` と `TemplateDicId = 26` の JSON 辞書。 |
| LED 点検出に失敗 | `TemplateParam`、`POITemplateParam`、画像タイプ、デバイス `Code/Type`。 |
| JSON 変更が効かない | V2 JSON を編集したか、旧強型テンプレートを編集したか確認する。 |
| 結果が表示されない | `ViewResultAlgType` と handler 登録。 |
| CSV が不正 | `ViewHandleLedCheck.SideSave(...)` は別途受け入れ確認が必要。 |

## 引き継ぎチェック

- `Code = LEDStripDetection` の変更は旧強型か JSON V2 か明記する。
- `Code = FindLED` の変更は `LedCheck` か `LedCheck2` か明記する。
- 強型パラメータ変更時はパラメータ クラス、既定値、編集 UI、現場サンプルを更新する。
- JSON パラメータ変更時は schema/説明 JSON、`Mysql*` 復元、バージョン方針を更新する。
- 結果表示変更時は handler、エクスポート、プロジェクト受け入れ、スクリーンショット例を同時に更新する。

## 続けて読む

- [JSON テンプレート](./json-templates.md)
- [POI テンプレート](./poi-template.md)
- [結果引き継ぎチェーン](../../engine-components/result-handoff-chain.md)
- [現在のアルゴリズムテンプレートカバレッジ](../current-algorithm-template-coverage.md)
