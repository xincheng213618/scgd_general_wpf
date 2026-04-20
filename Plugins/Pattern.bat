@echo off
call "%~dp0..\Scripts\package_plugin.bat" Pattern %*
exit /b %errorlevel%