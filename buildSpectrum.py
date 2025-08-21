import os
import zipfile
import pefile
import re
import subprocess
import sys

def build_project():
    """
    使用 MSBuild 编译解决方案，输出到 Release\x64 目录。
    """
    msbuild_path = r"C:\Program Files\Microsoft Visual Studio\2022\Preview\MSBuild\Current\Bin\msbuild.exe"
    sln_file = r"C:\Users\Xin\Desktop\scgd_general_wpf\Spectrum.sln"
    build_cmd = [
        msbuild_path, sln_file,
        "/p:Configuration=Release",
        "/p:Platform=x64"
    ]
    try:
        print("开始编译项目...")
        subprocess.run(build_cmd, check=True)
        print("编译完成。")
        return True
    except subprocess.CalledProcessError as e:
        print(f"编译失败：{e.stderr}")
        return False

def get_version_from_exe(exe_path):
    """
    读取 FileVersion 字段，并去掉如 1.2.3.4+45 中的 +45 部分
    """
    try:
        pe = pefile.PE(exe_path)
        for fileinfo in pe.FileInfo:
            for entry in fileinfo:
                if entry.Key == b'StringFileInfo':
                    for st in entry.StringTable:
                        version = st.entries.get(b'FileVersion')
                        if version:
                            version_str = version.decode('utf-8').strip()
                            m = re.match(r'^([0-9.]+)', version_str)
                            if m:
                                return m.group(1)
                            return version_str
    except Exception as e:
        print(f"读取版本号失败: {e}")
    return None

# 需要过滤的DLL文件名，均小写
SCGD_DLL_EXCLUDE = {
    'toupcam.dll',
    'scgdcamlayer.dll',
    'scgdprocess.dll',
    'scgddataprocess.dll',
    'scgdmilcam.dll',
    'scbase.dll',
    'cvcalibration.dll'
}

def should_include(file_path, folder):
    # 过滤 .pdb 文件
    if file_path.lower().endswith('.pdb'):
        return False
    rel_path = os.path.relpath(file_path, folder).replace("\\", "/")
    # 只保留 runtimes/win-x64 下的内容，其它 runtimes 子目录不打包
    if rel_path.startswith("runtimes/"):
        if rel_path.startswith("runtimes/win-x64/"):
            # 过滤掉 opencv_videoio_ffmpeg4110_64.dll
            if os.path.basename(rel_path).lower() == "opencv_videoio_ffmpeg4110_64.dll":
                return False
            return True
        else:
            return False
    # 过滤 scgd_internal_dll 下的不需要的 dll
    if rel_path.startswith("scgd_internal_dll/"):
        filename = os.path.basename(rel_path).lower()
        if filename in SCGD_DLL_EXCLUDE:
            return False
    return True

def zip_folder(folder_path, zip_name):
    with zipfile.ZipFile(zip_name, 'w', zipfile.ZIP_DEFLATED) as zipf:
        for root, dirs, files in os.walk(folder_path):
            for file in files:
                abs_file = os.path.join(root, file)
                if should_include(abs_file, folder_path):
                    rel_path = os.path.relpath(abs_file, folder_path)
                    zipf.write(abs_file, rel_path)
    print(f"成功打包为: {zip_name}")

def main():
    # Step 1: 先编译
    if not build_project():
        print("项目编译失败，终止打包。")
        sys.exit(1)

    # Step 2: 获取版本号
    folder = os.path.join("Plugins","Spectrum","bin",'x64','Release', "net8.0-windows")
    exe_path = os.path.join(folder, 'Spectrum.exe')
    print(exe_path)
    version = get_version_from_exe(exe_path)
    if not version:
        print("未能读取到版本号，打包终止。")
        sys.exit(1)

    # Step 3: 打包
    zip_name = f"Spectrum{version}.zip"
    zip_folder(folder, zip_name)

if __name__ == "__main__":
    main()