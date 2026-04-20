@echo off
call "%~dp0..\Scripts\package_plugin.bat" SystemMonitor %*
exit /b %errorlevel%