@echo off
call "%~dp0..\Scripts\package_project.bat" ProjectARVRLite %*
exit /b %errorlevel%