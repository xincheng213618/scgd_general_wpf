@echo off
cd /d "%~dp0..\Scripts"
python build_spectrum.py --upload %*
pause