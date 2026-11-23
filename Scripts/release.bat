@echo off
setlocal

cd /d "%~dp0.."

python Scripts\build.py
if errorlevel 1 exit /b %errorlevel%

python Scripts\build_update.py
if errorlevel 1 exit /b %errorlevel%
