#ColorVision.Common

이 페이지에서는 현재 UI/ColorVision.Common에서 수행하는 공유 기본 기능만 설명하고 더 이상 이전 문서의 "대규모 및 포괄적인 공개 SDK 인터페이스 모음"을 계속하지 않습니다.

## 모듈 포지셔닝

ColorVision.Common은 현재 UI 레이어의 공유 기본 라이브러리이며 주로 다음 콘텐츠를 제공합니다.

- MVVM 기본 유형
- 명령 캡슐화
- 공통 인터페이스 및 메타데이터 모델
- 대략적인 권한 제어
- Windows 기본 메소드 캡슐화
- 일반적으로 사용되는 도구

이는 독립적으로 실행되는 비즈니스 모듈이라기보다는 모듈 전체에서 재사용되는 기본 빌딩 블록 세트에 가깝습니다.

## 현재 가장 중요한 디렉토리

프로젝트 디렉터리를 살펴보면 먼저 알아야 할 가장 중요한 사항은 다음과 같습니다.

- MVVM/: `ViewModelBase`, `RelayCommand` 및 기타 기본 유형
- 인터페이스/: 구성, 메뉴, 상태 표시줄, 초기화, 보기 등과 같은 공유 인터페이스
- 승인/: `Authorization`, `AccessControl`, `PermissionMode`
- NativeMethods/: Windows API 래퍼
- 유틸리티/: 파일, 컬렉션, 창 등과 같은 일반적인 도구입니다.
- 입력/: 입력 관련 기능
- ThirdPartyApps/: 타사 애플리케이션 액세스와 관련된 정의

## 키 입력 유형

### 뷰모델베이스

'ViewModelBase'는 현재 'INotifyPropertyChanged'를 구현하고 수많은 구성 클래스, 관리자 및 뷰 모델에 의해 상속될 수 있는 가장 기본적인 바인딩 가능한 개체 기본 클래스입니다.

### RelayCommand 및 명령

현재 명령 계층에는 두 가지 주요 공통 입구가 있습니다.

- `RelayCommand` / `RelayCommand<T>`: 범용 명령 캡슐화
- `Commands`: 소수의 전역 `RoatedUICommand`

이전 문서에서는 명령 시스템이 완전한 독립 프레임워크 세트로 작성되었지만 현재 코드를 보면 실제로 자주 사용되는 것은 'RelayCommand'입니다.

### 인터페이스/

'Interfaces/'는 완전한 비즈니스 구현보다는 공유 경계 정의를 담당합니다. 현재 일반적인 인터페이스 그룹은 다음과 같습니다.

- `IConfig`, `IConfigSettingProvider`
- `IInitializer`, `InitializerBase`
- `IMenuItemProvider`
- `IStatusBarProvider`, `IStatusBarProviderUpdatable`
- `View` 및 `IViewManager`와 같은 보기 관련 유형

이러한 유형의 대부분은 최소한의 계약만 정의하며 실제 등록, 검색 및 실행 논리는 일반적으로 상위 수준 모듈에 있습니다.

### StatusBarMeta

`StatusBarMeta`는 이전 문서처럼 아이콘과 명령만 있는 단순화된 모델이 아닙니다. 현재 이미 다음을 호스팅하고 있습니다.

- 고유식별번호 및 이름
- 설명 텍스트
- 좌우 정렬 및 정렬
- `명령` 또는 `팝업` 두 가지 유형의 작업
- 소스 객체 바인딩
- 아이콘 리소스 또는 직접 아이콘 콘텐츠
- 대상 창 범위 및 기본 가시성

따라서 이는 단순한 DTO가 아니라 이미 UI 상태 표시줄 시스템의 핵심 메타데이터입니다.

### 승인/액세스 제어/허가 모드

'Authorizations/' 아래에는 현재 일반적으로 사용되는 대략적인 권한 제어가 제공됩니다.

-`Authorization.Instance.PermissionMode`
- `AccessControl.Check(...)`
- `RequiresPermissionAttribute`

여기서 경계에 특별한 주의를 기울이십시오.

- 공통 레이어는 전역적으로 대략적인 권한 모드만 제공합니다.
- `UI/ColorVision.Solution/Rbac`의 세분화된 로컬 RBAC

Common의 권한 시스템을 전체 프로젝트에 대한 유일한 완전한 RBAC로 작성하지 마십시오.

## 현재 구현은 어떤 모습인가요?

ColorVision.Common은 현재 외부 사용자에게 공개되는 안정적인 공용 프레임워크보다 "공유 프로토콜 계층 + 기본 도구 계층"에 더 가깝습니다. 많은 인터페이스에는 공통 이름이 있지만 실제 기능은 웨어하우스의 UI 모듈에 대한 통합 계약을 제공하는 것입니다.

예를 들면:

- `IConfig` 자체는 단지 인터페이스를 표시할 뿐입니다.
- `InitializerBase`는 기본 이름, 순서 및 종속성 구조만 제공합니다.
- `View`는 전체 보기 프레임이 아닌 색인, 제목, 아이콘이 있는 공유 ViewModel입니다.

## 이 모듈을 읽는 방법이 현재 더 적합합니다.

### 공유 MVVM 및 명령 기본 사항을 보고 싶습니다.

먼저 살펴보세요:

- `MVVM/ViewModelBase.cs`
- `MVVM/RelayCommand.cs`
-`Commands.cs`

### 구성, 메뉴, 상태 표시줄의 공개 계약을 보고 싶습니다.

먼저 살펴보세요:

- `인터페이스/구성/` 또는 `인터페이스/구성 설정/`
-`인터페이스/메뉴/`
-`인터페이스/상태 표시줄/`
- `인터페이스/IInitializer/`

### 권한 경계를 보고 싶습니다.

먼저 살펴보세요:

- `권한 부여/AccessControl.cs`
-`권한 부여/PermissionMode.cs`

### 기본 메소드 및 도구 클래스를 보고 싶습니다.

먼저 살펴보세요:

- `네이티브 메소드/`
-`유틸리티/`

## 현재 구현 경계

### 완전한 플러그인 플랫폼 문서는 아님

Common에 정의된 확장 인터페이스는 많지만 실제 플러그인 검색, 메뉴 등록, 구성 집계, 상태 표시줄 새로 고침 등은 모두 상위 모듈 구현에 분산되어 있습니다. 이는 공유 계약일 뿐이므로 통합 런타임 허브로 작성해서는 안 됩니다.

### 전권 센터가 아님

공통의 권한 확인은 전역 모드 전환이나 대략적인 제한에 적합하지만 솔루션 측의 로컬 RBAC와는 동일하지 않습니다.

### 많은 인터페이스는 "최종 추상화"가 아닌 "최소 모양"입니다.

`IConfig` 및 `IInitializer`와 같은 인터페이스는 매우 가볍습니다. 나중에 읽을 때 인터페이스 정의 자체에 머무르기보다는 먼저 구현 측면을 따라 실제 제어 체인을 확인해야 합니다.

## 이 페이지에서는 더 이상 아무것도 수행하지 않습니다.

이 페이지에서는 더 이상 다음과 같은 고위험 콘텐츠를 유지하지 않습니다.

- 버전 번호 및 패키지 릴리스 정보의 큰 섹션
- 이상적인 공개 SDK 목록
- 모든 인터페이스를 완전한 프레임워크 기능으로 확장
- 공통 권한 시스템을 전역적으로 고유한 RBAC로 잘못 작성함

## 계속 읽기

- [UI 컴포넌트 개요](./README.md)
- [ColorVision.UI](./ColorVision.UI.md)
- [ColorVision.Solution](./ColorVision.Solution.md)