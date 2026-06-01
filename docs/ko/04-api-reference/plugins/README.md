# 플러그인 및 상태 페이지

이 장에는 두 가지 범주의 콘텐츠만 포함됩니다.

- 현재 작업공간에서 소스 코드 플러그인 페이지에 직접 액세스할 수 있습니다.
- 해당 소스 코드가 없거나 더 이상 완전히 유지 관리되지 않아 이전 플러그인 페이지가 "역사적 상태 설명"으로 다시 작성됩니다.

더 이상 "완전한 플러그인 디렉토리"의 역할을 가정하지 않으며 여기에 나열된 모든 페이지가 현재 소스 트리에서 직접 개발할 수 있는 플러그인 프로젝트를 나타내는 기본 설정도 아닙니다.

## 먼저 이 장의 경계를 이해하세요.

- 현재 플러그인 로딩 모델은 `manifest.json` 및 `UI/ColorVision.UI/Plugins/PluginLoader.cs`의 실제 구현을 기반으로 해야 합니다.
- 플러그인 API 참조 페이지는 현재 문서에서 다룬 몇 가지 주제만 다루고 `Plugins/` 디렉토리를 완전히 반영하지는 않습니다.
- 문서 설명이 현재 소스 코드 디렉터리와 일치하지 않는 경우 소스 코드 디렉터리 및 런타임 로딩 동작이 우선되어야 합니다.

## 현재 어떤 페이지가 포함되어 있나요?

### 소스코드에 직접 연결될 수 있는 주제

- [스펙트럼 플러그인](./standard-plugins/spectrum.md)
- [SystemMonitor 플러그인](./standard-plugins/system-monitor.md)
- [EventVWR 플러그인](./standard-plugins/eventvwr.md)
- [Windows 서비스 플러그인](./standard-plugins/windows-service.md)

### 과거 상태 설명 페이지

- [패턴/그림카드 생성 기능](./standard-plugins/pattern.md)
- [ImageProjector(기록 상태)](./standard-plugins/image-projector.md)
- [ScreenRecorder(기록 상태)](./standard-plugins/screen-recorder.md)

이러한 페이지를 유지하는 목적은 기능 약속 페이지 역할을 계속하기보다는 "현재 웨어하우스에서 소스 코드가 여전히 일치할 수 있는지 여부와 현재 상황을 찾을 수 있는 곳"을 설명하는 것입니다.

## 이 장을 더 효과적으로 읽는 방법

1. 먼저 [플러그인 개발 개요](../../02-developer-guide/plugin-development/overview.md)를 읽고 플러그인 항목, 제품 형태 및 런타임 경계를 이해하세요.
2. 현재 'Plugins/' 디렉터리에 대상 플러그인의 해당 소스 코드가 있는지 다시 확인하세요.
3. 페이지에 "이전 상태"라고 명확하게 기재된 경우 현재 개발 매뉴얼이 아닌 현재 상태에 대한 설명으로 간주되어야 합니다.
4. 런타임 로딩 체인을 추적하려면 `PluginLoader`로 돌아가서 각 플러그인 디렉터리의 `manifest.json`을 읽어야 합니다.

## 현재 알려진 공백

- 현재 API 참조는 `Plugins/` 디렉토리의 모든 실제 프로젝트를 다루지 않습니다.
- Conscope와 같이 아직 소스 코드가 남아 있는 플러그인에는 현재 별도의 API 참조 페이지가 없습니다.
- 따라서 이 장은 "전체 플러그인 색인"보다는 "구성된 주제 항목"으로 더 적합합니다.

## 계속 읽기

- [API 참조 개요](../README.md)
- [플러그인 개발 개요](../../02-developer-guide/plugin-development/overview.md)
- [FlowEngineLib 노드 확장](../extensions/flow-node.md)