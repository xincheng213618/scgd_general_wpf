# エンジン開発ガイド

ColorVision Engine レイヤーの機能を開発および拡張する方法について説明します。

## 概要

ColorVision.Engine はシステムのコア エンジン層であり、以下を担当します。

- 🔧 設備サービス管理
- 🔄 プロセスエンジン
- 📐 アルゴリズムテンプレートシステム
- 📡 MQTT メッセージ処理
- 🖼️ OpenCV画像処理

## エンジンのアーキテクチャ


```
ColorVision.Engine
├── Services/          # 设备和服务
├── Templates/         # 模板系统
├── MQTT/              # MQTT 消息处理
├── Algorithms/        # 算法实现
└── Utilities/         # 工具类
```


## 主なコンポーネント

### 1. サービス体制

詳細については、「サービス開発ガイド」(./services.md)を参照してください。

### 2. テンプレートシステム

詳細については、「テンプレートシステム開発」(./templates.md)を参照してください。

### 3. MQTT メッセージの処理

詳細については、「MQTT メッセージ処理」(./mqtt.md) を参照してください。

### 4. OpenCV の統合

詳細については、「OpenCV統合開発」(./opencv-integration.md)を参照してください。

## 開発プロセス

### 1. サービスを作成する


```csharp
public class MyDeviceService : DeviceService
{
    public override string ServiceName => "My Device";
    
    protected override Task OnStartAsync()
    {
        // 初始化设备
        return Task.CompletedTask;
    }
    
    protected override Task OnStopAsync()
    {
        // 停止设备
        return Task.CompletedTask;
    }
}
```


### 2. 登録サービス


```csharp
ServiceManager.GetInstance().Add\<IMyDeviceService, MyDeviceService>();
```


### 3. サービスを利用する


```csharp
var service = ServiceManager.GetInstance().GetService\<IMyDeviceService>();
await service.StartAsync();
```


## ベストプラクティス

1. **インターフェイス定義**: 各サービスのインターフェイスを定義します。
2. **依存関係の挿入**: ServiceManager を使用して依存関係を管理する
3. **非同期操作**: 時間のかかる操作には async/await を使用します
4. **例外処理**: 例外を適切に処理し、ログを記録します。
5. **リソース管理**: IDisposable を実装してリソースを解放します

## 関連ドキュメント

- [サービス開発ガイド](./services.md)
- [テンプレートシステム開発](./templates.md)
- [MQTTメッセージ処理](./mqtt.md)
- [OpenCV統合開発](./opencv-integration.md)
- [エンジン API リファレンス](/ja/04-api-reference/engine-components/README.md)

## サンプルコード

参考：

- `Engine/ColorVision.Engine/Services/` - サービスの実装
- `Engine/ColorVision.Engine/Templates/` - テンプレート システム
- `Engine/ColorVision.Engine/MQTT/` - MQTT 実装

---

*技術的な詳細については、各サブトピックのドキュメントを参照してください。 *