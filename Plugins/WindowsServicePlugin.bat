@echo off
call "%~dp0..\Scripts\package_plugin.bat" WindowsServicePlugin %*
exit /b %errorlevel%