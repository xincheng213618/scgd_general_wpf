"""
编译并打包 Spectrum 插件。

用法:
    python buildSpectrum.py [-c Release] [-f net10.0-windows] [--no-zip] [--no-cvxp] [--upload]

输出:
    Release/Spectrum/Spectrum<version>.zip     独立安装包
    Release/Spectrum/Spectrum-<version>.cvxp   插件包 (上传后自动删除)
"""

import argparse
import json
import os
import re
import shutil
import subprocess
import sys
import time
import zipfile

import pefile

# ── 路径常量（基于脚本位置自动推导） ──────────────────────────────
SCRIPT_DIR = os.path.dirname(os.path.abspath(__file__))
REPO_ROOT = os.path.dirname(SCRIPT_DIR)

PROJECT_NAME = "Spectrum"
PROJECT_DIR = os.path.join(REPO_ROOT, "Plugins", PROJECT_NAME)
PROJECT_PATH = os.path.join(PROJECT_DIR, f"{PROJECT_NAME}.csproj")
BUILD_DIR = os.path.join(REPO_ROOT, "Release", PROJECT_NAME)

# 插件服务器目录 (本地映射盘)
PLUGIN_SERVER_DIR = os.path.join("H:\\", "ColorVision", "Plugins")
# HTTP 上传地址
UPLOAD_BASE_URL = "http://xc213618.ddns.me:9998/upload"

# cvxp 需要额外打入的文件名
_CVXP_EXTRA_FILES = ["README.md", "CHANGELOG.md", "manifest.json", "PackageIcon.png"]

# ── 打包时需要排除的文件名（小写） ────────────────────────────────
_FILE_EXCLUDE = {
    "toupcam.dll",
    "nncam.dll",
    "ikapc.dll",
    "oracle.manageddataaccess.dll",
    "scgdcamlayer.dll",
    "scgdprocess.dll",
    "scgddataprocess.dll",
    "scgdmilcam.dll",
    "scbase.dll",
    "cvcalibration.dll",
    "opencv_videoio_ffmpeg4110_64.dll",
    "opencv_videoio_ffmpeg4130_64.dll",
}


# ── 版本号 ────────────────────────────────────────────────────────
def get_version_from_pe(pe_path: str) -> str | None:
    """从 PE 文件的 FileVersion 字段读取版本号（去掉 +hash 后缀）。"""
    try:
        pe = pefile.PE(pe_path)
        try:
            for fileinfo in pe.FileInfo:
                for entry in fileinfo:
                    if entry.Key == b"StringFileInfo":
                        for st in entry.StringTable:
                            version = st.entries.get(b"FileVersion")
                            if version:
                                ver_str = version.decode("utf-8").strip()
                                m = re.match(r"^([0-9.]+)", ver_str)
                                return m.group(1) if m else ver_str
        finally:
            pe.close()
    except Exception as e:
        print(f"读取版本号失败: {e}")
    return None


# ── 编译 ──────────────────────────────────────────────────────────
def build_project(configuration: str, framework: str) -> str | None:
    """使用 dotnet publish 编译 Spectrum 项目，返回输出目录。"""
    output_dir = os.path.join(BUILD_DIR, framework)
    cmd = [
        "dotnet", "publish", PROJECT_PATH,
        "-c", configuration,
        "-f", framework,
        "-p:Platform=x64",
        "--self-contained", "false",
        "-o", output_dir,
    ]
    print(f"编译命令: {' '.join(cmd)}")
    try:
        subprocess.run(cmd, check=True, cwd=REPO_ROOT)
        print("编译完成。")
        return output_dir
    except subprocess.CalledProcessError as e:
        print(f"编译失败: {e}")
        return None


# ── 独立包过滤 ────────────────────────────────────────────────────
def _should_include(rel_path: str) -> bool:
    """判断文件是否应被打入独立 zip 包。"""
    lower = rel_path.lower().replace("\\", "/")
    filename = os.path.basename(lower)

    if lower.endswith(".pdb"):
        return False

    if filename in _FILE_EXCLUDE:
        return False

    # 只保留 runtimes/win-x64 和 runtimes/win，排除其它平台
    if lower.startswith("runtimes/"):
        if not (lower.startswith("runtimes/win-x64/") or lower.startswith("runtimes/win/")):
            return False

    return True


def _collect_files(folder_path: str) -> list[tuple[str, str]]:
    """收集需要打包的文件，返回 (绝对路径, 相对路径) 列表。"""
    result = []
    for root, _, files in os.walk(folder_path):
        for f in files:
            abs_path = os.path.join(root, f)
            rel = os.path.relpath(abs_path, folder_path)
            if _should_include(rel):
                result.append((abs_path, rel))
    return result


def zip_folder(folder_path: str, zip_path: str) -> None:
    """将编译产物目录打包为 zip（已过滤）。"""
    os.makedirs(os.path.dirname(zip_path), exist_ok=True)
    with zipfile.ZipFile(zip_path, "w", zipfile.ZIP_DEFLATED) as zf:
        for abs_path, rel in _collect_files(folder_path):
            zf.write(abs_path, rel)
    print(f"成功打包: {zip_path}")


# ── cvxp 插件包（与 build_plugin.py 逻辑一致） ───────────────────
def _find_extra_files() -> list[str]:
    """在 Plugins/ 和 Plugins/Spectrum/ 目录下查找额外文件。"""
    result = []
    search_dirs = [
        os.path.join(REPO_ROOT, "Plugins"),
        PROJECT_DIR,
    ]
    for d in search_dirs:
        for fname in _CVXP_EXTRA_FILES:
            fpath = os.path.join(d, fname)
            if os.path.isfile(fpath):
                result.append(fpath)
    return result


def build_cvxp(src_dir: str, ref_dir: str, cvxp_path: str) -> bool:
    """
    对比 src_dir 与 ref_dir，将差异文件打包为 .cvxp 插件包。
    逻辑与 build_plugin.py 的 compare_and_zip 一致。
    """
    if not os.path.isdir(ref_dir):
        print(f"ColorVision 参考目录不存在，跳过 cvxp 打包: {ref_dir}")
        return False

    temp_dir = os.path.join(BUILD_DIR, "_cvxp_temp")
    if os.path.exists(temp_dir):
        shutil.rmtree(temp_dir)

    project_path = os.path.join(temp_dir, PROJECT_NAME)
    os.makedirs(project_path)

    stripped_files = []

    for root, _, files in os.walk(src_dir):
        for f in files:
            if f.endswith(".pdb"):
                continue
            src_file = os.path.join(root, f)
            rel = os.path.relpath(src_file, src_dir)
            ref_file = os.path.join(ref_dir, rel)

            if not os.path.exists(ref_file):
                # 文件不在参考目录 → 插件独有，需要打包
                dest = os.path.join(project_path, rel)
                os.makedirs(os.path.dirname(dest), exist_ok=True)
                shutil.copy2(src_file, dest)
            else:
                # 文件在参考目录中也存在 → 记录为被剥离的共享依赖
                stripped_files.append(rel)

    # 写入 stripped_files.json
    with open(os.path.join(project_path, "stripped_files.json"), "w", encoding="utf-8") as fp:
        json.dump(stripped_files, fp, indent=2, ensure_ascii=False)
    print(f"stripped_files.json: {len(stripped_files)} entries")

    # 拷贝额外文件
    for fpath in _find_extra_files():
        shutil.copy2(fpath, os.path.join(project_path, os.path.basename(fpath)))

    # 打包 .cvxp
    os.makedirs(os.path.dirname(cvxp_path), exist_ok=True)
    with zipfile.ZipFile(cvxp_path, "w", zipfile.ZIP_DEFLATED) as zf:
        for root, _, files in os.walk(temp_dir):
            for f in files:
                abs_f = os.path.join(root, f)
                zf.write(abs_f, os.path.relpath(abs_f, temp_dir))

    shutil.rmtree(temp_dir)
    print(f"成功打包: {cvxp_path}")
    return True


# ── 上传 ──────────────────────────────────────────────────────────
def _version_tuple(v: str):
    return tuple(map(int, v.split(".")))


def copy_with_progress(src: str, dst: str) -> None:
    """带进度显示的文件复制。"""
    if os.path.isdir(dst):
        dst = os.path.join(dst, os.path.basename(src))
    size = os.path.getsize(src)
    copied = 0
    chunk = 1024 * 1024
    with open(src, "rb") as fi, open(dst, "wb") as fo:
        start = time.time()
        while True:
            data = fi.read(chunk)
            if not data:
                break
            fo.write(data)
            copied += len(data)
            elapsed = time.time() - start
            speed = copied / elapsed if elapsed > 0 else 0
            pct = copied / size * 100
            eta = (size - copied) / speed if speed > 0 else 0
            print(
                f"\r{copied / 1048576:.1f}/{size / 1048576:.1f} MB "
                f"({pct:.1f}%) {speed / 1048576:.1f} MB/s "
                f"ETA {time.strftime('%H:%M:%S', time.gmtime(eta))}",
                end="",
            )
        print()


def upload_file_http(file_path: str, folder: str) -> bool:
    """通过 HTTP PUT 上传文件到服务器。"""
    try:
        import requests
        from tqdm import tqdm
    except ImportError:
        print("上传需要 requests 和 tqdm 库，请 pip install requests tqdm")
        return False

    name = os.path.basename(file_path)
    url = f"{UPLOAD_BASE_URL}/{folder}/{name}"
    size = os.path.getsize(file_path)

    with open(file_path, "rb") as f:
        with tqdm(total=size, unit="B", unit_scale=True, desc=name, ascii=True) as bar:

            def chunks(fobj, chunk_size=1024):
                while True:
                    data = fobj.read(chunk_size)
                    if not data:
                        break
                    yield data
                    bar.update(len(data))

            resp = requests.put(url, data=chunks(f))

    if resp.status_code == 201:
        print(f"上传成功: {url}")
        return True
    else:
        print(f"上传失败 ({resp.status_code}): {resp.text}")
        return False


def upload_cvxp_to_server(cvxp_path: str, version: str) -> None:
    """将 cvxp 复制到插件服务器目录并更新 LATEST_RELEASE。"""
    target_dir = os.path.join(PLUGIN_SERVER_DIR, PROJECT_NAME)
    release_file = os.path.join(target_dir, "LATEST_RELEASE")

    if not os.path.isdir(os.path.dirname(PLUGIN_SERVER_DIR)):
        print(f"插件服务器目录不可访问: {PLUGIN_SERVER_DIR}")
        return

    os.makedirs(target_dir, exist_ok=True)

    # 读取当前版本
    try:
        with open(release_file, "r") as f:
            current = f.read().strip()
    except FileNotFoundError:
        current = "0.0.0.0"

    if _version_tuple(version) >= _version_tuple(current):
        with open(release_file, "w") as f:
            f.write(version)
        print(f"已更新 LATEST_RELEASE → {version}")
        copy_with_progress(cvxp_path, target_dir)
    else:
        print(f"服务器版本 ({current}) 已是最新，跳过上传。")


# ── 入口 ──────────────────────────────────────────────────────────
def main() -> None:
    parser = argparse.ArgumentParser(description="编译并打包 Spectrum")
    parser.add_argument("-c", "--configuration", default="Release",
                        help="编译配置 (默认: Release)")
    parser.add_argument("-f", "--framework", default="net10.0-windows",
                        help="目标框架 (默认: net10.0-windows)")
    parser.add_argument("--no-zip", action="store_true",
                        help="跳过独立 zip 打包")
    parser.add_argument("--no-cvxp", action="store_true",
                        help="跳过 cvxp 插件包")
    parser.add_argument("--upload", action="store_true",
                        help="打包后上传到服务器")
    args = parser.parse_args()

    # Step 1: 编译
    output_dir = build_project(args.configuration, args.framework)
    if not output_dir:
        print("编译失败，终止。")
        sys.exit(1)

    # Step 2: 读取版本号
    exe_path = os.path.join(output_dir, f"{PROJECT_NAME}.exe")
    version = get_version_from_pe(exe_path)
    if not version:
        print("未能读取版本号，终止。")
        sys.exit(1)
    print(f"版本号: {version}")

    # Step 3: 独立 zip
    zip_path = None
    if not args.no_zip:
        zip_path = os.path.join(BUILD_DIR, f"{PROJECT_NAME}{version}.zip")
        zip_folder(output_dir, zip_path)

    # Step 4: cvxp 插件包
    cvxp_path = None
    if not args.no_cvxp:
        ref_dir = os.path.join(
            REPO_ROOT, "ColorVision", "bin", "x64",
            args.configuration, args.framework,
        )
        cvxp_path = os.path.join(BUILD_DIR, f"{PROJECT_NAME}-{version}.cvxp")
        if not build_cvxp(output_dir, ref_dir, cvxp_path):
            cvxp_path = None

    # Step 5: 清理编译产物目录（文件已打入 zip/cvxp，不再需要）
    if os.path.isdir(output_dir):
        shutil.rmtree(output_dir)
        print(f"已清理编译目录: {output_dir}")

    # Step 6: 上传
    if args.upload:
        if zip_path and os.path.isfile(zip_path):
            upload_file_http(zip_path, PROJECT_NAME)
        if cvxp_path and os.path.isfile(cvxp_path):
            upload_cvxp_to_server(cvxp_path, version)
            # cvxp 仅用于服务器分发，上传后删除本地副本
            os.remove(cvxp_path)
            print(f"已删除本地 cvxp: {cvxp_path}")

    print("全部完成。")


if __name__ == "__main__":
    main()
