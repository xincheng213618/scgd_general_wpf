@echo off
setlocal
set "SCRIPT_DIR=%~dp0"

if "%~1"=="" (
    echo Usage: package_project.bat ProjectName [package_cvxp arguments]
    exit /b 1
)

set "PROJECT_NAME=%~1"
shift
set "PACKAGE_ARGS="

:collect_args
if "%~1"=="" goto run_package
set PACKAGE_ARGS=%PACKAGE_ARGS% "%~1"
shift
goto collect_args

:run_package

for %%I in ("%SCRIPT_DIR%..") do set "REPO_ROOT=%%~fI"
set "PYTHON_EXE=%REPO_ROOT%\.venv\Scripts\python.exe"
set "PROJECT_FILE=%REPO_ROOT%\Projects\%PROJECT_NAME%\%PROJECT_NAME%.csproj"

if not exist "%PYTHON_EXE%" set "PYTHON_EXE=python"
if not exist "%PROJECT_FILE%" (
    echo Project file not found: %PROJECT_FILE%
    exit /b 1
)

"%PYTHON_EXE%" "%REPO_ROOT%\Scripts\package_cvxp.py" --project-file "%PROJECT_FILE%" --build %PACKAGE_ARGS%
exit /b %errorlevel%
