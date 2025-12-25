@echo off
REM 编译 M_FindLuminousArea 测试程序
REM 需要先设置好 Visual Studio 环境

echo ========================================
echo 编译 M_FindLuminousArea 测试程序
echo ========================================
echo.

REM 检查是否在 Developer Command Prompt 中
where cl >nul 2>&1
if %errorlevel% neq 0 (
    echo 错误: 未找到 cl.exe 编译器
    echo 请在 "Developer Command Prompt for VS 2022" 中运行此脚本
    echo 或运行: "C:\Program Files\Microsoft Visual Studio\2022\Community\Common7\Tools\VsDevCmd.bat"
    pause
    exit /b 1
)

echo [1/4] 检查依赖...
if not exist "..\..\x64\Debug\opencv_helper.lib" (
    if not exist "..\..\x64\Release\opencv_helper.lib" (
        echo 警告: 未找到 opencv_helper.lib
        echo 请先编译 opencv_helper 项目
        echo 运行: msbuild ..\..\Core\opencv_helper\opencv_helper.vcxproj /p:Configuration=Debug /p:Platform=x64
        pause
        exit /b 1
    )
)

echo [2/4] 清理旧文件...
if exist test_find_luminous_area.exe del test_find_luminous_area.exe
if exist test_find_luminous_area.obj del test_find_luminous_area.obj

echo [3/4] 编译源文件...

REM 使用 Debug 配置编译
cl /EHsc /std:c++17 /MDd /Zi /Od ^
   /DWIN32 /D_DEBUG /D_CONSOLE ^
   /I"..\..\include" ^
   /I"..\..\packages\opencv\include" ^
   /I"..\..\packages\nlohmann\include" ^
   test_find_luminous_area.cpp ^
   /Fe:test_find_luminous_area.exe ^
   /link ^
   /DEBUG ^
   /SUBSYSTEM:CONSOLE ^
   /LIBPATH:"..\..\x64\Debug" opencv_helper.lib ^
   /LIBPATH:"..\..\packages\opencv\lib" opencv_world4100d.lib

if %errorlevel% neq 0 (
    echo.
    echo 编译失败!
    pause
    exit /b 1
)

echo [4/4] 编译成功!
echo.
echo 输出文件: test_find_luminous_area.exe
echo.
echo 运行方法:
echo   1. 基本测试: test_find_luminous_area.exe
echo   2. 测试图像: test_find_luminous_area.exe "图像路径.png"
echo.
echo 确保 opencv_helper.dll 和 OpenCV DLL 在可访问路径中
echo.

pause
