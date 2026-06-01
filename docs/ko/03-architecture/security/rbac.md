#RBAC 모듈

이 페이지에서는 현재 웨어하우스에 구현된 RBAC 하위 시스템을 설명합니다. 완전한 기업 보안 플랫폼에 작성한 초안 문서는 더 이상 유지되지 않습니다.

## 모듈 위치

현재 RBAC 구현은 `Engine/`이 아닌 `UI/ColorVision.Solution/Rbac/`에 중점을 두고 있습니다.

이는 현재 권한 시스템이 엔진 계층의 통합 보안 커널보다 데스크톱 솔루션 측의 로컬 사용자 및 권한 모듈에 더 가깝다는 것을 보여주기 때문에 중요합니다.

## 초기화 중에 일어나는 일

`RbacManager`는 이 하위 시스템의 주요 입구입니다. 현재 초기화 체인은 대략 다음과 같습니다.

1. `%AppData%/ColorVision/Config/` 디렉토리를 생성합니다.
2. 로컬 SQLite 데이터베이스 `Rbac.db`를 열거나 생성합니다.
3. SqlSugar CodeFirst를 사용하여 엔터티 테이블을 초기화합니다.
4. `AuthService`, `UserService`, `RoleService`, `PermissionService`, `AuditLogService`, `SessionService`, `PermissionChecker`, `TenantService`를 초기화합니다.
5. 기본 관리자 역할과 관리자 사용자를 생성합니다.
6. 사전 설정된 권한을 작성하고 'admin' 역할에 모든 권한을 할당합니다.
7. 로그인 캐시가 있는 경우 현재 사용자의 `PermissionMode`를 전역 인증 상태로 다시 동기화합니다.

이 체인은 RBAC의 현재 시작이 로컬이고 부트스트랩되며 외부 인증 서버에 의존하지 않음을 보여줍니다.

## 현재 존재하는 핵심 엔터티

`Entity/` 디렉토리에서 현재 가장 중요한 테이블 모델은 다음과 같습니다.

- `UserEntity`는 `sys_user`에 해당합니다.
- `UserDetailEntity`는 `sys_user_detail`에 해당합니다.
- `RoleEntity`는 `sys_role`에 해당합니다.
- `PermissionEntity`는 `sys_permission`에 해당합니다.
- `RolePermissionEntity`는 `sys_role_permission`에 해당합니다.
- `SessionEntity`는 `sys_session`에 해당합니다.
- `AuditLogEntity`는 `sys_audit_log`에 해당합니다.

이 구현이 테넌트 차원을 예약했음을 나타내는 'TenantEntity' 및 'UserTenantEntity'도 있지만 이는 현재 데스크톱 권한 항목 페이지의 주요 읽기 초점이 아닙니다.

## 이들 단체는 현재 어떤 일을 담당하고 있나요?

### 사용자 및 사용자 세부정보

'UserEntity'는 사용자 이름, 비밀번호 해시, 활성화 상태 및 일시 삭제 상태를 저장합니다.

`UserDetailEntity` 추가 저장 공간:

-`허가 모드`
- 이메일, 전화번호, 주소, 회사, 부서, 직위
- 사용자 아바타 및 메모

여기서 특별한 주의를 기울여야 합니다. 현재 전역적으로 대략적으로 세분화된 권한 수준은 역할 테이블에서 즉시 파생되지 않고 'UserDetailEntity.PermissionMode'에 직접 저장되고 로그인 후 'Authorization.Instance.PermissionMode'에 동기화됩니다.

### 역할 및 권한

`RoleEntity`는 역할의 기본 정보를 관리하고 `PermissionEntity`는 권한 코드를 관리하며 `RolePermissionEntity`는 역할과 권한 간의 연결을 관리합니다.

현재 권한 서비스에는 이미 사전 설정된 권한 코드 세트가 있습니다. 예:

-`user.create`
-`사용자.편집`
-`역할.할당_권한`
-`허가.관리`
- `audit.view`

이는 현재의 세분화된 권한이 더 이상 "관리자/게스트"에 국한되지 않고 작업 코딩에 의해 제어되기 시작했음을 보여줍니다.

### 세션

`SessionEntity`는 다음을 저장합니다.

- `세션토큰`
-사용자 ID
- 기기 정보 및 IP
- 생성 시간, 만료 시간, 마지막 활성 시간
- 취소되었는지 여부

'SessionService'는 64바이트 무작위 토큰 생성, 세션 확인, 활성 시간 업데이트, 세션 취소 및 만료된 세션 정리를 담당합니다.

### 감사

`AuditLogEntity` 현재 기록:

- 사용자 ID/사용자 이름
- 액션 코드
- 자세한 설명
- 시간
-IP

'AuditLogService'는 현재 모든 비즈니스 모듈을 포괄하는 통합 감사 버스가 아닌 RBAC 측에 대한 로컬 감사 기록을 제공합니다.

## 현재 로그인 체인을 통과하는 방법

현재 인증 체인은 대략 다음과 같습니다.

1. 사용자는 `LoginWindow`에 사용자 이름과 비밀번호를 입력합니다.
2. `AuthService.LoginAndGetDetailAsync(...)` 활성화되었지만 일시 삭제되지 않은 사용자를 쿼리합니다.
3. 비밀번호는 'PasswordHasher' 검증을 통과하며 로그인 시 이전 형식의 비밀번호가 업그레이드됩니다.
4. 시스템은 'UserDetailEntity'가 존재하는지 확인하고 역할 목록을 로드합니다.
5. 로그인 결과를 `LoginResultDto`에 씁니다.
6. `SessionService`는 SessionToken을 추가로 생성하고 유지할 수 있습니다.
7. 로그인 성공 후 'UserDetailEntity.PermissionMode'를 'Authorization.Instance.PermissionMode'로 동기화합니다.

따라서 현재 로그인 결과는 RBAC 하위 시스템의 내부 상태에 영향을 미칠 뿐만 아니라 전역 UI의 대략적인 권한 판단에도 직접적인 영향을 미칩니다.

## 현재 권한 확인 방법

현재 권한 확인에는 두 가지 수준이 있습니다.

### 대략적인 진입 판단

많은 창에서는 먼저 다음을 직접 결정합니다.

- `Authorization.Instance.PermissionMode > PermissionMode.Administrator`

예를 들어 사용자 관리 및 권한 관리 창에서는 이 단계를 먼저 차단합니다.

### 세분화된 권한 코드 판단

보다 자세한 권한 확인은 `PermissionChecker`를 통해 수행됩니다. 그것은:

- 사용자와 연관된 역할 ID를 쿼리합니다.
- 표를 결합하여 해당 권한 코드를 알아보세요.
- 만료 시간 및 LRU 제거 기능이 있는 캐시를 사용하여 결과 저장

따라서 현재 시스템은 "RBAC 전용"이나 "PermissionMode 전용"이 아니라 두꺼운 레이어와 얇은 레이어가 공존합니다.

## 현재 표시되는 관리 인터페이스

모듈 디렉터리를 보면 RBAC에는 현재 명확한 데스크톱 창 세트가 있습니다.

- `로그인 창`
-`등록 창`
- `비밀번호 변경 창`
- `UserManagerWindow`
-`PermissionManagerWindow`
-`RbacManagerWindow`

그 중에는:

- `UserManagerWindow`는 사용자 목록, 역할 보기, 시작 및 중지, 삭제, 비밀번호 재설정 및 기타 관리 작업을 담당합니다.
- `PermissionManagerWindow`는 역할별로 권한을 할당하고 저장 후 권한 캐시를 무효화하는 역할을 담당합니다.

## 현재 디자인에서 가장 주의가 필요한 경계

### 통합 원격 ID 플랫폼이 아닌 로컬 권한 시스템입니다.

현재 구현은 외부 인증 기관이 아닌 기본 SQLite 및 로컬 창에 의존합니다.

### 대략적인 `PermissionMode`는 완전히 대체되지 않았습니다.

많은 주요 항목은 RBAC 관리 논리에 들어가기 전에 여전히 'PermissionMode'를 확인합니다.

### 세분화된 권한 액세스는 로컬에서 이루어집니다.현재 확인할 수 있는 세분화된 권한 기능은 주로 RBAC 자체 관리창과 서비스 레이어에 집중되어 있다. 전체 제품이 아직 권한 코드 제어에 대한 전체 액세스 권한을 갖고 있다고 설명할 수는 없습니다.

## 이 페이지에서는 더 이상 아무것도 수행하지 않습니다.

이 페이지는 현재 구현과 일치하지 않는 콘텐츠를 더 이상 유지하지 않습니다.

- 가상의 일반 `AuthService`/`AuditService` 플랫폼 계층 다이어그램
- 모든 비즈니스 모듈이 RBAC에 의해 균일하게 차단된다고 가정합니다.
- 아직 구현되지 않은 다중 요소 인증, 네트워크 인증서, IP 화이트리스트 및 기타 보안 기능

향후 전체 시스템의 보안 아키텍처를 확장하려면 실제 액세스 포인트를 기반으로 또 다른 특수 페이지를 작성해야 합니다.

## 추천읽기순서

1. `UI/ColorVision.Solution/Rbac/RbacManager.cs`
2. `UI/ColorVision.Solution/Rbac/Entity/`
3. `UI/ColorVision.Solution/Rbac/Services/Auth/AuthService.cs`
4. `UI/ColorVision.Solution/Rbac/Services/SessionService.cs`
5. `UI/ColorVision.Solution/Rbac/Services/PermissionChecker.cs`
6. `UI/ColorVision.Solution/Rbac/UserManagerWindow.xaml.cs`
7. `UI/ColorVision.Solution/Rbac/PermissionManagerWindow.xaml.cs`

## 계속 읽기

- [보안 및 권한 제어](./overview.md)
- [아키텍처 런타임](../overview/runtime.md)
- [구성요소 상호작용](../overview/comComponent-interactions.md)