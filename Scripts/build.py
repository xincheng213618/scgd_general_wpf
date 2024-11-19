import subprocess
import os
import re
import shutil
import time


def rebuild_project(msbuild_path, solution_path, advanced_installer_path, aip_path):
    try:
        print(f"Running MSBuild: {msbuild_path} {solution_path}")
        subprocess.run([msbuild_path, solution_path, '/p:Configuration=Release', '/p:Platform=x64'], check=True)
        
        print(f"Running Advanced Installer: {advanced_installer_path} /rebuild {aip_path}")
        subprocess.run([advanced_installer_path, '/rebuild', aip_path], check=True)
    except subprocess.CalledProcessError as e:
        print(f"An error occurred while rebuilding the project: {e}")
        return None


def get_latest_file(directory, file_pattern):
    files = [os.path.join(directory, f) for f in os.listdir(directory) if re.match(file_pattern, f)]
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

def compare_and_write_version(latest_version, latest_release_path, latest_file, target_directory, changelog_src, changelog_dst):  
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
            copy_with_progress(latest_file, target_directory)
            print(f"Copied {latest_file} to {target_directory}")
        except IOError as e:
            print(f"Could not copy file to {target_directory}: {e}")
    else:
        print(f"The current version ({current_version}) is up to date.")   


if __name__ == "__main__":
    projects = {
        'ColorVision': {
            'msbuild_path': r'C:\Program Files\Microsoft Visual Studio\2022\Preview\MSBuild\Current\Bin\msbuild.exe',
            'solution_path': r"C:\Users\17917\Desktop\scgd_general_wpf\build.sln",
            'advanced_installer_path': r'C:\Users\17917\Desktop\AdvancedInstaller v19.7.1\App\ProgramFiles\bin\x86\AdvancedInstaller.com',
            'aip_path': r"C:\Users\17917\Documents\Advanced Installer\Projects\ColorVision\ColorVision.aip",
            'setup_files_dir': r"C:\Users\17917\Documents\Advanced Installer\Projects\ColorVision\Setup Files",
            'latest_release_path': r"H:\LATEST_RELEASE",
            'target_directory': r"H:\ColorVision",
            'changelog_src': r"C:\Users\17917\Desktop\scgd_general_wpf\CHANGELOG.md",
            'changelog_dst': r"H:\CHANGELOG.md",
            'file_pattern': r'ColorVision-\d+\.\d+\.\d+\.\d+\.exe'
        }
    }

    project_name = 'ColorVision'  # 或 'Microscope'
    project = projects[project_name]

    rebuild_project(project['msbuild_path'], project['solution_path'], project['advanced_installer_path'], project['aip_path'])
    latest_file = get_latest_file(project['setup_files_dir'], project['file_pattern'])
    print("latest_file: " + str(latest_file))
    if latest_file:
        latest_version = extract_version_from_filename(latest_file)
        if latest_version:
            compare_and_write_version(latest_version, project['latest_release_path'], latest_file, project['target_directory'], project['changelog_src'], project['changelog_dst'])
        else:
            print("Could not extract the version from the filename.")
    else:
        print("No .exe files found in the directory.")
        
    

