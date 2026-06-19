# 기존 플러그인 현장 검수 체크리스트

## 검수 대상

| 플러그인 | 최소 스모크 테스트 | 기록할 내용 |
| --- | --- | --- |
| Conoscope | Tool -> `VAM`, image load, focus point, color gamut/contrast, CSV export | MVS dependency, focus point, CSV |
| Spectrum | window open, status bar, connect/dark calibration/measure/export, `SpectrumStatus` | SN, calibration group, SQLite result, license |
| SystemMonitor | monitor window, status bar switches, disk/network/process refresh | settings, counters, degraded items |
| EventVWR | admin mode, Application errors, DumpType, process dump | registry, DumpFolder, rollback |
| WindowsServicePlugin | admin mode, service status, config file, test install | BaseLocation, service package, MySQL/MQTT, rollback |

Pattern, ImageProjector, ScreenRecorder 는 검수 대상이 아닙니다.
