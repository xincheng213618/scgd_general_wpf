@echo off
call "%~dp0..\Scripts\package_plugin.bat" EventVWR %*
exit /b %errorlevel%