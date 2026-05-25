# 엔진 개발 가이드

ColorVision 엔진 레이어의 기능을 개발하고 확장하는 방법을 설명합니다.

## 개요

ColorVision.Engine은 시스템의 핵심 엔진 레이어이며 다음을 담당합니다.

- 🔧 장비 서비스 관리
- 🔄 프로세스 엔진
- 📐 알고리즘 템플릿 시스템
- 📡 MQTT 메시지 처리
- 🖼️ OpenCV 이미지 처리

## 엔진 아키텍처

```
ColorVision.Engine
├── 서비스/ # 장비 및 서비스
├── 템플릿/ # 템플릿 시스템
├── MQTT/ # MQTT 메시지 처리
├── 알고리즘/ # 알고리즘 구현
└── 유틸리티/ # 도구
```
## 주요 구성 요소

### 1. 서비스 시스템

자세한 내용은 [서비스 개발 가이드](./services.md)를 참조하세요.

### 2. 템플릿 시스템

자세한 내용은 [템플릿 시스템 개발](./templates.md)을 참조하세요.

### 3. MQTT 메시지 처리

자세한 내용은 [MQTT 메시지 처리](./mqtt.md)를 참조하세요.

### 4. OpenCV 통합

자세한 내용은 [OpenCV 통합 개발](./opencv-integration.md)을 참조하세요.

## 개발 과정

### 1. 서비스 생성

```csharp
공개 클래스 MyDeviceService : DeviceService
{
    공개 재정의 문자열 ServiceName => "내 장치";
    
    보호된 재정의 작업 OnStartAsync()
    {
        //장치 초기화
        Task.CompletedTask를 반환합니다.
    }
    
    보호된 재정의 작업 OnStopAsync()
    {
        // 장치를 중지합니다
        Task.CompletedTask를 반환합니다.
    }
}
```
### 2. 등록 서비스

```csharp
ServiceManager.GetInstance().Add\<IMyDeviceService, MyDeviceService>();
```
### 3. 서비스 이용하기

```csharp
var service = ServiceManager.GetInstance().GetService\<IMyDeviceService>();
service.StartAsync()를 기다립니다.
```
## 모범 사례

1. **인터페이스 정의**: 각 서비스에 대한 인터페이스를 정의합니다.
2. **종속성 주입**: ServiceManager를 사용하여 종속성 관리
3. **비동기 작업**: 시간이 많이 걸리는 작업에는 async/await를 사용하세요.
4. **예외 처리**: 예외를 적절하게 처리하고 로그를 기록합니다.
5. **리소스 관리**: IDisposable을 구현하여 리소스 해제

## 관련 문서

- [서비스 개발 가이드](./services.md)
- [템플릿 시스템 개발](./templates.md)
- [MQTT 메시지 처리](./mqtt.md)
- [OpenCV 통합 개발](./opencv-integration.md)
- [엔진 API 레퍼런스](/ko/04-api-reference/engine-comComponents/README.md)

## 샘플 코드

참조:

- `Engine/ColorVision.Engine/Services/` - 서비스 구현
- `Engine/ColorVision.Engine/Templates/` - 템플릿 시스템
- `Engine/ColorVision.Engine/MQTT/` - MQTT 구현

---

*자세한 기술적인 내용은 각 하위주제 문서를 참고하세요. *
