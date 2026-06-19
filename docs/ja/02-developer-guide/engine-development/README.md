# Engine 開発ガイド

Engine 開発では、最初にどの業務チェーンを変更するのかを確認します。デバイスサービス、テンプレート、Flow ノード、アルゴリズム結果、顧客プロジェクト判定を同じ場所で混ぜて変更しないでください。

## 最初に読むページ

- [Engine 業務引き継ぎ](../../04-api-reference/engine-components/business-handoff.md)
- [Engine コンポーネントと業務引き継ぎ](../../04-api-reference/engine-components/README.md)
- [Engine ランタイムオブジェクトマップ](../../04-api-reference/engine-components/runtime-object-map.md)

このディレクトリのページは、具体的な開発テーマの入口です。

## よくある変更点

| 目的 | 主なディレクトリ | 最初に読む |
| --- | --- | --- |
| デバイスサービス追加/保守 | `Engine/ColorVision.Engine/Services/Devices/` | [サービス開発引き継ぎ](./services.md) |
| テンプレート追加/保守 | `Engine/ColorVision.Engine/Templates/` | [テンプレートシステム開発引き継ぎ](./templates.md) |
| Flow ノード追加/保守 | `Engine/ColorVision.Engine/Templates/Flow/`、`Engine/FlowEngineLib/` | [Engine テンプレートと Flow チェーン](../../04-api-reference/engine-components/template-flow-chain.md) |
| MQTT 変更 | `Engine/ColorVision.Engine/MQTT/`、デバイスサービスフォルダ | [MQTT メッセージ処理引き継ぎ](./mqtt.md) |
| OpenCV/native 変更 | `Engine/cvColorVision/`、`UI/ColorVision.Core/`、`Engine/ColorVision.Engine/Media/` | [OpenCV と native 統合引き継ぎ](./opencv-integration.md) |
| 結果表示変更 | `Templates/*/ViewHandle*.cs`、`UI/ColorVision.ImageEditor/` | [Engine 結果表示とプロジェクト引き継ぎ](../../04-api-reference/engine-components/result-handoff-chain.md) |
| 検証コマンド選択 | `Test/`、backend、scripts、docs | [テストと検証の引き継ぎ](../testing.md) |

## 検証

少なくとも変更したモジュール、ホスト、1 つの end-to-end シナリオを確認します。

```powershell
dotnet build Engine/ColorVision.Engine/ColorVision.Engine.csproj -c Release -p:Platform=x64
dotnet build ColorVision/ColorVision.csproj -c Release -p:Platform=x64
```

native/OpenCV 変更は [OpenCV と native 統合引き継ぎ](./opencv-integration.md) のコマンドも使います。ドキュメント変更は `npm run docs:build` を実行します。

## 保守原則

- デバイスサービスは状態、命令、設定、UI を扱い、顧客判定は扱いません。
- テンプレートはパラメータ、編集、保存、アルゴリズム入力を扱い、最終 CSV/PDF/MES 形式は扱いません。
- Flow ノードは visual execution unit です。結果解釈はテンプレート、結果、プロジェクト層に置きます。
- プロジェクトパッケージが顧客 Process、Recipe、Fix、protocol、exporter を持ちます。
