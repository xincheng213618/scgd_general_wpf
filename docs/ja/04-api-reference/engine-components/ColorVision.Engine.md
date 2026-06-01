#ColorVision.Engine

このページでは、現在のウェアハウスで実際に利用可能な `ColorVision.Engine` モジュールのみを説明しており、「完全な API テーブル + 統合された階層化されたブループリント + 疑似サンプル」という古いドラフトは維持されなくなりました。

## まず、このモジュールが現在どのようなものであるかを見てみましょう

現在のソース コードの状況によると、`ColorVision.Engine` は単純なアルゴリズム ライブラリではなく、ColorVision メイン プログラムのコア エンジン アセンブリ層です。現在、少なくとも次のことを担当しています。

- デバイスとサービス オブジェクトのホスト側の抽象化。
- テンプレート システムのロード、編集、永続化。
- MQTT リクエスト、ハートビート、メッセージのログ記録。
- FlowEngineLib は、メイン プログラムの UI とテンプレートをブリッジします。
- アルゴリズム表示層とテンプレートエディタ間の接続。

したがって、すべてのビジネスをローカルで直接カウントする単一のモジュールではなく、「ランタイム エンジン ホスティング層」に近いものになります。

## 現時点で最も重要なファイル

- `Engine/ColorVision.Engine/Templates/TemplateContorl.cs`
- `Engine/ColorVision.Engine/Templates/Jsons/ITemplateJson.cs`
- `Engine/ColorVision.Engine/Templates/Jsons/EditTemplateJson.xaml.cs`
- `Engine/ColorVision.Engine/Templates/Flow/FlowEngineManager.cs`
- `Engine/ColorVision.Engine/Templates/Flow/DisplayFlow.xaml.cs`
- `Engine/ColorVision.Engine/Services/DeviceService.cs`
- `Engine/ColorVision.Engine/Services/Devices/DeviceServiceFactory.cs`
- `Engine/ColorVision.Engine/Services/Core/MQTTServiceBase.cs`
- `Engine/ColorVision.Engine/Services/RC/MQTTRCService.cs`
- `Engine/ColorVision.Engine/Templates/POI/AlgorithmImp/AlgorithmPOI.cs`
- `Engine/ColorVision.Engine/Templates/ARVR/MTF/AlgorithmMTF.cs`

メイン エンジンがテンプレート、デバイス、メッセージ チェーン、プロセスをどのように編成するかを知りたいだけの場合、これらのコードはすでにバックボーンをカバーしています。

## 現在のコントロール プレーンをブロックに分割する方法

### テンプレートの読み込みとテンプレートの登録

`TemplateControl` は、現在のテンプレート システムの一般的な入り口です。すべてのアセンブリで `IITemplateLoad` 実装をスキャンし、MySQL が利用可能になった後に `Load()` を実行して、テンプレート インスタンスを `ITemplateNames` に登録します。

これは、テンプレート システムが現在、手書きの静的リストの代わりに以下に依存していることを意味します。

- イニシャライザトリガー
- アセンブリスキャン
- テンプレートインスタンスレジストリ

3 つのステップが連続します。

### JSON テンプレートの編集

`ITemplateJson<T>` は、現在の JSON テンプレートの実際の配置ポイントを示します。

- テンプレートデータはMySQLから読み込まれます
- テンプレート オブジェクトは、`Activator.CreateInstance` を通じてパラメータ オブジェクトにパッケージ化されます。
- 保存と削除もデータベースに直接書き戻します

対応するエディター `EditTemplateJson` は以下を提供します。

- テキストモード
- プロパティ編集モード
- アノテーションビューの切り替え
- 外部の JSON 検証 Web サイトへの素早いアクセス

これは、エンジン レイヤーが現在テンプレートを保存するだけでなく、テンプレート編集 UI の一部を直接ホストしていることを示しています。

### プロセスブリッジ層

`FlowEngineManager` および `DisplayFlow` は、`ColorVision.Engine` および `FlowEngineLib` のブリッジ表面です。彼らは現在、次のことを担当しています。

- FlowのMQTTデフォルト設定を初期化します。
- プロセステンプレートのリストと現在の選択を維持する
- Base64 データを使用してテンプレートを `FlowEngineControl` にロードします
- `MqttRCService` のサービス トークンと組み合わせて、利用可能なサービス ノードを更新します
- プロセス編集、テンプレート編集、バッチレコード表示などの UI 操作を提供します。

したがって、メイン プログラムのプロセス関数は `FlowEngineLib` だけでは完了せず、実際にウィンドウとテンプレート システムに入るには、このブリッジ コード層を通過する必要があります。

### デバイスとサービスの抽象化

`DeviceService` は現在のホスト側デバイス オブジェクトの基本的な抽象化であり、次の役割を果たします。

- ツリーノードの動作
- アイコンとコンテキスト メニュー
- 設定のインポートとエクスポート
- リセット、再起動、およびプロパティのコマンド
- MQTT サービス オブジェクトまたはインジケーター コントロールへのフック

また、`DeviceServiceFactoryRegistry` は、Camera、PG、Spectrum、SMU、Sensor などのサービス タイプをファクトリとして統一的に登録します。

これは、現在のデバイスのインスタンス化が分散したスイッチケースではなく、集中化されたファクトリー登録であることを示しています。

### MQTT ランタイム

`MQTTServiceBase` は、現在のメッセージ チェーンの最も重要なホスト基本クラスです。それは次のことを担当します。

- MQTT メッセージのサブスクライブ/パブリッシュ
- メンテナンス `MsgRecord`
- 心拍による判定 `IsAlive`
- タイムアウトを処理し、パケットのステータスを返す

`MqttRCService` はさらに、登録センター クライアントの役割を引き受け、以下の責任を負います。

- RC テーマのビルド
-再登録
- サービストークンキャッシュ
- RC接続状態

「サービスがオンラインかどうか、プロセスを更新できるかどうか、デバイス トークンがどこから来たのか」など、エンジン層での多くの問題は、最終的にはこの層に戻ってきます。

## アルゴリズムは現在この層でどのような役割を果たしていますか?

`AlgorithmPOI` と `AlgorithmMTF` の実装から判断すると、`ColorVision.Engine` のアルゴリズム クラスは現在次のとおりです。

- テンプレートエディタを開きます
- 組織テンプレートの選択状況
- MQTTパラメータの組み立て
- デバイス サービスを呼び出してコマンドを発行する

言い換えれば、この層のアルゴリズム オブジェクトは通常、画像計算をローカルで直接完了する純粋なアルゴリズム カーネルではなく、「表示およびコマンド アダプター」です。

## 現在、最もよくある間違いのいくつかが犯されています

### 「すべてのアルゴリズムがローカルで実行される」モジュールではありません

現在のアルゴリズム クラスの多くは、実際にテンプレート、ファイル名、デバイス情報を MQTT リクエストに組み立て、それらをデバイスまたはサーバーに渡して処理します。この層を純粋にローカルなアルゴリズム実装として書き続けると、実際の制御チェーンと矛盾します。

### テンプレート システムは初期化とデータベースから切り離せません

`TemplateControl` は、MySQL の初期化後のアセンブリ スキャンに依存します。 `ITemplateJson<T>` はデータベースとも直接対話します。これを「静的テンプレートの完全にローカルなセット」として記述すると、重要な前提が欠けています。

### すべてのプロセス関数が FlowEngineLib にあるわけではありません

メイン プログラムでフロー テンプレートを実際に編集、選択、実行できるようにするには、ブリッジ コードの `Templates/Flow/` 層も必要です。 FlowEngineLib を記述するだけでは、ホスト側の実際のコントロール サーフェスが欠落します。

### デバイス サービスのインスタンス化は現在レジストリ中心です

`DeviceServiceFactoryRegistry` はすでに現在の実際のインスタンス化エントリです。古いドキュメントの分散構造記述を使用し続けると、拡張ポイントに偏りが生じます。

## 推奨される読む順序

1. `Engine/ColorVision.Engine/Templates/TemplateContorl.cs`
2. `Engine/ColorVision.Engine/Templates/Jsons/ITemplateJson.cs`
3. `Engine/ColorVision.Engine/Services/DeviceService.cs`
4. `Engine/ColorVision.Engine/Services/Devices/DeviceServiceFactory.cs`
5. `Engine/ColorVision.Engine/Services/Core/MQTTServiceBase.cs`
6. `Engine/ColorVision.Engine/Services/RC/MQTTRCService.cs`
7. `Engine/ColorVision.Engine/Templates/Flow/FlowEngineManager.cs`
8. `Engine/ColorVision.Engine/Templates/Flow/DisplayFlow.xaml.cs`

このようにして、最初にテンプレートとサービス ホスト層を確認し、次にメッセージ チェーンとプロセス ブリッジング層を接続できます。

## 続きを読む

- [docs/04-api-reference/engine-components/FlowEngineLib.md](./FlowEngineLib.md)
- [ドキュメント/03-アーキテクチャ/コンポーネント/テンプレート/分析.md](../../03-アーキテクチャ/コンポーネント/テンプレート/分析.md)
- [docs/04-api-reference/algorithms/overview.md](../algorithms/overview.md)