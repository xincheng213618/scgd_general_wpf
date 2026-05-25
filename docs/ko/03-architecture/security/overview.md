# 보안 및 권한 제어

이 장에서는 현재 웨어하우스에 구현된 권한 및 세션 구현에 대해서만 설명합니다. 우리는 더 이상 네트워크, 데이터, 감사 및 인증의 전체 링크를 다루는 일반 보안 백서 세트로 ColorVision을 유지하지 않을 것입니다.

## 현재 존재하는 2단계 권한 경계

코드 관점에서 볼 때 현재 보안 관련 기능은 주로 두 가지 계층으로 나뉩니다.

- `UI/ColorVision.Common/Authorizations/` 아래의 대략적인 `PermissionMode`
- `UI/ColorVision.Solution/Rbac/` 아래의 로컬 RBAC 하위 시스템

이 두 수준은 상호 배타적이지 않고 공존합니다.

## 첫 번째 수준: 전역적으로 대략적인 권한

'Authorization.Instance.PermissionMode'는 여전히 많은 현재 창 및 작업의 첫 번째 경계입니다.

제공되는 수준은 다음과 같습니다.

- `최고관리자`
- `관리자`
- `파워유저`
-`사용자`
- '손님'

현재 많은 UI 포털에서는 "관리자만 사용자 관리 및 권한 관리 창을 열 수 있습니다."와 같은 판단을 내리기 위해 이 레이어를 직접 사용합니다.

따라서 RBAC 서비스 계층만 보면 현재 시스템이 달성한 세분화된 액세스 범위를 과대평가하기 쉽습니다.

## 두 번째 레이어: 솔루션 측 로컬 RBAC

더 자세한 사용자, 역할, 권한, 세션 및 감사 기능은 현재 `UI/ColorVision.Solution/Rbac/`에 집중되어 있습니다.

이 하위 시스템의 현재 기능은 다음과 같습니다.

- 로컬 SQLite 데이터베이스 사용
- 데이터베이스는 기본적으로 `%AppData%/ColorVision/Config/Rbac.db`에 있습니다.
- SqlSugar CodeFirst를 통해 테이블 구조 초기화
- 로그인, 사용자 관리, 권한 관리, 세션 및 감사 서비스 제공

전체 제품의 모든 보안 기능에 대한 유일한 일반적인 입구라기보다는 "솔루션 측 로컬 계정 및 권한 모듈"에 가깝습니다.

## 현재 안전 장에서 가장 주의해야 할 점은 무엇인가요?

### 로그인 및 세션

현재 'AuthService'는 SessionToken을 기반으로 한 사용자 이름 및 비밀번호 로그인과 자동 로그인 복구를 담당하고, 'SessionService'는 세션 생성, 확인, 취소 및 정리를 담당합니다.

이 부분은 현재 코드에서 가장 명시적인 인증 체인입니다.

### 역할 및 권한

현재 'RbacManager'는 역할, 권한, 사용자 및 역할-권한 매핑을 초기화하고 'PermissionChecker'를 통해 세분화된 권한 코드 확인을 수행합니다.

### 감사

현재 'AuditLogService'가 이미 존재하지만 전체 애플리케이션의 모든 작업을 포괄하는 글로벌 감사 플랫폼이 아니라 RBAC 관련 작업에 대한 로컬 감사 로그를 기록합니다.

## 현재 증거로 뒷받침되지 않는 콘텐츠

다음은 이 장에서 역량으로 계속해서 언급되어서는 안 됩니다.

- 다단계 인증
- 글로벌 네트워크 통신 암호화 정책
- 인증서 검증 시스템
- IP 화이트리스트
- 방화벽 정책
- 모든 모듈을 포괄하는 통합 감사 및 차단 체인

향후 이러한 기능이 실제로 구현된다면 아키텍처 개요에 미리 작성하기보다는 실제 코드를 기반으로 별도의 주제 페이지를 열어야 합니다.

## 추천읽기순서

다음 줄을 읽는 것이 좋습니다.

1. `UI/ColorVision.Common/Authorizations/PermissionMode.cs`
2. `UI/ColorVision.Common/Authorizations/AccessControl.cs`
3. `UI/ColorVision.Solution/Rbac/RbacManager.cs`
4. `UI/ColorVision.Solution/Rbac/Services/Auth/AuthService.cs`
5. `UI/ColorVision.Solution/Rbac/Services/SessionService.cs`
6. `UI/ColorVision.Solution/Rbac/Services/PermissionChecker.cs`
7. `UI/ColorVision.Solution/Rbac/UserManagerWindow.xaml.cs`
8. `UI/ColorVision.Solution/Rbac/PermissionManagerWindow.xaml.cs`

## 계속 읽기

- [RBAC 모듈](./rbac.md)
- [아키텍처 런타임](../overview/runtime.md)
- [구성요소 상호작용](../overview/comComponent-interactions.md)

## 설명

- 이 페이지는 현재 구현에서 검증 가능한 권한과 세션 경계만 유지하며 더 이상 일반화된 보안 기능 목록을 유지하지 않습니다.