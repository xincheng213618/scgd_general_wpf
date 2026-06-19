# プラグイン機能と引き継ぎマトリクス

## 現在のプラグイン

| プラグイン | 入口 | 主な機能 | 外部境界 | リスク |
| --- | --- | --- | --- | --- |
| Conoscope | Tool `VAM`, ImageEditor context menu | 画像表示、フォーカスポイント、色域、コントラスト分析 | MVS camera, `MvCameraControl.dll` | カメラ環境と native DLL |
| Spectrum | Tool spectrum window, window menu, status bar, Socket | 分光器接続、校正、測定、EQE、SQLite 結果 | native DLL, serial, SMU/Shutter/CFW, license | ウィンドウ/デバイス/校正状態が必要 |
| SystemMonitor | Tool, settings, host status bar | CPU/RAM/ディスク/ネットワーク/プロセス/GPU/キャッシュ監視 | Windows performance counters, CUDA | counter 初期化失敗時の劣化 |
| EventVWR | Help event window, Dump menu | Application error events, WER LocalDumps, process dump | EventLog, HKLM registry | administrator permission |
| WindowsServicePlugin | Help service manager, wizard | service install/register/start/stop, MySQL/MQTT config | Windows services, MySQL, MQTT, ZIP | administrator permission and local config changes |

## リリース前チェック

- `manifest.version`
- output DLL `FileVersion`
- README / CHANGELOG
- native DLL and device data
- administrator-only operations
- rollback package
