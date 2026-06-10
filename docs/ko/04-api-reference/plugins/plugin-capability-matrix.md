# 플러그인 기능 및 인수인계 매트릭스

## 현재 플러그인

| 플러그인 | 진입점 | 주요 기능 | 외부 경계 | 위험 |
| --- | --- | --- | --- | --- |
| Conoscope | Tool `VAM`, ImageEditor context menu | 이미지 보기, 포커스 포인트, 색역, 대비 분석 | MVS camera, `MvCameraControl.dll` | 카메라 환경과 native DLL |
| Spectrum | Tool spectrum window, window menu, status bar, Socket | 분광기 연결, 보정, 측정, EQE, SQLite 결과 | native DLL, serial, SMU/Shutter/CFW, license | 창/장치/보정 상태 필요 |
| SystemMonitor | Tool, settings, host status bar | CPU/RAM/disk/network/process/GPU/cache monitoring | Windows performance counters, CUDA | counter 초기화 실패 시 degraded 동작 |
| EventVWR | Help event window, Dump menu | Application error events, WER LocalDumps, process dump | EventLog, HKLM registry | administrator permission |
| WindowsServicePlugin | Help service manager, wizard | service install/register/start/stop, MySQL/MQTT config | Windows services, MySQL, MQTT, ZIP | 관리자 권한과 로컬 설정 변경 |

## 릴리스 전 확인

- `manifest.version`
- output DLL `FileVersion`
- README / CHANGELOG
- native DLL and device data
- administrator-only operations
- rollback package
