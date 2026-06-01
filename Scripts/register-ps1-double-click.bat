@echo off
setlocal

set "EXT_KEY=HKCU\Software\Classes\.ps1"
set "CLASS_KEY=HKCU\Software\Classes\Microsoft.PowerShellScript.1"
set "VERB_NAME=RunBypass"
set "VERB_KEY=%CLASS_KEY%\Shell\%VERB_NAME%"
set "USER_CHOICE_KEY=HKCU\Software\Microsoft\Windows\CurrentVersion\Explorer\FileExts\.ps1\UserChoice"
set "PS_EXE=%SystemRoot%\System32\WindowsPowerShell\v1.0\powershell.exe"

if not exist "%PS_EXE%" set "PS_EXE=powershell.exe"

if /i "%~1"=="--dry-run" goto :dryrun
if not "%~1"=="" goto :usage

reg add "%EXT_KEY%" /ve /t REG_SZ /d "Microsoft.PowerShellScript.1" /f >nul || goto :error
reg add "%CLASS_KEY%\Shell" /ve /t REG_SZ /d "%VERB_NAME%" /f >nul || goto :error
reg add "%VERB_KEY%" /ve /t REG_SZ /d "Run PowerShell Script" /f >nul || goto :error
reg add "%VERB_KEY%" /v "Icon" /t REG_SZ /d "%PS_EXE%,0" /f >nul || goto :error
reg add "%VERB_KEY%\Command" /ve /t REG_SZ /d "\"%PS_EXE%\" -NoLogo -NoProfile -ExecutionPolicy Bypass -File \"%%1\" %%*" /f >nul || goto :error
reg delete "%USER_CHOICE_KEY%" /f >nul 2>&1

echo Registered .ps1 double-click action for the current user.
echo Cleared the Explorer UserChoice override for .ps1.
echo Double-clicking a .ps1 file should now run it with PowerShell.
echo If Explorer still uses the old behavior, restart Explorer or sign out and sign back in.
exit /b 0

:dryrun
echo reg add "%EXT_KEY%" /ve /t REG_SZ /d "Microsoft.PowerShellScript.1" /f
echo reg add "%CLASS_KEY%\Shell" /ve /t REG_SZ /d "%VERB_NAME%" /f
echo reg add "%VERB_KEY%" /ve /t REG_SZ /d "Run PowerShell Script" /f
echo reg add "%VERB_KEY%" /v "Icon" /t REG_SZ /d "%PS_EXE%,0" /f
echo reg add "%VERB_KEY%\Command" /ve /t REG_SZ /d "\"%PS_EXE%\" -NoLogo -NoProfile -ExecutionPolicy Bypass -File \"%%1\" %%*" /f
echo reg delete "%USER_CHOICE_KEY%" /f
exit /b 0

:usage
echo Usage: %~nx0 [--dry-run]
exit /b 1

:error
echo Failed to update the current-user registry entries for .ps1.
exit /b 1