# 프로젝트 패키지 개요

`Projects/`에는 고객 프로젝트, 업무 패키지, 연동 데모가 있습니다. 런타임에는 주 프로그램의 `Plugins/<Name>/`에 배치될 수 있지만, 문서에서는 범용 플러그인과 분리해서 다룹니다. 프로젝트 패키지는 고객 워크플로우, Recipe/Fix, Socket/MES/Serial, 결과 내보내기가 핵심입니다.

먼저 [프로젝트 기능 및 인수인계 매트릭스](./project-capability-matrix.md)를 읽고, 현장 문제는 [프로젝트 실행 및 인수인계 플레이북](./project-package-playbook.md)에서 확인합니다. 릴리스, 현장 교체, rollback 증거는 [프로젝트 패키지 릴리스 증거 및 버전 확인표](./project-release-evidence.md)에 남깁니다. 공통 실행 체인은 [프로젝트 인수인계 매뉴얼](./project-handoff.md), 문서 대응 상태는 [현재 프로젝트 문서 커버리지](./current-project-coverage.md)를 참고합니다.

## 현재 프로젝트

| 프로젝트 | 소스 | manifest Id | 역할 | 문서 |
| --- | --- | --- | --- | --- |
| ProjectARVR | `Projects/ProjectARVR/` | `ProjectARVR` | 고정 PG 전환, Socket, 결과 집계 | [상세](./project-arvr.md) |
| ProjectARVRLite | `Projects/ProjectARVRLite/` | `ProjectARVRLite` | 설정 가능한 검사 항목, 전처리, CSV | [상세](./project-arvr-lite.md) |
| ProjectARVRPro | `Projects/ProjectARVRPro/` | `ProjectARVRPro` | AR/VR ProcessGroup, Recipe, Socket, 고객 출력 | [상세](./project-arvr-pro.md) |
| ProjectARVRPro.IntegrationDemo | `Projects/ProjectARVRPro.IntegrationDemo/` | 없음 | 고객 TCP/JSON 데모 | [상세](./project-arvr-pro-integration-demo.md) |
| ProjectBlackMura | `Projects/ProjectBlackMura/` | `ProjectBlackMura` | PG Serial, 5색 Flow, Excel | [상세](./project-black-mura.md) |
| ProjectHeyuan | `Projects/ProjectHeyuan/` | `ProjectHeyuan` | STX/ETX, WBRO 4점, CSV | [상세](./project-heyuan.md) |
| ProjectKB | `Projects/ProjectKB/` | `ProjectKB` | Modbus, MES DLL, 백라이트 보정 | [상세](./project-kb.md) |
| ProjectLUX | `Projects/ProjectLUX/` | `ProjectLUX` | 휘도, 색, MTF, 왜곡 자동화 | [상세](./project-lux.md) |
| ProjectShiyuan | `Projects/ProjectShiyuan/` | `ProjectShiyuan` | JND/POI 출력과 이미지 후처리 | [상세](./project-shiyuan.md) |

## 패키징

```powershell
Scripts\package_project.bat ProjectLUX --no-upload
```

이 배치는 `Scripts/package_cvxp.py`를 호출해 출력 DLL, README, CHANGELOG, manifest, PackageIcon을 모아 `.cvxp`를 생성합니다.

릴리스 증거는 [프로젝트 패키지 릴리스 증거 및 버전 확인표](./project-release-evidence.md)에 기록합니다.

## 유지보수 규칙

- 모든 `Projects/<Name>/`에는 README, docs 페이지, 커버리지 행이 필요합니다.
- manifest, 메뉴, Socket/MES/Serial 이벤트, Recipe, 결과 필드, 납품물을 변경하면 개별 프로젝트 페이지와 [프로젝트 패키지 릴리스 증거 및 버전 확인표](./project-release-evidence.md)도 업데이트합니다.
