# FlowEngineLib ノード拡張

このページでは、現在のウェアハウスで実際に利用可能なフロー ノードの拡張パスのみを説明します。スケマティック API に基づいた古いバージョンの「開発ガイド」は維持されなくなります。

## まず、ノード システムが実際にどのようなものかを見てみましょう。

現在のコードから判断すると、フロー ノードの拡張は主に次の基本クラスを中心に展開されます。

- `CVCommonNode`: すべてのノードの共通基本クラスで、`NodeName`、`NodeType`、`DeviceCode`、`NodeID`、`ZIndex`、`nodeEvent` / `nodeRunEvent` / `nodeEndEvent` などのパブリック機能を提供します。
- `BaseStartNode`: プロセス開始ノード。`CVStartCFC` の作成、`startActions` の実行維持、プロセスの最後での `Finished` のスローを担当します。
- `CVBaseServerNode`: 最も一般的なサービス/アルゴリズム クラスのノード基本クラス。入出力、MQTT リクエストのアセンブリ、タイムアウト処理、およびノー​​ドレベルの完了ポストバックを担当します。
- `CVEndNode`: プロセス終了ノード。最後に `startAction.FireFinished()` を呼び出してプロセス全体を完了としてマークします。

これは、現在のノード拡張機能が「インターフェイスを実装するだけ」の軽量プラグイン モデルではなく、`STNode` と一連の具体的な基本クラスに直接構築されていることを意味します。

## 現在、最も注目すべきコード アンカー

ノードを追加または理解したい場合は、まず次のファイルを読んでください。

- `Engine/FlowEngineLib/Base/CVCommonNode.cs`
- `Engine/FlowEngineLib/Base/CVBaseServerNode.cs`
- `Engine/FlowEngineLib/Start/BaseStartNode.cs`
- `Engine/FlowEngineLib/End/CVEndNode.cs`
- `Engine/FlowEngineLib/Algorithm/AlgorithmNode.cs`

その中で、`AlgorithmNode` は非常に典型的な実際の例です。ノード内のグラフを直接計算するのではなく、テンプレート、色、画像パス、その他のパラメーターを収集し、サーバーに送信される実際のリクエスト データを記述します。

## 現在のサービス ノードの拡張方法

`CVBaseServerNode` の実装から判断すると、現在最も一般的な拡張メソッドは次のとおりです。

1. `CVBaseServerNode` を継承します。
2. コンストラクターでタイトル、`NodeType`、サービス名、デバイス コードを決定し、`operatorCode` などのノード動作フィールドを設定します。
3. 入力、出力、または編集コントロールを `OnCreate()` に追加します。
4. `getBaseEventData(CVStartCFC start)`を書き換えて、実際に実行側に送信するパラメータオブジェクトをアセンブルします。
5. 必要に応じて、`OnServerResponse(...)`、`Reset(...)` を書き直すか、関連する仮想メソッドを接続して、応答処理とクリーンアップ ロジックを補足します。

古いドキュメントにある「`DoServerWork` を書き換えるとノード開発が完了する」という記述は、現在の `CVBaseServerNode` の実際の実装と矛盾しています。

## 現状に近いスケルトン


```csharp
using FlowEngineLib.Algorithm;
using FlowEngineLib.Base;
using FlowEngineLib.MQTT;
using ST.Library.UI.NodeEditor;

[STNode("/Custom/MyNode")]
public class MyNode : CVBaseServerNode
{
    public MyNode()
        : base("自定义节点", "Algorithm", "SVR.Custom", "DEV.Custom")
    {
        operatorCode = "CustomEvent";
    }

    protected override void OnCreate()
    {
        base.OnCreate();
        CreateTempControl(m_custom_item);
    }

    protected override object getBaseEventData(CVStartCFC start)
    {
        var param = new AlgorithmParam();
        BuildTemp(param);
        BuildImageParam(param);
        return param;
    }

    protected override void OnServerResponse(CVServerResponse resp, CVStartCFC startCFC)
    {
        base.OnServerResponse(resp, startCFC);
        // 按需处理返回数据
    }
}
```


このスケルトンは現在のコードに近いものです。通常、ノードの核心は、ノード自体でビジネス計算全体を完了するのではなく、「リクエスト データを構築して既存の実行チェーンに接続する方法」です。

## 開始ノードと終了ノードはそれぞれ何を制御しますか?

### `BaseStartNode`

開始ノードは現在、次の役割を担っています。

- `CVStartCFC` を作成して保存します
- `m_op_start` および複数の `m_op_loop` を通じて起動アクションを分散します。
- `Ready`、`Running`、および進行中の `startActions` の管理
- プロセスが実際に終了した後に `Finished` をスローします

したがって、プロセス エントリ ノードを拡張する場合は、テンプレート パラメータではなく、起動、ループ出力、およびプロセス ステータス管理に重点が置かれます。

### `CVEndNode`

エンドノードは現在、次の役割を担っています。

- `CVStartCFC` を受信するか、ループ内のアクションを継続します
- `DoNodeEnded(...)` で `startAction.DoFinishing()` を呼び出します
- `startAction.FireFinished()` への最終呼び出し

これは、現在のコードにおける「プロセス全体が終了した」という実際の終了でもあります。

## 現在、最もよくある間違いのいくつかが犯されています

### `nodeEndEvent` はプロセスが完了したことを意味するものではありません

`CVCommonNode.nodeEndEvent` はノードレベルの終了フィードバックのみを表します。プロセス全体は `CVEndNode` に進み、その後 `startAction.FireFinished()` によってトリガーされる必要があります。

### 存在しない `DoServerWork` を中心に新しいノードを設計しないでください。

現在、`CVBaseServerNode` の実際の拡張ポイントは次のようになります。

- `OnCreate()`
- `getBaseEventData(...)`
- `OnServerResponse(...)`
- `Reset(...)`

古いドキュメントに従って`DoServerWork`を探すと、そのまま拡張パスを勘違いしてしまいます。

### ノードとサービス トピックは、普遍的に一致すると自動的には推論されません。

`CVBaseServerNode` は現在、`GetSendTopic()`、`GetRecvTopic()`、`operatorCode`、および `FlowServiceManager` を介してメッセージ チェーンで動作します。これらのフィールドがサーバー契約と一致しない場合、ノードはタイムアウトになるか、応答の受信に失敗します。

### 分類パスには単一の固定仕様はありません

`[STNode("...")]` の現在のパス文字列は実際のツリー構造の一部ですが、ウェアハウス内の既存のノードはすでに `/00 全局`、`/03_2 Algorithm`、およびその他のスタイルを組み合わせて使用しています。拡張機能は、古いドキュメントで想定されている分類テーブルをコピーするのではなく、隣接するノードの既存のグループに従う必要があります。

## 推奨される読む順序

1. `CVCommonNode`: まず、パブリック プロパティ、イベント、および制御補助メソッドについて理解します。
2. `CVBaseServerNode`: 一般的なサービス ノードがどのようにリクエストを開始し、応答を待ち、タイムアウトを処理するかを見てみましょう。
3. `BaseStartNode`: プロセスの起動、ループ出力、および `Finished` イベント ソースを理解します。
4. `CVEndNode`: プロセスがどこで終了し、ループが閉じているかを確認します。
5. `AlgorithmNode` または他の隣接する実際のノード: 最後に、古いチュートリアル テンプレートから始めるのではなく、既存のノードに従って拡張します。

## 続きを読む

- [FlowEngineLib アーキテクチャ](../../03-architecture/components/engine/flow-engine.md)
- [エンジン コンポーネントの概要](../engine-components/README.md)
- [アルゴリズム システムの概要](../algorithms/overview.md)