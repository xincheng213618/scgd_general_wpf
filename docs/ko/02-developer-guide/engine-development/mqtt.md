# Engine MQTT 메시지 처리 인수인계

이 문서는 Engine 계층의 현재 MQTT 모델을 설명합니다. 현재 주 경로는 모듈마다 MQTT client를 따로 만드는 방식이 아닙니다. `MQTTControl`이 연결, 구독, 발행, trace를 관리하고 장치 서비스는 `MQTTServiceBase` / `MQTTDeviceService<T>`로 명령을 보내 응답을 기다립니다.

## 현재 계층

| 계층 | 핵심 객체 | 역할 |
| --- | --- | --- |
| 전역 연결 | `MQTTControl` | `IMqttClient`, 연결, 재연결, 구독 캐시, 발행, 최근 200개 trace |
| 설정 | `MQTTSetting`, `MQTTConfig` | Host, Port, UserName, UserPwd, 보안 저장 |
| 시작 | `MqttInitializer` | 호스트 초기화 시 MQTT 연결 |
| 장치 명령 | `MQTTServiceBase` | `MsgRecord`, `MsgSend`, `MsgID` 기반 `MsgReturn` 매칭, 타임아웃 |
| 장치 바인딩 | `MQTTDeviceService<T>` | 설정에서 `SendTopic`, `SubscribeTopic` 읽기 |
| Flow MQTT | `FlowEngineLib/MQTT/` | visual Flow의 publish/subscribe hub |

## 명령 체인

1. 장치 UI, Flow 또는 프로젝트가 구체 `MQTT*` 메서드를 호출합니다.
2. `MQTT*`가 `MsgSend`를 만들고 `EventName`과 파라미터를 설정합니다.
3. `MQTTServiceBase.PublishAsyncClient()`가 `MsgID`, `DeviceCode`, `Token`, `ServiceName`을 채웁니다.
4. `MsgRecord`를 만들고 메시지 DB에 저장하며 타임아웃 타이머를 시작합니다.
5. `MQTTControl.PublishAsyncClient()`가 `SendTopic`으로 발행합니다.
6. 응답은 `SubscribeTopic`으로 오고, `MsgID`로 대기 기록에 매칭됩니다.

## 변경 위치

| 목표 | 주요 파일 |
| --- | --- |
| broker 설정 | `MQTTSetting.cs`, `MQTTConnect.xaml.cs` |
| 연결/재연결 | `MQTTControl.cs`, `MqttInitializer.cs` |
| 장치 명령 추가 | `Services/Devices/*/MQTT*.cs` |
| topic 변경 | `DeviceServiceConfig`와 장치 설정 UI |
| 응답 처리 변경 | `MQTTServiceBase` 또는 구체 `MQTT*` |
| Flow MQTT 노드 | `FlowEngineLib/MQTT/` |

## 인수인계 검증

- MQTT 설정 저장 후 재시작해도 연결됩니다.
- SEND/RECV를 trace 또는 로그에서 확인할 수 있습니다.
- `MsgRecord`에 송신 시간, 수신 시간, 상태, 응답이 남습니다.
- 실패 응답을 성공으로 표시하지 않습니다.
- 타임아웃 후 대기 상태가 정리됩니다.
- 재연결 후 캐시된 topic이 다시 구독됩니다.

## 관련 문서

- [Engine 장치 서비스 체인](../../04-api-reference/engine-components/device-service-chain.md)
- [Engine 업무 시나리오 인수인계](../../04-api-reference/engine-components/business-scenario-playbook.md)
- [FlowEngineLib](../../04-api-reference/engine-components/FlowEngineLib.md)
- [테스트 및 검증 인수인계](../testing.md)
