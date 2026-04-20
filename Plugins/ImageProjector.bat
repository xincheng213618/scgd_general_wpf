@echo off
call "%~dp0..\Scripts\package_plugin.bat" ImageProjector %*
exit /b %errorlevel%