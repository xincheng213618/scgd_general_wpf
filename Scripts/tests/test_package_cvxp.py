import json
import tempfile
import unittest
from pathlib import Path

from Scripts.package_cvxp import (
    MAX_COPILOT_AGENT_METADATA_CHARACTERS,
    MAX_COPILOT_AGENT_ROLES,
    validate_plugin_manifest,
)


class PackageCvxManifestValidationTests(unittest.TestCase):
    def setUp(self) -> None:
        self._temp_directory = tempfile.TemporaryDirectory(prefix="package-cvxp-tests-")
        self.manifest_path = Path(self._temp_directory.name) / "manifest.json"

    def tearDown(self) -> None:
        self._temp_directory.cleanup()

    def test_valid_copilot_roles_report_bounded_advertised_metadata(self) -> None:
        self._write_manifest({
            "id": "sample.plugin",
            "copilot_agents": [
                self._create_role("workspace-reviewer", "Workspace reviewer", "WorkspaceReadOnly", ["GrepText", "ReadLocalFile"]),
                self._create_role("public-researcher", "Public researcher", "PublicWeb", ["WebSearch", "FetchUrl"]),
            ],
        })

        summary = validate_plugin_manifest(self.manifest_path)

        self.assertTrue(summary.manifest_present)
        self.assertEqual(2, summary.role_count)
        self.assertGreater(summary.metadata_characters, 0)
        self.assertLessEqual(summary.metadata_characters, MAX_COPILOT_AGENT_METADATA_CHARACTERS)

    def test_manifest_without_copilot_roles_remains_compatible(self) -> None:
        self._write_manifest({"id": "legacy.plugin", "name": "Legacy Plugin"})

        summary = validate_plugin_manifest(self.manifest_path)

        self.assertTrue(summary.manifest_present)
        self.assertEqual(0, summary.role_count)

    def test_mixed_workspace_and_web_capabilities_are_rejected(self) -> None:
        role = self._create_role("mixed-reviewer", "Mixed reviewer", "WorkspaceReadOnly", ["ReadLocalFile", "WebSearch"])
        self._write_manifest({"id": "sample.plugin", "copilot_agents": [role]})

        with self.assertRaisesRegex(ValueError, "cannot mix workspace and public-web capabilities"):
            validate_plugin_manifest(self.manifest_path)

    def test_role_count_over_runtime_limit_is_rejected(self) -> None:
        roles = [
            self._create_role(f"reviewer-{index:02}", f"Reviewer {index:02}", "WorkspaceReadOnly", ["GrepText"])
            for index in range(MAX_COPILOT_AGENT_ROLES + 1)
        ]
        self._write_manifest({"id": "sample.plugin", "copilot_agents": roles})

        with self.assertRaisesRegex(ValueError, f"at most {MAX_COPILOT_AGENT_ROLES} roles"):
            validate_plugin_manifest(self.manifest_path)

    def test_advertised_metadata_over_runtime_limit_is_rejected(self) -> None:
        roles = [
            self._create_role(f"reviewer-{index:02}", f"Reviewer {index:02}", "WorkspaceReadOnly", ["GrepText"], description="a" * 1_180)
            for index in range(7)
        ]
        self._write_manifest({"id": "sample.plugin", "copilot_agents": roles})

        with self.assertRaisesRegex(ValueError, f"must not exceed {MAX_COPILOT_AGENT_METADATA_CHARACTERS:,} characters"):
            validate_plugin_manifest(self.manifest_path)

    def test_invalid_json_is_rejected_before_packaging(self) -> None:
        self.manifest_path.write_text('{"id": "broken",}', encoding="utf-8")

        with self.assertRaisesRegex(ValueError, "invalid UTF-8 JSON"):
            validate_plugin_manifest(self.manifest_path)

    def _write_manifest(self, manifest: dict) -> None:
        self.manifest_path.write_text(json.dumps(manifest), encoding="utf-8")

    @staticmethod
    def _create_role(role_id: str, name: str, scope: str, capabilities: list[str], *, description: str = "Delegate a bounded read-only task.") -> dict:
        return {
            "id": role_id,
            "name": name,
            "description": description,
            "instructions": "Use only the declared read-only capabilities and return a concise result.",
            "scope": scope,
            "capabilities": capabilities,
        }


if __name__ == "__main__":
    unittest.main()
