# 플러그인 런타임 및 인수인계 플레이북

## 시나리오

| 문제 | 확인 항목 |
| --- | --- |
| 폴더는 있지만 로드되지 않음 | `manifest.json`, `dllpath`, `.deps.json`, host `ColorVision.*.dll` |
| 로드되었지만 진입점이 없음 | menu/status/settings/Socket provider |
| `.cvxp` 패키징 | `Scripts\package_plugin.bat Spectrum --no-upload` |
| device/native DLL 문제 | Spectrum: spectrometer DLL/serial/license, Conoscope: MVS SDK/`MvCameraControl.dll` |
| administrator permission | EventVWR, WindowsServicePlugin |
| Socket 실패 | SocketProtocol, JSON mode, port, Spectrum window/device state |

## 기록 이름 복원

Pattern, ImageProjector, ScreenRecorder 는 현재 플러그인이 아닙니다. 복원하려면 source, project, manifest, README, CHANGELOG, build copy, `.cvxp` 검증, [현재 플러그인 문서 커버리지](./current-plugin-coverage.md), matrix, navigation 을 업데이트해야 합니다.
