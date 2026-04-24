@echo off
call "%~dp0..\Scripts\package_project.bat" ProjectARVR %*
exit /b %errorlevel%