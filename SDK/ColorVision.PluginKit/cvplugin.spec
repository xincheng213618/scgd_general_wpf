# -*- mode: python ; coding: utf-8 -*-

from pathlib import Path

from PyInstaller.utils.hooks import collect_data_files, collect_submodules, copy_metadata


ROOT_DIR = Path(SPECPATH)
SCRIPT_PATH = ROOT_DIR / "scripts" / "package_cvxp.py"
SHARED_FILES_PATH = ROOT_DIR / "scripts" / "shared_files.json"

datas = [(str(SHARED_FILES_PATH), ".")]
hiddenimports = []

for package_name in ("requests", "urllib3", "idna", "charset_normalizer", "certifi"):
    try:
        hiddenimports.extend(collect_submodules(package_name))
    except Exception:
        pass

for package_name in ("requests", "certifi"):
    try:
        datas.extend(collect_data_files(package_name))
        datas.extend(copy_metadata(package_name))
    except Exception:
        pass

hiddenimports = sorted(set(hiddenimports))

a = Analysis(
    [str(SCRIPT_PATH)],
    pathex=[str(ROOT_DIR), str(ROOT_DIR / "scripts")],
    binaries=[],
    datas=datas,
    hiddenimports=hiddenimports,
    hookspath=[],
    hooksconfig={},
    runtime_hooks=[],
    excludes=[],
    noarchive=False,
    optimize=0,
)
pyz = PYZ(a.pure)

exe = EXE(
    pyz,
    a.scripts,
    a.binaries,
    a.datas,
    [],
    name="cvplugin",
    debug=False,
    bootloader_ignore_signals=False,
    strip=False,
    upx=True,
    upx_exclude=[],
    runtime_tmpdir=None,
    console=True,
    disable_windowed_traceback=False,
    argv_emulation=False,
    target_arch=None,
    codesign_identity=None,
    entitlements_file=None,
)