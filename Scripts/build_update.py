import ctypes
import os
import filecmp
import re
import zipfile
import time
from pathlib import PurePosixPath

try:
    from .backend_client import upload_file_to_folder
except ImportError:
    from backend_client import upload_file_to_folder

ALLOWED_RUNTIME_PREFIXES = (
    'runtimes/win/',
    'runtimes/win-x64/',
)
EXCLUDED_OUTPUT_DIRECTORIES = {'log', 'plugins', 'publish'}
SHELL_EXTENSION_FILE_PREFIX = 'colorvision.shellextension'
REQUIRED_SERVICE_HOST_RUNTIME_PATHS = (
    'ServiceHost/ColorVisionServiceHost.exe',
    'ServiceHost/ColorVisionServiceHost.dll',
    'ServiceHost/ColorVisionServiceHost.deps.json',
    'ServiceHost/ColorVisionServiceHost.runtimeconfig.json',
    'ServiceHost/Newtonsoft.Json.dll',
    'ServiceHost/System.ServiceProcess.ServiceController.dll',
    'ServiceHost/runtimes/win/lib/net10.0/System.ServiceProcess.ServiceController.dll',
    'ServiceHost/Tasks/RegisterFileAssociations.ps1',
    'ServiceHost/Tasks/RegisterThumbnail.ps1',
    'ServiceHost/Tasks/UnregisterThumbnail.ps1',
)
FULL_RELEASE_ZIP_RE = re.compile(
    r'^ColorVision-\[(\d+)\.(\d+)\.(\d+)\.(\d+)]\.zip$',
    re.IGNORECASE,
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


def normalize_archive_relative_path(path_value: str) -> str:
    return PurePosixPath(path_value.replace('\\', '/')).as_posix()


def should_keep_runtime_path(path_value: str) -> bool:
    normalized = normalize_archive_relative_path(path_value).lower()
    if not normalized.startswith('runtimes/'):
        return True

    return normalized.startswith(ALLOWED_RUNTIME_PREFIXES)


def is_shell_extension_file(path_value: str) -> bool:
    return os.path.basename(path_value).lower().startswith(SHELL_EXTENSION_FILE_PREFIX)

def is_root_service_host_file(path_value: str) -> bool:
    normalized = normalize_archive_relative_path(path_value).lower()
    return '/' not in normalized and os.path.basename(normalized).startswith('colorvisionservicehost.')


def validate_service_host_runtime(version_directory: str) -> None:
    missing_paths = []
    for relative_path in REQUIRED_SERVICE_HOST_RUNTIME_PATHS:
        path = os.path.join(version_directory, *PurePosixPath(relative_path).parts)
        if not os.path.isfile(path):
            missing_paths.append(relative_path)

    if missing_paths:
        raise FileNotFoundError(
            'ServiceHost runtime is incomplete: ' + ', '.join(missing_paths)
        )

def upload_file(file_path, folder_name):
    return upload_file_to_folder(file_path, folder_name)


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

def get_all_files(directory, include_shell_extension=True):
    """获取目录下的所有文件路径"""
    file_paths = []
    for root, dirs, files in os.walk(directory):
        dirs[:] = [d for d in dirs if d.lower() not in EXCLUDED_OUTPUT_DIRECTORIES]
        for file in files:
            if file.endswith('.pdb'):
                continue

            absolute_path = os.path.join(root, file)
            relative_path = os.path.relpath(absolute_path, directory)
            if not include_shell_extension and is_shell_extension_file(relative_path):
                continue
            if is_root_service_host_file(relative_path):
                continue

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
        create_full_zip(new_version_dir, incremental_zip.replace('Update', ''))
        return

    old_version_dir = f'temp_old_version_{os.getpid()}_{int(time.time())}'
    with zipfile.ZipFile(old_zip, 'r') as zipf:
        zipf.extractall(old_version_dir)

    old_files = get_all_files(old_version_dir, include_shell_extension=False)
    new_files = get_all_files(new_version_dir, include_shell_extension=False)
    old_files_dict = {os.path.relpath(file, old_version_dir): file for file in old_files}
    new_files_dict = {os.path.relpath(file, new_version_dir): file for file in new_files}
    files_to_zip = {}

    for rel_path, new_file in new_files_dict.items():
        old_file = old_files_dict.get(rel_path)
        if not old_file or not filecmp.cmp(old_file, new_file, shallow=False):
            files_to_zip[rel_path] = new_file

    service_host_prefix = f'ServiceHost{os.sep}'.lower()
    for rel_path, new_file in new_files_dict.items():
        if rel_path.lower().startswith(service_host_prefix):
            files_to_zip[rel_path] = new_file

    with zipfile.ZipFile(str(incremental_zip), 'w', zipfile.ZIP_DEFLATED) as zipf:
        for rel_path, file in sorted(files_to_zip.items()):
            zipf.write(str(file), str(rel_path))

    remove_directory_best_effort(old_version_dir)


def find_incremental_baseline(directory, version):
    """Find the deterministic cumulative baseline for an incremental package."""
    target_version = tuple(int(part) for part in version.split('.'))
    if len(target_version) != 4:
        raise ValueError(f'Expected a four-part version, got: {version}')
    if not os.path.isdir(directory):
        return None

    candidates = []
    for filename in os.listdir(directory):
        match = FULL_RELEASE_ZIP_RE.fullmatch(filename)
        if not match:
            continue

        candidate_version = tuple(int(part) for part in match.groups())
        if candidate_version[:2] != target_version[:2] or candidate_version >= target_version:
            continue
        candidates.append((candidate_version, os.path.join(directory, filename)))

    if not candidates:
        return None

    baseline_build = target_version[2] if target_version[3] > 1 else target_version[2] - 1
    same_branch = [item for item in candidates if item[0][2] == baseline_build]
    baseline_candidates = same_branch or candidates
    return min(baseline_candidates, key=lambda item: item[0])[1]


def main() -> int:
    version = get_file_version(exe_path)
    if not version:
        print(f"无法从 {exe_path} 读取版本号，终止。")
        return 1

    try:
        validate_service_host_runtime(new_version_dir)
    except FileNotFoundError as exc:
        print(str(exc))
        return 1

    print("打包版本: " + version)

    # 创建目录
    create_directory_if_not_exists(history_dir)
    create_directory_if_not_exists(update_dir)

    old_zip = find_incremental_baseline(history_dir, version)
    print(f"Incremental baseline: {old_zip or 'none (full installer only)'}")
    incremental_zip = os.path.join(update_dir, f'ColorVision-Update-[{version}].cvx')

    if old_zip:
        print(f"创建增量包: {incremental_zip}")
        make_incremental_zip(old_zip, new_version_dir, incremental_zip)
        if not upload_file(incremental_zip, "ColorVision/Update"):
            print("增量包上传失败，终止发布。")
            return 1
    print("创建全量包")
    full_zip = os.path.join(history_dir, f'ColorVision-[{version}].zip')
    create_full_zip(new_version_dir, full_zip)
    return 0


if __name__ == "__main__":
    raise SystemExit(main())

