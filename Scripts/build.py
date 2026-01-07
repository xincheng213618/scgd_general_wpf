import subprocess
import os
import re
import shutil
import time
from tqdm import tqdm
import requests

def rebuild_project(msbuild_path, solution_path, advanced_installer_path, aip_path):
    try:
        print(f"Running MSBuild: {msbuild_path} {solution_path}")
        subprocess.run([msbuild_path, solution_path, '/p:Configuration=Release', '/p:Platform=x64'], check=True)
        
        print(f"Running Advanced Installer: {advanced_installer_path} /rebuild {aip_path}")
        subprocess.run([advanced_installer_path, '/rebuild', aip_path], check=True)
    except subprocess.CalledProcessError as e:
        print(f"An error occurred while rebuilding the project: {e}")
        return None

def get_latest_file(directory):
    files = [os.path.join(directory, f) for f in os.listdir(directory)]
    if not files:
        return None
    latest_file = max(files, key=os.path.getctime)
    return latest_file


def extract_version_from_filename(filename):
    version_match = re.search(r'(\d+\.\d+\.\d+\.\d+)', filename)
    return version_match.group(1) if version_match else None


def version_tuple(version_string):
    return tuple(map(int, version_string.split('.')))


def copy_with_progress(src, dst):
    if os.path.isdir(dst):
        dst = os.path.join(dst, os.path.basename(src))
    file_size = os.path.getsize(src)
    copied = 0
    chunk_size = 1024 * 1024

    with open(src, 'rb') as fsrc, open(dst, 'wb') as fdst:
        start_time = time.time()
        while True:
            chunk = fsrc.read(chunk_size)
            if not chunk:
                break
            fdst.write(chunk)
            copied += len(chunk)

            elapsed_time = time.time() - start_time
            progress = copied / file_size * 100
            speed = copied / elapsed_time

            remaining_bytes = file_size - copied
            remaining_time = remaining_bytes / speed if speed > 0 else 0
            remaining_time_hms = time.strftime('%H:%M:%S', time.gmtime(remaining_time))

            print(f"\rCopied {copied / (1024 * 1024):.2f} MB of {file_size / (1024 * 1024):.2f} MB "
                  f"({progress:.2f}%) at {speed / (1024 * 1024):.2f} MB/s, "
                  f"remaining time {remaining_time_hms}", end='')

        print()

def upload_file(file_path, folder_name):
    file_size = os.path.getsize(file_path)
    file_name = os.path.basename(file_path)
    upload_url = f'http://xc213618.ddns.me:9998/upload/{folder_name}/{file_name}'

    with open(file_path, 'rb') as f:
        # Create a progress bar
        with tqdm(total=file_size, unit='B', unit_scale=True, desc=file_name, ascii=True) as progress_bar:
            # Define a custom iterable to update progress
            def read_in_chunks(file_object, chunk_size=1024):
                while True:
                    data = file_object.read(chunk_size)
                    if not data:
                        break
                    yield data
                    progress_bar.update(len(data))

            # Send the request using the custom iterable
            response = requests.put(upload_url, data=read_in_chunks(f))

    if response.status_code == 201:
        print('File uploaded successfully')
    else:
        print('File upload failed:', response.text)

def compare_and_write_version(latest_version, latest_release_path, latest_file, changelog_src, changelog_dst):
    try:
        with open(latest_release_path, 'r') as file:
            current_version = file.read().strip()
    except FileNotFoundError:
        current_version = '0.0.0.0'
    try:
        shutil.copy2(changelog_src, changelog_dst)
    except IOError as e:
        print(f"Could not copy file to {changelog_dst}: {e}")

    if version_tuple(latest_version) >= version_tuple(current_version):
        with open(latest_release_path, 'w') as file:
            file.write(latest_version)
        print(f"Updated the release version to {latest_version}")
        try:
            if(os.path.abspath(os.path.join(script_path, '..')).startswith("C:\\Users\\17917\\Desktop")):
                # copy_with_progress(latest_file, target_directory)
                  # 这里先禁用掉，上传慢，走百度云会快很多
                upload_file(latest_file,"ColorVision")
            print(f"Upload {latest_file} ")
        except IOError as e:
            print(f"Upload {latest_file}: {e}")
    else:
        print(f"The current version ({current_version}) is up to date.")

def compare_and_write_version_weixin(latest_version, latest_release_path, latest_file,target_directory ,changelog_src, changelog_dst):
    if not os.path.exists(latest_release_path):
        print(target_directory +"不存在，跳过更新。")
        return
    try:
        with open(latest_release_path, 'r') as file:
            current_version = file.read().strip()
    except FileNotFoundError:
        current_version = '0.0.0.0'
    try:
        shutil.copy2(changelog_src, changelog_dst)
    except IOError as e:
        print(f"Could not copy file to {changelog_dst}: {e}")

    if version_tuple(latest_version) >= version_tuple(current_version):
        with open(latest_release_path, 'w') as file:
            file.write(latest_version)
        print(f"Updated the release version to {latest_version}")
        try:
            shutil.copy(latest_file, target_directory)
            print(f"Upload {latest_file} ")
        except IOError as e:
            print(f"Upload {latest_file}: {e}")
    else:
        print(f"The current version ({current_version}) is up to date.")


if __name__ == "__main__":

    script_path =  os.path.abspath(os.path.dirname(__file__))
    base_path = os.path.abspath(os.path.join(script_path, '..'))  # 获取 base_path 的父级节点

    # 根据当前登录用户动态生成 Advanced Installer 项目与输出目录
    user_home = os.environ.get('USERPROFILE') or os.path.expanduser('~')
    documents_dir = os.path.join(user_home, 'Documents')
    ai_project_dir = os.path.join(documents_dir, 'Advanced Installer', 'Projects', 'ColorVision')
    dynamic_aip_path = os.path.join(ai_project_dir, 'ColorVision.aip')
    dynamic_setup_files_dir = os.path.join(ai_project_dir, 'Setup Files')

    projects = {
        'ColorVision': {
            'msbuild_path': r'C:\\Program Files\\Microsoft Visual Studio\\18\\Insiders\\MSBuild\\Current\\Bin\\MSBuild.exe',
            'solution_path': os.path.join(base_path, 'build.sln'),
            'advanced_installer_path': os.path.join(base_path,'..', 'AdvancedInstaller v19.7.1', 'App', 'ProgramFiles', 'bin', 'x86', 'AdvancedInstaller.com'),
            # 动态路径：仅修改这两个字段
            'aip_path': dynamic_aip_path,
            'setup_files_dir': dynamic_setup_files_dir,
            'latest_release_path': r'H:\\ColorVision\\LATEST_RELEASE',
            'target_directory': r'H:\\ColorVision',
            'changelog_src':  os.path.join(base_path, 'CHANGELOG.md'),
            'changelog_dst': r'H:\\ColorVision\\CHANGELOG.md',
            'wechat_target_directory': r'C:\\Users\\Xin\\Documents\\WXWork\\1688854819471931\\WeDrive\\视彩光电\\视彩（上海）光电技术有限公司\\视彩软件及工具简易教程\\新版软件安装包\\ColorVision',
            'baidu_target_directory': r'D:\\BaiduSyncdisk\\ColorVision'
    }
    }


    project_name = 'ColorVision'  # 或 'Microscope'
    project = projects[project_name]

    rebuild_project(project['msbuild_path'], project['solution_path'], project['advanced_installer_path'],   project['aip_path'])
    print(project['setup_files_dir'])
    latest_file = get_latest_file(project['setup_files_dir'])
    print("latest_file: " + str(latest_file))
    if latest_file:
        latest_version = extract_version_from_filename(latest_file)
        if latest_version:
            compare_and_write_version_weixin(latest_version, project['wechat_target_directory'] +"\LATEST_RELEASE", latest_file, project['wechat_target_directory'], project['changelog_src'] , project['wechat_target_directory']+"\CHANGELOG.md")
            compare_and_write_version_weixin(latest_version, project['baidu_target_directory'] +"\LATEST_RELEASE", latest_file, project['baidu_target_directory'], project['changelog_src'] , project['baidu_target_directory']+"\CHANGELOG.md")
            compare_and_write_version(latest_version, project['latest_release_path'], latest_file, project['changelog_src'], project['changelog_dst'])
        else:
            print("Could not extract the version from the filename.")
    else:
        print("No .exe files found in the directory.")
        
    

