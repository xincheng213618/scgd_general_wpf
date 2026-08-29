import importlib.util
import sys
import tempfile
import unittest
import zipfile
from pathlib import Path


REPO_ROOT = Path(__file__).resolve().parents[2]
SCRIPT = REPO_ROOT / "Scripts/verify_nuget_package_versions.py"
SPEC = importlib.util.spec_from_file_location("verify_nuget_package_versions", SCRIPT)
assert SPEC is not None and SPEC.loader is not None
MODULE = importlib.util.module_from_spec(SPEC)
sys.modules[SPEC.name] = MODULE
SPEC.loader.exec_module(MODULE)


def _package(path: Path, package_id: str, version: str) -> Path:
    nuspec = f"""<?xml version="1.0"?>
<package><metadata><id>{package_id}</id><version>{version}</version></metadata></package>
"""
    with zipfile.ZipFile(path, "w") as archive:
        archive.writestr(f"{package_id}.nuspec", nuspec)
    return path


class NuGetPackageVersionPreflightTests(unittest.TestCase):
    def test_detects_occupied_version_before_any_publish(self) -> None:
        with tempfile.TemporaryDirectory(prefix="nuget-version-preflight-") as directory:
            package = _package(Path(directory) / "ColorVision.ImageEditor.1.5.8.nupkg", "ColorVision.ImageEditor", "1.5.8")

            occupied = MODULE.find_occupied_versions(
                [package],
                lambda package_id: {"1.5.7", "1.5.8"} if package_id == "ColorVision.ImageEditor" else set(),
            )

            self.assertEqual([("ColorVision.ImageEditor", "1.5.8")], [(item.package_id, item.version) for item in occupied])

    def test_new_algorithms_and_matching_editor_versions_are_available_together(self) -> None:
        with tempfile.TemporaryDirectory(prefix="nuget-version-preflight-") as directory:
            root = Path(directory)
            packages = [
                _package(root / "ColorVision.Algorithms.1.5.9.nupkg", "ColorVision.Algorithms", "1.5.9"),
                _package(root / "ColorVision.ImageEditor.1.5.9.nupkg", "ColorVision.ImageEditor", "1.5.9"),
            ]

            occupied = MODULE.find_occupied_versions(packages, lambda _: {"1.5.8"})

            self.assertEqual([], occupied)

    def test_rejects_duplicate_packaged_identity_and_unmatched_glob(self) -> None:
        with tempfile.TemporaryDirectory(prefix="nuget-version-preflight-") as directory:
            root = Path(directory)
            first = _package(root / "first.nupkg", "ColorVision.Algorithms", "1.5.9")
            second = _package(root / "second.nupkg", "colorvision.algorithms", "1.5.9")
            with self.assertRaisesRegex(ValueError, "Duplicate packaged identity"):
                MODULE.find_occupied_versions([first, second], lambda _: set())
            with self.assertRaisesRegex(FileNotFoundError, "matched no files"):
                MODULE.resolve_packages([str(root / "missing-*.nupkg")])


if __name__ == "__main__":
    unittest.main()
