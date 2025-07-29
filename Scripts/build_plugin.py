import os
import zipfile
import shutil
import filecmp
import re
import json

import pefile
import time
import argparse

EXTRA_FILES = ["README.md", "CHANGELOG.md", "manifest.json","PackageIcon.png"]

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

def find_extra_files(base_path, type_name, project_name):
    """
    只返回 base_path, type, project 下每一层的 README.md、CHANGELOG.md、manifest.json 文件的绝对路径
    """
    extra_files = []
    # base_path
    for fname in EXTRA_FILES:
        fpath = os.path.join(base_path, fname)
        if os.path.isfile(fpath):
            extra_files.append(fpath)
    # base_path/type_name
    type_dir = os.path.join(base_path, type_name)
    for fname in EXTRA_FILES:
        fpath = os.path.join(type_dir, fname)
        if os.path.isfile(fpath):
            extra_files.append(fpath)
    # base_path/type_name/project_name
    project_dir = os.path.join(type_dir, project_name)
    for fname in EXTRA_FILES:
        fpath = os.path.join(project_dir, fname)
        if os.path.isfile(fpath):
            extra_files.append(fpath)
    return extra_files

def compare_and_zip(src_dir, ref_dir, output_zip, project_name, base_path, type_name):
    temp_dir = 'temp_dir'
    if os.path.exists(temp_dir):
        shutil.rmtree(temp_dir)
    os.makedirs(temp_dir)

    project_path = os.path.join(temp_dir, project_name)
    os.makedirs(project_path)

    # 拷贝差异文件
    for root, _, files in os.walk(src_dir):
        for file in files:
            if file.endswith('.pdb'):
                continue
            src_file_path = os.path.join(root, file)
            ref_file_path = os.path.join(ref_dir, os.path.relpath(src_file_path, src_dir))
            if not os.path.exists(ref_file_path):
                relative_path = os.path.relpath(src_file_path, src_dir)
                dest_path = os.path.join(project_path, relative_path)
                os.makedirs(os.path.dirname(dest_path), exist_ok=True)
                shutil.copy2(src_file_path, dest_path)

    # 拷贝额外文件：全部直接覆盖到 project_path 下
    extra_files = find_extra_files(base_path, type_name, project_name)
    for abs_path in extra_files:
        fname = os.path.basename(abs_path)
        dest_path = os.path.join(project_path, fname)
        shutil.copy2(abs_path, dest_path)  # 覆盖同名

    # 打包
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
            speed = copied / elapsed_time if elapsed_time > 0 else 0
            progress = copied / file_size * 100
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
        
def update_manifest_requires(manifest_path, required_version):
    """Updates the 'requires' field in the manifest.json file."""
    if not os.path.exists(manifest_path):
        print(f"Warning: manifest.json not found at {manifest_path}")
        return
    try:
        with open(manifest_path, 'r', encoding='utf-8') as f:
            manifest_data = json.load(f)
        
        manifest_data['requires'] = required_version
        
        with open(manifest_path, 'w', encoding='utf-8') as f:
            json.dump(manifest_data, f, indent=2)
        print(f"Successfully updated {manifest_path} with requires: {required_version}")

    except (json.JSONDecodeError, IOError) as e:
        print(f"Error updating manifest file {manifest_path}: {e}")
        
def build_project(project_name, type_name):
    print(f"Building project: {project_name}")
    script_path = os.path.abspath(os.path.dirname(__file__))
    base_path = os.path.abspath(os.path.join(script_path, '..'))  # 获取 base_path 的父级节点
    src_dir = os.path.join(base_path, type_name, project_name, 'bin', 'x64', 'Release', 'net8.0-windows')
    ref_dir = os.path.join(base_path, 'ColorVision', 'bin', 'x64', 'Release', 'net8.0-windows')
    target_dir = os.path.join("H:\\", 'ColorVision', 'Plugins')
    
        # --- Start: Update manifest.json ---
    # 1. Get version from ColorVision.Engine.dll
    engine_dll_path = os.path.join(ref_dir, 'ColorVision.Engine.dll')
    engine_version = get_file_version(engine_dll_path)
    
    if engine_version:
        print(f'Found ColorVision.Engine.dll version: {engine_version}')
        # 2. Find and update the project's manifest.json
        manifest_path = os.path.join(base_path, type_name, project_name, 'manifest.json')
        update_manifest_requires(manifest_path, engine_version)
        manifest_path = os.path.join(src_dir, 'manifest.json')
        update_manifest_requires(manifest_path, engine_version)
    else:
        print("Warning: Could not determine engine version. 'requires' field in manifest.json will not be updated.")
    # --- End: Update manifest.json ---
    
    # 获取 DLL 版本号
    dll_path = os.path.join(src_dir, f'{project_name}.dll')
    version = get_file_version(dll_path)
    print(f'dll_path: {dll_path} ，verison:{version}')

    # 设置输出 zip 文件名
    output_zip = f'{project_name}-{version}.zip'

    # 执行比较和打包
    compare_and_zip(src_dir, ref_dir, output_zip, project_name, base_path, type_name)

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
    parser.add_argument('-p', '--project_name', nargs='?', default='ProjectBase', help='The name of the project to build')
    parser.add_argument('-t', '--type', nargs='?', default='Plugins', help='The name of the project type')
    args = parser.parse_args()
    build_project(args.project_name, args.type)