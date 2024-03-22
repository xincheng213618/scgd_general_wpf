@echo off
if not exist ".\x64\Debug\" (
    mkdir ".\x64\Debug\"
)


copy /Y ".\packages\opencv\x64\opencv_core480d.dll" ".\x64\Debug\"
copy /Y ".\packages\opencv\x64\opencv_imgproc480d.dll" ".\x64\Debug\"
copy /Y ".\packages\opencv\x64\opencv_videoio480d.dll" ".\x64\Debug\"
copy /Y ".\packages\opencv\x64\opencv_highgui480d.dll" ".\x64\Debug\"
copy /Y ".\packages\opencv\x64\opencv_imgcodecs480d.dll" ".\x64\Debug\"


if not exist ".\x64\Release\" (
    mkdir ".\x64\Release\"
)

copy /Y ".\packages\opencv\x64\opencv_core480.dll" ".\x64\Release\"
copy /Y ".\packages\opencv\x64\opencv_imgproc480.dll" ".\x64\Release\"
copy /Y ".\packages\opencv\x64\opencv_videoio480.dll" ".\x64\Release\"
copy /Y ".\packages\opencv\x64\opencv_highgui480.dll" ".\x64\Release\"
copy /Y ".\packages\opencv\x64\opencv_imgcodecs480.dll" ".\x64\Release\"


if not exist ".\ColorVision\bin\x64\Debug\net6.0-windows\" (
    mkdir ".\ColorVision\bin\x64\Debug\net6.0-windows\"
)

copy /Y ".\packages\opencv\x64\opencv_core480d.dll" ".\ColorVision\bin\x64\Debug\net6.0-windows\""
copy /Y ".\packages\opencv\x64\opencv_imgproc480d.dll" ".\ColorVision\bin\x64\Debug\net6.0-windows\""
copy /Y ".\packages\opencv\x64\opencv_videoio480d.dll" ".\ColorVision\bin\x64\Debug\net6.0-windows\""
copy /Y ".\packages\opencv\x64\opencv_highgui480d.dll" ".\ColorVision\bin\x64\Debug\net6.0-windows\""
copy /Y ".\packages\opencv\x64\opencv_imgcodecs480d.dll" ".\ColorVision\bin\x64\Debug\net6.0-windows\""
copy /Y ".\x64\Release\OpenCVHelper.dll" ".\ColorVision\bin\x64\Debug\net6.0-windows\"



if not exist ".\ColorVision\bin\x64\Release\net6.0-windows\" (
    mkdir ".\ColorVision\bin\x64\Release\net6.0-windows\"
)

copy /Y ".\packages\opencv\x64\opencv_core480.dll" ".\ColorVision\bin\x64\Release\net6.0-windows\"
copy /Y ".\packages\opencv\x64\opencv_imgproc480.dll" ".\ColorVision\bin\x64\Release\net6.0-windows\"
copy /Y ".\packages\opencv\x64\opencv_videoio480.dll" ".\ColorVision\bin\x64\Release\net6.0-windows\"
copy /Y ".\packages\opencv\x64\opencv_highgui480.dll" ".\ColorVision\bin\x64\Release\net6.0-windows\"
copy /Y ".\packages\opencv\x64\opencv_imgcodecs480.dll" ".\ColorVision\bin\x64\Release\net6.0-windows\"
copy /Y ".\x64\Release\OpenCVHelper.dll" ".\ColorVision\bin\x64\Release\net6.0-windows\"




REM @echo off
REM setlocal enabledelayedexpansion

REM REM Define source and destination directories
REM set "SOURCE_DIR=.\packages\opencv\x64"
REM set "DEBUG_DIR=.\x64\Debug"
REM set "RELEASE_DIR=.\x64\Release"
REM set "COLORVISION_DEBUG_DIR=.\ColorVision\bin\x64\Debug\net6.0-windows"
REM set "COLORVISION_RELEASE_DIR=.\ColorVision\bin\x64\Release\net6.0-windows"

REM REM Define file names
REM set "DEBUG_FILES=opencv_core480d.dll opencv_imgproc480d.dll opencv_videoio480d.dll opencv_highgui480d.dll opencv_imgcodecs480d.dll"
REM set "RELEASE_FILES=opencv_core480.dll opencv_imgproc480.dll opencv_videoio480.dll opencv_highgui480.dll opencv_imgcodecs480.dll"

REM REM Create directories if they do not exist
REM for %%D in ("%DEBUG_DIR%" "%RELEASE_DIR%" "%COLORVISION_DEBUG_DIR%" "%COLORVISION_RELEASE_DIR%") do (
    REM if not exist "%%~D" (
        REM mkdir "%%~D" 2>nul
        REM if errorlevel 1 (
            REM echo Failed to create directory "%%~D"
            REM exit /b 1
        REM )
    REM )
REM )

REM REM Copy debug files
REM for %%F in (%DEBUG_FILES%) do (
    REM if exist "%SOURCE_DIR%\%%F" (
        REM copy /Y "%SOURCE_DIR%\%%F" "%DEBUG_DIR%"
        REM copy /Y "%SOURCE_DIR%\%%F" "%COLORVISION_DEBUG_DIR%"
    REM ) else (
        REM echo Warning: "%%F" not found in "%SOURCE_DIR%"
    REM )
REM )

REM REM Copy release files
REM for %%F in (%RELEASE_FILES%) do (
    REM if exist "%SOURCE_DIR%\%%F" (
        REM copy /Y "%SOURCE_DIR%\%%F" "%RELEASE_DIR%"
        REM copy /Y "%SOURCE_DIR%\%%F" "%COLORVISION_RELEASE_DIR%"
    REM ) else (
        REM echo Warning: "%%F" not found in "%SOURCE_DIR%"
    REM )
REM )

REM endlocal








