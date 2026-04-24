@echo off
setlocal

set "SCRIPT_DIR=%~dp0"
set "PYTHON_EXE=python"

rem Modify this path to your compiled plugin output directory.
set "SRC_DIR=C:\path\to\MyPlugin\bin\x64\Release\net10.0-windows"

rem Optional: set OUTPUT_DIR if you do not want the package next to package_cvxp.py.
set "OUTPUT_DIR=%SCRIPT_DIR%dist"

if not exist "%SRC_DIR%" (
    echo Source directory not found: %SRC_DIR%
    exit /b 1
)

if not exist "%SCRIPT_DIR%shared_files.json" (
    echo shared_files.json not found next to package_cvxp.py: %SCRIPT_DIR%shared_files.json
    exit /b 1
)

"%PYTHON_EXE%" "%SCRIPT_DIR%package_cvxp.py" --src-dir "%SRC_DIR%" --output-dir "%OUTPUT_DIR%" --no-upload
exit /b %errorlevel%