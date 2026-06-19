# アルゴリズムとテンプレートの概要

この章は現在、「テンプレート システムとアルゴリズムのアクセス チェーン」の紹介に凝縮されており、すべての画像処理方法を百科事典のカタログに統合するという古い書き方は維持されなくなります。

## この章は何について話しているのでしょうか?

ここでの「アルゴリズム」は、ウェアハウス内のすべての基礎となる画像処理コードの合計リストではなく、主に `Engine/ColorVision.Engine/Templates/` とその周囲のアクセス チェーンに対応します。現在の優先事項は次のとおりです。

- テンプレートがどのように検出、ロード、管理、編集されるか。
- フローテンプレートを`FlowEngineLib`に接続する方法。
- 特別なエディターを使用して JSON テンプレートをシステムに入力する方法。
- ARVR や POI などのビジネス テンプレート ファミリをアルゴリズム サービスに接続する方法。

OpenCV レベルで低レベルの処理関数を探している場合、エントリ ポイントは通常この章ではなく、`Engine/cvColorVision/`、`UI/ColorVision.Core/`、またはネイティブ DLL 側に近い場所にあります。

## 現在の章の構造

### エントリーページ

- [アルゴリズム システムの概要](./overview.md): 現在の実装チェーンの全体的な説明。最初にこのページを読むと時間を節約できます。
- [現在のアルゴリズムテンプレートカバレッジ](./current-algorithm-template-coverage.md): 実際の `Templates/` ディレクトリをドキュメント入口と不足項目に対応付けます。

### スペシャルカタログ

- `templates/`: テンプレート管理、プロセス テンプレート、JSON テンプレート、POI/ARVR、FindLightArea、JND、LED 検出、BuzProduct、Validate、Compliance、DataLoad、Matching、SysDictionary、FocusPoints、ImageCropping、テンプレートメニューなどのページ。
- `detectors/`: 少数の欠陥/検出トピック。
- `primitives/`: いくつかの基本コンポーネントの説明。

これらのディレクトリにはまだ歴史的なページがいくつかありますが、それらはこの章のホームページに安定した入り口として配置されていません。

## 最初に知っておく価値のあるコードアンカー

現在の状況から判断すると、次の種類のファイルは、テンプレートとアルゴリズム リンクについて知っておく価値があります。

- `Templates/TemplateContorl.cs`: テンプレートの検出と登録の入り口。
- `Templates/TemplateManagerWindow.xaml.cs`: テンプレート管理ウィンドウ。
- `Templates/TemplateEditorWindow.xaml.cs`: テンプレート編集ウィンドウ。
- `Templates/Flow/TemplateFlow.cs`: プロセス テンプレートとプロセス エディターのアクセス ポイント。
- `Templates/Jsons/ITemplateJson.cs`: JSON テンプレートの共通のロード/インポート/エクスポート ロジック。
- `Templates/Jsons/EditTemplateJson.xaml(.cs)`: JSON テンプレート編集コントロール。テキスト/属性の 2 つの編集モードを担当します。
- `Templates/POI/AlgorithmImp/AlgorithmPOI.cs`、`Templates/ARVR/*/Algorithm*.cs`: 典型的なビジネス アルゴリズム UI とメッセージ アセンブリの入り口。

## 現在のいくつかのキー境界

- 多くの `Algorithm*` クラス自体は、最終的なコンピューティング コアではありません。現在、これらはテンプレート パラメーター、ファイル パス、デバイス情報を収集し、MQTT/サービス チェーンを通じて実行リクエストを発行することにより責任を負います。
- `POI` は独立したトピックではなく、現在のコード内の複数のアルゴリズム ファミリによって共有される上流のテンプレートおよびパラメーター ソースです。
- `Flow` テンプレートにはさまざまな表現形式がありますが、依然として同じテンプレート システムの一部であり、通常のテンプレート チェーンから完全に分離すべきではありません。
- 現在、JSON テンプレートと従来の厳密に型指定されたテンプレートが共存しています。読み取るときは、システムがテンプレート定義メソッドを 1 つだけ予約していると想定しないでください。

## 推奨される読む順序

1. まず、[アルゴリズム システムの概要](./overview.md) を読んで、実行時のメイン チェーンの認識を確立します。
2. 次に、[現在のアルゴリズムテンプレートカバレッジ](./current-algorithm-template-coverage.md) で各 `Templates/` サブディレクトリの入口を確認します。
3. 次に、[テンプレート モジュール分析](../../03-architecture/components/templates/analysis.md) と比較して、ディレクトリと登録エントリを理解します。
4. プロセス テンプレートについて懸念がある場合は、[FlowEngineLib Architecture](../../03-architecture/components/engine/flow-engine.md) を参照してください。
5. 最後に、[FindLightArea 発光領域テンプレート](./templates/find-light-area.md)、[JND テンプレート](./templates/jnd-template.md)、[LED 検出テンプレート](./templates/led-detection.md)、[BuzProduct 製品業務パラメータテンプレート](./templates/buz-product-template.md)、[Validate 判定ルールテンプレート](./templates/validate-rules.md)、[Compliance 結果引き継ぎ](./templates/compliance-results.md)、[DataLoad データロードテンプレート](./templates/data-load-template.md)、[Matching テンプレートマッチング](./templates/matching-template.md)、[SysDictionary システム辞書テンプレート](./templates/sys-dictionary-template.md)、[FocusPoints フォーカスポイントテンプレート](./templates/focus-points-template.md)、[ImageCropping 画像クロッピングテンプレート](./templates/image-cropping-template.md)、[テンプレートメニュー入口](./templates/template-menu-entries.md) など、業務ドメイン別ページを読み、常にソース コードと照合します。
