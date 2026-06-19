# 프로젝트 기능 및 인수인계 매트릭스

이 페이지는 `Projects/` 아래의 고객 프로젝트를 가로로 비교합니다. 현장 문제, 외부 트리거, 주요 코드, 납품 검수 포인트를 한눈에 보기 위한 진입점입니다. 릴리스, 현장 교체, rollback 증거는 [프로젝트 패키지 릴리스 증거 및 버전 확인표](./project-release-evidence.md)에 기록합니다.

## 프로젝트 요약

| 프로젝트 | 업무 역할 | 외부 트리거 | 주요 출력 | 먼저 볼 코드 |
| --- | --- | --- | --- | --- |
| `ProjectARVR` | 초기 AR/VR 고정 이미지 전환 검사 | JSON Socket: `ProjectARVRInit`, `SwitchPGCompleted` | `ObjectiveTestResult`, CSV, Socket | `ARVRWindow.xaml.cs`, `Services/SocketControl.cs` |
| `ProjectARVRLite` | 경량 AR/VR 빠른 검사 | JSON Socket, 자동 창 열기 | CSV, Socket | `ARVRWindow.xaml.cs`, `TestTypeConfig.cs` |
| `ProjectARVRPro` | 주요 AR/VR ProcessGroup 패키지 | JSON Socket, `RunAll`, `SwitchGroup`, Serial 전환 | SQLite, CSV, Legacy CSV, XLSX, Socket | `Process/`, `Recipe/`, `Services/` |
| `ProjectARVRPro.IntegrationDemo` | 고객 측 TCP/JSON 샘플 | `6666`에 연결해 JSON 전송 | JSON, 결과 테이블, CSV | `Program.cs`, `MainWindow.xaml.cs`, `Contracts/` |
| `ProjectBlackMura` | 패널 Black Mura 검사 | PG/MES Serial: `CON`, `CCPI`, `CSN`, `CGI` | Excel, POI, Mura | `MainWindow.xaml.cs`, `HYMesManager.cs` |
| `ProjectHeyuan` | Heyuan 4점 WBRO | STX/ETX Serial | WBRO CSV, MES | `ProjectHeyuanWindow.xaml.cs`, `TempResult.cs` |
| `ProjectKB` | 키보드 백라이트 | Modbus, MES DLL, optional Socket | SQLite, CSV, summary, MES | `ProjectKBWindow.xaml.cs`, `Modbus/`, `MesDll.cs` |
| `ProjectLUX` | LUX 광학 자동화 | Text Socket: `T00XX,SN;` | SQLite, CSV, PDF | `LUXWindow.xaml.cs`, `Process/`, `Recipe/`, `Fix/` |
| `ProjectShiyuan` | JND/POI 출력 | 현재는 주로 수동 Flow | JND CSV, POI CSV, pseudo-color | `ShiyuanProjectWindow.xaml.cs` |

## 프로토콜별 분류

| 유형 | 프로젝트 | 인수인계 중점 |
| --- | --- | --- |
| JSON Socket | ARVR, ARVRLite, ARVRPro | `EventName`, SN, 이미지 전환 완료, 최종 결과 |
| Text Socket | LUX | `T00XX`와 `ProcessMeta.SocketCode` 매칭 |
| Serial/MES | BlackMura, Heyuan | STX/ETX, DeviceId, return code, NG 승인 |
| PLC/Modbus | KB | register, 값 `1`, 완료 writeback `0`, SN 출처 |
| Customer demo | IntegrationDemo | 공개 JSON contract만 유지하고 내부 DLL을 넣지 않음 |
| Manual/offline | Shiyuan | `DataPath`, 템플릿 선택, 고정 이미지 경로 |

## 최소 스모크 검수

| 프로젝트 | 확인점 |
| --- | --- |
| ARVR | `ProjectARVRInit`부터 `OpticCenter`까지 진행, CSV와 Socket 결과 생성 |
| ARVRLite | enabled item, 전처리, CSV, Socket 결과 확인 |
| ARVRPro | ProcessGroup 전환, `RunAll`, 이미지 전환, Recipe, CSV/Legacy/Socket |
| IntegrationDemo | 샘플 JSON, 온라인 연결, partial/sticky packet 처리 |
| BlackMura | 5색 PG 전환, `<SN>.xlsx`, POI overlay |
| Heyuan | Serial 연결, 4 POI, WBRO CSV, MES |
| KB | Modbus `1`로 시작, 완료 `0`, CSV/summary/MES 일치 |
| LUX | `T00XX,SN;`가 `SocketCode`와 매칭되고 CSV/SQLite 생성 |
| Shiyuan | 수동 Flow로 JND/POI CSV와 pseudo-color 생성 |
