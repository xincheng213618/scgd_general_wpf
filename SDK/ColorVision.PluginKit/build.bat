@echo off
setlocal
chcp 65001 >nul

set "ROOT_DIR=%~dp0"
for %%I in ("%ROOT_DIR%..\..\.venv\Scripts\python.exe") do set "PYTHON_EXE=%%~fI"

echo ========================================
echo   cvplugin 打包脚本
echo ========================================
echo.

if exist "%PYTHON_EXE%" (
    goto python_ready
)

set "PYTHON_EXE=python"
where python >nul 2>&1
if errorlevel 1 (
    echo [!] 未找到 Python，请先安装 Python 或仓库 .venv。
    pause
    exit /b 1
)

:python_ready

echo [*] 使用 Python: %PYTHON_EXE%

"%PYTHON_EXE%" -m PyInstaller --version >nul 2>&1
if errorlevel 1 (
    echo [!] 未找到 PyInstaller，正在安装...
    "%PYTHON_EXE%" -m pip install pyinstaller
    if errorlevel 1 (
        echo [!] PyInstaller 安装失败
        pause
        exit /b 1
    )
)

echo [*] 开始打包...
echo.

pushd "%ROOT_DIR%"
"%PYTHON_EXE%" -m PyInstaller --noconfirm --clean cvplugin.spec
set "BUILD_EXIT=%ERRORLEVEL%"
popd

if not "%BUILD_EXIT%"=="0" (
    echo.
    echo [!] 打包失败
    pause
    exit /b %BUILD_EXIT%
)

echo.
echo ========================================
echo   打包完成！
echo   Output: dist\cvplugin.exe
for %%F in ("%ROOT_DIR%dist\cvplugin.exe") do echo   大小: %%~zF bytes
echo ========================================
echo.
pause