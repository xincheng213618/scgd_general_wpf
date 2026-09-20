import hashlib
import json
import os
import tempfile
import unittest
import zipfile
from pathlib import Path
from unittest import mock

from Scripts.native_runtime_integrity import (
    NATIVE_PREFIX, ensure_native_runtime_integrity, resolve_native_sources, validate_native_archive,
)


class NativeRuntimeIntegrityTests(unittest.TestCase):
    def setUp(self):
        self.temp = tempfile.TemporaryDirectory()
        self.addCleanup(self.temp.cleanup)
        self.root = Path(self.temp.name)
        self.runtime = self.root / "runtime"
        self.package = self.root / "cache/opencvsharp4.runtime.win/4.99.0"
        self.relative = NATIVE_PREFIX + "OpenCvSharpExtern.dll"
        source = self.package / self.relative
        source.parent.mkdir(parents=True)
        source.write_bytes(b"valid-native")
        assets = {
            "packageFolders": {str(self.root / "cache"): {}},
            "libraries": {"OpenCvSharp4.runtime.win/4.99.0": {"path": "opencvsharp4.runtime.win/4.99.0"}},
            "targets": {"net10.0-windows": {"OpenCvSharp4.runtime.win/4.99.0": {
                "runtimeTargets": {self.relative: {"assetType": "native", "rid": "win-x64"}},
            }}},
        }
        self.assets = self.root / "ColorVision/obj/project.assets.json"
        self.assets.parent.mkdir(parents=True)
        self.assets.write_text(json.dumps(assets), encoding="utf-8")
        props = self.root / "packages/OpenCV.Release.x64.props"
        props.parent.mkdir(parents=True)
        props.write_text('''<Project xmlns="http://schemas.microsoft.com/developer/msbuild/2003">
<PropertyGroup><OpenCVRoot>$(MSBuildThisFileDirectory)opencv\\</OpenCVRoot>
<OpenCVBinDir>$(OpenCVRoot)x64\\vc18\\bin\\</OpenCVBinDir><OpenCVVersion>4990</OpenCVVersion></PropertyGroup>
<ItemGroup><OpenCVRuntimeDll Include="$(OpenCVBinDir)opencv_videoio_ffmpeg$(OpenCVVersion)_64.dll" /></ItemGroup>
</Project>''', encoding="utf-8")
        local = self.root / "packages/opencv/x64/vc18/bin/opencv_videoio_ffmpeg4990_64.dll"
        local.parent.mkdir(parents=True)
        local.write_bytes(b"valid-ffmpeg")

    def test_resolves_actual_nuget_version_and_cpp_property_sheet(self):
        sources = resolve_native_sources(self.root)
        self.assertEqual(sources[self.relative], self.package / self.relative)
        self.assertIn(NATIVE_PREFIX + "opencv_videoio_ffmpeg4990_64.dll", sources)

    def test_same_size_and_timestamp_corruption_is_repaired_by_content(self):
        sources = resolve_native_sources(self.root)
        for relative, source in sources.items():
            target = self.runtime / relative
            target.parent.mkdir(parents=True, exist_ok=True)
            target.write_bytes(b"!" * source.stat().st_size)
            os.utime(target, ns=(source.stat().st_atime_ns, source.stat().st_mtime_ns))
        with self.assertRaisesRegex(ValueError, "differs"):
            ensure_native_runtime_integrity(self.root, self.runtime)
        expected = ensure_native_runtime_integrity(self.root, self.runtime, repair=True)
        self.assertEqual(ensure_native_runtime_integrity(self.root, self.runtime), expected)
        for relative, source in sources.items():
            self.assertEqual((self.runtime / relative).read_bytes(), source.read_bytes())

    def test_good_files_are_not_rewritten(self):
        ensure_native_runtime_integrity(self.root, self.runtime, repair=True)
        with mock.patch("Scripts.native_runtime_integrity.shutil.copy2") as copy:
            ensure_native_runtime_integrity(self.root, self.runtime, repair=True)
        copy.assert_not_called()

    def test_missing_source_stops_before_any_repair(self):
        sources = resolve_native_sources(self.root)
        list(sources.values())[-1].unlink()
        with mock.patch("Scripts.native_runtime_integrity.shutil.copy2") as copy:
            with self.assertRaises(FileNotFoundError):
                ensure_native_runtime_integrity(self.root, self.runtime, repair=True)
        copy.assert_not_called()

    def test_failed_copy_cannot_replace_existing_destination(self):
        target = self.runtime / self.relative
        target.parent.mkdir(parents=True)
        target.write_bytes(b"old")
        def corrupt_copy(source, destination):
            Path(destination).write_bytes(b"bad-copy")
        with mock.patch("Scripts.native_runtime_integrity.shutil.copy2", side_effect=corrupt_copy):
            with self.assertRaisesRegex(ValueError, "copy verification failed"):
                ensure_native_runtime_integrity(self.root, self.runtime, repair=True)
        self.assertEqual(target.read_bytes(), b"old")
        self.assertEqual(list(target.parent.glob("*.repair-*")), [])

    def test_unresolved_assets_stop_validation(self):
        self.assets.write_text('{"targets": {}, "libraries": {}, "packageFolders": {}}', encoding="utf-8")
        with self.assertRaisesRegex(ValueError, "not resolved"):
            ensure_native_runtime_integrity(self.root, self.runtime, repair=True)

    def test_archive_corruption_and_missing_full_runtime_are_rejected(self):
        archive = self.root / "update.cvx"
        expected = {self.relative: hashlib.sha256(b"valid").hexdigest()}
        with zipfile.ZipFile(archive, "w") as z:
            z.writestr(self.relative, b"wrong")
        with self.assertRaisesRegex(ValueError, "differs"):
            validate_native_archive(archive, expected, require_all=False)
        with zipfile.ZipFile(archive, "w") as z:
            z.writestr("app.dll", b"app")
        validate_native_archive(archive, expected, require_all=False)
        with self.assertRaisesRegex(ValueError, "missing"):
            validate_native_archive(archive, expected, require_all=True)


if __name__ == "__main__":
    unittest.main()
