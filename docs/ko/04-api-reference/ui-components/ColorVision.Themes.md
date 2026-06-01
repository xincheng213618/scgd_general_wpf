#ColorVision.테마

이 페이지에서는 현재 UI/ColorVision.Themes에 구현된 테마 기능만 설명합니다. 더 이상 이전 문서의 "테마 개발 프레임워크 + 사용자 정의 테마 플랫폼 + 전체 FAQ 튜토리얼"의 작성 방법을 계속하지 않습니다.

## 모듈 포지셔닝

ColorVision.Themes는 현재 WPF 테마 리소스 및 창 모양 지원 라이브러리에 더 가깝습니다. 핵심 책임에는 네 가지 주요 범주가 있습니다.

- 테마 열거 및 테마 전환 입구 정의
- 애플리케이션에 리소스 사전 삽입
- Windows 테마 변경에 따라 인터페이스 업데이트
- 프로세스 창 제목 표시줄 색상 및 아이콘 연계

추상화되어 완성된 '임의의 커스텀 테마 플랫폼'이 아닙니다. 이전 문서에 언급된 Theme.Custom, ResourceDictionaryCustom 및 전체 사용자 정의 테마 등록 프로세스는 현재 코드에 해당 구현이 없습니다.

## 현재 가장 중요한 파일

현재 프로젝트 구조로 볼 때 가장 먼저 읽어볼 가치가 있는 내용은 다음과 같습니다.

- ThemeManager.cs: 테마 전환을 위한 메인 입구
- ThemeManagerExtensions.cs: 애플리케이션 및 창 확장 방법
- Theme.cs: 테마 열거 정의
- Themes/ 아래의 XAML: 테마별 기본 스타일 및 리소스 사전
- Controls/, Converter/, Utilities/: 테마 라이브러리에 포함된 컨트롤, 변환기 및 도구 코드

## 키 입력 유형

### 테마관리자

ThemeManager는 현재 테마 모듈의 중심 개체입니다. 다음을 담당합니다.

- CurrentTheme 및 CurrentUITheme 유지
- UseSystem, Light, Dark, Pink, Cyan의 5가지 테마 처리
- 테마에 따라 해당 ResourceDictionary 목록을 로드합니다.
- Windows 테마 변경 사항 모니터링
- 테마 전환 시 테마 변경 이벤트 발생
- 창 제목 표시줄 색상 조정

현재 리소스 사전은 여러 고정 목록 세트로 구성됩니다.

- ResourceDictionaryBase: 기본 공유 스타일
- ResourceDictionaryDark: 어두운 테마 리소스
- ResourceDictionaryWhite: 밝은 테마 리소스
- ResourceDictionaryPink: 핑크 테마 리소스
- ResourceDictionaryCyan: 청록색 테마 리소스

이는 현재 토픽 메커니즘이 런타임에 새로운 토픽 유형을 등록할 수 있는 개방형 모델이 아니라 "고정 토픽 열거 + 고정 리소스 사전 수집"의 구현임을 보여줍니다.

### 테마

현재 주제 열거에는 5개의 값만 있습니다.

-UseSystem
-빛
-어두운
-핑크
-청록색

UseSystem은 별도의 리소스 집합이 아니며, ApplyTheme 시 현재 AppsTheme에 해당하는 밝은 테마나 어두운 테마로 매핑됩니다.

### ThemeManager 확장

ThemeManagerExtensions는 실제로 매우 일반적으로 사용되는 두 가지 진입점을 제공합니다.

- Application.ApplyTheme : 테마 적용
- Application.ForceApplyTheme: 테마 리소스 강제 다시 로드

또한 Window.ApplyCaption은 창 다음에 로드됩니다.

- 제목 표시줄 색상 설정
- 현재 테마에 따라 창 아이콘 전환
- 주제 변경 사항을 구독하고 창이 닫힐 때 바인딩 해제

따라서 이 모듈은 리소스 사전을 관리할 뿐만 아니라 창 셸 모양 동작의 일부도 담당합니다.

## 현재 런타임 메인 체인

기존 주제 링크는 다음 항목에 더 가깝습니다.

1. 상단 UI에서 테마를 선택하세요.
2. Application.ApplyTheme가 ThemeManager.Current.ApplyTheme로 전송됩니다.
3. 현재 선택이 UseSystem인 경우 먼저 AppsTheme로 구문 분석됩니다.
4. ThemeManager는 Wpf.Ui와 이 모듈의 리소스 사전을 테마별로 Application.Resources.MergedDictionaries에 추가합니다.
5. CurrentTheme 및 CurrentUITheme이 업데이트되고 변경 이벤트가 트리거됩니다.
6. ApplyCaption이 호출된 창은 이에 따라 제목 표시줄 색상과 아이콘을 업데이트합니다.

## 시스템 테마를 따르는 방법

ThemeManager는 생성될 때 지연 초기화 프로세스를 시작합니다. 현재 구현에서는 애플리케이션 시작의 초기 단계에서 시스템 이벤트를 동기식으로 처리하는 대신 나중에 시스템 이벤트를 연결합니다.

주로 다음을 수신합니다.

- SystemEvents.UserPreferenceChanged
- SystemParameters.StaticPropertyChanged

그런 다음 레지스트리에서 개인화 항목을 읽어 판단하십시오.

-AppsUseLight테마
- 시스템은LightTheme를 사용합니다.

따라서 "Follow the System"은 현재 Windows 레지스트리 값과 시스템 이벤트에 의존하며, 프레임워크 계층에서 자동으로 제공하는 완전한 주제 동기화 서비스가 아닙니다.

## 제목 표시줄 색상 및 창 아이콘

ThemeManager는 DWM API를 호출하여 창 모양을 업데이트하는 역할도 담당합니다.

- 어두운 테마를 사용하면 몰입도 높은 어두운 제목 표시줄이 가능합니다.
- 분홍색 및 청록색 테마의 제목 표시줄 및 테두리 색상을 직접 설정
- 조명 및 팔로우 시스템 모드가 시스템 기본 제목 표시줄 색상으로 재설정됨

Window.ApplyCaption은 또한 현재 테마에 따라 창 아이콘 리소스를 전환합니다. 이 동작 부분은 현재 모듈의 매우 실용적인 값이지만 이전 문서에서는 명확하게 설명되지 않았습니다.

## 현재 구현 경계

### 테마 지속성은 ThemeManager 자체에서 수행되지 않습니다.

현재 테마 구성은 ColorVision.Themes 네임스페이스를 사용하지만 구성 클래스 ThemeConfig는 실제로 UI/ColorVision.UI/Themes에 있습니다.

이는 다음을 의미합니다.

- 테마 리소스 및 스위칭 코어는 UI/ColorVision.Themes에 있습니다.
- 메뉴, 단축키, 구성 항목 편집 등의 통합 로직이 UI/ColorVision.UI에 있습니다.

전체 "테마 구성 시스템"을 테마 프로젝트 자체에 위임하지 마십시오.

### 메뉴 및 단축키 항목은 UI 통합 레이어에 있습니다.

현재 테마 메뉴와 단축키 입구는 주로 다음 위치에 있습니다.

- UI/ColorVision.UI/Themes/ThemesHotKey.cs

다음을 담당합니다.

- 테마 메뉴 항목 생성
- 전환시 ThemeConfig.Instance.Theme에 쓰기
- Application.ApplyTheme 호출
- 테마 회전을 위한 Ctrl + Shift + T 단축키 제공

따라서 테마 모듈 자체는 기능 기반을 제공하며 실제로 데스크탑 메뉴 시스템과 인터페이스하는 것은 UI 레이어입니다.

### 이전 문서에 사용자 정의 테마 확장 지점이 존재하지 않습니다.

이러한 이전 문서에서 주장하는 인터페이스는 현재 코드에서 사용할 수 없습니다.

-테마.커스텀
- ThemeManager.ResourceDictionaryCustom
-ThemeConfig.FollowSystem

이러한 유형의 콘텐츠는 더 이상 API 참조에 기존 기능으로 기록될 수 없습니다.

## 이 모듈을 읽는 방법이 현재 더 적합합니다.

### 테마 전환 방법을 알고 싶으세요?

먼저 살펴보세요:

- ThemeManager.cs
-ThemeManagerExtensions.cs
- Theme.cs

### 테마가 애플리케이션 메뉴 및 구성에 어떻게 액세스하는지 확인하고 싶습니다.

먼저 살펴보세요:- UI/ColorVision.UI/Themes/ThemeConfig.cs
- UI/ColorVision.UI/Themes/ThemesHotKey.cs

### 테마 리소스가 어떤지 보고 싶습니다.

먼저 살펴보세요:

-테마/Base.xaml
-테마/Dark.xaml
-테마/White.xaml
-테마/Pink.xaml
-테마/Cyan.xaml

## 이 페이지에서는 더 이상 아무것도 수행하지 않습니다.

이 페이지에서는 더 이상 다음과 같은 고위험 콘텐츠를 유지하지 않습니다.

- 존재하지 않는 맞춤 테마 등록 API
- 가짜 ThemeConfig 구성 필드
- 튜토리얼 스타일의 완전한 테마 개발 프로세스
- 대규모 세그먼트 버전 번호, 프레임워크 호환성 매트릭스, 성능 번호 약속

나중에 주제 관련 콘텐츠를 추가하려면 일반 튜토리얼로 돌아가기보다는 실제 리소스 사전, 창 동작 또는 UI 액세스 포인트를 추가하는 데 우선순위를 두어야 합니다.

## 계속 읽기

- [UI 컴포넌트 개요](./README.md)
- [ColorVision.UI](./ColorVision.UI.md)