@echo off
REM Build and preview VitePress documentation

echo Building VitePress documentation...
call npm run docs:build

if %ERRORLEVEL% EQU 0 (
    echo.
    echo Build successful! Starting preview server...
    call npm run docs:preview
) else (
    echo.
    echo Build failed! Check the error messages above.
    pause
)
