# UI 컴포넌트 개요

이 장에서는 이제 현재 코드 구현과 일치하는 UI 모듈 소개 페이지만 유지하고 이전 버전 개요의 "버전 호환성 매트릭스 + 샘플 코드 + 확장 튜토리얼"의 혼합 작성 방법을 더 이상 유지하지 않습니다.

## 이 장을 읽는 방법

이 창고에 처음 입장하는 경우 다음 순서로 인식을 확립하는 것이 좋습니다.

1. 구성, 플러그인, 메뉴, 속성 편집기 및 단축키의 크로스커팅 인프라를 이해하려면 먼저 [ColorVision.UI](./ColorVision.UI.md)를 살펴보세요.
2. 작업 공간 쉘과 데스크탑 보조 창을 이해하려면 [ColorVision.Solution](./ColorVision.Solution.md) 및 [ColorVision.UI.Desktop](./ColorVision.UI.Desktop.md)을 다시 살펴보십시오.
3. 이미지 관련 기능은 [ColorVision.Core](./ColorVision.Core.md) -> [ColorVision.ImageEditor](./ColorVision.ImageEditor.md)를 따라 찾아보세요.
4. 독립적인 하위 시스템을 더 깊이 파고들려면 해당 단일 페이지를 입력하세요.

## 모듈 맵

### 기본 레이어

- [ColorVision.Common](./ColorVision.Common.md): MVVM, 공유 인터페이스, 상태 표시줄 메타데이터 및 대략적인 권한 기본 사항입니다.
- [ColorVision.Core](./ColorVision.Core.md): `HImage` 및 P/Invoke 내보내기 표면을 담당하는 기본 이미지/비디오 기능 브리징 레이어입니다.

### 기능층

- [ColorVision.Database](./ColorVision.Database.md): 데이터베이스 브라우저, 공급자 등록, SQLite 로그 및 일반 DAO.
- [ColorVision.ImageEditor](./ColorVision.ImageEditor.md): `ImageView`, `DrawCanvas`, 편집기 도구, 오프너 및 이미지 상호 작용 메인 체인.
- [ColorVision.Scheduler](./ColorVision.Scheduler.md): Quartz 스케줄러, 작업 복구, 실행 기록 및 관리 창.
- [ColorVision.SocketProtocol](./ColorVision.SocketProtocol.md) : 로컬 TCP 서비스, 요청 배포, 메시지 기록 및 관리 창입니다.

### 셸 및 작업공간

- [ColorVision.Solution](./ColorVision.Solution.md): 작업 공간, 편집기, 터미널, 다중 이미지 보기 및 솔루션 측 로컬 RBAC.
- [ColorVision.UI](./ColorVision.UI.md): 구성, 플러그인, 메뉴, 속성 편집기, 다중 언어 및 로깅과 같은 교차 기능을 다루는 UI 인프라 모음입니다.
- [ColorVision.UI.Desktop](./ColorVision.UI.Desktop.md): 설정 창, 마법사, 메뉴 관리, 구성 관리 및 기타 데스크탑 보조 창.

### 테마 레이어

- [ColorVision.Themes](./ColorVision.Themes.md): 테마 리소스 사전, 테마 전환 입구 및 창 모양 지원.

## 현재 혼란스러운 여러 경계

- `ColorVision.UI`는 단일 컨트롤 라이브러리가 아니라 UI 인프라의 교차 모음입니다.
- `ColorVision.Solution`은 "단순한 솔루션 파일 트리"가 아니며 작업 공간 셸 및 로컬 RBAC 하위 모듈도 호스팅합니다.
- `ColorVision.UI.Desktop`은 전체 제품의 주요 입구가 아니라 데스크탑 보조 창 및 관리 도구 모음에 가깝습니다.
- `ColorVision.Core`는 높은 수준의 관리되는 이미지 프레임워크가 아니라 기본 상호 운용성 레이어입니다.
- `ColorVision.ImageEditor`는 순수한 디스플레이 컨트롤이 아니며 오프너, 도구, 프리미티브, 오버레이 및 런타임 서비스를 함께 정렬합니다.

## 계속해서 제안 읽기

### 구성, 메뉴, 권한, 플러그인을 보고 싶습니다.

먼저 살펴보세요:

- [ColorVision.UI](./ColorVision.UI.md)
- [ColorVision.Solution](./ColorVision.Solution.md)
- [ColorVision.Common](./ColorVision.Common.md)

### 이미지 링크를 보고싶다

먼저 살펴보세요:

- [ColorVision.Core](./ColorVision.Core.md)
- [ColorVision.ImageEditor](./ColorVision.ImageEditor.md)
- [ColorVision.Themes](./ColorVision.Themes.md)

### 데스크탑 도구와 운영 및 유지 관리 보조 기능을 보고 싶습니다.

먼저 살펴보세요:

- [ColorVision.Database](./ColorVision.Database.md)
- [ColorVision.Scheduler](./ColorVision.Scheduler.md)
- [ColorVision.SocketProtocol](./ColorVision.SocketProtocol.md)
- [ColorVision.UI.Desktop](./ColorVision.UI.Desktop.md)