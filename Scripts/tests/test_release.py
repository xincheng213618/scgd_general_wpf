import tempfile
import unittest
from pathlib import Path
from types import SimpleNamespace
from unittest import mock

from Scripts import build, build_update, release
from Scripts.release_artifacts import PreparedArtifact


class TwoPhaseReleaseOrchestrationTests(unittest.TestCase):
    def setUp(self) -> None:
        self._temporary_directory = tempfile.TemporaryDirectory(prefix="two-phase-release-tests-")
        self.root = Path(self._temporary_directory.name)
        self.project = mock.sentinel.project
        self.settings = build.RemoteUploadSettings(
            base_url="http://example.test:9998",
            folder_name="ColorVision",
            username="user",
            password="password",
        )
        installer = self.root / "ColorVision-1.2.3.4.exe"
        changelog = self.root / "CHANGELOG.md"
        full_zip = self.root / "ColorVision-[1.2.3.4].zip"
        incremental_zip = self.root / "ColorVision-Update-[1.2.3.4].cvx"
        installer.write_bytes(b"installer")
        changelog.write_text("## 1.2.3.4", encoding="utf-8")
        full_zip.write_bytes(b"full")
        incremental_zip.write_bytes(b"incremental")
        self.primary = build.PreparedPrimaryRelease(
            "1.2.3.4",
            PreparedArtifact.capture(installer),
            PreparedArtifact.capture(changelog),
        )
        self.update = build_update.PreparedUpdateRelease(
            "1.2.3.4",
            PreparedArtifact.capture(full_zip),
            PreparedArtifact.capture(incremental_zip),
        )
        self.args = SimpleNamespace(project="ColorVision")

    def tearDown(self) -> None:
        self._temporary_directory.cleanup()

    def test_all_local_prepare_gates_finish_before_publish(self) -> None:
        events: list[str] = []

        def primary_prepare(project):
            self.assertIs(self.project, project)
            events.append("primary-prepare")
            return self.primary

        def update_prepare(*, expected_version):
            self.assertEqual(self.primary.version, expected_version)
            events.append("update-prepare")
            return self.update

        def publish(version, installer, changelog, settings, *, incremental_file, required_local_artifacts):
            self.assertEqual(self.primary.version, version)
            self.assertEqual(self.primary.installer, installer)
            self.assertEqual(self.primary.changelog, changelog)
            self.assertEqual(self.update.incremental_package, incremental_file)
            self.assertEqual((self.update.full_package,), required_local_artifacts)
            self.assertIs(self.settings, settings)
            events.append("publish")
            return True

        with (
            mock.patch.object(build, "build_projects", return_value={"ColorVision": self.project}),
            mock.patch.object(build, "build_remote_settings", return_value=self.settings),
            mock.patch.object(build, "preflight_remote_upload", side_effect=lambda _settings: events.append("preflight") or True),
            mock.patch.object(build, "prepare_primary_release", side_effect=primary_prepare),
            mock.patch.object(build_update, "prepare_update_release", side_effect=update_prepare),
            mock.patch.object(build, "publish_primary_release", side_effect=publish),
        ):
            result = release.run_release(self.args)

        self.assertEqual(0, result)
        self.assertEqual(["preflight", "primary-prepare", "update-prepare", "publish"], events)

    def test_primary_gate_failure_performs_no_update_prepare_or_upload(self) -> None:
        with (
            mock.patch.object(build, "build_projects", return_value={"ColorVision": self.project}),
            mock.patch.object(build, "build_remote_settings", return_value=self.settings),
            mock.patch.object(build, "preflight_remote_upload", return_value=True),
            mock.patch.object(build, "prepare_primary_release", return_value=None),
            mock.patch.object(build_update, "prepare_update_release") as update_mock,
            mock.patch.object(build, "publish_primary_release") as publish_mock,
        ):
            result = release.run_release(self.args)

        self.assertEqual(1, result)
        update_mock.assert_not_called()
        publish_mock.assert_not_called()

    def test_update_gate_failure_performs_no_upload(self) -> None:
        with (
            mock.patch.object(build, "build_projects", return_value={"ColorVision": self.project}),
            mock.patch.object(build, "build_remote_settings", return_value=self.settings),
            mock.patch.object(build, "preflight_remote_upload", return_value=True),
            mock.patch.object(build, "prepare_primary_release", return_value=self.primary),
            mock.patch.object(build_update, "prepare_update_release", return_value=None),
            mock.patch.object(build, "publish_primary_release") as publish_mock,
        ):
            result = release.run_release(self.args)

        self.assertEqual(1, result)
        publish_mock.assert_not_called()

    def test_prepared_version_mismatch_performs_no_upload(self) -> None:
        mismatched_update = build_update.PreparedUpdateRelease(
            "9.9.9.9",
            self.update.full_package,
            self.update.incremental_package,
        )
        with (
            mock.patch.object(build, "build_projects", return_value={"ColorVision": self.project}),
            mock.patch.object(build, "build_remote_settings", return_value=self.settings),
            mock.patch.object(build, "preflight_remote_upload", return_value=True),
            mock.patch.object(build, "prepare_primary_release", return_value=self.primary),
            mock.patch.object(build_update, "prepare_update_release", return_value=mismatched_update),
            mock.patch.object(build, "publish_primary_release") as publish_mock,
        ):
            result = release.run_release(self.args)

        self.assertEqual(1, result)
        publish_mock.assert_not_called()

    def test_release_batch_has_one_normal_python_entrypoint(self) -> None:
        batch_path = Path(__file__).resolve().parents[1] / "release.bat"
        commands = [line.strip().casefold() for line in batch_path.read_text(encoding="utf-8").splitlines()]

        self.assertEqual(["python scripts\\release.py"], [line for line in commands if line.startswith("python ")])


if __name__ == "__main__":
    unittest.main()
