# Engine 개발 가이드

Engine 개발은 먼저 어떤 업무 체인을 변경하는지 확인하는 것부터 시작합니다. 장치 서비스, 템플릿, Flow 노드, 알고리즘 결과, 고객 프로젝트 판정을 한 곳에서 섞어 수정하지 마세요.

## 먼저 읽기

- [Engine 업무 인수인계](../../04-api-reference/engine-components/business-handoff.md)
- [Engine 구성요소 및 업무 인수인계](../../04-api-reference/engine-components/README.md)
- [Engine 런타임 객체 맵](../../04-api-reference/engine-components/runtime-object-map.md)

이 디렉터리의 페이지는 구체 개발 주제의 코드 진입점입니다.

## 자주 바꾸는 위치

| 목표 | 주요 디렉터리 | 먼저 볼 문서 |
| --- | --- | --- |
| 장치 서비스 추가/유지보수 | `Engine/ColorVision.Engine/Services/Devices/` | [서비스 개발 인수인계](./services.md) |
| 템플릿 추가/유지보수 | `Engine/ColorVision.Engine/Templates/` | [템플릿 시스템 개발 인수인계](./templates.md) |
| Flow 노드 추가/유지보수 | `Engine/ColorVision.Engine/Templates/Flow/`, `Engine/FlowEngineLib/` | [Engine 템플릿 및 Flow 체인](../../04-api-reference/engine-components/template-flow-chain.md) |
| MQTT 변경 | `Engine/ColorVision.Engine/MQTT/`, 장치 서비스 폴더 | [MQTT 메시지 처리 인수인계](./mqtt.md) |
| OpenCV/native 변경 | `Engine/cvColorVision/`, `UI/ColorVision.Core/`, `Engine/ColorVision.Engine/Media/` | [OpenCV 및 native 통합 인수인계](./opencv-integration.md) |
| 결과 표시 변경 | `Templates/*/ViewHandle*.cs`, `UI/ColorVision.ImageEditor/` | [Engine 결과 표시 및 프로젝트 인수인계](../../04-api-reference/engine-components/result-handoff-chain.md) |
| 검증 명령 선택 | `Test/`, backend, scripts, docs | [테스트 및 검증 인수인계](../testing.md) |

## 개발 검증

최소한 수정한 모듈, 호스트, 하나의 end-to-end 시나리오를 검증합니다.

```powershell
dotnet build Engine/ColorVision.Engine/ColorVision.Engine.csproj -c Release -p:Platform=x64
dotnet build ColorVision/ColorVision.csproj -c Release -p:Platform=x64
```

native/OpenCV 변경은 [OpenCV 및 native 통합 인수인계](./opencv-integration.md)의 명령도 사용합니다. 문서 변경은 `npm run docs:build`를 실행합니다.

## 유지보수 원칙

- 장치 서비스는 상태, 명령, 설정, UI를 담당하며 고객 판정은 담당하지 않습니다.
- 템플릿은 파라미터, 편집, 저장, 알고리즘 입력을 담당하며 최종 CSV/PDF/MES 형식은 담당하지 않습니다.
- Flow 노드는 visual execution unit입니다. 결과 해석은 템플릿, 결과, 프로젝트 계층에 둡니다.
- 프로젝트 패키지가 고객 Process, Recipe, Fix, protocol, exporter를 담당합니다.
