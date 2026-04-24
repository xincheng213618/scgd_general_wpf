@echo off
call "%~dp0..\Scripts\package_plugin.bat" CaptchaOCR %*
exit /b %errorlevel%
