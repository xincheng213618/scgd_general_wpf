import os
import subprocess

def convert_cvraw_to_tif(directory):
    # 遍历指定目录及其子目录
    for root, _, files in os.walk(directory):
        for file in files:
            if file.endswith('.cvraw'):
                cvraw_path = os.path.join(root, file)
                tif_path = os.path.splitext(cvraw_path)[0] + '.tif'
                # 构建命令
                command = fr'"C:\Program Files\ColorVision Inc\ColorVision\ColorVision" -e "{cvraw_path}" -o "C:\Users\Xin\Desktop\pythontool" -q -t jpg -mx 10 '
                print(command)
                # 执行命令
                try:
                    result = subprocess.run(command, check=True, shell=True, capture_output=True, text=True)
                    print(f'Success: {result.stdout}')
                except subprocess.CalledProcessError as e:
                    print(f'Error converting {cvraw_path}: {e.stderr}')

# 使用示例
convert_cvraw_to_tif(r'D:\CVTest\DEV.Camera.Default\Data\2025-05-06')