import os
import time
import re

def clean_cvcie_files(directory):
    # 获取当前时间
    current_time = time.time()
    files_with_suffix = set()

        # 使用正则表达式匹配文件名结构
    pattern_with_suffix = re.compile(r"^(.*)_\d{14}_.+\.cvraw$")
    pattern_without_suffix = re.compile(r"^(.*)_\d{14}\.cvraw$")

    # 首先遍历文件，记录带后缀的文件名
    for root, dirs, files in os.walk(directory):
        for filename in files:
            if filename.endswith('.cvraw'):
                if pattern_with_suffix.match(filename):
                    base_name = pattern_with_suffix.match(filename).group(1)
                    files_with_suffix.add(base_name)
                    

    # 遍历目录中的所有文件和子目录
    for root, dirs, files in os.walk(directory):
        for filename in files:

            file_path = os.path.join(root, filename)
            file_mtime = os.path.getmtime(file_path)
            file_age = current_time - file_mtime

            # 删除超过10分钟的 CVCIE 文件
            if filename.endswith('cvcie') and file_age > 600:
                os.remove(file_path)
                print(f"Deleted CVCIE: {file_path}")

            # 删除不带后缀的 CVRAW 文件（如果有对应后缀文件）且超过10分钟
            if filename.endswith('.cvraw'):
                match = pattern_without_suffix.match(filename)
                if match:
                    base_name = match.group(1)
                    if base_name in files_with_suffix and file_age > 600:
                        os.remove(file_path)
                        print(f"Deleted CVRAW: {file_path}")



# 使用示例
directory = 'D:\\CVTest\\DEV.Camera.Default\\Data'
clean_cvcie_files(directory)
