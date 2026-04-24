@echo off
call "%~dp0..\Scripts\package_project.bat" ProjectLUX %*
exit /b %errorlevel%