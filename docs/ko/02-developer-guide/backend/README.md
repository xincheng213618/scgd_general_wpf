# 플러그인 마켓플레이스 백엔드

ColorVision 플러그인 마켓 백엔드는 플러그인 게시, 다운로드 및 버전 제어를 관리하기 위한 Python Flask 기반의 경량 서비스입니다.

## 기능 개요

백엔드 서비스는 다음과 같은 핵심 기능을 제공합니다.

- **웹 관리 인터페이스** - 플러그인 찾아보기, 검색, 다운로드 및 업로드
- **REST API** - WPF 데스크톱 클라이언트를 위한 인터페이스 제공
- **레거시 호환성** - 이전 버전의 클라이언트와 호환되는 라우팅을 지원합니다.
- **다운로드 통계** - SQLite 기반 다운로드 통계

## 프로젝트 구조

```
백엔드/마켓플레이스/
├── app.py # Flask 애플리케이션 메인 입구 (Web UI + API + 이전 버전 호환)
├── app_changelog.py # 로그 관리 모듈 업데이트
├── app_releases.py # 애플리케이션 버전 출시 관리
├── Catalog_view_models.py # 플러그인 카탈로그 뷰 모델
├── config.json # 구성 파일
├── download_stats.py # 통계 모듈 다운로드
├── Feedback_service.py # 사용자 피드백 서비스
├── Marketplace.db # SQLite 데이터베이스 (자동 생성, gitignored)
├── Marketplace_services.py # 시장 데이터 서비스
├── package_publish.py # 패키지 게시 확인 및 처리
├── page_contexts.py # 페이지 컨텍스트 구성
├──plugin_marketplace.py # 플러그인 마켓의 핵심 로직
├── 플러그인_queries.py # 플러그인 쿼리 인터페이스
├── 요구 사항.txt # Python 종속성
├── Runtime_health.py # 런타임 상태 확인
├── Storage_browser.py # 스토리지 브라우저
├── Storage_paths.py # 저장경로 관리
├── Storage_uploads.py # 업로드 처리
├── update_retention.py # 패키지 보존 정책 업데이트
├── static/ # 정적 리소스
└── template/ # Jinja2 템플릿 파일
    ├── 베이스.html
    ├── index.html
    ├── 플러그인.html
    ├──plugin_detail.html
    ├── 업로드.html
    └── browser.html
```
## 설치 및 실행

### 환경 요구사항

-파이썬 3.9+
-핍

### 종속성 설치

``배쉬
CD 백엔드/마켓플레이스
pip 설치 -r 요구사항.txt
```
### 구성 파일

`config.json`을 편집합니다.

```json
{
    "storage_path": "H:\\ColorVision",
    "호스트": "0.0.0.0",
    "포트": 9998,
    "디버그": 거짓,
    "secret_key": "당신의 비밀 키",
    "app_release_keep_count": 5,
    "plugin_package_keep_count": 3,
    "업로드_인증": {
        "사용자 이름": "관리자",
        "비밀번호": "관리자"
    }
}
```
구성 항목 설명:

| 구성 항목 | 설명 | 기본값 |
|---------|------|---------|
| `저장_경로` | 플러그인 및 애플리케이션의 저장 경로 | `저장/` |
| '호스트' | 청취주소 | `0.0.0.0` |
| '항구' | 수신 포트 | `9998` |
| `디버그` | 디버그 모드 | '거짓' |
| `비밀_키` | 플라스크 키 | 수정 필요 |
| `업로드_인증` | 인증 자격 증명 업로드 | 수정 필요 |

### 서비스 시작

``배쉬
# 기본 구성 사용
pythonapp.py

#저장 경로 지정
python app.py --storage H:\ColorVision

#포트 지정
파이썬 app.py --port 9999
```
## API 인터페이스

### 웹 UI 라우팅

| 라우팅 | 기능 |
|------|------|
| `GET /` | 홈 - 스토리지 개요, 빠른 링크 |
| `GET /플러그인` | 플러그인 마켓 - 검색, 분류, 정렬 |
| `GET /plugins/{id}` | 플러그인 세부정보 - 버전 목록, README, 다운로드 |
| `GET /업로드` | 페이지 업로드 |
| `POST /업로드` | 업로드 처리 |
| `GET /browse[/path]` | 파일 브라우저 |
| `GET /releases` | 릴리스 목록 |
| `GET /업데이트` | 패키지 목록 업데이트 |
| `GET /도구` | 도구 다운로드 목록 |

### REST API

| 방법 | 경로 | 설명 |
|------|------|------|
| 받기 | `/api/플러그인` | 플러그인 검색(키워드, 카테고리, 정렬, 페이지 매기기) |
| 받기 | `/api/plugins/{id}` | 플러그인 세부정보 + 모든 버전 |
| 받기 | `/api/plugins/{id}/최신 버전` | 일반 텍스트 최신 버전 |
| 포스트 | `/api/plugins/batch-version-check` | 일괄 버전 확인 |
| 받기 | `/api/플러그인/카테고리` | 모든 카테고리 가져오기 |
| 받기 | `/api/packages/{id}/{버전}` | 플러그인 패키지 다운로드 |
| 포스트 | `/api/패키지/게시` | 새 버전 게시(기본 인증 필요) |
| 받기 | `/api/stats` | 통계 다운로드 |
| 받기 | `/api/건강` | 상태 확인 엔드포인트 |
| 받기 | `/api/준비` | 준비 확인 끝점 |

### 이전 버전 호환 라우팅| 라우팅 모드 | 설명 |
|------------|------|
| `PUT /업로드/{경로}` | 이전 빌드 스크립트 업로드와 호환 가능 |
| `/D%3A/ColorVision/플러그인/{경로}` | 이전 클라이언트 버전 확인 및 다운로드와 호환 가능 |

## 인증

업로드 인터페이스는 HTTP 기본 인증을 사용하여 보호됩니다.

``배쉬
# 컬 예제 사용
컬 -u 사용자 이름:비밀번호 -X POST http://localhost:9998/api/packages/publish \
  -F "PluginId=스펙트럼" \
  -F "버전=1.0.0.1" \
  -F "패키지=@Spectrum-1.0.0.1.cvxp"
```
## 저장 구조

백엔드는 기존 파일 시스템 구조를 직접 사용합니다.

```
{저장_경로}/
├── LATEST_RELEASE # 최신 버전 번호 적용
├── CHANGELOG.md # 애플리케이션 업데이트 로그
├── History/ # History 전체 설치 패키지
├── 업데이트/ # 증분 업데이트 패키지
├── Plugins/ # 플러그인 디렉터리
│ ├── 스펙트럼/
│ │ ├── 최신_RELEASE
│ │ ├── 매니페스트.json
│ │ ├── PackageIcon.png
│ │ ├── README.md
│ │ ├── CHANGELOG.md
│ │ └── 스펙트럼-1.0.0.1.cvxp
│ └── ...
└── 도구/ # 도구 다운로드
```
## 테스트

백엔드에는 완전한 테스트 모음이 포함되어 있습니다.

``배쉬
#모든 테스트 실행
파이썬 -m pytest

#특정 테스트 파일 실행
파이썬 test_app.py
파이썬 test_app_releases.py
파이썬 test_page_contexts.py
파이썬 test_upload_services.py
```
## 빌드 스크립트와 통합

백엔드는 `Scripts/` 디렉터리의 빌드 스크립트와 통합됩니다.

- `publish_plugin.py` - `/api/packages/publish`를 사용하여 플러그인 게시
- `build.py` - 기본 프로그램 설치 패키지 업로드
- `build_update.py` - 증분 업데이트 패키지 업로드
- `build_spectrum.py` - 스펙트럼 플러그인 업로드

## 기술 스택

| 계층 구조 | 선택 | 버전 |
|------|------|------|
| 언어 | 파이썬 | 3.9+ |
| 프레임워크 | 플라스크 | >=3.0 |
| 템플릿 엔진 | 진자2 | 내장 |
| CSS 프레임워크 | 부트스트랩 5 | 5.x |
| 데이터베이스 | SQLite | 내장 |
| 마크다운 렌더링 | 인하 | >=3.8 |

## 접속주소

서비스가 시작된 후:

- 웹 UI: http://localhost:9998
- 플러그인 마켓: http://localhost:9998/plugins
- API: http://localhost:9998/api/plugins
- 파일 찾아보기: http://localhost:9998/browse

## 배포 권장사항

### 프로덕션 환경 배포

1. **Gunicorn/uWSGI 사용**

``배쉬
pip 설치 건니콘
gunicorn -w 4 -b 0.0.0.0:9998 앱:앱
```
2. **Nginx 역방향 프록시**

```nginx
서버 {
    들어라 80;
    server_name 마켓플레이스.example.com;

    위치/{
        프록시패스 http://localhost:9998;
        Proxy_set_header 호스트 $host;
        Proxy_set_header X-Real-IP $remote_addr;
    }
}
```
3. **HTTPS 활성화**

Let's Encrypt 또는 자체 서명된 인증서를 사용하여 HTTPS를 활성화합니다.

4. **모니터링 및 로깅**

- 로그 회전 구성
-모니터링 알람 설정
- 정기적으로 디스크 공간을 확인하십시오.

## 문제 해결

### 서비스를 시작할 수 없습니다

포트가 사용 중인지 확인합니다.
``배쉬
netstat -an | findstr 9998
```
### 업로드 실패

- 'upload_auth'가 올바르게 구성되었는지 확인하세요.
- 저장 경로 권한 확인
- 로그 오류 메시지 보기

### 데이터베이스 오류

자동으로 생성된 `marketplace.db` 파일을 삭제하면 서비스 재시작 후 자동으로 재구축됩니다.
