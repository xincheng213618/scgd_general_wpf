@echo off
cd /d "%~dp0"
python build_spectrum.py --upload %*
pause
