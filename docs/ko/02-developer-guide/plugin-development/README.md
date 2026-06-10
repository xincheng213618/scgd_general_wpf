# 플러그인 개발 개요

이 장은 ColorVision의 기능을 확장해야 하는 개발자를 위한 것이며 현재 유효한 플러그인 개발 경로에 우선순위를 부여합니다.

## 창고 내 플러그인 위치

- 런타임 플러그인 소스 코드는 `Plugins/`에 있습니다.
- 플러그인은 런타임 시 기본 프로그램에 의해 검색되고 로드됩니다.
- 플러그인에 UI가 있는 경우 일반적으로 WPF를 활성화하고 기본 애플리케이션의 인터페이스 규칙을 따라야 합니다.

## 플러그인 개발을 위한 최단 경로

1. 먼저 [확장성 개요](../core-concepts/extensibility.md)를 살펴보세요.
2. [플러그인 개발 시작하기](./getting-started.md)를 다시 살펴보세요.
3. 실행 단계를 이해해야 할 경우 [플러그인 라이프사이클](./lifecycle.md)을 참조하세요.

## 현재 권장되는 규칙

- 대상 프레임은 기본 저장소와 동일한 Windows 데스크탑 방향을 유지합니다.
- 인터페이스가 필요할 때 WPF를 활성화합니다.
- 빌드 후 제품을 기본 프로그램 출력 디렉터리의 `Plugins/<Name>/`에 복사합니다.
- 새로운 규칙을 만드는 대신 기존 표준 플러그인 구성을 우선시합니다.

## 기존 플러그인에 대한 권장 참고자료

- [Conoscope 플러그인](../../04-api-reference/plugins/standard-plugins/conoscope.md)
- [Spectrum 플러그인](../../04-api-reference/plugins/standard-plugins/spectrum.md)
- [SystemMonitor 플러그인](../../04-api-reference/plugins/standard-plugins/system-monitor.md)
- [스펙트럼 플러그인](../../04-api-reference/plugins/standard-plugins/spectrum.md)
- [SystemMonitor 플러그인](../../04-api-reference/plugins/standard-plugins/system-monitor.md)

## 설명

- 이 페이지는 단지 입구만 제공할 뿐 상세한 역사적 디자인 세부 사항을 확장하지는 않습니다.
- 플러그인이 프로젝트 수준 사용자 정의 논리에 의존하는 경우 `Projects/`에서 해당 구현도 확인해야 합니다.
