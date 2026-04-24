@echo off
call "%~dp0..\Scripts\package_project.bat" ProjectBlackMura %*
exit /b %errorlevel%