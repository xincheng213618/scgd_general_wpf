# 既存プラグイン現地検収チェックリスト

## 検収対象

| プラグイン | 最小スモークテスト | 記録するもの |
| --- | --- | --- |
| Conoscope | Tool -> `VAM`, image load, focus point, color gamut/contrast, CSV export | MVS dependency, focus point, CSV |
| Spectrum | window open, status bar, connect/dark calibration/measure/export, `SpectrumStatus` | SN, calibration group, SQLite result, license |
| SystemMonitor | monitor window, status bar switches, disk/network/process refresh | settings, counters, degraded items |
| EventVWR | admin mode, Application errors, DumpType, process dump | registry, DumpFolder, rollback |
| WindowsServicePlugin | admin mode, service status, config file, test install | BaseLocation, service package, MySQL/MQTT, rollback |

Pattern、ImageProjector、ScreenRecorder は検収対象外です。
