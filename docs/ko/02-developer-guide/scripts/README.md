# 스크립트 빌드 및 릴리스

ColorVision 프로젝트에는 애플리케이션 구축, 플러그인 패키징, 업데이트 게시 및 백엔드 업로드 관리를 위한 Python 스크립트 세트가 포함되어 있습니다.

## 스크립트 개요

| 스크립트 | 기능 |
|------|------|
| `build.py` | 기본 프로그램 설치 패키지 빌드 및 게시 |
| `build_update.py` | 증분 업데이트 패키지 구축 |
| `build_plugin.py` | 내부적으로 `package_cvxp.py`로 전달되는 호환 항목 |
| `generate_shared_files.py` | 호스트 출력 디렉터리를 스캔하여 `shared_files.json` 생성 |
| `package_cvxp.py` | `shared_files.json`을 기반으로 플러그인 제거 및 패키지/업로드 |
| `패키지_플러그인.bat` | `package_cvxp.py` 웨어하우스에서 원클릭 구성 및 플러그인 호출 |
| `패키지_프로젝트.bat` | 한 번의 클릭으로 웨어하우스에서 프로젝트를 빌드하고 `package_cvxp.py` |
| `package_cvxp_demo.bat` | 외부 플러그인 작성자를 위한 최소 패키징 예제 |
| `build_spectrum.py` | 스펙트럼 플러그인 빌드 |
| `publish_plugin.py` | 시장 백엔드에 플러그인 게시 |
| `backend_client.py` | 백엔드 업로드 공유 모듈 |
| `file_manager.py` | 파일 관리 도구 |

## 환경 구성

### 인증 구성

스크립트는 백엔드 인증을 위해 다음 환경 변수를 사용합니다.

``파워셸
#PowerShell
$env:COLORVISION_UPLOAD_URL = "http://xc213618.ddns.me:9998"
$env:COLORVISION_UPLOAD_USERNAME = "신청"
$env:COLORVISION_UPLOAD_PASSWORD = "신청"
```
``배쉬
# 배쉬(Git Bash/WSL)
내보내기 COLORVISION_UPLOAD_URL="http://xc213618.ddns.me:9998"
COLORVISION_UPLOAD_USERNAME="xincheng" 내보내기
COLORVISION_UPLOAD_PASSWORD="xincheng" 내보내기
```
::: 팁
환경 변수가 설정되지 않은 경우 스크립트는 기본 자격 증명 `xincheng/xincheng`을 사용합니다.
:::

### 선택적 구성

| 환경 변수 | 설명 | 기본값 |
|----------|------|---------|
| `COLORVISION_UPLOAD_URL` | 백엔드 업로드 주소 | `http://xc213618.ddns.me:9998` |
| `COLORVISION_UPLOAD_FOLDER` | 폴더 업로드 | '컬러비전' |
| `COLORVISION_UPLOAD_USERNAME` | 사용자 이름 업로드 | `신청` |
| `COLORVISION_UPLOAD_PASSWORD` | 비밀번호 업로드 | `신청` |
| `COLORVISION_REMOTE_UPLOAD` | 원격 업로드 활성화 여부 | `1`(활성화) |

## build.py - 메인 프로그램 빌드

기본 프로그램 설치 패키지를 빌드하고 백엔드에 업로드합니다.

### 사용법

``파워셸
# 빌드 완료(컴파일 + 패키지 + 업로드)
pyScripts\build.py

# 빌드를 건너뛰고 최신 설치 패키지만 업로드하세요.
py 스크립트\build.py --skip-build

# 원격 업로드 건너뛰기
py 스크립트\build.py --skip-remote-upload
```
### 기능 설명

1. MSBuild를 사용하여 솔루션 컴파일
2. 고급 설치 프로그램을 사용하여 설치 패키지 빌드
3. 백엔드 사전 확인 수행(`/api/health` + `/api/ready`)
4. 백엔드에 설치 패키지 업로드

### 전제조건

- 비주얼 스튜디오 2022+(MSBuild)
-고급 설치 프로그램
- Python 종속성: `요청`, `tqdm`

## build_update.py - 증분 업데이트 빌드

증분 업데이트 패키지를 생성합니다(변경 파일만 포함).

### 사용법

``파워셸
pyScripts\build_update.py
```
### 작동 원리

1. 'ColorVision.exe'를 읽어 최신 버전을 받으세요.
2. 기준으로 과거 버전 찾기
3. 파일 차이점을 비교하여 증분 패키지 생성
4. 'Update/' 디렉터리에 증분 패키지를 업로드합니다.

### 출력 파일

- `{History}/ColorVision-[{version}].zip` - 전체 패키지
- `{History}/update/ColorVision-Update-[{version}].cvx` - 증분 패키지

## build_plugin.py - 호환 가능한 항목

이전 패키징 구현이 제거되었습니다.

현재 `build_plugin.py`는 호환 가능한 진입점으로만 예약되어 있습니다. 웨어하우스의 일반적인 호출을 `package_cvxp.py`로 전달하고 마이그레이션 프롬프트를 출력합니다. 새 스크립트의 기본 진입점으로 사용하지 마십시오.

### 사용법

``파워셸
py Scripts\build_plugin.py -t 프로젝트 -p ProjectARVR --no-upload
```
### 권장 대안

- 웨어하우스의 플러그인: `Scripts\package_plugin.bat Spectrum --no-upload`
- 창고의 프로젝트: `Scripts\package_project.bat ProjectARVR --no-upload`
- 웨어하우스 외부: `py Scripts\package_cvxp.py --src-dir C:\src\MyPlugin\bin\x64\Release\net10.0-windows --no-upload`

## generate_shared_files.py - 공유 파일 테이블 생성

호스트 프로그램 출력 디렉터리를 스캔하고 `shared_files.json`을 생성합니다.

### 사용법

``파워셸
py 스크립트\generate_shared_files.py

py 스크립트\generate_shared_files.py `
    --root-dir C:\Users\17917\Desktop\scgd_general_wpf\ColorVision\bin\x64\Release\net10.0-windows `
    --output C:\temp\shared_files.json
```
### 출력 내용- `generated_at`: 생성된 시간
- `shared_files`: 호스트 디렉터리의 모든 상대 파일 경로

### 필터 규칙

- `Plugins` 디렉토리를 자동으로 무시합니다.
- `Log` 디렉토리를 자동으로 무시합니다.
- 일반적으로 호스트 공유 파일이 변경된 후 한 번만 다시 생성하면 됩니다.

## package_cvxp.py - 단일 파일 패키지 업로드

단일 파일 스크립트는 `shared_files.json`을 읽고 공유 파일과 `.pdb`를 제거하고 직접 업로드할 수 있는 `.cvxp`를 생성합니다.

### 사용법

``파워셸
#현지 포장만 가능
py Scripts\package_cvxp.py --project-file Plugins\Spectrum\Spectrum.csproj --build --no-upload

#컴파일 출력 디렉터리를 지정합니다.
py 스크립트\package_cvxp.py `
    --src-dir 플러그인\패턴\bin\x64\Release\net10.0-windows `
    --플러그인-루트 플러그인\패턴

# 컴파일 출력 디렉터리만 전달하고 플러그인 루트 디렉터리를 자동으로 추론합니다.
py 스크립트\package_cvxp.py `
    --src-dir C:\src\MyPlugin\bin\x64\Release\net10.0-windows `
    --업로드 안함
```
### 매개변수

| 매개변수 | 설명 | 기본값 |
|------|------|---------|
| `--src-dir` | 플러그인 컴파일 출력 디렉터리 | 비어 있음 |
| `--프로젝트 파일` | 플러그인 `.csproj` 경로 | 비어 있음 |
| `--플러그인-루트` | `README.md`와 같은 추가 파일을 보완하는 데 사용되는 플러그인 루트 디렉토리 | 자동 추론 |
| `--플러그인 이름` | 플러그인 이름 | 자동 추론 |
| `--공유 파일` | `shared_files.json` 경로; 전달되지 않으면 스크립트와 동일한 디렉터리에 있는 파일을 먼저 읽습니다. | 자동검색 |
| `--출력-디렉터리` | `.cvxp` 출력 디렉토리 | `스크립트/` |
| `--build` | 패키징하기 전에 `dotnet build` 실행 | 닫기 |
| `--dotnet` | `--build`에서 사용하는 `dotnet` 명령 | `닷넷` |
| `--업로드 안함` | 업로드하지 않고 패키지만 | 닫기 |
| `--유지 패키지` | 업로드 후 로컬 패키지 유지 | 닫기 |

### 패키징 논리

1. `shared_files.json`을 읽어보세요.
2. 플러그인 출력 디렉터리를 탐색합니다.
3. 모든 `.pdb` 파일 필터링
4. `shared_files.json`에 존재하는 모든 공유 파일을 필터링합니다.
5. `stripped_files.json`을 작성하세요.
6. `.cvxp`로 패키지
7. `--no-upload`가 지정되지 않은 경우 패키지 및 `LATEST_RELEASE`를 업로드합니다.

### 디렉터리 외부로 직접 전송

`--src-dir`이 `PluginName/bin/x64/Release/net10.0-windows` 또는 `PluginName/bin/Release/net10.0-windows`와 같은 디렉터리를 가리키는 경우 스크립트는 자동으로 `PluginName` 디렉터리를 `plugin_root`로 식별하므로 `--plugin-root`가 전달되지 않더라도 여전히 프로젝트 루트 디렉터리를 가져올 수 있습니다. `README.md`, `CHANGELOG.md`, `manifest.json`, `PackageIcon.png`.

## package_plugin.bat - 창고의 플러그인에 대한 빠른 입력

이 일괄 프로세스는 웨어하우스의 플러그인 프로젝트에서만 사용됩니다. 자동으로 `.venv`를 찾고 `package_cvxp.py --build`를 자동으로 호출하므로 각 플러그인 디렉토리의 `.bat` 파일은 전달을 위해 한 줄만 유지할 수 있습니다.

### 사용법

``파워셸
Scripts\package_plugin.bat 패턴 --no-upload
```
## package_project.bat - 창고의 프로젝트에 대한 빠른 입력

이 배치는 `package_plugin.bat`과 유사하지만 대상 디렉터리가 `Projects/*/*.csproj`로 변경됩니다. 고객 프로젝트 또는 프로젝트 기반 플러그인에 적합합니다.

### 사용법

``파워셸
스크립트\패키지_프로젝트.bat ProjectARVR --no-upload
```
## package_cvxp_demo.bat - 외부 전달 예시

이 일괄 처리는 창고 외부의 사용 시나리오를 대상으로 합니다. `package_cvxp.py`, `shared_files.json`과 이 데모를 같은 디렉터리에 넣고, 내부의 `SRC_DIR`을 수정하여 직접 패키징합니다.

### 사용법

``파워셸
스크립트\패키지_cvxp_demo.bat
```
## build_spectrum.py - 스펙트럼 플러그인 빌드

Spectrum 플러그인에 특별히 최적화된 스크립트를 빌드하세요.

### 사용법

``파워셸
# 빌드 및 업로드
py 스크립트\build_spectrum.py --upload

# 업로드하지 않고 빌드만 함
pyScripts\build_spectrum.py
```
### 기능

- .zip 및 .cvxp 출력 형식 지원
- 매핑된 플러그인 서버 경로에 복사된 .cvxp 패키지
- 인증을 사용한 .zip 패키지 업로드

## 게시_플러그인.py - 플러그인 게시

API를 통해 플러그인 패키지를 플러그인 시장에 게시합니다.

### 사용법

``파워셸
# 기본 릴리스
py 스크립트\publish_plugin.py -p Spectrum -v 1.0.0.1 -f Spectrum-1.0.0.1.cvxp

# 완전한 매개변수
py 스크립트\publish_plugin.py `
  -p 스펙트럼`
  -v 1.0.0.1`
  -f 스펙트럼-1.0.0.1.cvxp`
  -n "스펙트럼 플러그인" `
  -d "스펙트럼 분석 플러그인"`
  -a "저자 이름"`
  -c "분석"`
  --changelog CHANGELOG.md `
  --icon 패키지Icon.png

# 백엔드 주소를 지정합니다
py Scripts\publish_plugin.py -p Spectrum -v 1.0.0.1 -f Spectrum-1.0.0.1.cvxp --api-url http://localhost:9999
```
### 매개변수| 매개변수 | 설명 | 필수 |
|------|------|------|
| `-p, --plugin-id` | 플러그인 고유 ID | 예 |
| `-v, --version` | 버전 번호(예: 1.0.0.1) | 예 |
| `-f, --file` | 패키지 파일 경로 | 예 |
| `-n, --name` | 표시 이름 | 아니요 |
| `-d, --description` | 설명 | 아니 |
| `-a, --author` | 저자 | 아니 |
| `-c, --category` | 카테고리 | 아니요 |
| `-r, --requires` | 최소 엔진 버전 | 아니요 |
| `--changelog` | 로그 파일 또는 텍스트 변경 | 아니요 |
| `--아이콘` | 아이콘 파일 경로 | 아니요 |
| `--api-url` | 백엔드 주소 | 아니요 |
| `--사용자 이름` | 사용자 이름 | 아니요 |
| `--password` | 비밀번호 | 아니요 |

### 인증

게시 인터페이스에는 기본 인증 인증이 필요합니다.

``파워셸
# 방법 1: 환경 변수
$env:COLORVISION_UPLOAD_USERNAME = "귀하의 사용자"
$env:COLORVISION_UPLOAD_PASSWORD = "귀하의 비밀번호"

# 방법 2: 명령줄 매개변수
py Scripts\publish_plugin.py ... --username 귀하의 사용자 --password 귀하의 비밀번호
```
## backend_client.py - 백엔드 클라이언트

다른 스크립트에 대한 인증 및 업로드 기능을 제공하는 공유 백엔드 업로드 모듈입니다.

### 주요 기능

- 인증정보 확인(환경변수 -> 기본값)
- 업로드 URL 빌드
- 백엔드 사전 점검(상태 점검 + 준비 상태 점검)
- 스트리밍 PUT 업로드
- 인증 멀티파트 POST

### 사용 예

``파이썬
backend_client 가져오기에서(
    원격업로드 설정,
    preflight_remote_upload,
    업로드_파일,
    해결_업로드_자격 증명,
)

# 자격 증명 구문 분석
사용자 이름, 비밀번호 =solve_upload_credentials()

# 업로드 설정 구성
설정 = RemoteUploadSettings(
    base_url="http://localhost:9998",
    폴더_이름="플러그인/내 플러그인",
    사용자 이름=사용자 이름,
    비밀번호=비밀번호,
)

# 비행 전
preflight_remote_upload(설정)인 경우:
    # 파일 업로드
    upload_file(설정, "경로/to/file.cvxp")
```
### 프리플라이트 로직

업로드하기 전에 2단계 확인이 수행됩니다.

1. **상태 확인**(`GET /api/health`) - 백엔드 서비스를 사용할 수 있는지 확인
2. **준비 확인**(`GET /api/ready`) - 백엔드가 업로드를 수신할 준비가 되었는지 확인합니다.

백엔드가 404(이전 버전 백엔드)를 반환하면 호환 모드에 있는 것으로 간주되어 업로드가 계속됩니다.

## file_manager.py - 파일 관리

파일 관리 도구 클래스.

### 기능

- 파일 업로드 관리
- 경로 처리
- 진행상황 표시

### 사용 예

``파이썬
file_manager에서 FileManager 가져오기

fm = 파일관리자()

# 파일 업로드
fm.upload_file("path/to/file.zip", "ColorVision/업데이트")
```
## 스크립트 테스트

각 스크립트에는 해당 테스트 파일이 있습니다.

| 테스트 파일 | 설명 |
|------------|------|
| `test_backend_client.py` | 백엔드 클라이언트 테스트 |
| `test_build.py` | 빌드 스크립트 테스트 |
| `test_file_manager.py` | 파일 관리 테스트 |
| `test_build_update.py` | 빌드 테스트 업데이트 |
| `test_publish_plugin.py` | 플러그인 퍼블리싱 테스트 |

### 테스트 실행

``파워셸
# 단일 테스트 실행
파이썬 스크립트\test_backend_client.py

# 파이테스트를 사용한다
pytest 스크립트\test_*.py -v
```
## 문제 해결

### 업로드 실패(401 무단)

- 환경변수나 기본 자격 증명이 올바른지 확인하세요.
- 백엔드 `config.json`에서 `upload_auth` 구성을 확인하세요.

### 업로드 실패(연결 오류)

- 백엔드 서비스가 실행 중인지 확인
- 네트워크 연결 확인
- `COLORVISION_UPLOAD_URL` 구성 확인

### 빌드 실패

- MSBuild 경로가 올바른지 확인하세요.
- Advanced Installer가 설치되어 있는지 확인하세요.
- 솔루션이 제대로 컴파일되는지 확인

### 버전 번호를 읽지 못했습니다.

- 대상 DLL/EXE가 존재하는지 확인하세요.
- 파일 버전 정보가 올바르게 삽입되었는지 확인하세요.

## 모범 사례

1. **환경 변수 사용** - 스크립트에 민감한 정보를 하드코딩하지 마세요.
2. **실행 전 실패 처리** - 백엔드를 사용할 수 없을 때 스크립트는 명확한 오류 메시지를 제공합니다.
3. **버전 번호 관리** - DLL/EXE의 버전 정보가 출시된 버전과 일치하는지 확인합니다.
4. **먼저 테스트** - 공식 출시 전에 테스트 스크립트를 사용하여 기능을 확인합니다.
