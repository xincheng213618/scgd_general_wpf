@echo off
setlocal enabledelayedexpansion

REM Define the registry path
set "uninstallKey=HKLM\SOFTWARE\Microsoft\Windows\CurrentVersion\Uninstall"

REM Get all subkeys under the uninstall key
for /f "tokens=*" %%i in ('reg query "%uninstallKey%"') do (
    REM Get the DisplayName value
    for /f "tokens=*" %%j in ('reg query "%%i" /v DisplayName 2^>nul') do (
        set "displayName="
        for /f "tokens=2,*" %%k in ("%%j") do (
            set "displayName=%%l"
        )
        
        REM Check if DisplayName is "ColorVision"
        if "!displayName!"=="ColorVision" (
            REM Get the ModifyPath value
            for /f "tokens=*" %%m in ('reg query "%%i" /v ModifyPath 2^>nul') do (
                set "modifyPath="
                for /f "tokens=2,*" %%n in ("%%m") do (
                    set "modifyPath=%%o"
                )
                
                REM Execute the ModifyPath value
                call !modifyPath!
            )
        )
    )
)

endlocal
