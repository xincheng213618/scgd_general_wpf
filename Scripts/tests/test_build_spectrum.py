import hashlib
import base64
import json
import tempfile
import unittest
from pathlib import Path
from types import SimpleNamespace
from unittest import mock

from Scripts import build_spectrum


class SpectrumPackageFilterTests(unittest.TestCase):
    def test_standalone_package_filter_keeps_only_supported_runtime_assets(self) -> None:
        self.assertTrue(build_spectrum._should_include("Spectrum.exe"))
        self.assertTrue(build_spectrum._should_include("runtimes/win/native/device.dll"))
        self.assertTrue(build_spectrum._should_include("runtimes/win-x64/native/device.dll"))

        self.assertFalse(build_spectrum._should_include("Symbols/Spectrum.pdb"))
        self.assertFalse(build_spectrum._should_include("nested/SCBase.dll"))
        self.assertFalse(build_spectrum._should_include("runtimes/linux-x64/native/device.so"))
        self.assertFalse(build_spectrum._should_include("runtimes/win-arm64/native/device.dll"))

    def test_collected_files_have_normalized_deterministic_order(self) -> None:
        with tempfile.TemporaryDirectory(prefix="spectrum-filter-tests-") as temp_dir_name:
            root = Path(temp_dir_name)
            (root / "z.dll").write_bytes(b"z")
            (root / "a").mkdir()
            (root / "a" / "b.dll").write_bytes(b"b")

            collected = build_spectrum._collect_files(root)

        self.assertEqual(["a/b.dll", "z.dll"], [relative for _, relative in collected])


class SpectrumManifestTests(unittest.TestCase):
    def setUp(self) -> None:
        self._temp_directory = tempfile.TemporaryDirectory(prefix="spectrum-manifest-tests-")
        self.package_path = Path(self._temp_directory.name) / "Spectrum2.3.3.3.zip"
        self.package_bytes = b"PK\x03\x04payload"
        self.package_path.write_bytes(self.package_bytes)

    def tearDown(self) -> None:
        self._temp_directory.cleanup()

    def test_manifest_is_deterministic_canonical_utf8_json(self) -> None:
        published_at = "2026-08-07T03:04:05Z"
        manifest = build_spectrum.build_release_manifest(
            "2.3.3.3",
            "修复更新流程",
            self.package_path,
            published_at_utc=published_at,
        )

        first = build_spectrum.canonical_json_bytes(manifest)
        second = build_spectrum.canonical_json_bytes(manifest)
        expected_hash = hashlib.sha256(self.package_bytes).hexdigest()
        expected = (
            '{"package":{"fileName":"Spectrum2.3.3.3.zip","sha256":"'
            f'{expected_hash}","size":{len(self.package_bytes)}}},'
            '"productId":"Spectrum","publishedAtUtc":"2026-08-07T03:04:05Z",'
            '"releaseNotes":"修复更新流程","schemaVersion":1,"version":"2.3.3.3"}'
        ).encode("utf-8")

        self.assertEqual(expected, first)
        self.assertEqual(first, second)
        self.assertFalse(first.startswith(b"\xef\xbb\xbf"))
        self.assertFalse(first.endswith(b"\n"))
        self.assertEqual(
            {"schemaVersion", "productId", "version", "publishedAtUtc", "releaseNotes", "package"},
            set(manifest),
        )

    def test_signed_release_passes_exact_manifest_bytes_to_signer(self) -> None:
        signer = mock.Mock(return_value=b"raw-signature")

        signed_release = build_spectrum.create_signed_release(
            "2.3.3.3",
            "notes",
            self.package_path,
            published_at_utc="2026-08-07T03:04:05Z",
            signer=signer,
        )

        signer.assert_called_once_with(signed_release.manifest_bytes)
        self.assertEqual(b"raw-signature", signed_release.signature_bytes)

    def test_powershell_signer_uses_current_user_certificate_and_raw_output(self) -> None:
        observed = {}

        def fake_runner(command, **kwargs):
            observed["command"] = command
            observed["environment"] = kwargs["env"]
            Path(kwargs["env"]["SPECTRUM_SIGN_OUTPUT"]).write_bytes(b"signature-bytes")
            return SimpleNamespace(returncode=0, stdout="", stderr="")

        signature = build_spectrum.sign_manifest_bytes(
            b'{"schemaVersion":1}',
            powershell_executable="powershell.exe",
            runner=fake_runner,
        )

        self.assertEqual(b"signature-bytes", signature)
        self.assertIn("RSACertificateExtensions", observed["command"][-1])
        self.assertIn("RSASignaturePadding]::Pkcs1", observed["command"][-1])
        self.assertEqual(
            build_spectrum.SPECTRUM_SIGNING_CERTIFICATE_THUMBPRINT,
            observed["environment"]["SPECTRUM_SIGN_THUMBPRINT"],
        )
        self.assertEqual("xincheng", observed["environment"]["SPECTRUM_SIGN_COMMON_NAME"])

    def test_non_four_part_version_is_rejected(self) -> None:
        with self.assertRaisesRegex(build_spectrum.SpectrumReleaseError, "四段"):
            build_spectrum.build_release_manifest(
                "2.3.3",
                "notes",
                self.package_path,
                published_at_utc="2026-08-07T03:04:05Z",
            )

    def test_version_component_above_pe_limit_is_rejected(self) -> None:
        with self.assertRaisesRegex(build_spectrum.SpectrumReleaseError, "65535"):
            build_spectrum.build_release_manifest(
                "2.3.3.65536",
                "notes",
                self.package_path,
                published_at_utc="2026-08-07T03:04:05Z",
            )


class SpectrumPublishFlowTests(unittest.TestCase):
    def setUp(self) -> None:
        self._temp_directory = tempfile.TemporaryDirectory(prefix="spectrum-publish-tests-")
        root = Path(self._temp_directory.name)
        self.zip_path = root / "Spectrum2.3.3.3.zip"
        self.cvxp_path = root / "Spectrum-2.3.3.3.cvxp"
        self.zip_path.write_bytes(b"PK\x03\x04zip")
        self.cvxp_path.write_bytes(b"PK\x03\x04cvxp")
        manifest = build_spectrum.build_release_manifest(
            "2.3.3.3",
            "notes",
            self.zip_path,
            published_at_utc="2026-08-07T03:04:05Z",
        )
        self.signed_release = build_spectrum.SignedRelease(
            manifest,
            build_spectrum.canonical_json_bytes(manifest),
            b"signature",
        )

    def tearDown(self) -> None:
        self._temp_directory.cleanup()

    @staticmethod
    def _publish_payload(signed_release, *, created):
        package = signed_release.manifest["package"]
        return {
            "created": created,
            "version": signed_release.manifest["version"],
            "latest": {
                "manifestBase64": base64.b64encode(signed_release.manifest_bytes).decode("ascii"),
                "signatureBase64": base64.b64encode(signed_release.signature_bytes).decode("ascii"),
            },
            "release": {
                "version": signed_release.manifest["version"],
                "publishedAtUtc": signed_release.manifest["publishedAtUtc"],
                "releaseNotes": signed_release.manifest["releaseNotes"],
                "fileName": package["fileName"],
                "size": package["size"],
                "sha256": package["sha256"],
                "downloadUrl": f"/api/tool/spectrum/download/{signed_release.manifest['version']}",
            },
        }

    def test_publish_uses_exact_multipart_contract(self) -> None:
        response = mock.Mock(status_code=201, text="")
        response.json.return_value = self._publish_payload(self.signed_release, created=True)

        with mock.patch.object(
            build_spectrum,
            "post_multipart_with_auth",
            return_value=response,
        ) as post:
            effective_release = build_spectrum.publish_standalone_release(
                "2.3.3.3",
                "notes",
                self.zip_path,
                self.signed_release,
                base_url="http://example.test:9998",
                username="user",
                password="password",
                session=object(),
            )

        self.assertEqual(self.signed_release, effective_release)
        _, kwargs = post.call_args
        self.assertEqual({"Version": "2.3.3.3", "ReleaseNotes": "notes"}, kwargs["data"])
        self.assertEqual({"Manifest", "Signature", "Package"}, set(kwargs["files"]))
        self.assertEqual("application/json", kwargs["files"]["Manifest"][2])
        self.assertEqual("application/octet-stream", kwargs["files"]["Signature"][2])
        self.assertEqual("application/zip", kwargs["files"]["Package"][2])

    def test_idempotent_publish_reuses_first_signed_manifest_for_recovery(self) -> None:
        existing_manifest = json.loads(json.dumps(self.signed_release.manifest))
        existing_manifest["publishedAtUtc"] = "2026-08-07T02:00:00Z"
        existing_release = build_spectrum.SignedRelease(
            existing_manifest,
            build_spectrum.canonical_json_bytes(existing_manifest),
            b"first-signature",
        )
        response = mock.Mock(status_code=200, text="")
        response.json.return_value = self._publish_payload(existing_release, created=False)

        with mock.patch.object(
            build_spectrum,
            "post_multipart_with_auth",
            return_value=response,
        ):
            effective_release = build_spectrum.publish_standalone_release(
                "2.3.3.3",
                "notes",
                self.zip_path,
                self.signed_release,
                base_url="http://example.test:9998",
                username="user",
                password="password",
                session=object(),
            )

        self.assertEqual(existing_release, effective_release)

    def test_plugin_latest_is_last_remote_write_before_verification(self) -> None:
        events = []

        def record(name, result=None):
            def callback(*args, **kwargs):
                events.append(name)
                return result

            return callback

        with (
            mock.patch.object(build_spectrum, "preflight_remote_upload", side_effect=record("preflight", True)),
            mock.patch.object(build_spectrum, "backend_upload_file", side_effect=record("plugin-package", True)),
            mock.patch.object(build_spectrum, "backend_upload_content", side_effect=record("plugin-latest", True)),
            mock.patch.object(
                build_spectrum,
                "publish_standalone_release",
                side_effect=record("standalone-publish", self.signed_release),
            ),
            mock.patch.object(build_spectrum, "verify_plugin_latest", side_effect=record("plugin-verify")),
            mock.patch.object(build_spectrum, "verify_plugin_package", side_effect=record("plugin-package-verify")),
            mock.patch.object(build_spectrum, "verify_standalone_release", side_effect=record("standalone-verify")),
        ):
            build_spectrum.publish_built_artifacts(
                "2.3.3.3",
                "notes",
                self.zip_path,
                self.cvxp_path,
                self.signed_release,
                base_url="http://example.test:9998",
                username="user",
                password="password",
                session=object(),
            )

        self.assertEqual(
            [
                "preflight",
                "plugin-package",
                "plugin-package-verify",
                "standalone-publish",
                "plugin-latest",
                "plugin-verify",
                "standalone-verify",
            ],
            events,
        )

    def test_plugin_package_verification_checks_remote_bytes(self) -> None:
        response = mock.Mock(status_code=200, text="")
        response.iter_content.return_value = [self.cvxp_path.read_bytes()]
        session = mock.Mock()
        session.get.return_value = response

        build_spectrum.verify_plugin_package(
            "2.3.3.3",
            self.cvxp_path,
            base_url="http://example.test:9998",
            session=session,
        )

        response.iter_content.return_value = [b"different"]
        with self.assertRaisesRegex(build_spectrum.SpectrumReleaseError, "大小|SHA-256"):
            build_spectrum.verify_plugin_package(
                "2.3.3.3",
                self.cvxp_path,
                base_url="http://example.test:9998",
                session=session,
            )

    def test_upload_failure_propagates_and_keeps_local_cvxp(self) -> None:
        with (
            mock.patch.object(build_spectrum, "preflight_remote_upload", return_value=True),
            mock.patch.object(build_spectrum, "backend_upload_file", return_value=False),
            mock.patch.object(build_spectrum, "publish_standalone_release") as standalone_publish,
        ):
            with self.assertRaisesRegex(build_spectrum.SpectrumReleaseError, "cvxp"):
                build_spectrum.publish_built_artifacts(
                    "2.3.3.3",
                    "notes",
                    self.zip_path,
                    self.cvxp_path,
                    self.signed_release,
                    base_url="http://example.test:9998",
                    username="user",
                    password="password",
                    session=object(),
                )

        standalone_publish.assert_not_called()
        self.assertTrue(self.cvxp_path.is_file())


class SpectrumClientUpdateContractTests(unittest.TestCase):
    @classmethod
    def setUpClass(cls) -> None:
        cls.repo_root = Path(__file__).resolve().parents[2]

    def test_install_gate_covers_continuous_and_serial_operations(self) -> None:
        measurement = (self.repo_root / "Plugins/Spectrum/MainWindow.xaml.cs").read_text(encoding="utf-8")
        manager = (self.repo_root / "Plugins/Spectrum/SpectrometerManager.cs").read_text(encoding="utf-8")

        self.assertIn(
            "Manager.IsBusy || continuousMeasurementTask is { IsCompleted: false }",
            measurement,
        )
        self.assertIn(
            "continuousMeasurementTask = RunContinuousMeasurementAsync(continuousMeasurementCancellation.Token);",
            measurement,
        )
        self.assertIn("await continuousMeasurementTask;", measurement)
        self.assertIn("SpectrumMeasurementResult result = await Manager.MeasureAsync(cancellationToken);", measurement)

        manager_busy_line = next(
            line for line in manager.splitlines() if "public bool IsBusy =>" in line
        )
        for busy_state in (
            "IsDeviceBusy",
            "IsMeasurementActive",
            "SmuController.IsBusy",
            "ShutterController.IsBusy",
            "FilterWheelController.IsBusy",
        ):
            self.assertIn(busy_state, manager_busy_line)

        for relative_path in (
            "Plugins/Spectrum/Configs/ShutterController.cs",
            "Plugins/Spectrum/Configs/FilterWheelController.cs",
        ):
            controller = (self.repo_root / relative_path).read_text(encoding="utf-8")
            self.assertIn("public bool IsBusy => Volatile.Read(ref activeOperationCount) > 0;", controller)
            self.assertIn("Interlocked.Increment(ref activeOperationCount);", controller)
            self.assertIn("Interlocked.Decrement(ref activeOperationCount);", controller)

    def test_restore_failure_is_checked_before_restart(self) -> None:
        service = (self.repo_root / "Plugins/Spectrum/Update/SpectrumUpdateService.cs").read_text(encoding="utf-8")
        restore_start = service.index("\n            :restore\n")
        failed_start = service.index("\n            :failed\n", restore_start)
        restore_block = service[restore_start:failed_start]
        restore_failed_start = restore_block.index("\n            :restore_failed\n")
        restore_failed_block = restore_block[restore_failed_start:]

        self.assertIn("if errorlevel 8 goto :restore_failed", restore_block)
        for required_file in (
            "Spectrum.exe",
            "Spectrum.dll",
            "Spectrum.deps.json",
            "Spectrum.runtimeconfig.json",
        ):
            self.assertIn(f'if not exist \"%SPECTRUM_INSTALL%\\{required_file}\" goto :restore_failed', restore_block)
        self.assertIn("exit /b 2", restore_failed_block)
        self.assertNotIn('start \"\" \"%SPECTRUM_INSTALL%\\Spectrum.exe\"', restore_failed_block)


if __name__ == "__main__":
    unittest.main()
