@echo off
call "%~dp0..\Scripts\package_plugin.bat" ScreenRecorder %*
exit /b %errorlevel%