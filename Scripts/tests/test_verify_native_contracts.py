import tempfile
import unittest
import zipfile
from pathlib import Path

from Scripts.build_update import create_full_zip
from Scripts.verify_native_contracts import (
    AMD64_MACHINE,
    CUDA_PACKAGE_MEMBER,
    CUDA_TRACKED_DLL,
    NativeContractError,
    read_header_exports,
    read_managed_dllimports,
    read_pe_exports,
    validate_cuda_export_sets,
    validate_cuda_source_contracts,
    validate_native_contracts,
)


REPO_ROOT = Path(__file__).resolve().parents[2]


class PeExportReaderTests(unittest.TestCase):
    def test_reads_tracked_cuda_pe_without_external_tools(self) -> None:
        machine, exports = read_pe_exports((REPO_ROOT / CUDA_TRACKED_DLL).read_bytes())

        self.assertEqual(AMD64_MACHINE, machine)
        self.assertIn("CM_Fusion_Batch", exports)
        self.assertIn("M_FreeHImageData", exports)

    def test_rejects_non_pe_input(self) -> None:
        with self.assertRaisesRegex(NativeContractError, "not a PE image"):
            read_pe_exports(b"not a PE")


class SourceContractTests(unittest.TestCase):
    def test_header_and_managed_parsers_report_declared_entry_points(self) -> None:
        header = read_header_exports((REPO_ROOT / "Native/include/cuda_export.h").read_text(encoding="utf-8-sig"))
        managed = read_managed_dllimports((REPO_ROOT / "UI/ColorVision.Core/OpenCVCuda.cs").read_text(encoding="utf-8-sig"))

        self.assertEqual(
            {"M_FreeHImageData", "M_Fusion", "CM_Fusion", "CM_Fusion_Async", "CM_Fusion_Batch"},
            set(managed),
        )
        self.assertTrue(set(managed).issubset(header))

    def test_rejects_non_cdecl_cuda_import(self) -> None:
        source = '''
            private const string LibPath = "opencv_cuda.dll";
            [DllImport(LibPath, EntryPoint = "CM_Fusion")]
            private static extern int CM_FusionNative(string value);
        '''

        with self.assertRaisesRegex(NativeContractError, "not declared Cdecl"):
            read_managed_dllimports(source)

    def test_rejects_header_export_missing_from_tracked_pe(self) -> None:
        header = frozenset({"CM_Fusion", "CM_SetLogCallback", "CM_SetLogEnabled", "CM_SetLogLevel", "CM_EnableNativeSink"})
        managed = frozenset({"CM_Fusion"})
        binary = frozenset(header - {"CM_Fusion"}) | {"NvOptimusEnablementCuda"}

        with self.assertRaisesRegex(NativeContractError, r"missing=\['CM_Fusion'\]"):
            validate_cuda_export_sets(header, managed, binary)


class StaticAbiMutationTests(unittest.TestCase):
    def setUp(self) -> None:
        self.header = self._read("Native/include/cuda_export.h")
        self.managed = self._read("UI/ColorVision.Core/OpenCVCuda.cs")
        self.native_struct = self._read("Native/include/custom_structs.h")
        self.managed_struct = self._read("UI/ColorVision.Core/HImage.cs")
        self.log_bridge = self._read("UI/ColorVision.Core/NativeLogBridge.cs")

    def test_rejects_header_return_type_mutation(self) -> None:
        mutated = self._replace_once(self.header, "int CM_Fusion(", "void CM_Fusion(")

        with self.assertRaisesRegex(NativeContractError, "function signature drift"):
            self._validate(header=mutated)

    def test_rejects_header_parameter_type_mutation(self) -> None:
        mutated = self._replace_once(
            self.header,
            "int CM_Fusion(const char* fusionjson, HImage* outImage)",
            "int CM_Fusion(const char* fusionjson, HImage outImage)",
        )

        with self.assertRaisesRegex(NativeContractError, "function signature drift"):
            self._validate(header=mutated)

    def test_rejects_managed_return_and_parameter_mutation(self) -> None:
        mutated = self._replace_once(
            self.managed,
            "private static extern int CM_FusionNative(string fusionjson, out HImage hImage);",
            "private static extern void CM_FusionNative(string fusionjson, ref HImage hImage);",
        )

        with self.assertRaisesRegex(NativeContractError, "DllImport signature drift"):
            self._validate(managed=mutated)

    def test_rejects_open_cv_cuda_library_name_mutation(self) -> None:
        mutated = self._replace_once(
            self.managed,
            'private const string LibPath = "opencv_cuda.dll";',
            'private const string LibPath = "opencv_cuda_v2.dll";',
        )

        with self.assertRaisesRegex(NativeContractError, "OpenCVCuda.LibPath drift"):
            self._validate(managed=mutated)

    def test_rejects_native_himage_field_order_mutation(self) -> None:
        mutated = self._replace_once(
            self.native_struct,
            "    int rows;\n    int cols;",
            "    int cols;\n    int rows;",
        )

        with self.assertRaisesRegex(NativeContractError, "Native HImage layout/pack drift"):
            self._validate(native_struct=mutated)

    def test_rejects_native_himage_pack_mutation(self) -> None:
        mutated = self._replace_once(
            self.native_struct,
            "typedef struct HImage\n{",
            "#pragma pack(push, 1)\ntypedef struct HImage\n{",
        )

        with self.assertRaisesRegex(NativeContractError, "Native HImage layout/pack drift"):
            self._validate(native_struct=mutated)

    def test_rejects_managed_himage_pack_mutation(self) -> None:
        mutated = self._replace_once(
            self.managed_struct,
            "[StructLayout(LayoutKind.Sequential)]\n    public struct HImage",
            "[StructLayout(LayoutKind.Sequential, Pack = 1)]\n    public struct HImage",
        )

        with self.assertRaisesRegex(NativeContractError, "Managed HImage layout/pack drift"):
            self._validate(managed_struct=mutated)

    def test_rejects_native_log_callback_convention_mutation(self) -> None:
        mutated = self._replace_once(
            self.header,
            "typedef void(__stdcall* CVNativeLogCallback)",
            "typedef void(__cdecl* CVNativeLogCallback)",
        )

        with self.assertRaisesRegex(NativeContractError, "CVNativeLogCallback signature drift"):
            self._validate(header=mutated)

    def test_rejects_native_log_bridge_delegate_convention_mutation(self) -> None:
        mutated = self._replace_once(
            self.log_bridge,
            "[UnmanagedFunctionPointer(CallingConvention.StdCall)]",
            "[UnmanagedFunctionPointer(CallingConvention.Cdecl)]",
        )

        with self.assertRaisesRegex(NativeContractError, "delegate ABI drift"):
            self._validate(log_bridge=mutated)

    def test_rejects_native_log_bridge_dynamic_export_mutation(self) -> None:
        mutated = self._replace_once(
            self.log_bridge,
            '$"{exportPrefix}SetLogLevel"',
            '$"{exportPrefix}SetVerbosity"',
        )

        with self.assertRaisesRegex(NativeContractError, "dynamic export binding drift"):
            self._validate(log_bridge=mutated)

    def _validate(
        self,
        *,
        header: str | None = None,
        managed: str | None = None,
        native_struct: str | None = None,
        managed_struct: str | None = None,
        log_bridge: str | None = None,
    ) -> None:
        validate_cuda_source_contracts(
            header if header is not None else self.header,
            managed if managed is not None else self.managed,
            native_struct if native_struct is not None else self.native_struct,
            managed_struct if managed_struct is not None else self.managed_struct,
            log_bridge if log_bridge is not None else self.log_bridge,
        )

    @staticmethod
    def _read(relative_path: str) -> str:
        return (REPO_ROOT / relative_path).read_text(encoding="utf-8-sig")

    def _replace_once(self, source: str, old: str, new: str) -> str:
        self.assertEqual(1, source.count(old), f"Mutation target must be unique: {old!r}")
        return source.replace(old, new, 1)


class FullZipContractIntegrationTests(unittest.TestCase):
    def setUp(self) -> None:
        self._temporary_directory = tempfile.TemporaryDirectory(prefix="native-full-zip-tests-")
        self.root = Path(self._temporary_directory.name)
        self.version_directory = self.root / "version"
        self.version_directory.mkdir()
        self.full_zip = self.root / "ColorVision-[test].zip"

    def tearDown(self) -> None:
        self._temporary_directory.cleanup()

    def test_create_full_zip_output_passes_native_contract_validator(self) -> None:
        self._write_cuda((REPO_ROOT / CUDA_TRACKED_DLL).read_bytes())

        create_full_zip(self.version_directory, self.full_zip)
        report = validate_native_contracts(REPO_ROOT, package_files=(self.full_zip,))

        self.assertEqual((self.full_zip.resolve(),), report.package_files)

    def test_create_full_zip_output_with_stale_cuda_fails_native_contract_validator(self) -> None:
        self._write_cuda(b"stale opencv_cuda binary")

        create_full_zip(self.version_directory, self.full_zip)

        with self.assertRaisesRegex(NativeContractError, "differs from tracked DLL"):
            validate_native_contracts(REPO_ROOT, package_files=(self.full_zip,))

    def _write_cuda(self, content: bytes) -> None:
        path = self.version_directory / Path(CUDA_PACKAGE_MEMBER)
        path.parent.mkdir(parents=True)
        path.write_bytes(content)


class RepositoryNativeContractTests(unittest.TestCase):
    def setUp(self) -> None:
        self._temporary_directory = tempfile.TemporaryDirectory(prefix="native-contract-tests-")
        self.root = Path(self._temporary_directory.name)

    def tearDown(self) -> None:
        self._temporary_directory.cleanup()

    def test_current_repository_contract_is_complete(self) -> None:
        report = validate_native_contracts(REPO_ROOT)

        self.assertEqual(AMD64_MACHINE, read_pe_exports(report.tracked_dll.read_bytes())[0])
        self.assertEqual(10, len(report.exports))

    def test_accepts_exact_runtime_and_nupkg_bytes(self) -> None:
        tracked_bytes = (REPO_ROOT / CUDA_TRACKED_DLL).read_bytes()
        runtime_path = self.root / "runtimes/win-x64/native/opencv_cuda.dll"
        runtime_path.parent.mkdir(parents=True)
        runtime_path.write_bytes(tracked_bytes)
        package_path = self.root / "ColorVision.Core.test.nupkg"
        with zipfile.ZipFile(package_path, "w", zipfile.ZIP_DEFLATED) as archive:
            archive.writestr(CUDA_PACKAGE_MEMBER, tracked_bytes)

        report = validate_native_contracts(
            REPO_ROOT,
            runtime_files=(runtime_path,),
            package_files=(package_path,),
        )

        self.assertEqual((runtime_path.resolve(),), report.runtime_files)
        self.assertEqual((package_path.resolve(),), report.package_files)

    def test_rejects_package_with_different_cuda_bytes(self) -> None:
        package_path = self.root / "ColorVision.Core.test.nupkg"
        with zipfile.ZipFile(package_path, "w", zipfile.ZIP_DEFLATED) as archive:
            archive.writestr(CUDA_PACKAGE_MEMBER, b"stale DLL")

        with self.assertRaisesRegex(NativeContractError, "differs from tracked DLL"):
            validate_native_contracts(REPO_ROOT, package_files=(package_path,))

    def test_rejects_package_that_omits_cuda(self) -> None:
        package_path = self.root / "ColorVision.Core.test.nupkg"
        with zipfile.ZipFile(package_path, "w", zipfile.ZIP_DEFLATED) as archive:
            archive.writestr("README.md", "missing native runtime")

        with self.assertRaisesRegex(NativeContractError, "must contain exactly one"):
            validate_native_contracts(REPO_ROOT, package_files=(package_path,))


if __name__ == "__main__":
    unittest.main()
