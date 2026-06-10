# 現在のアルゴリズムテンプレートカバレッジ

このページは、実際の `Engine/ColorVision.Engine/Templates/` ディレクトリと現在のドキュメント入口を対応付けます。すべてのアルゴリズム仕様を保証する表ではなく、引き継ぎ時に「このテンプレート ディレクトリはどこから読むか、何がまだ不足しているか」を判断するための地図です。

## カバレッジ状態

| 状態 | 意味 |
| --- | --- |
| 専用ページあり | 主な入口、実行チェーン、境界を説明する引き継ぎ向けページがある。 |
| 横断カバー | テンプレート管理、ROI/POI、共通アルゴリズム、Engine チェーンのページで現在カバーしている。 |
| 専用ページ待ち | 所属は明確だが、業務意味や受け入れ基準は後で独立ページにする必要がある。 |

## Templates ディレクトリカバレッジ

| テンプレート ディレクトリ | 業務上の役割 | 先に読むドキュメント | 状態 | 引き継ぎの焦点 |
| --- | --- | --- | --- | --- |
| `ARVR/` | AR/VR 検査テンプレート群。パラメータ、アルゴリズム要求、結果表示をつなぐ。 | [ARVR テンプレート](./templates/arvr-template.md)、[結果引き継ぎチェーン](../engine-components/result-handoff-chain.md) | 専用ページあり | テンプレートマトリクス、手動イベント、Flow `operatorCode`、POI 依存、結果表、handler、受け入れ確認をカバー。 |
| `BuzProduct/` | 製品マスター、詳細、POI、Validate ルールを結び付ける製品/業務テンプレート。 | [BuzProduct 製品業務パラメータテンプレート](./templates/buz-product-template.md)、[Validate 判定ルールテンプレート](./templates/validate-rules.md) | 専用ページあり | `BuzProduc` のソース綴り、マスター/詳細表、`poi_id`、`val_rule_temp_id` を追う。 |
| `Compliance/` | Y/XYZ/JND 結果と `ValidateResult` を読む結果表示/判定解釈層。 | [Compliance 結果引き継ぎ](./templates/compliance-results.md)、[結果引き継ぎチェーン](../engine-components/result-handoff-chain.md) | 専用ページあり | 三つの結果詳細表、handler タイプ対応、`ValidateRuleResultType.M` 判定を追う。 |
| `DataLoad/` | Flow の DataLoad ノードへデバイス、シリアル番号、結果タイプ、ZIndex を渡すデータロード テンプレート。 | [DataLoad データロードテンプレート](./templates/data-load-template.md)、[テンプレートと Flow チェーン](../engine-components/template-flow-chain.md) | 個別ページあり | `AlgDataLoadNode` のテンプレート経路と `AlgDataLoadNode2` の明示パラメータ経路を区別する。 |
| `FindLightArea/` | 発光領域/ROI 検出テンプレート。OpenCV helper と ROI 出力に関係する。 | [FindLightArea 発光領域テンプレート](./templates/find-light-area.md)、[ROI プリミティブ](./primitives/roi.md) | 専用ページあり | `Event_LightArea2_GetData`、`RoiParam`、点テーブル、凸包オーバーレイ。 |
| `Flow/` | テンプレート システムと `FlowEngineLib` の可視化フローを接続する。 | [フロー エンジン](./templates/flow-engine.md)、[Engine テンプレートと Flow チェーン](../engine-components/template-flow-chain.md) | 専用ページあり | `TemplateFlow` の保存経路、`.cvflow` パッケージ、インポート/エクスポート、実行スケジューリング、ノード設定境界をカバー。 |
| `FocusPoints/` | 旧発光領域/フォーカスポイントのパラメータテンプレート。二値化、フィルタ、形態処理、ROI 境界を保存。 | [FocusPoints フォーカスポイントテンプレート](./templates/focus-points-template.md)、[FindLightArea 発光領域テンプレート](./templates/find-light-area.md) | 個別ページあり | 手動 `Event_LightArea_GetData` と Flow `operatorCode = "FocusPoints"` を区別する。 |
| `ImageCropping/` | 四点 ROI、Flow 二入力クロッピング、クロップ結果表示をつなぐ強型テンプレート。 | [ImageCropping 画像クロッピングテンプレート](./templates/image-cropping-template.md)、[結果引き継ぎチェーン](../engine-components/result-handoff-chain.md) | 個別ページあり | `Event_Image_Cropping`、`OLED.GetRIAand`、`ROI_MasterId`、`ViewHandleImageCropping` を追跡する。 |
| `JND/` | JND 関連検査テンプレート。AR/VR や表示品質業務に関係する。 | [JND テンプレート](./templates/jnd-template.md)、[POI テンプレート](./templates/poi-template.md) | 専用ページあり | `CutOff`、`POITemplateParam`、`h_jnd/v_jnd`、プロジェクト OK/NG 境界。 |
| `Jsons/` | JSON テンプレート体系。テキスト/プロパティ編集とインポート/エクスポートを提供する。 | [JSON テンプレート](./templates/json-templates.md)、[Templates API リファレンス](./templates/api-reference.md) | 専用ページあり | 現在の JSON サブテンプレート一覧、schema index、V2/旧強型境界、handler、受け入れ確認をカバー。 |
| `LedCheck/` | LED 検査テンプレート群。LED、輝度、欠陥チェックに使う。 | [LED 検出テンプレート](./templates/led-detection.md)、[POI テンプレート](./templates/poi-template.md) | 専用ページあり | `FindLED` 新旧入口、POI 依存、handler 登録、エクスポート境界。 |
| `LEDStripDetection/` | LED ストリップ検出テンプレート。JSON、帯状領域、欠陥結果に関係する。 | [LED 検出テンプレート](./templates/led-detection.md)、[JSON テンプレート](./templates/json-templates.md) | 専用ページあり | 旧強型 `Event_LED_StripDetection` と JSON V2 `Version = 2.0` の区別。 |
| `Matching/` | 手動 UI、Flow ノード、MQTT 要求、AOI 結果表示を含むテンプレートマッチング/位置決めチェーン。 | [Matching テンプレートマッチング](./templates/matching-template.md)、[結果引き継ぎチェーン](../engine-components/result-handoff-chain.md) | 個別ページあり | `MatchTemplate`、`TemplateFile`、`t_scgd_algorithm_result_detail_aoi`、四点 overlay を追跡する。 |
| `Menus/` | テンプレートメニューの分岐、親子関係、既定編集ウィンドウを定義する入口ラッパー。 | [テンプレートメニュー入口](./templates/template-menu-entries.md)、[テンプレート管理](./templates/template-management.md) | 個別ページあり | `OwnerGuid`、`Order`、`Header`、`Template`、`ShowTemplateWindow()` を追跡する。 |
| `POI/` | POI テンプレート群。点、領域、上流パラメータを提供する。 | [POI テンプレート](./templates/poi-template.md)、[POI プリミティブ](./primitives/poi.md) | 専用ページあり | 主/伴生テンプレート、専用点表、実行パラメータ、BuildPOI、Flow 消費、結果 handler をカバー。 |
| `SysDictionary/` | `mod_type = 7` のアルゴリズム既定辞書マスターと明細を保守するシステム辞書テンプレート。 | [SysDictionary システム辞書テンプレート](./templates/sys-dictionary-template.md)、[Templates API リファレンス](./templates/api-reference.md) | 個別ページあり | `TemplateModParam`、`symbol`、`default_val`、`val_type`、移行境界を追跡する。 |
| `Validate/` | 既定合規辞書と実判定テンプレートの二層を持つ判定ルール体系。 | [Validate 判定ルールテンプレート](./templates/validate-rules.md)、[テンプレート管理](./templates/template-management.md) | 専用ページあり | `mod_type = 110/111/120`、`CIEParams/JNDParams`、ルール主/詳細表を追う。 |

## 主要入口ファイル

| ファイル | 引き継ぎ用途 |
| --- | --- |
| `TemplateContorl.cs` | テンプレート検出、`IITemplateLoad` ロード、登録入口。 |
| `TemplateManagerWindow.xaml(.cs)` | テンプレート管理画面。UI 操作からテンプレート データを追う入口。 |
| `TemplateEditorWindow.xaml(.cs)` | 汎用テンプレート編集画面。プロパティ編集、保存、検証を追う入口。 |
| `TemplateSearchProvider.cs` | テンプレート検索入口。検索できない原因調査に使う。 |
| `TemplateSampleLibrary.cs` | テンプレート サンプルと再利用入口。既定テンプレート来源を追う。 |

## メンテナンス規則

- 新しい `Templates/<Name>/` ディレクトリを追加したら、まずこの表に行を追加し、専用ページが必要か判断する。
- `Algorithm*`、結果ビュー、MQTT 実行要求を含む場合、パラメータ来源、実行サービス、結果フィールド、失敗処理を書く。
- メニュー、辞書、ラッパーだけのディレクトリでも、どのテンプレート群に仕えるかを書く。
- “専用ページ待ち”のディレクトリが顧客納品、DLL リリース、現場受け入れ対象に入ったら、先に独立ページへ昇格する。

## 次の優先補完

1. Flow 変換/校正ノードは [Flow 変換と校正ノード](../engine-components/flow-conversion-calibration-nodes.md) に移しました。現在の source tree には `Templates/FileConvert/`、`Templates/ImageTransform/`、`Templates/Calibration/` がないため、今後は node chain として保守します。
2. `Menus/`、`SysDictionaryMod/`: メニュー入口、辞書既定値、テンプレートウィンドウ登録関係を引き継ぎチェックリスト化する。
3. `Projects/` 配下の未整理な顧客プロジェクト: 業務入口、依存テンプレート、プラグイン能力、現場受け入れ基準をそろえる。
