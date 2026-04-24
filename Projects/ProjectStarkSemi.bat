@echo off
call "%~dp0..\Scripts\package_project.bat" ProjectStarkSemi %*
exit /b %errorlevel%