#!/bin/bash
# ColorVision 许可证生成工具启动脚本
# 使用说明: ./run.sh [参数]

# 检查是否有参数传入
if [ $# -eq 0 ]; then
    echo "启动交互式许可证生成工具..."
    dotnet run --project LicenseGenerator.csproj
else
    echo "运行许可证生成工具..."
    dotnet run --project LicenseGenerator.csproj -- "$@"
fi
