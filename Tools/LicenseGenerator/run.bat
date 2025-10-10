@echo off
REM ColorVision 许可证生成工具启动脚本
REM 使用说明: run.bat [参数]

setlocal

REM 检查是否有参数传入
if "%~1"=="" (
    echo 启动交互式许可证生成工具...
    dotnet run --project LicenseGenerator.csproj
) else (
    echo 运行许可证生成工具...
    dotnet run --project LicenseGenerator.csproj -- %*
)

endlocal
