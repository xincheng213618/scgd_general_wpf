import subprocess
import os
import re
import shutil


# 执行 AdvancedInstaller.com 命令
def rebuild_project(aip_path):
    try:
        subprocess.run(r'dotnet build C:\Users\17917\Desktop\scgd_general_wpf\build.sln -c Release /p:Platform=x64', check=True)
        subprocess.run([r'C:\Users\17917\Desktop\AdvancedInstaller v19.7.1\App\ProgramFiles\bin\x86\AdvancedInstaller.com', '/rebuild', aip_path], check=True)
    except subprocess.CalledProcessError as e:
        print(f"An error occurred while rebuilding the project: {e}")
        return None


# 获取目录中最新的文件
def get_latest_file(directory, file_pattern):
    files = [os.path.join(directory, f) for f in os.listdir(directory) if re.match(file_pattern, f)]
    if not files:
        return None
    latest_file = max(files, key=os.path.getctime)
    return latest_file


# 从文件名中提取版本号
def extract_version_from_filename(filename):
    version_match = re.search(r'(\d+\.\d+\.\d+\.\d+)', filename)
    return version_match.group(1) if version_match else None

def version_tuple(version_string):
    return tuple(map(int, version_string.split('.')))
# 比较版本号并写入文件
def compare_and_write_version(latest_version, latest_release_path, latest_file, target_directory):
    try:
        with open(latest_release_path, 'r') as file:
            current_version = file.read().strip()
    except FileNotFoundError:
        current_version = '0.0.0.0'

    if version_tuple(latest_version) >= version_tuple(current_version):
        with open(latest_release_path, 'w') as file:
            file.write(latest_version)
        print(f"Updated the release version to {latest_version}")
        # 复制文件到目标目录
        try:
            shutil.copy(latest_file, target_directory)
            print(f"Copied {latest_file} to {target_directory}")
        except IOError as e:
            print(f"Could not copy file to {target_directory}: {e}")
    else:
        print(f"The current version ({current_version}) is up to date.")


# 主程序
if __name__ == "__main__":
    aip_path = r"C:\Users\17917\Documents\Advanced Installer\Projects\ColorVision\ColorVision.aip"
    setup_files_dir = r"C:\Users\17917\Documents\Advanced Installer\Projects\ColorVision\Setup Files"
    latest_release_path = r"H:\LATEST_RELEASE"
    target_directory = r"H:\ColorVision"

    rebuild_project(aip_path)
    latest_file = get_latest_file(setup_files_dir, r'ColorVision-\d+\.\d+\.\d+\.\d+\.exe')
    print("latest_file" +latest_file)
    if latest_file:
        latest_version = extract_version_from_filename(latest_file)
        if latest_version:
            compare_and_write_version(latest_version, latest_release_path, latest_file, target_directory)
        else:
            print("Could not extract the version from the filename.")
    else:
        print("No .exe files found in the directory.")
# 访问 https://www.jetbrains.com/help/pycharm/ 获取 PyCharm 帮助
