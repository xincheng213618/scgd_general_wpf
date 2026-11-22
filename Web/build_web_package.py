"""Build and verify a NAS Web runtime ZIP without using the live checkout or server."""

from __future__ import annotations

import argparse
import hashlib
import json
from datetime import datetime, timezone
from email.parser import BytesParser
from pathlib import Path, PurePosixPath
import re
import shutil
import stat
import subprocess
import sys
import tempfile
from time import perf_counter
from zipfile import ZIP_DEFLATED, ZipFile, ZipInfo


BACKEND_REQUIREMENTS = 'Backend/requirements.txt'
BACKEND_LOCK = 'Backend/requirements-package.txt'
FRONTEND_PREFIX = 'Web/Frontend/'
BACKEND_PREFIX = 'Web/Backend/'
FRONTEND_ROOT_FILES = {
    'package.json', 'package-lock.json', 'index.html', 'eslint.config.js', 'vite.config.ts',
    'tsconfig.json', 'tsconfig.app.json', 'tsconfig.node.json',
}
FRONTEND_SOURCE_DIRS = {'src', 'public', 'tests', 'scripts'}
BACKEND_SOURCE_DIRS = {'db', 'ports', 'routes', 'services'}
BACKEND_SOURCE_REQUIREMENTS = {'requirements.txt', 'requirements-package.txt', 'requirements.lock.txt'}
OBJECT_ID = re.compile(r'(?:[0-9a-f]{40}|[0-9a-f]{64})\Z')
WINDOWS_RESERVED = re.compile(r'(?i)(?:con|prn|aux|nul|com[1-9]|lpt[1-9])(?:\..*)?\Z')
PINNED_REQUIREMENT = re.compile(r'[A-Za-z0-9][A-Za-z0-9._-]*==[A-Za-z0-9][A-Za-z0-9.!+_-]*\Z')


def arguments() -> argparse.Namespace:
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--git', required=True)
    parser.add_argument('--npm', required=True)
    parser.add_argument('--source-path', type=Path, required=True)
    parser.add_argument('--revision', default='HEAD')
    parser.add_argument('--source-commit')
    parser.add_argument('--output-directory', type=Path, required=True)
    parser.add_argument('--python-requirements-lock', type=Path)
    parser.add_argument('--keep-staging', action='store_true')
    return parser.parse_args()


def git_output(options: argparse.Namespace, *command: str) -> bytes:
    return subprocess.run(
        [options.git, '-C', str(options.source_path), *command], check=True, stdout=subprocess.PIPE,
    ).stdout


def resolve_source(options: argparse.Namespace) -> tuple[str, str, int]:
    options.source_path = options.source_path.resolve(strict=True)
    root = Path(git_output(options, 'rev-parse', '--show-toplevel').decode('utf-8').strip()).resolve()
    if root != options.source_path:
        raise ValueError('--source-path must be the Git worktree root.')

    revision = git_output(options, 'rev-parse', '--verify', '--end-of-options', f'{options.revision}^{{object}}').decode('ascii').strip()
    source_tree = git_output(options, 'rev-parse', '--verify', '--end-of-options', f'{revision}^{{tree}}').decode('ascii').strip()
    if not OBJECT_ID.fullmatch(source_tree):
        raise ValueError('Unable to identify the Git source tree.')

    if source_tree == revision and not options.source_commit:
        raise ValueError('A Git tree input requires -SourceCommit to identify its base commit.')
    commit = git_output(options, 'rev-parse', '--verify', '--end-of-options', f'{options.source_commit or revision}^{{commit}}').decode('ascii').strip()
    if not OBJECT_ID.fullmatch(commit) or (source_tree != revision and options.source_commit and commit != revision):
        raise ValueError('The source commit does not match the selected revision.')
    stamp = int(git_output(options, 'show', '-s', '--format=%ct', commit).decode('ascii').strip())
    return commit, source_tree, stamp


def safe_archive_path(path: str) -> None:
    parts = path.split('/')
    if any(not piece or piece in ('.', '..') or piece.endswith((' ', '.'))
           or WINDOWS_RESERVED.fullmatch(piece) or re.search(r'[<>:"\\|?*\x00-\x1f]', piece) for piece in parts):
        raise ValueError(f'Invalid Windows archive path: {path!r}')


def should_export(path: str) -> bool:
    if path.startswith(BACKEND_PREFIX):
        parts = path[len(BACKEND_PREFIX):].split('/')
        if len(parts) == 1 and parts[0] in BACKEND_SOURCE_REQUIREMENTS:
            return True
        return (len(parts) == 1 or parts[0] in BACKEND_SOURCE_DIRS) and parts[-1].endswith('.py') \
            and not any(part.startswith(('.', 'test_', 'tests')) or part == '__pycache__' for part in parts)

    if path.startswith(FRONTEND_PREFIX):
        parts = path[len(FRONTEND_PREFIX):].split('/')
        return (len(parts) == 1 and parts[0] in FRONTEND_ROOT_FILES) or (
            parts[0] in FRONTEND_SOURCE_DIRS and not any(part.startswith('.') or part == 'node_modules' for part in parts)
        )
    return False


def tracked_sources(options: argparse.Namespace, source_tree: str) -> dict[str, str]:
    tree_list = git_output(options, 'ls-tree', '-r', '-z', source_tree, '--', 'Web/Backend', 'Web/Frontend')
    sources: dict[str, str] = {}
    seen: set[str] = set()
    for entry in tree_list.split(b'\0'):
        if not entry:
            continue
        metadata, raw_path = entry.split(b'\t', 1)
        path = raw_path.decode('utf-8')
        if not should_export(path):
            continue
        safe_archive_path(path)
        mode, kind, blob = metadata.decode('ascii').split()
        if path.casefold() in seen or mode not in {'100644', '100755'} or kind != 'blob':
            raise ValueError(f'Selected source is not a unique regular file: {path!r}')
        seen.add(path.casefold())
        sources[path] = blob
    required = {f'{FRONTEND_PREFIX}{filename}' for filename in ('package.json', 'package-lock.json', 'index.html')}
    required.update(f'{BACKEND_PREFIX}{filename}' for filename in ('app.py', 'app_setup.py', 'config_loader.py', 'requirements.txt'))
    if missing := required - sources.keys():
        raise ValueError(f'Revision is missing required Web sources: {", ".join(sorted(missing))}')
    return dict(sorted(sources.items()))


def export_sources(options: argparse.Namespace, source_tree: str, stage: Path) -> None:
    sources = tracked_sources(options, source_tree)
    pending = list(sources)
    archive_path = stage / 'sources.zip'
    while pending:
        # Keep individual git.exe invocations below Windows' command-line limit.
        batch: list[str] = []
        command_size = 0
        while pending and command_size < 16_000:
            path = pending.pop(0)
            batch.append(path)
            command_size += len(path) + 4
        git_output(options, '-c', 'core.autocrlf=false', '-c', 'core.eol=lf', 'archive', '--format=zip',
                   f'--output={archive_path}', source_tree, '--', *batch)
        with ZipFile(archive_path) as archive:
            entries = [entry for entry in archive.infolist() if not entry.is_dir()]
            if len(entries) != len(batch) or {entry.filename for entry in entries} != set(batch):
                raise ValueError('Git archive exported an unexpected source set.')
            for entry in entries:
                data = archive.read(entry)
                blob = sources[entry.filename]
                digest = hashlib.new('sha1' if len(blob) == 40 else 'sha256')
                digest.update(f'blob {len(data)}\0'.encode('ascii'))
                digest.update(data)
                if digest.hexdigest() != blob:
                    raise ValueError(f'Git archive contents differ from the selected tree: {entry.filename!r}')
                output = stage / PurePosixPath(entry.filename)
                output.parent.mkdir(parents=True, exist_ok=True)
                output.write_bytes(data)
        archive_path.unlink()


def use_backend_lock(options: argparse.Namespace, backend: Path) -> bool:
    candidates = [backend / 'requirements-package.txt', backend / 'requirements.lock.txt']
    tracked_locks = [path for path in candidates if path.is_file()]
    lock = options.python_requirements_lock
    if lock is None:
        if len(tracked_locks) > 1:
            raise ValueError('The source tree contains two backend requirements locks.')
        lock = next(iter(tracked_locks), None)
    if lock is None:
        return False

    text = lock.read_text(encoding='utf-8-sig')
    lines = [line.split('#', 1)[0].strip() for line in text.splitlines()]
    pins = [line for line in lines if line]
    if not pins or any(not PINNED_REQUIREMENT.fullmatch(line) for line in pins):
        raise ValueError('The backend lock must contain exact name==version pins and optional comments.')
    normalized = {normalize_package_name(line.split('==', 1)[0]) for line in pins}
    if len(normalized) != len(pins):
        raise ValueError('The backend lock contains duplicate package pins.')
    (backend / 'requirements-package.txt').write_bytes(('\n'.join(pins) + '\n').encode('utf-8'))
    (backend / 'requirements.lock.txt').unlink(missing_ok=True)
    return True


def normalize_package_name(name: str) -> str:
    return re.sub(r'[-_.]+', '-', name).casefold()


def verify_wheelhouse(directory: Path, lock: Path) -> tuple[int, int]:
    expected = {}
    for line in lock.read_text(encoding='utf-8').splitlines():
        name, version = line.split('==', 1)
        expected[normalize_package_name(name)] = version

    found: dict[str, str] = {}
    size = 0
    for wheel in sorted(directory.iterdir()):
        if wheel.suffix != '.whl' or not wheel.is_file() or wheel.is_symlink():
            raise ValueError(f'Unexpected entry in the backend wheelhouse: {wheel.name!r}')
        with ZipFile(wheel) as archive:
            metadata_paths = [entry for entry in archive.namelist() if entry.count('/') == 1 and entry.endswith('.dist-info/METADATA')]
            if len(metadata_paths) != 1:
                raise ValueError(f'Wheel must contain one distribution metadata file: {wheel.name!r}')
            metadata = BytesParser().parsebytes(archive.read(metadata_paths[0]), headersonly=True)
        name = normalize_package_name(metadata.get('Name', '').strip())
        version = metadata.get('Version', '').strip()
        if name in found or name not in expected or expected[name] != version:
            raise ValueError(f'Wheel does not match a unique backend lock pin: {wheel.name!r}')
        found[name] = version
        size += wheel.stat().st_size
    if missing := expected.keys() - found.keys():
        raise ValueError(f'Backend wheelhouse is missing locked packages: {", ".join(sorted(missing))}')
    return len(found), size


def download_backend_wheels(backend: Path) -> tuple[int, int]:
    lock = backend / 'requirements-package.txt'
    wheels = backend / 'wheels'
    wheels.mkdir()
    subprocess.run([
        sys.executable, '-m', 'pip', 'download', '--quiet', '--no-input', '--disable-pip-version-check',
        '--only-binary=:all:', '--platform', 'win_amd64', '--implementation', 'cp', '--python-version', '3.10', '--abi', 'cp310',
        '--dest', str(wheels), '-r', str(lock),
    ], cwd=backend, check=True, stdout=sys.stderr)
    return verify_wheelhouse(wheels, lock)


def run_frontend(options: argparse.Namespace, frontend: Path) -> dict[str, float]:
    phases: dict[str, float] = {}
    for name, command in (
        ('frontendInstallSeconds', ['ci', '--no-audit', '--no-fund']),
        ('frontendTestSeconds', ['run', 'test']),
        ('frontendBuildSeconds', ['run', 'build']),
    ):
        started = perf_counter()
        subprocess.run([options.npm, *command], cwd=frontend, check=True, stdout=sys.stderr)
        phases[name] = round(perf_counter() - started, 3)
    return phases


def package_files(backend: Path, dist: Path) -> dict[str, Path]:
    files: dict[str, Path] = {}
    folded: set[str] = set()
    for prefix, root in (('Backend', backend), ('Frontend/dist', dist)):
        for entry in root.rglob('*'):
            metadata = entry.lstat()
            if stat.S_ISLNK(metadata.st_mode) or getattr(metadata, 'st_file_attributes', 0) & stat.FILE_ATTRIBUTE_REPARSE_POINT:
                raise ValueError(f'The package cannot contain links: {entry.name!r}')
            if not entry.is_file():
                continue
            relative = entry.relative_to(root).as_posix()
            name = f'{prefix}/{relative}'
            safe_archive_path(name)
            if name.casefold() in folded:
                raise ValueError(f'Archive contains conflicting file names: {name!r}')
            if prefix == 'Frontend/dist' and any(part.startswith('.') for part in relative.split('/')) \
                    and relative not in {'.vite/manifest.json', '.vite/manifest.json.br', '.vite/manifest.json.gz'}:
                raise ValueError(f'Unexpected hidden frontend output: {relative!r}')
            folded.add(name.casefold())
            files[name] = entry
    if not {'Frontend/dist/index.html', 'Frontend/dist/.vite/manifest.json', BACKEND_REQUIREMENTS}.issubset(files):
        raise ValueError('Package is missing requirements, frontend index or Vite manifest.')
    return dict(sorted(files.items()))


def sha256_stream(stream) -> str:
    digest = hashlib.sha256()
    for block in iter(lambda: stream.read(1 << 20), b''):
        digest.update(block)
    return digest.hexdigest()


def sha256_file(path: Path) -> str:
    with path.open('rb') as stream:
        return sha256_stream(stream)


def json_bytes(value: object) -> bytes:
    return (json.dumps(value, sort_keys=True, ensure_ascii=False, indent=2) + '\n').encode('utf-8')


def write_zip(path: Path, manifest: bytes, files: dict[str, Path]) -> None:
    with ZipFile(path, mode='w', compression=ZIP_DEFLATED, compresslevel=6) as archive:
        for name, data in [('manifest.json', manifest), *((name, item.read_bytes()) for name, item in files.items())]:
            entry = ZipInfo(name, date_time=(1980, 1, 1, 0, 0, 0))
            entry.create_system = 3
            entry.external_attr = 0o100644 << 16
            archive.writestr(entry, data, compress_type=ZIP_DEFLATED, compresslevel=6)


def verify_zip(path: Path, manifest: bytes, hashes: dict[str, str]) -> None:
    with ZipFile(path) as archive:
        if archive.namelist() != ['manifest.json', *hashes] or archive.read('manifest.json') != manifest:
            raise ValueError('ZIP contents do not match the manifest.')
        for name, expected in hashes.items():
            with archive.open(name) as stream:
                if sha256_stream(stream) != expected:
                    raise ValueError(f'ZIP checksum verification failed: {name!r}')


def publish_file(source: Path, destination: Path) -> None:
    if destination.exists():
        if sha256_file(source) != sha256_file(destination):
            raise ValueError(f'An output with the same release ID already exists and differs: {destination.name}')
        return

    # Copy into a unique sibling first so the named artifact never appears partially written.
    with tempfile.NamedTemporaryFile(dir=destination.parent, prefix=f'.{destination.name}.', delete=False) as staged:
        temporary = Path(staged.name)
    try:
        shutil.copyfile(source, temporary)
        temporary.replace(destination)
    finally:
        temporary.unlink(missing_ok=True)


def build(options: argparse.Namespace) -> dict[str, object]:
    commit, source_tree, stamp = resolve_source(options)
    temporary_root = Path(tempfile.gettempdir()).resolve(strict=True)
    stage = Path(tempfile.mkdtemp(prefix='ColorVision-WebPackage-', dir=temporary_root)).resolve(strict=True)
    try:
        export_sources(options, source_tree, stage)
        backend, frontend = stage / 'Web' / 'Backend', stage / 'Web' / 'Frontend'
        if 'COLORVISION_WEB_DATA_DIR' not in (backend / 'config_loader.py').read_text(encoding='utf-8-sig'):
            raise ValueError('Revision does not support the packaged backend runtime (COLORVISION_WEB_DATA_DIR).')
        locked_requirements = use_backend_lock(options, backend)
        requirements_path = BACKEND_LOCK if locked_requirements else BACKEND_REQUIREMENTS
        wheel_count = wheel_bytes = 0
        wheel_started = perf_counter()
        if locked_requirements:
            wheel_count, wheel_bytes = download_backend_wheels(backend)
        wheel_seconds = round(perf_counter() - wheel_started, 3)
        phase_seconds = run_frontend(options, frontend)
        phase_seconds['backendWheelDownloadSeconds'] = wheel_seconds
        files = package_files(backend, frontend / 'dist')
        hashes = {name: sha256_file(path) for name, path in files.items()}
        identity = hashlib.sha256(json_bytes({'sourceTree': source_tree, 'files': hashes})).hexdigest()
        timestamp = datetime.fromtimestamp(stamp, timezone.utc).strftime('%Y%m%dT%H%M%SZ')
        release_id = f'{timestamp}-{commit[:12]}-{identity[:16]}'
        manifest = json_bytes({
            'schemaVersion': 1,
            'releaseId': release_id,
            'commit': commit,
            'sourceTree': source_tree,
            'backendRequirementsSha256': hashes[requirements_path],
            'files': [{'path': name, 'sha256': sha256} for name, sha256 in hashes.items()],
        })
        zip_path = stage / 'release.zip'
        manifest_path = stage / 'manifest.json'
        manifest_path.write_bytes(manifest)
        write_zip(zip_path, manifest, files)
        verify_zip(zip_path, manifest, hashes)
        output = options.output_directory.resolve()
        output.mkdir(parents=True, exist_ok=True)
        target_zip = output / f'ColorVision-Web-{release_id}.zip'
        target_manifest = output / f'ColorVision-Web-{release_id}.manifest.json'
        publish_file(zip_path, target_zip)
        publish_file(manifest_path, target_manifest)
        result = {
            'releaseId': release_id, 'commit': commit, 'sourceTree': source_tree,
            'zipPath': str(target_zip), 'manifestPath': str(target_manifest), 'sha256': sha256_file(target_zip),
            'fileCount': len(files), 'bytes': target_zip.stat().st_size, 'phaseSeconds': phase_seconds,
            'backendRequirementsPath': requirements_path, 'wheelCount': wheel_count, 'wheelBytes': wheel_bytes,
        }
        if options.keep_staging:
            result['stagingPath'] = str(stage)
        return result
    finally:
        if not options.keep_staging and stage.parent == temporary_root and stage.name.startswith('ColorVision-WebPackage-'):
            shutil.rmtree(stage)


def main() -> int:
    try:
        print(json.dumps(build(arguments()), ensure_ascii=True))
        return 0
    except (OSError, ValueError, subprocess.CalledProcessError) as error:
        print(f'WEB_PACKAGE_ERROR: {error}', file=sys.stderr)
        return 1


if __name__ == '__main__':
    raise SystemExit(main())
