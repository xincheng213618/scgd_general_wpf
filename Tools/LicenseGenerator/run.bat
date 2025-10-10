@echo off
REM ColorVision 许可证生成工具启动脚本 (UI 版本)
REM 使用说明: run.bat

setlocal

echo 启动许可证生成工具 (图形界面)...
dotnet run --project LicenseGenerator.csproj

endlocal

