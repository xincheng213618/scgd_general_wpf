import ctypes
import os
import filecmp
import zipfile
import time
from pathlib import PurePosixPath

from file_manager import FileManager


ALLOWED_RUNTIME_PREFIXES = (
    'runtimes/win/',
    'runtimes/win-x64/',
)

# ----------------------
# 动态路径计算（去除用户名硬编码）
# ----------------------
script_path = os.path.abspath(os.path.dirname(__file__))
base_path = os.path.abspath(os.path.join(script_path, '..'))  # 仓库根目录
user_home = os.environ.get('USERPROFILE') or os.path.expanduser('~')
desktop_dir = os.path.join(user_home, 'Desktop')

# 构建相关路径（基于仓库根目录）
new_version_dir = os.path.join(base_path, 'ColorVision', 'bin', 'x64', 'Release', 'net10.0-windows')
exe_path = os.path.join(new_version_dir, 'ColorVision.exe')

# 输出历史与增量包目录（基于当前用户桌面）
history_dir = os.path.join(desktop_dir, 'History')
update_dir = os.path.join(history_dir, 'update')
file_manager = FileManager()


def normalize_archive_relative_path(path_value: str) -> str:
    return PurePosixPath(path_value.replace('\\', '/')).as_posix()


def should_keep_runtime_path(path_value: str) -> bool:
    normalized = normalize_archive_relative_path(path_value).lower()
    if not normalized.startswith('runtimes/'):
        return True

    return normalized.startswith(ALLOWED_RUNTIME_PREFIXES)

def upload_file(file_path, folder_name):
    return file_manager.upload_file(file_path, folder_name)



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
def get_file_version_from_pefile(file_path):
    try:
        import pefile
    except ImportError:
        return None

    try:
        pe = pefile.PE(file_path)
    except Exception:
        return None

    version_info = None

    try:
        if hasattr(pe, 'FileInfo'):
            for file_info in pe.FileInfo:
                for entry in file_info:
                    if entry.Key == b'StringFileInfo':
                        for st in entry.StringTable:
                            if b'FileVersion' in st.entries:
                                version_info = st.entries[b'FileVersion'].decode('utf-8')
                                break
    finally:
        pe.close()

    return version_info


class VS_FIXEDFILEINFO(ctypes.Structure):
    _fields_ = [
        ("dwSignature", ctypes.c_uint32),
        ("dwStrucVersion", ctypes.c_uint32),
        ("dwFileVersionMS", ctypes.c_uint32),
        ("dwFileVersionLS", ctypes.c_uint32),
        ("dwProductVersionMS", ctypes.c_uint32),
        ("dwProductVersionLS", ctypes.c_uint32),
        ("dwFileFlagsMask", ctypes.c_uint32),
        ("dwFileFlags", ctypes.c_uint32),
        ("dwFileOS", ctypes.c_uint32),
        ("dwFileType", ctypes.c_uint32),
        ("dwFileSubtype", ctypes.c_uint32),
        ("dwFileDateMS", ctypes.c_uint32),
        ("dwFileDateLS", ctypes.c_uint32),
    ]


class LANGANDCODEPAGE(ctypes.Structure):
    _fields_ = [
        ("wLanguage", ctypes.c_uint16),
        ("wCodePage", ctypes.c_uint16),
    ]


def query_file_version_string(version_buffer, sub_block):
    value = ctypes.c_void_p()
    value_len = ctypes.c_uint()
    if not ctypes.windll.version.VerQueryValueW(
        version_buffer,
        sub_block,
        ctypes.byref(value),
        ctypes.byref(value_len),
    ):
        return None

    if not value.value or value_len.value == 0:
        return None

    return ctypes.wstring_at(value.value).strip()


def get_file_version_from_windows_resource(file_path):
    """Read FileVersion from Windows version resources without third-party packages."""
    if os.name != "nt" or not os.path.isfile(file_path):
        return None

    size = ctypes.windll.version.GetFileVersionInfoSizeW(file_path, None)
    if not size:
        return None

    version_buffer = ctypes.create_string_buffer(size)
    if not ctypes.windll.version.GetFileVersionInfoW(file_path, 0, size, version_buffer):
        return None

    translations = ctypes.c_void_p()
    translations_len = ctypes.c_uint()
    if ctypes.windll.version.VerQueryValueW(
        version_buffer,
        r"\VarFileInfo\Translation",
        ctypes.byref(translations),
        ctypes.byref(translations_len),
    ):
        count = translations_len.value // ctypes.sizeof(LANGANDCODEPAGE)
        translation_array = ctypes.cast(
            translations,
            ctypes.POINTER(LANGANDCODEPAGE * count),
        ).contents
        for translation in translation_array:
            sub_block = (
                rf"\StringFileInfo\{translation.wLanguage:04x}"
                rf"{translation.wCodePage:04x}\FileVersion"
            )
            version_info = query_file_version_string(version_buffer, sub_block)
            if version_info:
                return version_info

    for language_codepage in ("040904b0", "040904e4", "080404b0", "080404e4"):
        version_info = query_file_version_string(
            version_buffer,
            rf"\StringFileInfo\{language_codepage}\FileVersion",
        )
        if version_info:
            return version_info

    fixed_info = ctypes.c_void_p()
    fixed_info_len = ctypes.c_uint()
    if ctypes.windll.version.VerQueryValueW(
        version_buffer,
        "\\",
        ctypes.byref(fixed_info),
        ctypes.byref(fixed_info_len),
    ):
        if fixed_info.value and fixed_info_len.value >= ctypes.sizeof(VS_FIXEDFILEINFO):
            fixed_version = ctypes.cast(fixed_info, ctypes.POINTER(VS_FIXEDFILEINFO)).contents
            if fixed_version.dwSignature == 0xFEEF04BD:
                return (
                    f"{fixed_version.dwFileVersionMS >> 16}."
                    f"{fixed_version.dwFileVersionMS & 0xffff}."
                    f"{fixed_version.dwFileVersionLS >> 16}."
                    f"{fixed_version.dwFileVersionLS & 0xffff}"
                )

    return None


def get_file_version(file_path):
    """获取可执行文件的版本信息"""
    version_info = get_file_version_from_pefile(file_path)
    if version_info:
        return version_info

    version_info = get_file_version_from_windows_resource(file_path)
    return version_info

def get_all_files(directory):
    """获取目录下的所有文件路径"""
    file_paths = []
    for root, dirs, files in os.walk(directory):
        dirs[:] = [d for d in dirs if d not in {'log', 'Plugins'}]
        for file in files:
            if file.endswith('.pdb'):
                continue

            absolute_path = os.path.join(root, file)
            relative_path = os.path.relpath(absolute_path, directory)
            if not should_keep_runtime_path(relative_path):
                continue

            file_paths.append(absolute_path)
    return file_paths

def create_directory_if_not_exists(directory):
    """如果目录不存在，则创建它"""
    if not os.path.exists(directory):
        os.makedirs(directory)

def create_full_zip(version_dir, output_zip):
    """创建全量更新包"""
    all_files = get_all_files(version_dir)
    with zipfile.ZipFile(str(output_zip), 'w', zipfile.ZIP_DEFLATED) as zipf:
        for file in all_files:
            zipf.write(str(file), str(os.path.relpath(file, version_dir)))

def remove_directory_best_effort(directory, retries=5, delay_seconds=0.5):
    """清理临时目录；短暂文件占用不应阻断增量包上传。"""
    if not os.path.exists(directory):
        return True

    last_error = None
    for attempt in range(retries):
        try:
            for root, dirs, files in os.walk(directory, topdown=False):
                for name in files:
                    os.remove(os.path.join(root, name))
                for name in dirs:
                    os.rmdir(os.path.join(root, name))
            os.rmdir(directory)
            return True
        except OSError as exc:
            last_error = exc
            if attempt < retries - 1:
                time.sleep(delay_seconds)

    print(f"Warning: could not remove temporary directory {directory}: {last_error}")
    return False

def make_incremental_zip(old_zip, new_version_dir, incremental_zip):
    """制作增量更新包"""
    if not os.path.exists(old_zip):
        # 如果旧版本 ZIP 不存在，创建全量更新包
        create_full_zip(new_version_dir, incremental_zip.replace('Update', ''))
        return

    # 解压旧版本 ZIP 文件
    old_version_dir = f'temp_old_version_{os.getpid()}_{int(time.time())}'
    with zipfile.ZipFile(old_zip, 'r') as zipf:
        zipf.extractall(old_version_dir)

    # 获取文件列表
    old_files = get_all_files(old_version_dir)
    new_files = get_all_files(new_version_dir)

    # 创建一个相对路径的字典
    old_files_dict = {os.path.relpath(file, old_version_dir): file for file in old_files}
    new_files_dict = {os.path.relpath(file, new_version_dir): file for file in new_files}

    # 找出新增或修改的文件
    files_to_zip = []

    for rel_path, new_file in new_files_dict.items():
        old_file = old_files_dict.get(rel_path)
        if not old_file or not filecmp.cmp(old_file, new_file, shallow=False):
            files_to_zip.append(new_file)

    # 创建增量 ZIP 包
    with zipfile.ZipFile(str(incremental_zip), 'w', zipfile.ZIP_DEFLATED) as zipf:
        for file in files_to_zip:
            zipf.write(str(file), str(os.path.relpath(file, new_version_dir)))

    remove_directory_best_effort(old_version_dir)

def find_latest_zip(directory, version):
    """在目录中找到指定版本的最新 ZIP 文件"""
    target_version_parts = version.split('.')
    target_major_version = int(target_version_parts[2])  # 获取第三位版本号
    target_minor_version = int(target_version_parts[3])  # 获取第四位版本号

    # 调整基准版本号
    if target_minor_version == 1:
        target_major_version -= 1

    zip_files = [os.path.join(directory, f) for f in os.listdir(directory) if f.endswith('.zip')]
    if not zip_files:
        return None

    # 过滤出符合目标版本的 ZIP 文件
    matching_files = []
    for file in zip_files:
        filename = os.path.basename(file)
        parts = filename.split('.')

        if len(parts) >= 4:
            major_version = int(parts[2])
            if major_version == target_major_version:
                matching_files.append(file)

    # 如果没有匹配的文件，返回最新的文件
    if not matching_files:
        return max(zip_files, key=os.path.getmtime)

    # 返回匹配的文件中最新的一个
    latest_zip = min(matching_files, key=os.path.getmtime)
    return latest_zip


def main() -> int:
    version = get_file_version(exe_path)
    if not version:
        print(f"无法从 {exe_path} 读取版本号，终止。")
        return 1

    print("打包版本: " + version)

    # 创建目录
    create_directory_if_not_exists(history_dir)
    create_directory_if_not_exists(update_dir)

    # 查找最新的全量包
    old_zip = find_latest_zip(history_dir, version)
    print(f"baseline Version{old_zip}")
    incremental_zip = os.path.join(update_dir, f'ColorVision-Update-[{version}].cvx')

    if old_zip:
        print(f"创建增量包: {incremental_zip}")
        make_incremental_zip(old_zip, new_version_dir, incremental_zip)
        upload_file(incremental_zip, "ColorVision/Update")
        # copy_with_progress(incremental_zip,"H:\\ColorVision\\Update")

    print("创建全量包")
    full_zip = os.path.join(history_dir, f'ColorVision-[{version}].zip')
    create_full_zip(new_version_dir, full_zip)
    return 0


if __name__ == "__main__":
    raise SystemExit(main())

