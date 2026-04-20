@echo off
call "%~dp0..\Scripts\package_project.bat" ProjectARVRPro %*
exit /b %errorlevel%