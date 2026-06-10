# プラグイン実行と引き継ぎプレイブック

## シナリオ

| 問題 | 対応 |
| --- | --- |
| フォルダはあるがロードされない | `manifest.json`、`dllpath`、`.deps.json`、host `ColorVision.*.dll` を確認 |
| ロードされたが入口が出ない | menu/status/settings/Socket provider を確認 |
| `.cvxp` を作る | `Scripts\package_plugin.bat Spectrum --no-upload` |
| device/native DLL 問題 | Spectrum は分光器 DLL/serial/license、Conoscope は MVS SDK/`MvCameraControl.dll` |
| administrator permission | EventVWR と WindowsServicePlugin は管理者権限を明記 |
| Socket が動かない | SocketProtocol、JSON mode、port、Spectrum window/device state を確認 |

## 履歴名の復帰

Pattern、ImageProjector、ScreenRecorder は現在のプラグインではありません。復帰させる場合は、ソース、project、manifest、README、CHANGELOG、ビルドコピー、`.cvxp` 検証、[現在のプラグイン文書カバレッジ](./current-plugin-coverage.md)、マトリクス、ナビゲーションを更新してください。
