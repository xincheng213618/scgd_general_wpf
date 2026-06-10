# FindLightArea 발광 영역 템플릿

이 문서는 `Engine/ColorVision.Engine/Templates/FindLightArea/`의 실제 인수인계 체인을 설명합니다. 범용 ROI SDK가 아니라 템플릿 파라미터, 입력 이미지, MQTT 알고리즘 요청, 발광 영역 포인트, 이미지 오버레이를 연결하는 업무 템플릿입니다.

## 범위

| 항목 | 현재 구현 |
| --- | --- |
| 템플릿 코드 | `FindLightArea` |
| 템플릿 클래스 | `TemplateRoi : ITemplate<RoiParam>, IITemplateLoad` |
| 파라미터 클래스 | `RoiParam` |
| 실행 진입점 | `AlgorithmRoi`, 표시명 "发光区定位1" |
| UI 패널 | `DisplayRoi.xaml(.cs)` |
| MQTT 이벤트 | `MQTTAlgorithmEventEnum.Event_LightArea2_GetData` |
| 결과 핸들러 | `ViewHandleFindLightArea` |
| 결과 테이블 | `t_scgd_algorithm_result_detail_light_area` |

## 소스 진입점

| 파일 | 인수인계 용도 |
| --- | --- |
| `TemplateRoi.cs` | `FindLightArea` 템플릿을 등록하고 `TemplateDicId = 31` 및 `MysqlRoi` 복구를 설정한다. |
| `ROIParam.cs` | `Threshold`, `Times`, `SmoothSize`를 정의한다. |
| `AlgorithmRoi.cs` | 알고리즘 요청을 조립하고 MQTT 명령을 보낸다. |
| `DisplayRoi.xaml.cs` | 템플릿 선택, 이미지 입력, 배치/Raw/로컬 파일, 실행 버튼을 처리한다. |
| `AlgResultLightAreaDao.cs` | 결과 모델, 로드, 이미지 오버레이, 목록 표시를 정의한다. |
| `MysqlRoi.cs` | MySQL 사전과 기본 템플릿 항목을 복구한다. |

## 실행 체인

1. `TemplateRoi`가 템플릿 시스템에서 발견되어 `TemplateControl`에 등록된다.
2. UI에서 `TemplateRoi.Params`의 `RoiParam`을 선택한다.
3. `DisplayRoi`는 배치 번호, 알고리즘 서비스 Raw/CIE 파일, 로컬 이미지 파일을 지원한다.
4. 확장자는 `Raw`, `CIE`, `Tif`, `Src`로 매핑되며, `HistoryFilePath`가 있으면 전체 경로로 바꾼다.
5. `AlgorithmRoi.SendCommand(...)`는 `ImgFileName`, `FileType`, `DeviceCode`, `DeviceType`, `TemplateParam`을 보낸다.
6. 명령은 `Event_LightArea2_GetData`로 발행된다.
7. `ViewHandleFindLightArea`가 `LightArea` / `FindLightArea` 결과를 처리한다.

## 파라미터

| 파라미터 | 기본값 | 인수인계 메모 |
| --- | --- | --- |
| `Threshold` | `1` | 발광 영역 임계값. 변경 시 이미지 종류와 노출 조건을 함께 기록한다. |
| `Times` | `1` | 알고리즘 서비스가 해석하는 처리 횟수 파라미터. |
| `SmoothSize` | `1` | 평활화 크기. 포인트 목록뿐 아니라 볼록 껍질 결과도 검증한다. |

## 결과 표시

`AlgResultLightAreaModel`은 `PosX`, `PosY`, `Pid`를 저장합니다. 표시 시 `GrahamScan.ComputeConvexHull(...)`로 볼록 껍질을 만들고 투명한 파란색 `DVPolygon`으로 이미지에 그립니다.

주의할 점:

- 포인트 목록과 볼록 껍질은 같은 산출물이 아닙니다. 껍질이 이상하면 입력 이미지와 ROI 파라미터를 먼저 확인합니다.
- 현재 `SideSave(...)`는 파일을 만들지만 포인트 행을 쓰지 않습니다. 안정적인 CSV 내보내기로 보지 마십시오.

## 문제 해결

| 현상 | 먼저 확인할 것 |
| --- | --- |
| 템플릿 드롭다운이 비어 있음 | 어셈블리 로드, `IITemplateLoad`, `TemplateDicId = 31` 복구. |
| 알고리즘 서비스가 이미지를 읽지 못함 | `ImgFileName`, `FileType`, 히스토리 경로, 장치 `Code/Type`. |
| 결과 포인트가 없음 | 결과 타입과 결과 테이블의 `Pid`. |
| 오버레이 모양이 이상함 | `Threshold`, `Times`, `SmoothSize`, 입력 이미지, 볼록 껍질 입력점. |

## 인수인계 체크리스트

- 파라미터를 바꾸면 `ROIParam.cs`, `MysqlRoi.cs`, 현장 추천값을 함께 갱신한다.
- 실행 이벤트를 바꾸면 `AlgorithmRoi.SendCommand(...)`, Flow 노드 설명, 이 문서를 갱신한다.
- 결과 구조를 바꾸면 결과 테이블, 표시 열, 내보내기를 갱신한다.
- 프로젝트가 결과를 소비하면 포인트, 볼록 껍질, 이미지 영역 중 무엇을 쓰는지 명시한다.

## 이어서 읽기

- [ROI 프리미티브](../primitives/roi.md)
- [OpenCV 통합](../../../02-developer-guide/engine-development/opencv-integration.md)
- [결과 인수인계 체인](../../engine-components/result-handoff-chain.md)
- [현재 알고리즘 템플릿 커버리지](../current-algorithm-template-coverage.md)
