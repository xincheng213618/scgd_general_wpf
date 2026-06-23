@echo off
setlocal

powershell -NoProfile -ExecutionPolicy Bypass -File "%~dp0Run-Web.ps1" %*
if errorlevel 1 (
  echo.
  echo ColorVision Web failed to start.
  pause
)

endlocal
