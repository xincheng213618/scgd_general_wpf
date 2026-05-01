@echo off
call "%~dp0..\Scripts\package_plugin.bat" YoloObjectDetection %*
exit /b %errorlevel%