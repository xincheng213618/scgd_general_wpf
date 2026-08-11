@echo off
setlocal

cd /d "%~dp0.."

python Scripts\release.py
if errorlevel 1 exit /b %errorlevel%
