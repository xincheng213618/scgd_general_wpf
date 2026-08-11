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
    validate_cuda_native_export_build,
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

    def test_rejects_conditional_or_overridden_native_export_definitions(self) -> None:
        project_source = (REPO_ROOT / "Native/opencv_cuda/opencv_cuda.vcxproj").read_text(
            encoding="utf-8-sig"
        )
        mutations = (
            (
                "conditional-definition",
                "<PreprocessorDefinitions>NDEBUG;OPENCVCUDA_EXPORTS;",
                '<PreprocessorDefinitions Condition="\'Never\' == \'true\'">NDEBUG;OPENCVCUDA_EXPORTS;',
            ),
            (
                "later-override",
                "</Project>",
                "<ItemDefinitionGroup Condition=\"'$(Configuration)|$(Platform)'=='Release|x64'\">"
                "<ClCompile><PreprocessorDefinitions>NDEBUG;%(PreprocessorDefinitions)"
                "</PreprocessorDefinitions></ClCompile></ItemDefinitionGroup></Project>",
            ),
            (
                "extra-export-macro",
                "<PreprocessorDefinitions>NDEBUG;OPENCVCUDA_EXPORTS;",
                "<PreprocessorDefinitions>NDEBUG;OPENCVCUDA_EXPORTS;"
                "CV_EXTRA_EXPORT=__declspec(dllexport);",
            ),
        )
        for name, old, new in mutations:
            with self.subTest(name=name), tempfile.TemporaryDirectory(
                prefix="cuda-native-project-mutation-"
            ) as directory:
                self.assertEqual(1, project_source.count(old))
                project = Path(directory) / "opencv_cuda.vcxproj"
                project.write_text(project_source.replace(old, new, 1), encoding="utf-8")
                with self.assertRaises(NativeContractError):
                    validate_cuda_native_export_build(project)

        release_marker = '<ItemDefinitionGroup Condition="\'$(Configuration)|$(Platform)\'==\'Release|x64\'">'
        release_start = project_source.index(release_marker)
        release_end = project_source.index("</ItemDefinitionGroup>", release_start) + len(
            "</ItemDefinitionGroup>"
        )
        conditional_ancestor = (
            project_source[:release_start]
            + '<Choose><When Condition="\'Never\' == \'true\'">'
            + project_source[release_start:release_end]
            + "</When></Choose>"
            + project_source[release_end:]
        )
        with tempfile.TemporaryDirectory(prefix="cuda-native-project-ancestor-mutation-") as directory:
            project = Path(directory) / "opencv_cuda.vcxproj"
            project.write_text(conditional_ancestor, encoding="utf-8")
            with self.assertRaises(NativeContractError):
                validate_cuda_native_export_build(project)


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

        with self.assertRaisesRegex(NativeContractError, "unknown or reordered instance field"):
            self._validate(native_struct=mutated)

    def test_rejects_native_himage_pack_mutation(self) -> None:
        mutated = self._replace_once(
            self.native_struct,
            "#pragma pack(push, 8)\ntypedef struct HImage",
            "#pragma pack(push, 1)\ntypedef struct HImage",
        )

        with self.assertRaisesRegex(NativeContractError, "Native HImage layout/pack drift"):
            self._validate(native_struct=mutated)

    def test_rejects_native_himage_missing_pack_pop(self) -> None:
        mutated = self._replace_once(
            self.native_struct,
            "} HImage;\n#pragma pack(pop)",
            "} HImage;",
        )

        with self.assertRaisesRegex(NativeContractError, "default pack state"):
            self._validate(native_struct=mutated)

    def test_rejects_native_himage_nested_pack_scope(self) -> None:
        mutated = self._replace_once(
            self.native_struct,
            "#pragma pack(push, 8)\ntypedef struct HImage",
            "#pragma pack(push, 1)\n#pragma pack(push, 8)\ntypedef struct HImage",
        )

        with self.assertRaisesRegex(NativeContractError, "default pack state"):
            self._validate(native_struct=mutated)

    def test_rejects_pack_leaks_after_himage(self) -> None:
        mutations = (
            ("leaked-push", self.native_struct + "\n#pragma pack(push, 1)\n", "default pack state"),
            ("extra-pop", self.native_struct + "\n#pragma pack(pop)\n", "unmatched"),
        )
        for name, mutated, error in mutations:
            with self.subTest(name=name):
                with self.assertRaisesRegex(NativeContractError, error):
                    self._validate(native_struct=mutated)

    def test_rejects_managed_himage_pack_mutation(self) -> None:
        mutated = self._replace_once(
            self.managed_struct,
            "[StructLayout(LayoutKind.Sequential, Pack = 8)]\n    public struct HImage",
            "[StructLayout(LayoutKind.Sequential, Pack = 1)]\n    public struct HImage",
        )

        with self.assertRaisesRegex(NativeContractError, "Managed HImage layout/pack drift"):
            self._validate(managed_struct=mutated)

    def test_rejects_duplicate_managed_himage_pack(self) -> None:
        mutated = self._replace_once(
            self.managed_struct,
            "[StructLayout(LayoutKind.Sequential, Pack = 8)]\n    public struct HImage",
            "[StructLayout(LayoutKind.Sequential, Pack = 1, Pack = 8)]\n    public struct HImage",
        )

        with self.assertRaisesRegex(NativeContractError, "declare Pack exactly once"):
            self._validate(managed_struct=mutated)

    def test_rejects_unknown_himage_instance_fields(self) -> None:
        mutations = (
            (
                "native",
                self.native_struct,
                "    int rows;",
                "    int rows;\n    int generation;",
            ),
            (
                "managed",
                self.managed_struct,
                "        public int rows;",
                "        public int rows;\n        public int generation;",
            ),
        )
        for name, source, old, new in mutations:
            with self.subTest(name=name):
                mutated = self._replace_once(source, old, new)
                with self.assertRaisesRegex(NativeContractError, "unknown or reordered instance field"):
                    if name == "native":
                        self._validate(native_struct=mutated)
                    else:
                        self._validate(managed_struct=mutated)

    def test_rejects_double_and_long_himage_fields(self) -> None:
        mutations = (
            ("native-double", "native", "    int rows;", "    int rows;\n    double scale;"),
            ("native-long", "native", "    int rows;", "    int rows;\n    long generation;"),
            ("managed-double", "managed", "        public int rows;", "        public int rows;\n        public double scale;"),
            ("managed-long", "managed", "        public int rows;", "        public int rows;\n        private long generation;"),
        )
        for name, kind, old, new in mutations:
            with self.subTest(name=name):
                source = self.native_struct if kind == "native" else self.managed_struct
                mutated = self._replace_once(source, old, new)
                with self.assertRaisesRegex(NativeContractError, "unsupported top-level declaration"):
                    if kind == "native":
                        self._validate(native_struct=mutated)
                    else:
                        self._validate(managed_struct=mutated)

    def test_rejects_virtual_inheritance_and_non_standard_layout_mutations(self) -> None:
        mutations = (
            (
                "virtual",
                "    int type() const",
                "    virtual int type() const",
                "unsupported top-level declaration",
            ),
            (
                "inheritance",
                "typedef struct HImage\n{",
                "typedef struct HImage : HImageBase\n{",
                "must not use inheritance",
            ),
            (
                "mixed-access",
                "    int rows;",
                "private:\n    int rows;\npublic:",
                "unsupported top-level declaration",
            ),
        )
        for name, old, new, error in mutations:
            with self.subTest(name=name):
                mutated = self._replace_once(self.native_struct, old, new)
                with self.assertRaisesRegex(NativeContractError, error):
                    self._validate(native_struct=mutated)

    def test_rejects_windows_export_macro_mutations(self) -> None:
        mutations = (
            ("missing-export", "#define COLORVISIONCORE_API __declspec(dllexport)", "#define COLORVISIONCORE_API"),
            ("import-in-export-branch", "#define COLORVISIONCORE_API __declspec(dllexport)", "#define COLORVISIONCORE_API __declspec(dllimport)"),
        )
        for name, old, new in mutations:
            with self.subTest(name=name):
                mutated = self._replace_once(self.header, old, new)
                with self.assertRaisesRegex(NativeContractError, "OPENCVCUDA_EXPORTS"):
                    self._validate(header=mutated)

    def test_rejects_export_macro_post_override_and_raw_string_decoy(self) -> None:
        post_override = (
            self.header
            + "\n#undef COLORVISIONCORE_API\n#define COLORVISIONCORE_API\n"
        )
        with self.assertRaisesRegex(NativeContractError, "exact OPENCVCUDA_EXPORTS"):
            self._validate(header=post_override)

        real_header = self._replace_once(
            self.header,
            "#ifdef OPENCVCUDA_EXPORTS",
            "#if defined(OPENCVCUDA_EXPORTS)",
        )
        real_header = self._replace_once(
            real_header,
            "#define COLORVISIONCORE_API __declspec(dllexport)",
            "#define COLORVISIONCORE_API __declspec(dllimport)",
        )
        raw_decoy = f'const char* AbiDecoy = R"CVABI({self.header})CVABI";\n{real_header}'
        with self.assertRaisesRegex(NativeContractError, "exact OPENCVCUDA_EXPORTS"):
            self._validate(header=raw_decoy)

    def test_rejects_direct_dllexport_outside_the_contract_macro(self) -> None:
        mutated = (
            self.header
            + '\nextern "C" __declspec(dllexport) long CM_Unexpected(long value);\n'
        )

        with self.assertRaises(NativeContractError):
            self._validate(header=mutated)

    def test_rejects_unparsed_macro_export_declaration(self) -> None:
        mutated = self.header + "\nEXTERN_C CV_EXTRA_EXPORT long CM_Unexpected(long value);\n"

        with self.assertRaises(NativeContractError):
            self._validate(header=mutated)

    def test_rejects_unreviewed_native_contract_includes(self) -> None:
        native_struct = self._replace_once(
            self.native_struct,
            "#include <type_traits>",
            '#include <type_traits>\n#include "himage_abi_override.h"',
        )
        with self.assertRaises(NativeContractError):
            self._validate(native_struct=native_struct)

        header = self._replace_once(
            self.header,
            '#include "custom_structs.h"',
            '#include "custom_structs.h"\n#include "cuda_export_override.h"',
        )
        with self.assertRaises(NativeContractError):
            self._validate(header=header)

    def test_rejects_string_literal_himage_decoys(self) -> None:
        native_real = self._replace_once(
            self.native_struct,
            "typedef struct HImage",
            "struct HImage",
        )
        native_real = self._replace_once(native_real, "    int rows;", "    long rows;")
        native_decoy = (
            f'const char* AbiDecoy = R"CVABI({self.native_struct})CVABI";\n'
            f"{native_real}"
        )
        with self.assertRaisesRegex(NativeContractError, "does not declare typedef struct HImage"):
            self._validate(native_struct=native_decoy)

        managed_real = self._replace_once(
            self.managed_struct,
            "public struct HImage",
            "public partial struct HImage",
        )
        managed_real = self._replace_once(
            managed_real,
            "        public int rows;",
            "        public long rows;",
        )
        managed_decoy = f'const string AbiDecoy = """\n{self.managed_struct}\n""";\n{managed_real}'
        with self.assertRaisesRegex(NativeContractError, "does not declare a StructLayout"):
            self._validate(managed_struct=managed_decoy)

    def test_rejects_conditional_and_token_rewriting_himage_mutations(self) -> None:
        mutations = (
            (
                "native-int-macro",
                "native",
                "#define int long\n" + self.native_struct + "\n#undef int\n",
            ),
            (
                "native-field-macro",
                "native",
                "#define rows rows; int generation\n" + self.native_struct + "\n#undef rows\n",
            ),
            (
                "native-conditional-decoy",
                "native",
                "#if 0\ntypedef struct HImage { int rows; } HImage;\n#endif\n" + self.native_struct,
            ),
            (
                "managed-conditional-decoy",
                "managed",
                "#if ABI_DECOY\n[StructLayout(LayoutKind.Sequential, Pack = 8)] "
                "public struct HImage : IDisposable { public long rows; }\n#endif\n"
                + self.managed_struct,
            ),
        )
        for name, kind, mutated in mutations:
            with self.subTest(name=name):
                with self.assertRaisesRegex(NativeContractError, "conditional compilation|token-rewriting"):
                    if kind == "native":
                        self._validate(native_struct=mutated)
                    else:
                        self._validate(managed_struct=mutated)

    def test_rejects_line_spliced_native_token_rewriting(self) -> None:
        line_splice = "\\" + "\n"
        mutated = self._replace_once(
            self.native_struct,
            "#pragma pack(push, 8)\ntypedef struct HImage",
            f"#defi{line_splice}ne int long\n#pragma pack(push, 8)\ntypedef struct HImage",
        )
        mutated = self._replace_once(
            mutated,
            "} HImage;\n#pragma pack(pop)",
            f"}} HImage;\n#pragma pack(pop)\n#un{line_splice}def int",
        )

        with self.assertRaises(NativeContractError):
            self._validate(native_struct=mutated)

    def test_rejects_alternate_native_pack_pragmas(self) -> None:
        for name, directive in (
            ("msvc-intrinsic", "__pragma(pack(push, 1))"),
            ("preprocessor-digraph", "%:pragma pack(push, 1)"),
        ):
            with self.subTest(name=name):
                mutated = self._replace_once(
                    self.native_struct,
                    "} HImage;\n#pragma pack(pop)",
                    f"}} HImage;\n#pragma pack(pop)\n{directive}",
                )
                with self.assertRaises(NativeContractError):
                    self._validate(native_struct=mutated)

    def test_rejects_unparsed_fully_qualified_dllimport(self) -> None:
        unexpected_import = (
            "        [System.Runtime.InteropServices.DllImport(LibPath, EntryPoint = \"CM_Unexpected\", "
            "CallingConvention = CallingConvention.StdCall)]\n"
            "        private static extern long UnexpectedNative(long value);\n\n"
        )
        mutated = self._replace_once(
            self.managed,
            "        private static void PrepareNativeLogging()",
            unexpected_import + "        private static void PrepareNativeLogging()",
        )

        with self.assertRaises(NativeContractError):
            self._validate(managed=mutated)

    def test_rejects_unreviewed_library_import_source_generator(self) -> None:
        unexpected_import = (
            "        [System.Runtime.InteropServices.LibraryImport(LibPath, "
            "EntryPoint = \"CM_Unexpected\")]\n"
            "        private static partial long UnexpectedNative(long value);\n\n"
        )
        mutated = self._replace_once(
            self.managed,
            "public static class OpenCVCuda",
            "public static partial class OpenCVCuda",
        )
        mutated = self._replace_once(
            mutated,
            "        private static void PrepareNativeLogging()",
            unexpected_import + "        private static void PrepareNativeLogging()",
        )

        with self.assertRaises(NativeContractError):
            self._validate(managed=mutated)

    def test_rejects_csharp_type_aliases_that_rebind_abi_tokens(self) -> None:
        managed = self._replace_once(
            self.managed,
            "using System.Runtime.InteropServices;",
            "using System.Runtime.InteropServices;\nusing HImage = System.IntPtr;",
        )
        with self.assertRaises(NativeContractError):
            self._validate(managed=managed)

        managed_struct = self._replace_once(
            self.managed_struct,
            "using System.Runtime.InteropServices;",
            "using System.Runtime.InteropServices;\nusing IntPtr = System.Int64;",
        )
        with self.assertRaises(NativeContractError):
            self._validate(managed_struct=managed_struct)

    def test_commented_out_contract_declarations_do_not_count(self) -> None:
        header_mutation = self._replace_once(
            self.header,
            'extern "C" COLORVISIONCORE_API int CM_Fusion(const char* fusionjson, HImage* outImage);',
            '// extern "C" COLORVISIONCORE_API int CM_Fusion(const char* fusionjson, HImage* outImage);',
        )
        with self.assertRaisesRegex(NativeContractError, "function signature drift"):
            self._validate(header=header_mutation)

        managed_declaration = (
            '[DllImport(LibPath, EntryPoint = "CM_Fusion", CallingConvention = CallingConvention.Cdecl)]\n'
            '        private static extern int CM_FusionNative(string fusionjson, out HImage hImage);'
        )
        managed_mutation = self._replace_once(
            self.managed,
            managed_declaration,
            f"/* {managed_declaration} */",
        )
        with self.assertRaisesRegex(NativeContractError, "DllImport signature drift"):
            self._validate(managed=managed_mutation)

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
