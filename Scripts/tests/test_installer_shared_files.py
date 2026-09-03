import os
import tempfile
import unittest
from pathlib import Path
from xml.etree import ElementTree

from Scripts.installer_shared_files import collect_installer_shared_files


class InstallerSharedFilesTests(unittest.TestCase):
    def setUp(self) -> None:
        self.temp = tempfile.TemporaryDirectory(prefix="installer-shared-files-tests-")
        self.addCleanup(self.temp.cleanup)
        self.root = Path(self.temp.name)
        self.runtime = self.root / "runtime"
        self.runtime.mkdir()
        self.aip = self.root / "installer" / "ColorVision.aip"
        self.aip.parent.mkdir()
        self.directories = [
            {"Directory": "APPDIR", "Directory_Parent": "TARGETDIR", "DefaultDir": "APPDIR:."},
            {"Directory": "Service", "Directory_Parent": "APPDIR", "DefaultDir": "SERVIC~1|ServiceHost"},
        ]
        self.components: list[dict[str, str]] = []
        self.files: list[dict[str, str]] = []

    def host_file(self, name: str) -> Path:
        path = self.runtime / name
        path.parent.mkdir(parents=True, exist_ok=True)
        path.write_bytes(b"fixture")
        return path

    def add_file(self, source: Path, *, directory: str = "APPDIR", filename: str | None = None) -> None:
        identifier = f"file{len(self.files)}"
        self.components.append({"Component": identifier, "Directory_": directory})
        self.files.append({
            "File": identifier,
            "Component_": identifier,
            "FileName": filename or source.name,
            "SourcePath": os.path.relpath(source, self.aip.parent).replace("/", "\\"),
        })

    def resolve(self, *, extra_table: bool = False) -> set[str]:
        root = ElementTree.Element("DOCUMENT")
        for name, rows in (("MsiDirsComponent", self.directories), ("MsiCompsComponent", self.components), ("MsiFilesComponent", self.files)):
            table = ElementTree.SubElement(root, "COMPONENT", {"cid": "caphyon.advinst.msicomp." + name})
            for row in rows:
                ElementTree.SubElement(table, "ROW", row)
        if extra_table:
            table = ElementTree.SubElement(root, "COMPONENT", {"cid": "caphyon.advinst.msicomp.DynamicPropertyComponent"})
            ElementTree.SubElement(table, "ROW", {"SourcePath": str(self.runtime / "Unlisted.dll")})
        ElementTree.ElementTree(root).write(self.aip, encoding="utf-8")
        return collect_installer_shared_files(self.aip, self.runtime)

    def test_collects_only_matching_root_and_nested_host_sources(self) -> None:
        self.add_file(self.host_file("Host.dll"), filename="HOST~1.DLL|Host.dll")
        self.add_file(self.host_file("ServiceHost/Worker.dll"), directory="Service")
        self.assertEqual({"Host.dll", "ServiceHost/Worker.dll"}, self.resolve())

    def test_service_copy_does_not_imply_the_same_root_file_is_shared(self) -> None:
        self.host_file("Worker.dll")
        self.add_file(self.host_file("ServiceHost/Worker.dll"), directory="Service")
        self.assertEqual({"ServiceHost/Worker.dll"}, self.resolve())

    def test_relocated_and_renamed_sources_are_not_shared(self) -> None:
        self.add_file(self.host_file("Relocated.dll"), directory="Service")
        self.add_file(self.host_file("Original.dll"), filename="Renamed.dll")
        self.add_file(self.host_file("ServiceHost/Moved.dll"))
        self.assertEqual(set(), self.resolve())

    def test_missing_and_external_sources_are_not_shared(self) -> None:
        self.add_file(self.runtime / "Missing.dll")
        outside = self.root / "Outside.dll"
        outside.write_bytes(b"fixture")
        self.add_file(outside)
        self.assertEqual(set(), self.resolve())

    def test_non_file_sourcepath_rows_do_not_count(self) -> None:
        self.host_file("Unlisted.dll")
        self.assertEqual(set(), self.resolve(extra_table=True))

    def test_unknown_source_and_directory_macros_are_not_expanded(self) -> None:
        self.add_file(self.host_file("Host.dll"))
        for source in ("[|AI_APPDIR|]Host.dll", "%HOST_ROOT%\\Host.dll", "$(HostRoot)/Host.dll", "bad\tpath"):
            with self.subTest(source=source):
                self.files[0]["SourcePath"] = source
                self.assertEqual(set(), self.resolve())
        self.files.clear()
        self.components.clear()
        self.directories[1]["DefaultDir"] = "[DYNAMIC_FOLDER]"
        self.add_file(self.host_file("ServiceHost/Worker.dll"), directory="Service")
        self.assertEqual(set(), self.resolve())

    def test_missing_component_or_directory_reference_is_not_shared(self) -> None:
        self.add_file(self.host_file("Host.dll"))
        self.files[0]["Component_"] = "missing"
        self.assertEqual(set(), self.resolve())
        self.files[0]["Component_"] = "file0"
        self.components[0]["Directory_"] = "missing"
        self.assertEqual(set(), self.resolve())

    def test_cycles_and_non_appdir_destinations_are_not_shared(self) -> None:
        self.add_file(self.host_file("ServiceHost/Worker.dll"), directory="Service")
        for parent in ("Service", "Other", "TARGETDIR"):
            with self.subTest(parent=parent):
                self.directories[1]["Directory_Parent"] = parent
                self.directories.append({"Directory": "Other", "Directory_Parent": "Service", "DefaultDir": "Other"})
                self.assertEqual(set(), self.resolve())
                self.directories.pop()

    def test_duplicate_file_component_or_directory_identifiers_are_ambiguous(self) -> None:
        self.add_file(self.host_file("Host.dll"))
        for rows in (self.files, self.components, self.directories):
            with self.subTest(table=rows):
                rows.append(dict(rows[0]))
                self.assertEqual(set(), self.resolve())
                rows.pop()

    def test_conditionally_installed_components_are_not_assumed_shared(self) -> None:
        self.add_file(self.host_file("Host.dll"))
        self.components[0]["Condition"] = "ENABLE_OPTIONAL_HOST"
        self.assertEqual(set(), self.resolve())

    def test_unsafe_or_macro_based_destination_names_are_not_shared(self) -> None:
        self.add_file(self.host_file("Host.dll"))
        for filename in ("../Host.dll", "sub\\Host.dll", "Host.dll:ads", "[HOST_FILE]", "Host.dll ", "Host.dll.", "A|B|Host.dll"):
            with self.subTest(filename=filename):
                self.files[0]["FileName"] = filename
                self.assertEqual(set(), self.resolve())

    def test_defaultdir_target_source_and_dot_directory_are_supported(self) -> None:
        self.directories[1]["DefaultDir"] = "SERVIC~1|ServiceHost:OtherSource"
        self.directories.append({"Directory": "Same", "Directory_Parent": "Service", "DefaultDir": "."})
        self.add_file(self.host_file("ServiceHost/Worker.dll"), directory="Same")
        self.assertEqual({"ServiceHost/Worker.dll"}, self.resolve())

    def test_source_symlink_outside_host_is_not_shared(self) -> None:
        outside = self.root / "Outside.dll"
        outside.write_bytes(b"fixture")
        link = self.runtime / "Link.dll"
        try:
            link.symlink_to(outside)
        except OSError as exc:
            self.skipTest(f"Symlinks unavailable: {exc}")
        self.add_file(link)
        self.assertEqual(set(), self.resolve())

    def test_unreadable_or_malformed_aip_remains_a_caller_visible_error(self) -> None:
        with self.assertRaises(FileNotFoundError):
            collect_installer_shared_files(self.aip, self.runtime)
        self.aip.write_text("not XML", encoding="utf-8")
        with self.assertRaises(ElementTree.ParseError):
            collect_installer_shared_files(self.aip, self.runtime)

    def test_source_symlink_cycle_is_not_shared(self) -> None:
        link = self.runtime / "Loop.dll"
        try:
            link.symlink_to(link)
        except OSError as exc:
            self.skipTest(f"Symlinks unavailable: {exc}")
        self.add_file(link)
        self.assertEqual(set(), self.resolve())


if __name__ == "__main__":
    unittest.main()
