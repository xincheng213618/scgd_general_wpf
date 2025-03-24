import os
import zipfile
import shutil
import filecmp
import re

import pefile
import time
import argparse

def get_file_version(file_path):
    """获取可执行文件的版本信息"""
    pe = pefile.PE(file_path)
    version_info = None

    if hasattr(pe, 'FileInfo'):
        for file_info in pe.FileInfo:
            for entry in file_info:
                if entry.Key == b'StringFileInfo':
                    for st in entry.StringTable:
                        if b'FileVersion' in st.entries:
                            version_info = st.entries[b'FileVersion'].decode('utf-8')
                            break
    return version_info


def compare_and_zip(src_dir, ref_dir, output_zip,project_name):
    temp_dir = 'temp_dir'
    if os.path.exists(temp_dir):
        shutil.rmtree(temp_dir)
    os.makedirs(temp_dir)

    project_path = os.path.join(temp_dir, project_name)
    os.makedirs(project_path)

    for root, _, files in os.walk(src_dir):
        for file in files:
            if file.endswith('.pdb'):
                continue  # Skip .pdb files
            src_file_path = os.path.join(root, file)
            ref_file_path = os.path.join(ref_dir, os.path.relpath(src_file_path, src_dir))

            if not os.path.exists(ref_file_path):
                relative_path = os.path.relpath(src_file_path, src_dir)
                dest_path = os.path.join(project_path, relative_path)

                os.makedirs(os.path.dirname(dest_path), exist_ok=True)
                shutil.copy2(src_file_path, dest_path)

    with zipfile.ZipFile(output_zip, 'w', zipfile.ZIP_DEFLATED) as zipf:
        for root, _, files in os.walk(temp_dir):
            for file in files:
                file_path = os.path.join(root, file)
                arcname = os.path.relpath(file_path, temp_dir)
                zipf.write(file_path, arcname)

    shutil.rmtree(temp_dir)


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
def version_tuple(version_string):
    return tuple(map(int, version_string.split('.')))

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
        try:
            copy_with_progress(latest_file, target_directory)
            print(f"Copied {latest_file} to {target_directory}")
        except IOError as e:
            print(f"Could not copy file to {target_directory}: {e}")
    else:
        print(f"The current version ({current_version}) is up to date.")




def build_project(project_name,type):
    print(f"Building project: {project_name}")
    base_path = os.path.abspath(os.path.dirname(__file__))
    src_dir = os.path.join(base_path, type, project_name, 'bin', 'x64', 'Release', 'net8.0-windows')
    ref_dir = os.path.join(base_path, 'ColorVision', 'bin', 'x64', 'Release', 'net8.0-windows')
    target_dir = os.path.join(base_path, 'ColorVision', type)

    # 获取 DLL 版本号
    dll_path = os.path.join(src_dir, f'{project_name}.dll')
    version = get_file_version(dll_path)
    print(f'dll_path: {dll_path} ，verison:{version}')

    # 设置输出 zip 文件名
    output_zip = f'{project_name}-{version}.zip'

    # 执行比较和打包
    compare_and_zip(src_dir, ref_dir, output_zip,project_name)

    # 定义目标目录和版本文件路径
    project_target_dir = os.path.join(target_dir, project_name)
    latest_release_path = os.path.join(project_target_dir, 'LATEST_RELEASE')
    # 创建目标目录（如果不存在）
    os.makedirs(project_target_dir, exist_ok=True)
    compare_and_write_version(version, latest_release_path, output_zip, project_target_dir)
    print(f'打包完成: {output_zip}')
    os.remove(output_zip)
    print(f'删除: {output_zip}')



if __name__ == '__main__':
    parser = argparse.ArgumentParser(description='Build the specified project.')
    parser.add_argument('-p','--project_name',nargs='?', default='ProjectBase', help='The name of the project to build')
    parser.add_argument('-t','--type', nargs='?', default='Plugins', help='The name of the project to build')

    args = parser.parse_args()
    build_project(args.project_name,args.type)
