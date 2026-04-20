@echo off
call "%~dp0..\Scripts\package_project.bat" ProjectKB %*
exit /b %errorlevel%