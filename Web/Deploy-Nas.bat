@echo off
setlocal

powershell -NoProfile -ExecutionPolicy Bypass -File "%~dp0Deploy-Nas.ps1" %*
if errorlevel 1 (
  echo.
  echo ColorVision Web NAS deployment failed.
  pause
)

endlocal
