# OpenCV 및 native 통합 개발 인수인계

이 문서는 현재 저장소의 OpenCV/native 경계를 설명합니다. Engine에는 `cvColorVision` SDK/native wrapper 계층이 있고, UI/Core에는 `opencv_helper.dll` / `opencv_cuda.dll` P/Invoke wrapper가 있으며, 파일 표시에는 `.cvraw` / `.cvcie` 파싱과 썸네일이 포함됩니다.

## 현재 계층

| 계층 | 디렉터리 또는 파일 | 역할 |
| --- | --- | --- |
| 장치 SDK wrapper | `Engine/cvColorVision/` | 카메라, 분광기, 센서, OLED 알고리즘, MQTTMessageLib, native export |
| UI/Core native wrapper | `UI/ColorVision.Core/` | `HImage`, `OpenCVMediaHelper`, `OpenCVCuda`, `ImageCompute` |
| 파일 파싱/표시 | `Engine/ColorVision.Engine/Media/` | `.cvraw`, `.cvcie`, 썸네일, CIE export, 이미지 도구 |
| 테스트 | `Test/opencv_helper_test/` | C++ 검증. 현재는 `M_FindLuminousArea` 중심 |

## 변경 위치

| 목표 | 주요 위치 |
| --- | --- |
| SDK export 추가 | `Engine/cvColorVision/` |
| WPF에서 호출할 이미지 처리 추가 | `UI/ColorVision.Core/OpenCVMediaHelper.cs` 또는 `OpenCVCuda.cs` |
| `.cvraw` / `.cvcie` 표시나 썸네일 변경 | `Engine/ColorVision.Engine/Media/` |
| 밝은 영역, pseudo-color, SFR, white balance 변경 | native `opencv_helper.dll` 및 C# signature |
| CUDA fusion 변경 | `opencv_cuda.dll`, `OpenCVCuda`, `ImageCompute` |

## P/Invoke 규칙

- C# signature는 calling convention, 문자열 인코딩, 구조체 layout, 해제 방식까지 native export와 일치해야 합니다.
- `HImage`는 native buffer를 가지므로 실패 시 출력 버퍼를 해제해야 합니다.
- `IntPtr` 문자열을 반환하는 helper는 `FreeResult()` 필요 여부를 확인합니다.
- x64가 주요 배포 대상입니다. native DLL, 테스트, 호스트 platform을 맞춥니다.
- `cvColorVision`은 순수 managed 알고리즘 라이브러리가 아니라 native binding과 메시지 타입 계층입니다.

## 검증 명령

```powershell
dotnet build UI/ColorVision.Core/ColorVision.Core.csproj -c Release -p:Platform=x64
dotnet build Engine/cvColorVision/cvColorVision.csproj -c Release -p:Platform=x64
msbuild Test/opencv_helper_test/opencv_helper_test.vcxproj /p:Configuration=Debug /p:Platform=x64
Test/opencv_helper_test/build_test_find_luminous.bat
```

## 관련 문서

- [cvColorVision](../../04-api-reference/engine-components/cvColorVision.md)
- [ColorVision.Core](../../04-api-reference/ui-components/ColorVision.Core.md)
- [ColorVision.ShellExtension](../../04-api-reference/engine-components/ColorVision.ShellExtension.md)
- [테스트 및 검증 인수인계](../testing.md)
