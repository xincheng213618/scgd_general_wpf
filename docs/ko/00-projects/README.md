# 프로젝트 설명

이 장은 `Projects/` 아래의 고객 프로젝트 패키지를 설명합니다. 이 패키지들은 런타임에는 플러그인처럼 로드되지만, 핵심은 범용 도구가 아니라 고객별 검사 순서, Recipe/Fix, 외부 프로토콜, 결과 출력, 현장 납품입니다.

처음 인수인계할 때는 이 페이지로 전체 지도를 잡고, [프로젝트 기능 및 인수인계 매트릭스](../04-api-reference/projects/project-capability-matrix.md), [프로젝트 실행 및 인수인계 플레이북](../04-api-reference/projects/project-package-playbook.md), [프로젝트 패키지 릴리스 증거 및 버전 확인표](../04-api-reference/projects/project-release-evidence.md)를 읽습니다. 공통 실행 체인은 [프로젝트 인수인계 매뉴얼](../04-api-reference/projects/project-handoff.md)을 확인한 뒤 개별 프로젝트 페이지로 이동합니다.

## 현재 프로젝트 지도

| 프로젝트 | 역할 | 상세 문서 |
| --- | --- | --- |
| ProjectARVR | 초기 AR/VR 광학 검사. 고정 PG 전환, Socket 이벤트, `ObjectiveTestResult` 집계 | [ProjectARVR](../04-api-reference/projects/project-arvr.md) |
| ProjectARVRLite | 경량 AR/VR 빠른 검사. 설정 가능한 항목, 전처리, Socket 전환, CSV | [ProjectARVRLite](../04-api-reference/projects/project-arvr-lite.md) |
| ProjectARVRPro | 주요 AR/VR 전문 패키지. ProcessGroup, Recipe, 이미지 전환, Socket, 다중 출력 | [ProjectARVRPro](../04-api-reference/projects/project-arvr-pro.md) |
| ProjectARVRPro.IntegrationDemo | 고객, MES, PLC, 상위 제어기용 TCP/JSON 연동 예제 | [ARVRPro Integration Demo](../04-api-reference/projects/project-arvr-pro-integration-demo.md) |
| ProjectBlackMura | 디스플레이 패널 Black Mura 검사. PG 시리얼 전환, 5색 플로우, Excel 보고서 | [ProjectBlackMura](../04-api-reference/projects/project-black-mura.md) |
| ProjectHeyuan | Heyuan 고객용 4점 WBRO 색/휘도 검사. STX/ETX 시리얼과 CSV | [ProjectHeyuan](../04-api-reference/projects/project-heyuan.md) |
| ProjectKB | 키보드 백라이트 휘도/균일도. Modbus, MES DLL, 자동 보정, CSV/summary | [ProjectKB](../04-api-reference/projects/project-kb.md) |
| ProjectLUX | LUX 광학 자동화. 휘도, 색, MTF, 왜곡, 광학 중심, VID, 광속 | [ProjectLUX](../04-api-reference/projects/project-lux.md) |
| ProjectShiyuan | Shiyuan 고객용 JND/POI 내보내기와 고정 이미지 후처리 | [ProjectShiyuan](../04-api-reference/projects/project-shiyuan.md) |

## 권장 읽기 순서

1. [프로젝트 기능 및 인수인계 매트릭스](../04-api-reference/projects/project-capability-matrix.md)
2. [현재 프로젝트 문서 커버리지](../04-api-reference/projects/current-project-coverage.md)
3. [프로젝트 실행 및 인수인계 플레이북](../04-api-reference/projects/project-package-playbook.md)
4. [프로젝트 패키지 릴리스 증거 및 버전 확인표](../04-api-reference/projects/project-release-evidence.md)
5. [프로젝트 인수인계 매뉴얼](../04-api-reference/projects/project-handoff.md)
6. 해당 프로젝트 페이지

## 유지보수 규칙

- 새 `Projects/<Name>/`을 추가하면 이 페이지, 프로젝트 개요, 커버리지 표, 개별 페이지를 함께 업데이트합니다.
- 외부 프로토콜, 결과 출력, ProcessGroup, Recipe/Fix, 납품 검수 조건을 바꾸면 개별 페이지, 매트릭스, 플레이북, 릴리스 증거 페이지를 같이 업데이트합니다.
- 현재 소스 코드로 확인 가능한 동작만 문서화합니다.
