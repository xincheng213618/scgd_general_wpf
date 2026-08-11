import tempfile
import unittest
from pathlib import Path
from types import SimpleNamespace
from unittest import mock

from Scripts import build, build_update, release


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
        self.primary = build.PreparedPrimaryRelease(
            "1.2.3.4",
            self.root / "ColorVision-1.2.3.4.exe",
            self.root / "CHANGELOG.md",
        )
        self.update = build_update.PreparedUpdateRelease(
            "1.2.3.4",
            self.root / "ColorVision-[1.2.3.4].zip",
            self.root / "ColorVision-Update-[1.2.3.4].cvx",
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

        def publish(version, installer, changelog, settings, *, incremental_file):
            self.assertEqual(self.primary.version, version)
            self.assertEqual(self.primary.installer_file, installer)
            self.assertEqual(self.primary.changelog_src, changelog)
            self.assertEqual(self.update.incremental_zip, incremental_file)
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

    def test_release_batch_has_one_normal_python_entrypoint(self) -> None:
        batch_path = Path(__file__).resolve().parents[1] / "release.bat"
        commands = [line.strip().casefold() for line in batch_path.read_text(encoding="utf-8").splitlines()]

        self.assertEqual(["python scripts\\release.py"], [line for line in commands if line.startswith("python ")])


if __name__ == "__main__":
    unittest.main()
