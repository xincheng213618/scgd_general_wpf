import argparse
import hashlib
import json
import os
import re
import shutil
import struct
import subprocess
import sys
import tempfile
import uuid
import zipfile
from dataclasses import dataclass
from pathlib import Path
from xml.etree import ElementTree


CUDA_HEADER = Path("Native/include/cuda_export.h")
CUDA_NATIVE_STRUCTS = Path("Native/include/custom_structs.h")
CUDA_MANAGED_WRAPPER = Path("UI/ColorVision.Core/OpenCVCuda.cs")
CUDA_MANAGED_STRUCTS = Path("UI/ColorVision.Core/HImage.cs")
CUDA_NATIVE_LOG_BRIDGE = Path("UI/ColorVision.Core/NativeLogBridge.cs")
CUDA_PROJECT = Path("UI/ColorVision.Core/ColorVision.Core.csproj")
CUDA_NATIVE_PROJECT = Path("Native/opencv_cuda/opencv_cuda.vcxproj")
CUDA_TRACKED_DLL = Path("x64/Release/opencv_cuda.dll")
CUDA_PACKAGE_MEMBER = "runtimes/win-x64/native/opencv_cuda.dll"
CUDA_DYNAMIC_MANAGED_EXPORTS = frozenset({
    "CM_EnableNativeSink",
    "CM_SetLogCallback",
    "CM_SetLogEnabled",
    "CM_SetLogLevel",
})
CUDA_ALLOWED_BINARY_ONLY_EXPORTS = frozenset({"NvOptimusEnablementCuda"})
AMD64_MACHINE = 0x8664
DEFAULT_WINDOWS_X64_PACK = 8
CUDA_EXPORT_SOURCE = "cuda_export.cpp"
EXPECTED_DLLIMPORT_NAMED_ARGUMENTS = frozenset({"EntryPoint", "CallingConvention", "CharSet"})
DEFAULT_MSBUILD_CANDIDATES = (
    Path(r"C:\Program Files\Microsoft Visual Studio\18\Insiders\MSBuild\Current\Bin\amd64\MSBuild.exe"),
    Path(r"C:\Program Files\Microsoft Visual Studio\18\Insiders\MSBuild\Current\Bin\MSBuild.exe"),
    Path(r"C:\Program Files\Microsoft Visual Studio\2022\Preview\MSBuild\Current\Bin\amd64\MSBuild.exe"),
    Path(r"C:\Program Files\Microsoft Visual Studio\2022\Preview\MSBuild\Current\Bin\MSBuild.exe"),
    Path(r"C:\Program Files\Microsoft Visual Studio\2022\BuildTools\MSBuild\Current\Bin\amd64\MSBuild.exe"),
    Path(r"C:\Program Files\Microsoft Visual Studio\2022\Enterprise\MSBuild\Current\Bin\amd64\MSBuild.exe"),
    Path(r"C:\Program Files\Microsoft Visual Studio\2022\Professional\MSBuild\Current\Bin\amd64\MSBuild.exe"),
    Path(r"C:\Program Files\Microsoft Visual Studio\2022\Community\MSBuild\Current\Bin\amd64\MSBuild.exe"),
)


@dataclass(frozen=True)
class AbiParameter:
    type_name: str
    name: str
    modifier: str = ""
    attributes: tuple[str, ...] = ()


@dataclass(frozen=True)
class AbiFunction:
    name: str
    return_type: str
    calling_convention: str
    parameters: tuple[AbiParameter, ...]


@dataclass(frozen=True)
class AbiField:
    type_name: str
    name: str
    attributes: tuple[str, ...] = ()


@dataclass(frozen=True)
class AbiStructLayout:
    name: str
    pack: int
    fields: tuple[AbiField, ...]
    offsets: tuple[int, ...]
    size: int


EXPECTED_HEADER_FUNCTIONS = {
    "M_FreeHImageData": AbiFunction(
        "M_FreeHImageData", "void", "cdecl", (AbiParameter("unsigned char*", "data"),)
    ),
    "M_Fusion": AbiFunction(
        "M_Fusion",
        "int",
        "cdecl",
        (AbiParameter("const char*", "fusionjson"), AbiParameter("HImage*", "outImage")),
    ),
    "CM_Fusion": AbiFunction(
        "CM_Fusion",
        "int",
        "cdecl",
        (AbiParameter("const char*", "fusionjson"), AbiParameter("HImage*", "outImage")),
    ),
    "CM_Fusion_Async": AbiFunction(
        "CM_Fusion_Async",
        "int",
        "cdecl",
        (AbiParameter("const char*", "fusionjson"), AbiParameter("HImage*", "outImage")),
    ),
    "CM_Fusion_Batch": AbiFunction(
        "CM_Fusion_Batch",
        "int",
        "cdecl",
        (
            AbiParameter("const char*", "batchjson"),
            AbiParameter("HImage*", "outImages"),
            AbiParameter("int", "outCapacity"),
            AbiParameter("int*", "outCount"),
        ),
    ),
    "CM_SetLogCallback": AbiFunction(
        "CM_SetLogCallback", "void", "cdecl", (AbiParameter("CVNativeLogCallback", "callback"),)
    ),
    "CM_SetLogEnabled": AbiFunction(
        "CM_SetLogEnabled", "void", "cdecl", (AbiParameter("int", "enabled"),)
    ),
    "CM_SetLogLevel": AbiFunction(
        "CM_SetLogLevel", "void", "cdecl", (AbiParameter("int", "level"),)
    ),
    "CM_EnableNativeSink": AbiFunction(
        "CM_EnableNativeSink", "void", "cdecl", (AbiParameter("int", "enabled"),)
    ),
}

EXPECTED_MANAGED_IMPORTS = {
    "M_FreeHImageData": AbiFunction(
        "M_FreeHImageData", "void", "cdecl", (AbiParameter("IntPtr", "data"),)
    ),
    "M_Fusion": AbiFunction(
        "M_Fusion",
        "int",
        "cdecl",
        (AbiParameter("string", "fusionjson"), AbiParameter("HImage", "hImage", "out")),
    ),
    "CM_Fusion": AbiFunction(
        "CM_Fusion",
        "int",
        "cdecl",
        (AbiParameter("string", "fusionjson"), AbiParameter("HImage", "hImage", "out")),
    ),
    "CM_Fusion_Async": AbiFunction(
        "CM_Fusion_Async",
        "int",
        "cdecl",
        (AbiParameter("string", "fusionjson"), AbiParameter("HImage", "hImage", "out")),
    ),
    "CM_Fusion_Batch": AbiFunction(
        "CM_Fusion_Batch",
        "int",
        "cdecl",
        (
            AbiParameter("string", "batchjson"),
            AbiParameter("HImage[]", "outImages", attributes=("Out",)),
            AbiParameter("int", "outCapacity"),
            AbiParameter("int", "outCount", "out"),
        ),
    ),
}

EXPECTED_NATIVE_LOG_CALLBACK = AbiFunction(
    "CVNativeLogCallback",
    "void",
    "stdcall",
    (
        AbiParameter("int", "source"),
        AbiParameter("int", "level"),
        AbiParameter("const char*", "message"),
    ),
)

EXPECTED_LOG_DELEGATES = {
    "NativeLogCallback": AbiFunction(
        "NativeLogCallback",
        "void",
        "stdcall",
        (
            AbiParameter("int", "source"),
            AbiParameter("int", "level"),
            AbiParameter("IntPtr", "messagePtr"),
        ),
    ),
    "SetCallbackDelegate": AbiFunction(
        "SetCallbackDelegate", "void", "cdecl", (AbiParameter("IntPtr", "callback"),)
    ),
    "SetIntDelegate": AbiFunction(
        "SetIntDelegate", "void", "cdecl", (AbiParameter("int", "value"),)
    ),
}

EXPECTED_NATIVE_HIMAGE_FIELDS = (
    AbiField("int", "rows"),
    AbiField("int", "cols"),
    AbiField("int", "channels"),
    AbiField("int", "depth"),
    AbiField("int", "stride"),
    AbiField("bool", "isDispose"),
    AbiField("unsigned char*", "pData"),
)
EXPECTED_MANAGED_HIMAGE_FIELDS = (
    AbiField("int", "rows"),
    AbiField("int", "cols"),
    AbiField("int", "channels"),
    AbiField("int", "depth"),
    AbiField("int", "stride"),
    AbiField("bool", "isDispose", ("MarshalAs(UnmanagedType.I1)",)),
    AbiField("IntPtr", "pData"),
)
EXPECTED_HIMAGE_OFFSETS = (0, 4, 8, 12, 16, 20, 24)
EXPECTED_HIMAGE_SIZE = 32


class NativeContractError(RuntimeError):
    pass


@dataclass(frozen=True)
class NativeContractReport:
    tracked_dll: Path
    sha256: str
    size: int
    exports: tuple[str, ...]
    abi_functions: tuple[str, ...]
    himage_size: int
    runtime_files: tuple[Path, ...]
    package_files: tuple[Path, ...]


def _mask_non_code(source: str) -> str:
    result = list(source)
    index = 0

    def mask(start: int, end: int, *, preserve_quotes: tuple[int, ...] = ()) -> None:
        preserved = set(preserve_quotes)
        for position in range(start, end):
            if position not in preserved and source[position] not in "\r\n":
                result[position] = " "

    while index < len(source):
        character = source[index]
        following = source[index + 1] if index + 1 < len(source) else ""
        if character == "/" and following == "/":
            end = source.find("\n", index + 2)
            end = len(source) if end < 0 else end
            mask(index, end)
            index = end
            continue
        if character == "/" and following == "*":
            end = source.find("*/", index + 2)
            if end < 0:
                raise NativeContractError("Unterminated block comment in ABI contract source.")
            end += 2
            mask(index, end)
            index = end
            continue

        cpp_raw = re.match(r'(?:(?:u8|u|U|L)?R)"(?P<delimiter>[^ ()\\\t\r\n]{0,16})\(', source[index:])
        if cpp_raw and (index == 0 or not (source[index - 1].isalnum() or source[index - 1] == "_")):
            terminator = ")" + cpp_raw.group("delimiter") + '"'
            end = source.find(terminator, index + cpp_raw.end())
            if end < 0:
                raise NativeContractError("Unterminated C++ raw string in ABI contract source.")
            end += len(terminator)
            mask(index, end)
            index = end
            continue

        cs_raw = re.match(r'\$*(?P<quotes>"{3,})', source[index:])
        if cs_raw:
            terminator = cs_raw.group("quotes")
            end = source.find(terminator, index + cs_raw.end())
            if end < 0:
                raise NativeContractError("Unterminated C# raw string in ABI contract source.")
            end += len(terminator)
            mask(index, end)
            index = end
            continue

        verbatim_prefix = next(
            (prefix for prefix in ("$@\"", "@$\"", "@\"") if source.startswith(prefix, index)),
            None,
        )
        if verbatim_prefix:
            cursor = index + len(verbatim_prefix)
            while cursor < len(source):
                if source.startswith('""', cursor):
                    cursor += 2
                    continue
                if source[cursor] == '"':
                    cursor += 1
                    break
                cursor += 1
            else:
                raise NativeContractError("Unterminated C# verbatim string in ABI contract source.")
            mask(index, cursor)
            index = cursor
            continue

        if character == '"':
            cursor = index + 1
            while cursor < len(source):
                if source[cursor] in "\r\n":
                    raise NativeContractError("Unexpected newline in ABI contract string literal.")
                if source[cursor] == "\\":
                    cursor += 2
                    continue
                if source[cursor] == '"':
                    break
                cursor += 1
            if cursor >= len(source):
                raise NativeContractError("Unterminated string in ABI contract source.")
            mask(index, cursor + 1, preserve_quotes=(index, cursor))
            index = cursor + 1
            continue

        if character == "'":
            cursor = index + 1
            while cursor < len(source):
                if source[cursor] in "\r\n":
                    raise NativeContractError("Unexpected newline in ABI contract character literal.")
                if source[cursor] == "\\":
                    cursor += 2
                    continue
                if source[cursor] == "'":
                    cursor += 1
                    break
                cursor += 1
            else:
                raise NativeContractError("Unterminated character literal in ABI contract source.")
            mask(index, cursor)
            index = cursor
            continue
        index += 1
    return "".join(result)


def _original_group(source: str, match: re.Match[str], group: str) -> str:
    start, end = match.span(group)
    return source[start:end]


def _reject_contract_token_aliases(source: str, context: str) -> None:
    if re.search(r"\\\r?\n", source):
        raise NativeContractError(
            f"{context} must not use preprocessor line splicing in ABI contract source."
        )
    code = _mask_non_code(source)
    aliases = []
    if re.search(r"\b(?:__pragma|_Pragma)\s*\(", code):
        aliases.append("pragma operator")
    if "%:" in code:
        aliases.append("preprocessor digraph")
    if aliases:
        raise NativeContractError(
            f"{context} must not use alternate preprocessor tokens: {aliases}."
        )


def _read_source_directives(source: str) -> list[tuple[str, str]]:
    code = _mask_non_code(source)
    directives: list[tuple[str, str]] = []
    for match in re.finditer(
        r"^\s*#\s*(?P<name>[A-Za-z_]\w*)\b(?P<body>[^\r\n]*)$",
        code,
        flags=re.MULTILINE,
    ):
        directives.append(
            (
                match.group("name").casefold(),
                re.sub(r"\s+", " ", _original_group(source, match, "body").strip()),
            )
        )
    return directives


def _validate_custom_struct_directives(source: str) -> None:
    expected = [
        ("pragma", "once"),
        ("include", "<opencv2/core.hpp>"),
        ("include", "<combaseapi.h>"),
        ("include", "<cstddef>"),
        ("include", "<cstdint>"),
        ("include", "<cstring>"),
        ("include", "<limits>"),
        ("include", "<type_traits>"),
    ]
    directives = [
        directive
        for directive in _read_source_directives(source)
        if not (directive[0] == "pragma" and directive[1].startswith("pack("))
    ]
    if directives != expected:
        raise NativeContractError(
            f"custom_structs.h preprocessor/include contract drift: found={directives!r}."
        )


def _reject_csharp_type_aliases(source: str, context: str) -> None:
    code = _mask_non_code(source)
    if re.search(r"^\s*(?:global\s+)?using\s+[A-Za-z_]\w*\s*=", code, re.MULTILINE):
        raise NativeContractError(f"{context} must not use C# type aliases in ABI contract source.")


def _reject_csharp_module_attributes(source: str, context: str) -> None:
    code = _mask_non_code(source)
    if re.search(r"\[\s*module\s*:", code):
        raise NativeContractError(
            f"{context} must not use module-level attributes that can alter P/Invoke defaults."
        )


def validate_colorvision_core_module_attributes(project_directory: Path) -> None:
    try:
        source_paths = sorted(
            path
            for path in project_directory.rglob("*.cs")
            if {part.casefold() for part in path.relative_to(project_directory).parts}.isdisjoint(
                {"bin", "obj"}
            )
        )
    except OSError as exc:
        raise NativeContractError(
            f"Could not enumerate ColorVision.Core C# contract sources: {exc}"
        ) from exc
    if not source_paths:
        raise NativeContractError(
            f"ColorVision.Core contains no C# contract sources: {project_directory}."
        )

    for source_path in source_paths:
        try:
            source = source_path.read_text(encoding="utf-8-sig")
        except OSError as exc:
            raise NativeContractError(
                f"Could not read ColorVision.Core C# contract source {source_path}: {exc}"
            ) from exc
        _reject_csharp_module_attributes(
            source,
            f"ColorVision.Core/{source_path.relative_to(project_directory).as_posix()}",
        )


def _reject_contract_preprocessor_mutation(source: str, context: str) -> None:
    _reject_contract_token_aliases(source, context)
    code = _mask_non_code(source)
    matches = re.findall(
        r"^\s*#\s*(if|ifdef|ifndef|elif|else|endif|define|undef)\b",
        code,
        flags=re.MULTILINE | re.IGNORECASE,
    )
    if matches:
        raise NativeContractError(
            f"{context} must not use conditional compilation or token-rewriting directives: {matches}."
        )


def _unpack_from(data: bytes, format_string: str, offset: int, description: str):
    size = struct.calcsize(format_string)
    if offset < 0 or offset + size > len(data):
        raise NativeContractError(f"Truncated PE while reading {description}.")
    return struct.unpack_from(format_string, data, offset)


def _read_c_string(data: bytes, offset: int, description: str) -> str:
    if offset < 0 or offset >= len(data):
        raise NativeContractError(f"Invalid PE offset while reading {description}.")
    end = data.find(b"\0", offset)
    if end < 0:
        raise NativeContractError(f"Unterminated PE string while reading {description}.")
    try:
        return data[offset:end].decode("ascii")
    except UnicodeDecodeError as exc:
        raise NativeContractError(f"Non-ASCII PE export name in {description}.") from exc


def read_pe_exports(data: bytes) -> tuple[int, frozenset[str]]:
    if len(data) < 64 or data[:2] != b"MZ":
        raise NativeContractError("File is not a PE image (missing MZ header).")

    (pe_offset,) = _unpack_from(data, "<I", 0x3C, "DOS PE offset")
    if pe_offset + 24 > len(data) or data[pe_offset:pe_offset + 4] != b"PE\0\0":
        raise NativeContractError("File is not a PE image (missing PE signature).")

    file_header = pe_offset + 4
    machine, section_count, _, _, _, optional_size, _ = _unpack_from(
        data, "<HHIIIHH", file_header, "COFF header"
    )
    optional_header = file_header + 20
    (magic,) = _unpack_from(data, "<H", optional_header, "optional-header magic")
    if magic == 0x20B:
        number_of_rva_offset = 108
        data_directories_offset = 112
    elif magic == 0x10B:
        number_of_rva_offset = 92
        data_directories_offset = 96
    else:
        raise NativeContractError(f"Unsupported PE optional-header magic: 0x{magic:04X}.")

    if optional_size < data_directories_offset + 8:
        raise NativeContractError("PE optional header does not contain an export directory.")
    (number_of_rva_and_sizes,) = _unpack_from(
        data,
        "<I",
        optional_header + number_of_rva_offset,
        "data-directory count",
    )
    if number_of_rva_and_sizes < 1:
        return machine, frozenset()

    export_rva, _ = _unpack_from(
        data,
        "<II",
        optional_header + data_directories_offset,
        "export data directory",
    )
    if export_rva == 0:
        return machine, frozenset()

    (size_of_headers,) = _unpack_from(
        data, "<I", optional_header + 60, "SizeOfHeaders"
    )
    section_table = optional_header + optional_size
    sections: list[tuple[int, int, int, int]] = []
    for index in range(section_count):
        offset = section_table + index * 40
        _, virtual_size, virtual_address, raw_size, raw_offset = _unpack_from(
            data, "<8sIIII", offset, f"section {index}"
        )
        sections.append((virtual_address, virtual_size, raw_offset, raw_size))

    def rva_to_offset(rva: int, description: str) -> int:
        if rva < size_of_headers:
            if rva >= len(data):
                raise NativeContractError(f"Invalid header RVA while reading {description}.")
            return rva
        for virtual_address, virtual_size, raw_offset, raw_size in sections:
            extent = max(virtual_size, raw_size)
            if virtual_address <= rva < virtual_address + extent:
                delta = rva - virtual_address
                if delta >= raw_size or raw_offset + delta >= len(data):
                    raise NativeContractError(f"RVA has no file data while reading {description}.")
                return raw_offset + delta
        raise NativeContractError(f"Unmapped RVA 0x{rva:X} while reading {description}.")

    export_offset = rva_to_offset(export_rva, "export directory")
    export_directory = _unpack_from(
        data, "<IIHHIIIIIII", export_offset, "export directory"
    )
    name_count = export_directory[7]
    names_rva = export_directory[9]
    if name_count > 100_000:
        raise NativeContractError(f"Unreasonable PE export-name count: {name_count}.")
    names_offset = rva_to_offset(names_rva, "export-name table")

    exports: set[str] = set()
    for index in range(name_count):
        (name_rva,) = _unpack_from(
            data, "<I", names_offset + index * 4, f"export-name RVA {index}"
        )
        name_offset = rva_to_offset(name_rva, f"export name {index}")
        name = _read_c_string(data, name_offset, f"export name {index}")
        if name in exports:
            raise NativeContractError(f"Duplicate named PE export: {name}.")
        exports.add(name)
    return machine, frozenset(exports)


def _split_top_level(value: str) -> list[str]:
    parts: list[str] = []
    start = 0
    square_depth = 0
    round_depth = 0
    for index, character in enumerate(value):
        if character == "[":
            square_depth += 1
        elif character == "]":
            square_depth -= 1
        elif character == "(":
            round_depth += 1
        elif character == ")":
            round_depth -= 1
        elif character == "," and square_depth == 0 and round_depth == 0:
            parts.append(value[start:index].strip())
            start = index + 1
    final = value[start:].strip()
    if final:
        parts.append(final)
    return parts


def _normalize_cpp_type(type_name: str) -> str:
    normalized = re.sub(r"\s+", " ", type_name.strip())
    normalized = re.sub(r"\s*\*\s*", "*", normalized)
    return normalized


def _parse_cpp_parameters(value: str, context: str) -> tuple[AbiParameter, ...]:
    if not value.strip() or value.strip() == "void":
        return ()
    parameters: list[AbiParameter] = []
    for declaration in _split_top_level(value):
        match = re.fullmatch(r"(?P<type>.*(?:\s|\*))(?P<name>[A-Za-z_]\w*)", declaration.strip())
        if not match:
            raise NativeContractError(f"Could not parse C++ ABI parameter {declaration!r} in {context}.")
        parameters.append(
            AbiParameter(_normalize_cpp_type(match.group("type")), match.group("name"))
        )
    return tuple(parameters)


def _parse_cpp_function(name: str, prefix: str, parameters: str) -> AbiFunction:
    conventions = re.findall(r"__(cdecl|stdcall|fastcall|vectorcall)\b", prefix)
    if len(conventions) > 1:
        raise NativeContractError(f"Multiple calling conventions declared for {name}.")
    calling_convention = conventions[0] if conventions else "cdecl"
    return_type = re.sub(r"__(?:cdecl|stdcall|fastcall|vectorcall)\b", "", prefix)
    return AbiFunction(
        name,
        _normalize_cpp_type(return_type),
        calling_convention,
        _parse_cpp_parameters(parameters, name),
    )


def read_header_functions(source: str) -> dict[str, AbiFunction]:
    code = _mask_non_code(source)
    declarations = list(re.finditer(
        r'extern\s+"(?P<linkage>[^"]*)"\s+COLORVISIONCORE_API\s+'
        r"(?P<prefix>[^;()]+?)\s+(?P<name>[A-Za-z_]\w*)\s*"
        r"\((?P<parameters>[^;()]*)\)\s*;",
        code,
        flags=re.MULTILINE,
    ))
    extern_declarations = list(re.finditer(r'\bextern\s+"[^"]*"', code))
    if [match.start() for match in extern_declarations] != [
        match.start() for match in declarations
    ]:
        raise NativeContractError(
            "cuda_export.h may declare extern language-linkage functions only through "
            "COLORVISIONCORE_API."
        )
    functions: dict[str, AbiFunction] = {}
    for declaration in declarations:
        linkage = _original_group(source, declaration, "linkage")
        if linkage != "C":
            raise NativeContractError(f"CUDA export uses unsupported language linkage: {linkage!r}.")
        prefix = declaration.group("prefix")
        name = declaration.group("name")
        parameters = declaration.group("parameters")
        if name in functions:
            raise NativeContractError(f"cuda_export.h contains duplicate export declaration: {name}.")
        functions[name] = _parse_cpp_function(name, prefix, parameters)
    if not functions:
        raise NativeContractError("No CUDA exports were found in cuda_export.h.")
    macro_uses = re.findall(r"\bCOLORVISIONCORE_API\b", code)
    if len(macro_uses) != len(functions) + 2:
        raise NativeContractError(
            "cuda_export.h contains an unparsed COLORVISIONCORE_API export declaration."
        )
    if code.count(";") != len(functions) + 1 or "{" in code or "}" in code:
        raise NativeContractError(
            "cuda_export.h contains an unparsed declaration or inline export definition."
        )
    return functions


def validate_windows_export_macro(source: str) -> None:
    _reject_contract_token_aliases(source, "cuda_export.h")
    expected_all_directives = [
        ("pragma", "once"),
        ("include", "<string>"),
        ("include", "<opencv2/opencv.hpp>"),
        ("include", '"custom_structs.h"'),
        ("ifdef", "OPENCVCUDA_EXPORTS"),
        ("define", "COLORVISIONCORE_API __declspec(dllexport)"),
        ("else", ""),
        ("define", "COLORVISIONCORE_API __declspec(dllimport)"),
        ("endif", ""),
    ]
    code = _mask_non_code(source)
    directives = []
    for match in re.finditer(
        r"^\s*#\s*(?P<name>if|ifdef|ifndef|elif|else|endif|define|undef)\b(?P<body>[^\r\n]*)$",
        code,
        flags=re.MULTILINE | re.IGNORECASE,
    ):
        directives.append(
            (
                match.group("name").casefold(),
                re.sub(r"\s+", " ", match.group("body").strip()),
            )
        )
    expected = [
        ("ifdef", "OPENCVCUDA_EXPORTS"),
        ("define", "COLORVISIONCORE_API __declspec(dllexport)"),
        ("else", ""),
        ("define", "COLORVISIONCORE_API __declspec(dllimport)"),
        ("endif", ""),
    ]
    if directives != expected:
        raise NativeContractError(
            "cuda_export.h must contain only the exact OPENCVCUDA_EXPORTS "
            f"dllexport/dllimport branch; found={directives!r}."
        )
    all_directives = _read_source_directives(source)
    if all_directives != expected_all_directives:
        raise NativeContractError(
            f"cuda_export.h preprocessor/include contract drift: found={all_directives!r}."
        )
    if len(re.findall(r"__declspec\s*\(\s*dllexport\s*\)", code, re.IGNORECASE)) != 1:
        raise NativeContractError(
            "cuda_export.h must use __declspec(dllexport) only in COLORVISIONCORE_API."
        )
    if len(re.findall(r"__declspec\s*\(\s*dllimport\s*\)", code, re.IGNORECASE)) != 1:
        raise NativeContractError(
            "cuda_export.h must use __declspec(dllimport) only in COLORVISIONCORE_API."
        )


def read_header_callback(source: str) -> AbiFunction:
    source = _mask_non_code(source)
    matches = re.findall(
        r"typedef\s+(?P<return>[^;()]+?)\s*\(\s*"
        r"(?P<convention>__(?:cdecl|stdcall|fastcall|vectorcall))\s*\*\s*"
        r"(?P<name>[A-Za-z_]\w*)\s*\)\s*"
        r"\((?P<parameters>[^;()]*)\)\s*;",
        source,
        flags=re.MULTILINE,
    )
    callbacks = [item for item in matches if item[2] == "CVNativeLogCallback"]
    if len(callbacks) != 1:
        raise NativeContractError(
            "cuda_export.h must contain exactly one CVNativeLogCallback typedef."
        )
    return_type, convention, name, parameters = callbacks[0]
    return _parse_cpp_function(name, f"{return_type} {convention}", parameters)


def read_header_exports(source: str) -> frozenset[str]:
    return frozenset(read_header_functions(source))


def _normalize_cs_attribute(attribute: str) -> str:
    return re.sub(r"\s+", "", attribute.strip())


def _parse_cs_parameters(value: str, context: str) -> tuple[AbiParameter, ...]:
    if not value.strip():
        return ()
    parameters: list[AbiParameter] = []
    for declaration in _split_top_level(value):
        remainder = declaration.strip()
        attributes: list[str] = []
        while remainder.startswith("["):
            closing = remainder.find("]")
            if closing < 0:
                raise NativeContractError(f"Unterminated C# parameter attribute in {context}.")
            attributes.append(_normalize_cs_attribute(remainder[1:closing]))
            remainder = remainder[closing + 1:].strip()
        match = re.fullmatch(
            r"(?:(?P<modifier>out|ref|in)\s+)?"
            r"(?P<type>[A-Za-z_]\w*(?:\[\])?)\s+"
            r"(?P<name>[A-Za-z_]\w*)",
            remainder,
        )
        if not match:
            raise NativeContractError(f"Could not parse C# ABI parameter {declaration!r} in {context}.")
        parameters.append(
            AbiParameter(
                match.group("type"),
                match.group("name"),
                match.group("modifier") or "",
                tuple(attributes),
            )
        )
    return tuple(parameters)


def _read_const_string(source: str, code: str, name: str, context: str) -> str:
    matches = list(re.finditer(
        rf"\bconst\s+string\s+{re.escape(name)}\s*=\s*\"(?P<value>[^\"]*)\"\s*;",
        code,
    ))
    if len(matches) != 1:
        raise NativeContractError(f"{context} must declare exactly one const string {name}.")
    value = _original_group(source, matches[0], "value")
    if "\\" in value or "\r" in value or "\n" in value:
        raise NativeContractError(f"{context}.{name} must use a simple literal value.")
    return value


def _read_dllimport_named_arguments(arguments: str, method_name: str) -> dict[str, str]:
    parts = _split_top_level(arguments)
    if not parts or parts[0] != "LibPath":
        raise NativeContractError(f"CUDA DllImport {method_name} must reference OpenCVCuda.LibPath.")

    named_arguments: dict[str, str] = {}
    for part in parts[1:]:
        match = re.fullmatch(
            r"(?P<name>[A-Za-z_]\w*)\s*=\s*(?P<value>.+)",
            part,
            flags=re.DOTALL,
        )
        if not match:
            raise NativeContractError(
                f"CUDA DllImport {method_name} contains an unsupported argument: {part!r}."
            )
        name = match.group("name")
        if name not in EXPECTED_DLLIMPORT_NAMED_ARGUMENTS:
            raise NativeContractError(
                f"CUDA DllImport {method_name} contains an unsupported named argument: {name}."
            )
        if name in named_arguments:
            raise NativeContractError(
                f"CUDA DllImport {method_name} contains a duplicate named argument: {name}."
            )
        named_arguments[name] = match.group("value").strip()

    if set(named_arguments) != EXPECTED_DLLIMPORT_NAMED_ARGUMENTS:
        raise NativeContractError(
            f"CUDA DllImport {method_name} named arguments drifted: "
            f"expected={sorted(EXPECTED_DLLIMPORT_NAMED_ARGUMENTS)!r}, "
            f"found={sorted(named_arguments)!r}."
        )
    return named_arguments


def read_managed_import_contract(source: str) -> tuple[str, dict[str, AbiFunction]]:
    _reject_contract_preprocessor_mutation(source, "OpenCVCuda")
    _reject_csharp_type_aliases(source, "OpenCVCuda")
    _reject_csharp_module_attributes(source, "OpenCVCuda")
    code = _mask_non_code(source)
    if re.search(r"\bLibraryImport(?:Attribute)?\b", code):
        raise NativeContractError(
            "OpenCVCuda supports only the reviewed DllImport declarations, not LibraryImport."
        )
    library_name = _read_const_string(source, code, "LibPath", "OpenCVCuda")
    declarations = list(re.finditer(
        r"\[DllImport\((?P<arguments>.*?)\)\]\s*"
        r"private\s+static\s+extern\s+"
        r"(?P<return>[A-Za-z_]\w*(?:\[\])?)\s+"
        r"(?P<method>[A-Za-z_]\w*)\s*"
        r"\((?P<parameters>[^;()]*)\)\s*;",
        code,
        flags=re.DOTALL,
    ))
    import_attributes = re.findall(r"\[DllImport\((.*?)\)\]", code, flags=re.DOTALL)
    if not declarations or len(declarations) != len(import_attributes):
        raise NativeContractError("Could not pair every CUDA DllImport attribute with its declaration.")
    extern_tokens = re.findall(r"\bextern\b", code)
    dllimport_tokens = re.findall(r"\bDllImport(?:Attribute)?\b", code)
    if len(extern_tokens) != len(declarations) or len(dllimport_tokens) != len(declarations):
        raise NativeContractError(
            "OpenCVCuda contains an unparsed extern method or DllImport attribute spelling."
        )

    functions: dict[str, AbiFunction] = {}
    for declaration in declarations:
        arguments = _original_group(source, declaration, "arguments")
        return_type = declaration.group("return")
        method_name = declaration.group("method")
        parameters = declaration.group("parameters")
        named_arguments = _read_dllimport_named_arguments(arguments, method_name)
        if named_arguments["CallingConvention"] != "CallingConvention.Cdecl":
            raise NativeContractError(f"CUDA DllImport {method_name} is not declared Cdecl.")
        if named_arguments["CharSet"] != "CharSet.Ansi":
            raise NativeContractError(
                f"CUDA DllImport {method_name} must declare exactly CharSet.Ansi."
            )
        entry_point_match = re.fullmatch(
            r'"(?P<value>[A-Za-z_]\w*)"', named_arguments["EntryPoint"]
        )
        if not entry_point_match:
            raise NativeContractError(
                f"CUDA DllImport {method_name} has an invalid EntryPoint literal."
            )
        export_name = entry_point_match.group("value")
        if export_name in functions:
            raise NativeContractError(f"Duplicate CUDA DllImport entry point: {export_name}.")
        functions[export_name] = AbiFunction(
            export_name,
            return_type,
            "cdecl",
            _parse_cs_parameters(parameters, export_name),
        )
    return library_name, functions


def read_managed_dllimports(source: str) -> frozenset[str]:
    _, functions = read_managed_import_contract(source)
    return frozenset(functions)


def read_native_log_bridge_contract(
    source: str,
) -> tuple[str, dict[str, AbiFunction], dict[str, str]]:
    _reject_contract_preprocessor_mutation(source, "NativeLogBridge")
    _reject_csharp_type_aliases(source, "NativeLogBridge")
    code = _mask_non_code(source)
    library_name = _read_const_string(source, code, "CudaLib", "NativeLogBridge")
    delegate_matches = re.findall(
        r"\[UnmanagedFunctionPointer\(\s*CallingConvention\.([A-Za-z_]+)\s*\)\]\s*"
        r"(?:public|private)\s+delegate\s+"
        r"(?P<return>[A-Za-z_]\w*)\s+(?P<name>[A-Za-z_]\w*)\s*"
        r"\((?P<parameters>[^;()]*)\)\s*;",
        code,
        flags=re.DOTALL,
    )
    delegate_tokens = re.findall(r"\bdelegate\b", code)
    unmanaged_attribute_tokens = re.findall(
        r"\bUnmanagedFunctionPointer(?:Attribute)?\b", code
    )
    if (
        len(delegate_tokens) != len(delegate_matches)
        or len(unmanaged_attribute_tokens) != len(delegate_matches)
    ):
        raise NativeContractError(
            "NativeLogBridge contains an unparsed native callback delegate contract."
        )
    delegates: dict[str, AbiFunction] = {}
    for convention, return_type, name, parameters in delegate_matches:
        if name in delegates:
            raise NativeContractError(f"Duplicate NativeLogBridge delegate declaration: {name}.")
        delegates[name] = AbiFunction(
            name,
            return_type,
            convention.casefold(),
            _parse_cs_parameters(parameters, name),
        )

    binding_matches = re.finditer(
        r"GetExport<(?P<delegate>[A-Za-z_]\w*)>\(\s*module\s*,\s*"
        r'\$"(?P<literal>[^"]*)"\s*\)',
        code,
    )
    bindings: dict[str, str] = {}
    for binding in binding_matches:
        delegate_name = binding.group("delegate")
        literal = _original_group(source, binding, "literal")
        literal_match = re.fullmatch(r"\{exportPrefix\}(?P<suffix>[A-Za-z_]\w*)", literal)
        if not literal_match:
            raise NativeContractError(
                f"NativeLogBridge contains an invalid dynamic export literal: {literal!r}."
            )
        suffix = literal_match.group("suffix")
        if suffix in bindings:
            raise NativeContractError(f"Duplicate NativeLogBridge export binding: {suffix}.")
        bindings[suffix] = delegate_name

    prefix_match = re.search(
        r"return\s+source\s*==\s*NativeLogSource\.OpencvCuda\s*"
        r'\?\s*"(?P<cuda>[^"]*)"\s*:\s*"(?P<helper>[^"]*)"\s*;',
        code,
    )
    if not prefix_match or (
        _original_group(source, prefix_match, "cuda"),
        _original_group(source, prefix_match, "helper"),
    ) != ("CM_", "M_"):
        raise NativeContractError("NativeLogBridge must map OpencvCuda exports to the CM_ prefix.")
    return library_name, delegates, bindings


def _extract_braced_region(source: str, opening_brace: int, context: str) -> tuple[str, int]:
    depth = 0
    for index in range(opening_brace, len(source)):
        character = source[index]
        if character == "{":
            depth += 1
        elif character == "}":
            depth -= 1
            if depth == 0:
                return source[opening_brace + 1:index], index
    raise NativeContractError(f"Unterminated braced declaration for {context}.")


def _top_level_lines(body: str):
    depth = 0
    for line in body.splitlines():
        stripped = line.strip()
        if depth == 0:
            yield stripped
        depth += line.count("{") - line.count("}")
        if depth < 0:
            raise NativeContractError("Unexpected closing brace while parsing ABI structure.")
    if depth != 0:
        raise NativeContractError("Unbalanced nested braces while parsing ABI structure.")


def _native_pack_state(source: str) -> tuple[int, tuple[int, ...]]:
    current = 0
    stack: list[int] = []
    for match in re.finditer(
        r"^\s*#\s*pragma\s+pack\s*\((?P<arguments>[^)]*)\)",
        source,
        flags=re.MULTILINE,
    ):
        parts = [part.strip() for part in match.group("arguments").split(",") if part.strip()]
        if not parts:
            current = 0
        elif parts[0] == "push" and len(parts) in {1, 2}:
            stack.append(current)
            if len(parts) == 2 and parts[1].isdigit():
                current = int(parts[1])
            elif len(parts) == 2:
                raise NativeContractError(f"Unsupported #pragma pack value: {match.group(0)!r}.")
        elif parts == ["pop"]:
            if not stack:
                raise NativeContractError("custom_structs.h contains an unmatched #pragma pack(pop).")
            current = stack.pop()
        elif len(parts) == 1 and parts[0].isdigit():
            current = int(parts[0])
        else:
            raise NativeContractError(f"Unsupported #pragma pack form: {match.group(0)!r}.")
    return current, tuple(stack)


def _calculate_layout(
    name: str,
    pack: int,
    fields: tuple[AbiField, ...],
    type_layouts: dict[str, tuple[int, int]],
) -> AbiStructLayout:
    effective_pack = pack or DEFAULT_WINDOWS_X64_PACK
    offset = 0
    offsets: list[int] = []
    struct_alignment = 1
    for field in fields:
        if field.type_name not in type_layouts:
            raise NativeContractError(f"Unsupported {name} ABI field type: {field.type_name}.")
        size, natural_alignment = type_layouts[field.type_name]
        alignment = min(natural_alignment, effective_pack)
        offset = (offset + alignment - 1) // alignment * alignment
        offsets.append(offset)
        offset += size
        struct_alignment = max(struct_alignment, alignment)
    size = (offset + struct_alignment - 1) // struct_alignment * struct_alignment
    return AbiStructLayout(name, pack, fields, tuple(offsets), size)


def read_native_himage_layout(source: str) -> AbiStructLayout:
    _reject_contract_preprocessor_mutation(source, "custom_structs.h")
    _validate_custom_struct_directives(source)
    source = _mask_non_code(source)
    if _native_pack_state(source) != (0, ()):
        raise NativeContractError(
            "custom_structs.h must restore the default pack state at end of file."
        )
    matches = list(re.finditer(r"typedef\s+struct\s+HImage(?P<suffix>[^\{;]*)\{", source))
    if len(matches) != 1:
        raise NativeContractError("custom_structs.h does not declare typedef struct HImage.")
    match = matches[0]
    if match.group("suffix").strip():
        raise NativeContractError(
            "Native HImage must not use inheritance or declaration modifiers."
        )

    pack_match = re.search(
        r"#\s*pragma\s+pack\s*\(\s*push\s*,\s*(?P<pack>\d+)\s*\)\s*$",
        source[:match.start()],
    )
    if not pack_match:
        raise NativeContractError(
            "Native HImage must be immediately preceded by #pragma pack(push, 8)."
        )
    pack = int(pack_match.group("pack"))
    if _native_pack_state(source[:pack_match.start()]) != (0, ()):
        raise NativeContractError(
            "Native HImage pack scope must start from the balanced Windows default state."
        )

    opening_brace = source.find("{", match.start())
    body, closing_brace = _extract_braced_region(source, opening_brace, "native HImage")
    trailer = re.match(
        r"\s*HImage\s*;\s*#\s*pragma\s+pack\s*\(\s*pop\s*\)",
        source[closing_brace + 1:],
    )
    if not trailer:
        raise NativeContractError(
            "Native HImage must be immediately followed by a matching #pragma pack(pop)."
        )

    fields: list[AbiField] = []
    method_names: set[str] = set()
    awaiting_method_body = False
    for line in _top_level_lines(body):
        if not line or line.startswith("//"):
            continue
        if line == "{":
            if not awaiting_method_body:
                raise NativeContractError(
                    "Native HImage contains an unexpected top-level method body."
                )
            awaiting_method_body = False
            continue
        method_match = re.fullmatch(r"int\s+(type|elemSize)\s*\(\s*\)\s+const", line)
        if method_match:
            method_name = method_match.group(1)
            if awaiting_method_body or method_name in method_names:
                raise NativeContractError(
                    f"Native HImage contains a duplicate or unterminated {method_name} method."
                )
            method_names.add(method_name)
            awaiting_method_body = True
            continue
        field_match = re.fullmatch(
            r"(?P<type>(?:unsigned\s+char|int|bool)\s*\*?)\s*"
            r"(?P<name>[A-Za-z_]\w*)(?:\s*=\s*[^;]+)?\s*;"
            r"(?:\s*//.*)?",
            line,
        )
        if field_match:
            if awaiting_method_body:
                raise NativeContractError("Native HImage method declaration is missing its body.")
            field = AbiField(
                _normalize_cpp_type(field_match.group("type")), field_match.group("name")
            )
            field_index = len(fields)
            if (
                field_index >= len(EXPECTED_NATIVE_HIMAGE_FIELDS)
                or field != EXPECTED_NATIVE_HIMAGE_FIELDS[field_index]
            ):
                raise NativeContractError(
                    f"Native HImage contains an unknown or reordered instance field: {field!r}."
                )
            fields.append(field)
            continue
        raise NativeContractError(
            f"Native HImage contains unsupported top-level declaration: {line!r}."
        )
    if awaiting_method_body:
        raise NativeContractError("Native HImage method declaration is missing its body.")
    return _calculate_layout(
        "native HImage",
        pack,
        tuple(fields),
        {"int": (4, 4), "bool": (1, 1), "unsigned char*": (8, 8)},
    )


def read_managed_himage_layout(source: str) -> AbiStructLayout:
    _reject_contract_preprocessor_mutation(source, "HImage.cs")
    _reject_csharp_type_aliases(source, "HImage.cs")
    source = _mask_non_code(source)
    matches = list(re.finditer(
        r"\[StructLayout\((?P<layout>[^]]+)\)\]\s*"
        r"public\s+struct\s+HImage(?P<suffix>[^\{]*)\{",
        source,
        flags=re.DOTALL,
    ))
    if len(matches) != 1:
        raise NativeContractError("HImage.cs does not declare a StructLayout for HImage.")
    match = matches[0]
    if re.sub(r"\s+", "", match.group("suffix")) != ":IDisposable":
        raise NativeContractError(
            "Managed HImage must implement only IDisposable and must not use declaration modifiers."
        )
    layout_parts = _split_top_level(match.group("layout"))
    if not layout_parts or layout_parts[0].strip() != "LayoutKind.Sequential":
        raise NativeContractError("Managed HImage must use LayoutKind.Sequential.")
    pack = 0
    pack_seen = False
    for part in layout_parts[1:]:
        pack_match = re.fullmatch(r"Pack\s*=\s*(\d+)", part)
        if pack_match:
            if pack_seen:
                raise NativeContractError("Managed HImage StructLayout must declare Pack exactly once.")
            pack_seen = True
            pack = int(pack_match.group(1))
        else:
            raise NativeContractError(f"Unsupported managed HImage StructLayout option: {part!r}.")

    opening_brace = source.find("{", match.start())
    body, _ = _extract_braced_region(source, opening_brace, "managed HImage")
    fields: list[AbiField] = []
    pending_attributes: list[str] = []
    dispose_seen = False
    awaiting_method_body = False
    for line in _top_level_lines(body):
        if not line or line.startswith("//"):
            continue
        if line == "{":
            if not awaiting_method_body:
                raise NativeContractError(
                    "Managed HImage contains an unexpected top-level method body."
                )
            awaiting_method_body = False
            continue
        if re.fullmatch(r"public\s+void\s+Dispose\s*\(\s*\)", line):
            if pending_attributes or awaiting_method_body or dispose_seen:
                raise NativeContractError("Managed HImage contains an invalid Dispose declaration.")
            dispose_seen = True
            awaiting_method_body = True
            continue
        attribute_match = re.fullmatch(r"\[(.+)\]", line)
        if attribute_match:
            if awaiting_method_body:
                raise NativeContractError("Managed HImage Dispose declaration is missing its body.")
            pending_attributes.append(_normalize_cs_attribute(attribute_match.group(1)))
            continue
        field_match = re.fullmatch(
            r"public\s+(?P<type>int|bool|IntPtr)\s+(?P<name>[A-Za-z_]\w*)\s*;"
            r"(?:\s*//.*)?",
            line,
        )
        if field_match:
            if awaiting_method_body:
                raise NativeContractError("Managed HImage Dispose declaration is missing its body.")
            field = AbiField(
                field_match.group("type"),
                field_match.group("name"),
                tuple(pending_attributes),
            )
            field_index = len(fields)
            if (
                field_index >= len(EXPECTED_MANAGED_HIMAGE_FIELDS)
                or field != EXPECTED_MANAGED_HIMAGE_FIELDS[field_index]
            ):
                raise NativeContractError(
                    f"Managed HImage contains an unknown or reordered instance field: {field!r}."
                )
            fields.append(field)
            pending_attributes.clear()
            continue
        raise NativeContractError(
            f"Managed HImage contains unsupported top-level declaration: {line!r}."
        )
    if pending_attributes:
        raise NativeContractError("Managed HImage contains an attribute without a field.")
    if awaiting_method_body:
        raise NativeContractError("Managed HImage Dispose declaration is missing its body.")
    return _calculate_layout(
        "managed HImage",
        pack,
        tuple(fields),
        {"int": (4, 4), "bool": (1, 1), "IntPtr": (8, 8)},
    )


def validate_cuda_source_contracts(
    header_source: str,
    managed_source: str,
    native_struct_source: str,
    managed_struct_source: str,
    log_bridge_source: str,
) -> tuple[dict[str, AbiFunction], AbiStructLayout]:
    validate_windows_export_macro(header_source)
    header_functions = read_header_functions(header_source)
    if header_functions != EXPECTED_HEADER_FUNCTIONS:
        raise NativeContractError(
            f"cuda_export.h function signature drift: expected={EXPECTED_HEADER_FUNCTIONS!r}, "
            f"found={header_functions!r}."
        )
    header_callback = read_header_callback(header_source)
    if header_callback != EXPECTED_NATIVE_LOG_CALLBACK:
        raise NativeContractError(
            f"CVNativeLogCallback signature drift: expected={EXPECTED_NATIVE_LOG_CALLBACK!r}, "
            f"found={header_callback!r}."
        )

    managed_library, managed_functions = read_managed_import_contract(managed_source)
    if managed_library.casefold() != "opencv_cuda.dll":
        raise NativeContractError(
            f"OpenCVCuda.LibPath drift: expected 'opencv_cuda.dll', found {managed_library!r}."
        )
    if managed_functions != EXPECTED_MANAGED_IMPORTS:
        raise NativeContractError(
            f"OpenCVCuda DllImport signature drift: expected={EXPECTED_MANAGED_IMPORTS!r}, "
            f"found={managed_functions!r}."
        )

    native_layout = read_native_himage_layout(native_struct_source)
    managed_layout = read_managed_himage_layout(managed_struct_source)
    if native_layout.pack != 8 or native_layout.fields != EXPECTED_NATIVE_HIMAGE_FIELDS:
        raise NativeContractError(f"Native HImage layout/pack drift: found={native_layout!r}.")
    if managed_layout.pack != 8 or managed_layout.fields != EXPECTED_MANAGED_HIMAGE_FIELDS:
        raise NativeContractError(f"Managed HImage layout/pack drift: found={managed_layout!r}.")
    if (
        native_layout.offsets != EXPECTED_HIMAGE_OFFSETS
        or managed_layout.offsets != EXPECTED_HIMAGE_OFFSETS
        or native_layout.size != EXPECTED_HIMAGE_SIZE
        or managed_layout.size != EXPECTED_HIMAGE_SIZE
    ):
        raise NativeContractError(
            f"HImage x64 layout drift: native={native_layout!r}, managed={managed_layout!r}."
        )

    log_library, delegates, bindings = read_native_log_bridge_contract(log_bridge_source)
    if log_library.casefold() != "opencv_cuda.dll":
        raise NativeContractError(
            f"NativeLogBridge.CudaLib drift: expected 'opencv_cuda.dll', found {log_library!r}."
        )
    if delegates != EXPECTED_LOG_DELEGATES:
        raise NativeContractError(
            f"NativeLogBridge delegate ABI drift: expected={EXPECTED_LOG_DELEGATES!r}, "
            f"found={delegates!r}."
        )
    expected_bindings = {
        "SetLogCallback": "SetCallbackDelegate",
        "SetLogEnabled": "SetIntDelegate",
        "SetLogLevel": "SetIntDelegate",
        "EnableNativeSink": "SetIntDelegate",
    }
    if bindings != expected_bindings:
        raise NativeContractError(
            f"NativeLogBridge dynamic export binding drift: expected={expected_bindings!r}, "
            f"found={bindings!r}."
        )
    return header_functions, native_layout


def validate_cuda_package_binding(project_path: Path) -> None:
    try:
        root = ElementTree.parse(project_path).getroot()
    except (ElementTree.ParseError, OSError) as exc:
        raise NativeContractError(f"Could not read CUDA package binding: {project_path}: {exc}") from exc

    matches = []
    for item in root.iter("Content"):
        include = (item.attrib.get("Include") or "").replace("\\", "/").casefold()
        if include == "../../x64/release/opencv_cuda.dll":
            matches.append(item)
    if len(matches) != 1:
        raise NativeContractError(
            "ColorVision.Core must contain exactly one x64/Release/opencv_cuda.dll package item."
        )

    item = matches[0]
    if (item.attrib.get("Condition") or "").strip():
        raise NativeContractError("opencv_cuda.dll packaging must not be conditional; omission must fail the build.")
    metadata = {child.tag: (child.text or "").strip() for child in item}
    if metadata.get("Pack", "").casefold() != "true":
        raise NativeContractError("opencv_cuda.dll package item must set Pack=true.")
    if metadata.get("CopyToOutputDirectory", "").casefold() not in {"always", "preservenewest"}:
        raise NativeContractError("opencv_cuda.dll package item must copy to the runtime output.")
    package_path = metadata.get("PackagePath", "").replace("\\", "/").strip("/")
    link_path = (item.attrib.get("Link") or "").replace("\\", "/").strip("/")
    if package_path.casefold() != "runtimes/win-x64/native":
        raise NativeContractError(f"Unexpected opencv_cuda.dll PackagePath: {package_path!r}.")
    if link_path.casefold() != CUDA_PACKAGE_MEMBER.casefold():
        raise NativeContractError(f"Unexpected opencv_cuda.dll Link path: {link_path!r}.")


def _resolve_vs_msbuild_path() -> Path:
    configured_path = os.environ.get("COLORVISION_MSBUILD_PATH")
    if configured_path:
        candidate = Path(configured_path)
        if candidate.is_file():
            return candidate
        raise NativeContractError(
            f"Configured Visual Studio MSBuild does not exist: {candidate}."
        )

    discovered_path = shutil.which("MSBuild.exe") or shutil.which("msbuild")
    if discovered_path:
        return Path(discovered_path)
    for candidate in DEFAULT_MSBUILD_CANDIDATES:
        if candidate.is_file():
            return candidate
    raise NativeContractError(
        "Visual Studio MSBuild is required to evaluate the Release|x64 CUDA build contract. "
        "dotnet msbuild cannot evaluate this vcxproj."
    )


def _read_evaluated_cuda_build_items(
    project_path: Path,
    *,
    msbuild_path: str | Path | None = None,
) -> dict[str, list[dict[str, str]]]:
    project_path = project_path.resolve()
    executable = Path(msbuild_path) if msbuild_path is not None else _resolve_vs_msbuild_path()
    token = uuid.uuid4().hex
    cuda_capture_item = f"_ColorVisionCudaContract_{token}"
    cl_capture_item = f"_ColorVisionClContract_{token}"
    cuda_capture_target = f"ColorVisionCaptureCudaContract_{token}"
    cl_capture_target = f"ColorVisionCaptureClContract_{token}"
    shadow_path: Path | None = None
    probe_path: Path | None = None
    try:
        project_source = project_path.read_text(encoding="utf-8-sig")
        if project_source.count("</Project>") != 1:
            raise NativeContractError(
                "opencv_cuda.vcxproj must contain exactly one closing Project element "
                "for evaluated contract probing."
            )

        shadow_descriptor, shadow_name = tempfile.mkstemp(
            prefix=f".{project_path.stem}.contract-",
            suffix=project_path.suffix,
            dir=project_path.parent,
        )
        os.close(shadow_descriptor)
        shadow_path = Path(shadow_name)
        probe_descriptor, probe_name = tempfile.mkstemp(
            prefix=f".{project_path.stem}.contract-",
            suffix=".targets",
            dir=project_path.parent,
        )
        os.close(probe_descriptor)
        probe_path = Path(probe_name)

        probe_path.write_text(
            f'''<Project xmlns="http://schemas.microsoft.com/developer/msbuild/2003">
  <PropertyGroup>
    <CudaCompileDependsOn>AddCudaCompileMetadata</CudaCompileDependsOn>
  </PropertyGroup>
  <Target Name="{cuda_capture_target}" BeforeTargets="CudaBuild">
    <ItemGroup>
      <{cuda_capture_item} Include="@(CudaCompile)" />
      <CudaCompile Remove="@(CudaCompile)" />
    </ItemGroup>
  </Target>
  <Target Name="{cl_capture_target}" BeforeTargets="ClCompile">
    <ItemGroup>
      <{cl_capture_item} Include="@(ClCompile)" />
      <ClCompile Remove="@(ClCompile)" />
    </ItemGroup>
  </Target>
  <Target Name="ClCompile" />
</Project>
''',
            encoding="utf-8",
        )
        shadow_source = project_source.replace(
            "</Project>",
            f'  <Import Project="{probe_path.name}" />\n</Project>',
            1,
        )
        shadow_path.write_text(shadow_source, encoding="utf-8")

        with tempfile.TemporaryDirectory(prefix="colorvision-cuda-contract-") as output_directory:
            output_root = Path(output_directory)
            command = [
                str(executable),
                str(shadow_path),
                "-nologo",
                "-p:Configuration=Release",
                "-p:Platform=x64",
                f"-p:IntDir={output_root / 'obj'}{os.sep}",
                f"-p:OutDir={output_root / 'out'}{os.sep}",
                "-t:CudaBuild;ClCompile",
                "-getItem:_CudaCompileHostDefinition",
                f"-getItem:{cl_capture_item}",
                f"-getItem:{cuda_capture_item}",
            ]
            try:
                result = subprocess.run(
                    command,
                    cwd=project_path.parent,
                    capture_output=True,
                    text=True,
                    errors="replace",
                    check=False,
                )
            except OSError as exc:
                raise NativeContractError(
                    f"Could not run Visual Studio MSBuild for CUDA contract evaluation: {exc}"
                ) from exc
        if result.returncode != 0:
            diagnostic = (result.stderr or result.stdout).strip()
            raise NativeContractError(
                "Visual Studio MSBuild could not evaluate the Release|x64 CUDA contract"
                + (f": {diagnostic}" if diagnostic else ".")
            )
        payload = json.loads(result.stdout)
        items = payload["Items"]
        if not isinstance(items, dict):
            raise NativeContractError(
                "Visual Studio MSBuild evaluated CUDA contract payload does not contain an item map."
            )
        result_items: dict[str, list[dict[str, str]]] = {}
        for result_name, item_type in (
            ("_CudaCompileHostDefinition", "_CudaCompileHostDefinition"),
            ("ClCompile", cl_capture_item),
            ("CudaCompile", cuda_capture_item),
        ):
            values = items.get(item_type)
            if not isinstance(values, list) or any(not isinstance(value, dict) for value in values):
                raise NativeContractError(
                    f"Visual Studio MSBuild did not return a valid {result_name} item list."
                )
            result_items[result_name] = values
        return result_items
    except (json.JSONDecodeError, KeyError, TypeError) as exc:
        raise NativeContractError(
            "Visual Studio MSBuild returned an invalid evaluated CUDA contract payload."
        ) from exc
    except OSError as exc:
        raise NativeContractError(
            f"Could not prepare the evaluated CUDA contract probe: {exc}"
        ) from exc
    finally:
        cleanup_errors: list[str] = []
        for temporary_path in (shadow_path, probe_path):
            if temporary_path is None:
                continue
            try:
                temporary_path.unlink(missing_ok=True)
            except OSError as exc:
                cleanup_errors.append(f"{temporary_path}: {exc}")
        if cleanup_errors:
            raise NativeContractError(
                "Could not remove evaluated CUDA contract probe files: "
                + "; ".join(cleanup_errors)
            )


def _validate_export_definition(metadata: dict[str, str], context: str, field: str) -> None:
    value = metadata.get(field)
    if not isinstance(value, str):
        raise NativeContractError(f"{context} is missing evaluated {field} metadata.")
    export_definitions = [
        token
        for token in (part.strip() for part in value.split(";"))
        if token.split("=", 1)[0].casefold() == "opencvcuda_exports"
    ]
    if export_definitions != ["OPENCVCUDA_EXPORTS"]:
        raise NativeContractError(
            f"{context} must contain exactly one evaluated OPENCVCUDA_EXPORTS definition; "
            f"found={export_definitions!r}."
        )


def _validate_evaluated_cuda_native_export_build(
    project_path: Path,
    *,
    msbuild_path: str | Path | None = None,
) -> None:
    evaluated = _read_evaluated_cuda_build_items(project_path, msbuild_path=msbuild_path)
    host_definitions = evaluated["_CudaCompileHostDefinition"]
    if len(host_definitions) != 1:
        raise NativeContractError(
            "Release|x64 CUDA host compilation must have exactly one evaluated ClCompile definition."
        )
    host_definition = host_definitions[0]
    if host_definition.get("CallingConvention") != "Cdecl":
        raise NativeContractError(
            "Release|x64 evaluated ClCompile CallingConvention must be Cdecl; "
            f"found={host_definition.get('CallingConvention')!r}."
        )
    if host_definition.get("StructMemberAlignment") != "Default":
        raise NativeContractError(
            "Release|x64 evaluated ClCompile StructMemberAlignment must be Default; "
            f"found={host_definition.get('StructMemberAlignment')!r}."
        )
    _validate_export_definition(
        host_definition,
        "Release|x64 evaluated ClCompile",
        "PreprocessorDefinitions",
    )

    expected_source = (project_path.parent / CUDA_EXPORT_SOURCE).resolve()
    if not expected_source.is_file():
        raise NativeContractError(f"CUDA export source does not exist: {expected_source}.")
    expected_source_key = os.path.normcase(str(expected_source))
    cuda_target_items = [
        item
        for item in evaluated["CudaCompile"]
        if os.path.normcase(str(Path(item.get("FullPath", "")).resolve())) == expected_source_key
    ]
    cl_target_items = [
        item
        for item in evaluated["ClCompile"]
        if os.path.normcase(str(Path(item.get("FullPath", "")).resolve())) == expected_source_key
    ]
    if len(cuda_target_items) != 1 or cl_target_items:
        raise NativeContractError(
            "cuda_export.cpp must be exactly one evaluated CudaCompile item and must not be ClCompile; "
            f"CudaCompile={len(cuda_target_items)}, ClCompile={len(cl_target_items)}."
        )
    cuda_target = cuda_target_items[0]
    if Path(cuda_target.get("Identity", "")).name.casefold() != CUDA_EXPORT_SOURCE.casefold():
        raise NativeContractError(
            f"Evaluated CUDA export target identity drifted: {cuda_target.get('Identity')!r}."
        )
    if cuda_target.get("ExcludedFromBuild", "").casefold() not in {"", "false"}:
        raise NativeContractError("cuda_export.cpp must not be excluded from the Release|x64 build.")
    if cuda_target.get("UseHostDefines", "").casefold() != "true":
        raise NativeContractError(
            "Release|x64 evaluated CudaCompile UseHostDefines must be true for cuda_export.cpp."
        )
    _validate_export_definition(
        cuda_target,
        "Release|x64 evaluated cuda_export.cpp CudaCompile",
        "Defines",
    )

    for item in evaluated["ClCompile"]:
        identity = item.get("Identity", "<unknown>")
        if item.get("CallingConvention") != "Cdecl":
            raise NativeContractError(
                f"Release|x64 evaluated ClCompile {identity} CallingConvention must be Cdecl."
            )
        if item.get("StructMemberAlignment") != "Default":
            raise NativeContractError(
                f"Release|x64 evaluated ClCompile {identity} StructMemberAlignment must be Default."
            )
        _validate_export_definition(
            item,
            f"Release|x64 evaluated ClCompile {identity}",
            "PreprocessorDefinitions",
        )


def validate_cuda_native_export_build(
    project_path: Path,
    *,
    msbuild_path: str | Path | None = None,
    require_evaluated: bool = True,
) -> None:
    try:
        root = ElementTree.parse(project_path).getroot()
    except (ElementTree.ParseError, OSError) as exc:
        raise NativeContractError(f"Could not read CUDA native project: {project_path}: {exc}") from exc

    all_definition_groups = [
        element
        for element in root.iter()
        if element.tag.rsplit("}", 1)[-1] == "ItemDefinitionGroup"
    ]
    direct_definition_groups = [
        element
        for element in root
        if element.tag.rsplit("}", 1)[-1] == "ItemDefinitionGroup"
    ]
    if len(all_definition_groups) != len(direct_definition_groups):
        raise NativeContractError(
            "opencv_cuda ItemDefinitionGroup contracts must be unconditional direct Project children."
        )

    allowed_definition_conditions = {
        "'$(configuration)|$(platform)'=='debug|win32'",
        "'$(configuration)|$(platform)'=='release|win32'",
        "'$(configuration)|$(platform)'=='debug|x64'",
        "'$(configuration)|$(platform)'=='release|x64'",
    }
    release_groups = []
    for element in direct_definition_groups:
        condition = re.sub(r"\s+", "", element.attrib.get("Condition") or "").casefold()
        compile_elements = [
            child for child in element
            if child.tag.rsplit("}", 1)[-1] == "ClCompile"
        ]
        definitions = [
            child
            for compile_element in compile_elements
            for child in compile_element
            if child.tag.rsplit("}", 1)[-1] == "PreprocessorDefinitions"
        ]
        if definitions:
            if condition not in allowed_definition_conditions:
                raise NativeContractError(
                    "opencv_cuda.vcxproj contains an unreviewed preprocessor-definition group: "
                    f"{element.attrib.get('Condition')!r}."
                )
            if len(compile_elements) != 1 or len(definitions) != 1:
                raise NativeContractError(
                    "Each opencv_cuda configuration must define preprocessor symbols once."
                )
            if (compile_elements[0].attrib.get("Condition") or "").strip() or (
                definitions[0].attrib.get("Condition") or ""
            ).strip():
                raise NativeContractError(
                    "opencv_cuda preprocessor definitions must be unconditional inside their configuration group."
                )
        if condition == "'$(configuration)|$(platform)'=='release|x64'":
            release_groups.append(element)
    if len(release_groups) != 1:
        raise NativeContractError(
            "opencv_cuda.vcxproj must define exactly one Release|x64 ItemDefinitionGroup."
        )

    release_compile_elements = [
        child for child in release_groups[0]
        if child.tag.rsplit("}", 1)[-1] == "ClCompile"
    ]
    if len(release_compile_elements) != 1:
        raise NativeContractError("opencv_cuda Release|x64 must define one ClCompile contract.")
    release_definitions = [
        child for child in release_compile_elements[0]
        if child.tag.rsplit("}", 1)[-1] == "PreprocessorDefinitions"
    ]
    if len(release_definitions) != 1:
        raise NativeContractError(
            "opencv_cuda Release|x64 must define PreprocessorDefinitions exactly once."
        )
    release_definitions_element = release_definitions[0]
    definitions = [
        token.strip()
        for token in (release_definitions_element.text or "").split(";")
        if token.strip()
    ]
    expected_release_definitions = [
        "NDEBUG",
        "OPENCVCUDA_EXPORTS",
        "_WINDOWS",
        "_USRDLL",
        "%(PreprocessorDefinitions)",
    ]
    if definitions != expected_release_definitions:
        raise NativeContractError(
            "opencv_cuda Release|x64 preprocessor definitions drifted: "
            f"expected={expected_release_definitions!r}, found={definitions!r}."
        )
    calling_convention_elements = [
        child
        for child in release_compile_elements[0]
        if child.tag.rsplit("}", 1)[-1] == "CallingConvention"
    ]
    if len(calling_convention_elements) > 1:
        raise NativeContractError(
            "opencv_cuda Release|x64 ClCompile must not duplicate CallingConvention."
        )
    calling_convention = (
        (calling_convention_elements[0].text or "").strip()
        if calling_convention_elements
        else None
    )
    if calling_convention and calling_convention != "Cdecl":
        raise NativeContractError(
            "opencv_cuda Release|x64 ClCompile CallingConvention must be Cdecl when explicit; "
            f"found={calling_convention!r}."
        )
    struct_alignment_elements = [
        child
        for child in release_compile_elements[0]
        if child.tag.rsplit("}", 1)[-1] == "StructMemberAlignment"
    ]
    if len(struct_alignment_elements) > 1:
        raise NativeContractError(
            "opencv_cuda Release|x64 ClCompile must not duplicate StructMemberAlignment."
        )
    struct_alignment = (
        (struct_alignment_elements[0].text or "").strip()
        if struct_alignment_elements
        else None
    )
    if struct_alignment and struct_alignment != "Default":
        raise NativeContractError(
            "opencv_cuda Release|x64 ClCompile StructMemberAlignment must be Default when explicit; "
            f"found={struct_alignment!r}."
        )

    release_cuda_elements = [
        child for child in release_groups[0]
        if child.tag.rsplit("}", 1)[-1] == "CudaCompile"
    ]
    if len(release_cuda_elements) != 1:
        raise NativeContractError("opencv_cuda Release|x64 must define one CudaCompile contract.")
    use_host_defines_elements = [
        child
        for child in release_cuda_elements[0]
        if child.tag.rsplit("}", 1)[-1] == "UseHostDefines"
    ]
    if len(use_host_defines_elements) > 1:
        raise NativeContractError(
            "opencv_cuda Release|x64 CudaCompile must not duplicate UseHostDefines."
        )
    use_host_defines = (
        (use_host_defines_elements[0].text or "").strip()
        if use_host_defines_elements
        else None
    )
    if use_host_defines and use_host_defines.casefold() != "true":
        raise NativeContractError(
            "opencv_cuda Release|x64 CudaCompile UseHostDefines must be true when explicit."
        )

    cuda_source_items = []
    cl_source_items = []
    for element in root.iter():
        item_type = element.tag.rsplit("}", 1)[-1]
        include = (element.attrib.get("Include") or "").replace("\\", "/")
        if Path(include).name.casefold() != CUDA_EXPORT_SOURCE.casefold():
            continue
        if item_type == "CudaCompile":
            cuda_source_items.append(element)
        elif item_type == "ClCompile":
            cl_source_items.append(element)
    if len(cuda_source_items) != 1 or cl_source_items:
        raise NativeContractError(
            "cuda_export.cpp must be exactly one CudaCompile item and must not be ClCompile; "
            f"CudaCompile={len(cuda_source_items)}, ClCompile={len(cl_source_items)}."
        )
    if (cuda_source_items[0].attrib.get("Condition") or "").strip():
        raise NativeContractError("cuda_export.cpp CudaCompile item must not be conditional.")
    source_path = project_path.parent / Path(
        (cuda_source_items[0].attrib.get("Include") or "").replace("\\", "/")
    )
    if not source_path.is_file():
        raise NativeContractError(f"CUDA export source does not exist: {source_path.resolve()}.")

    if require_evaluated:
        _validate_evaluated_cuda_native_export_build(project_path, msbuild_path=msbuild_path)


def _sha256(data: bytes) -> str:
    return hashlib.sha256(data).hexdigest().upper()


def _read_package_cuda_bytes(package_path: Path) -> bytes:
    try:
        with zipfile.ZipFile(package_path) as archive:
            matches = [
                name for name in archive.namelist()
                if name.replace("\\", "/").casefold() == CUDA_PACKAGE_MEMBER.casefold()
            ]
            if len(matches) != 1:
                raise NativeContractError(
                    f"{package_path} must contain exactly one {CUDA_PACKAGE_MEMBER}; found {len(matches)}."
                )
            return archive.read(matches[0])
    except (OSError, zipfile.BadZipFile, KeyError) as exc:
        raise NativeContractError(f"Could not read native package {package_path}: {exc}") from exc


def validate_cuda_export_sets(
    header_exports: frozenset[str],
    managed_exports: frozenset[str],
    binary_exports: frozenset[str],
) -> None:
    missing_dynamic_exports = CUDA_DYNAMIC_MANAGED_EXPORTS - header_exports
    if missing_dynamic_exports:
        raise NativeContractError(
            "CUDA dynamic managed export drift: "
            f"missing from header={sorted(missing_dynamic_exports)}."
        )

    expected_managed = header_exports - CUDA_DYNAMIC_MANAGED_EXPORTS
    if managed_exports != expected_managed:
        raise NativeContractError(
            "CUDA managed DllImport drift: "
            f"missing={sorted(expected_managed - managed_exports)}, "
            f"unexpected={sorted(managed_exports - expected_managed)}."
        )

    expected_binary = header_exports | CUDA_ALLOWED_BINARY_ONLY_EXPORTS
    if binary_exports != expected_binary:
        raise NativeContractError(
            "Tracked opencv_cuda.dll export drift: "
            f"missing={sorted(expected_binary - binary_exports)}, "
            f"unexpected={sorted(binary_exports - expected_binary)}."
        )


def validate_native_contracts(
    repository_root: str | Path,
    *,
    runtime_files: tuple[str | Path, ...] = (),
    package_files: tuple[str | Path, ...] = (),
    require_evaluated_native_build: bool = True,
) -> NativeContractReport:
    root = Path(repository_root).resolve()
    header_path = root / CUDA_HEADER
    native_struct_path = root / CUDA_NATIVE_STRUCTS
    managed_path = root / CUDA_MANAGED_WRAPPER
    managed_struct_path = root / CUDA_MANAGED_STRUCTS
    log_bridge_path = root / CUDA_NATIVE_LOG_BRIDGE
    project_path = root / CUDA_PROJECT
    native_project_path = root / CUDA_NATIVE_PROJECT
    tracked_path = root / CUDA_TRACKED_DLL
    validate_colorvision_core_module_attributes(project_path.parent)
    try:
        header_source = header_path.read_text(encoding="utf-8-sig")
        managed_source = managed_path.read_text(encoding="utf-8-sig")
        header_functions, himage_layout = validate_cuda_source_contracts(
            header_source,
            managed_source,
            native_struct_path.read_text(encoding="utf-8-sig"),
            managed_struct_path.read_text(encoding="utf-8-sig"),
            log_bridge_path.read_text(encoding="utf-8-sig"),
        )
        tracked_bytes = tracked_path.read_bytes()
    except OSError as exc:
        raise NativeContractError(f"Could not read CUDA contract input: {exc}") from exc

    header_exports = frozenset(header_functions)
    managed_exports = frozenset(read_managed_import_contract(managed_source)[1])
    machine, binary_exports = read_pe_exports(tracked_bytes)
    if machine != AMD64_MACHINE:
        raise NativeContractError(
            f"Tracked opencv_cuda.dll machine is 0x{machine:04X}; expected AMD64 (0x{AMD64_MACHINE:04X})."
        )
    validate_cuda_export_sets(header_exports, managed_exports, binary_exports)

    validate_cuda_package_binding(project_path)
    validate_cuda_native_export_build(
        native_project_path,
        require_evaluated=require_evaluated_native_build,
    )
    tracked_hash = _sha256(tracked_bytes)
    verified_runtime_files: list[Path] = []
    for runtime_file in runtime_files:
        runtime_path = Path(runtime_file).resolve()
        try:
            runtime_bytes = runtime_path.read_bytes()
        except OSError as exc:
            raise NativeContractError(f"Could not read CUDA runtime copy {runtime_path}: {exc}") from exc
        if runtime_bytes != tracked_bytes:
            raise NativeContractError(
                f"CUDA runtime copy differs from tracked DLL: {runtime_path} "
                f"(expected SHA256 {tracked_hash}, found {_sha256(runtime_bytes)})."
            )
        verified_runtime_files.append(runtime_path)

    verified_package_files: list[Path] = []
    for package_file in package_files:
        package_path = Path(package_file).resolve()
        package_bytes = _read_package_cuda_bytes(package_path)
        if package_bytes != tracked_bytes:
            raise NativeContractError(
                f"Packaged opencv_cuda.dll differs from tracked DLL: {package_path} "
                f"(expected SHA256 {tracked_hash}, found {_sha256(package_bytes)})."
            )
        package_machine, package_exports = read_pe_exports(package_bytes)
        if package_machine != machine or package_exports != binary_exports:
            raise NativeContractError(f"Packaged opencv_cuda.dll PE identity drifted: {package_path}.")
        verified_package_files.append(package_path)

    return NativeContractReport(
        tracked_dll=tracked_path,
        sha256=tracked_hash,
        size=len(tracked_bytes),
        exports=tuple(sorted(binary_exports)),
        abi_functions=tuple(sorted(header_functions)),
        himage_size=himage_layout.size,
        runtime_files=tuple(verified_runtime_files),
        package_files=tuple(verified_package_files),
    )


def parse_args() -> argparse.Namespace:
    parser = argparse.ArgumentParser(
        description="Verify the source, managed, tracked-PE, runtime, and package opencv_cuda contract."
    )
    parser.add_argument("--repository-root", type=Path, default=Path(__file__).resolve().parents[1])
    parser.add_argument("--runtime", action="append", type=Path, default=[])
    parser.add_argument("--package", action="append", type=Path, default=[])
    parser.add_argument(
        "--static-native-project-only",
        action="store_true",
        help=(
            "Run the portable XML/source layer without claiming VS/CUDA evaluated metadata proof. "
            "Release validation must not use this option."
        ),
    )
    return parser.parse_args()


def main() -> int:
    args = parse_args()
    packages = list(args.package)
    try:
        report = validate_native_contracts(
            args.repository_root,
            runtime_files=tuple(args.runtime),
            package_files=tuple(packages),
            require_evaluated_native_build=not args.static_native_project_only,
        )
    except NativeContractError as exc:
        print(f"Native contract verification failed: {exc}", file=sys.stderr)
        return 1

    print(f"Verified opencv_cuda.dll: {report.size} bytes, SHA256 {report.sha256}")
    print("PE machine: AMD64 (0x8664)")
    print("Named exports: " + ", ".join(report.exports))
    print(
        f"Static ABI: {len(report.abi_functions)} function signatures; "
        f"HImage AMD64 size {report.himage_size} bytes"
    )
    if args.static_native_project_only:
        print(
            "Portable native-project checks passed; Release|x64 evaluated MSBuild metadata "
            "was not verified."
        )
    for path in report.runtime_files:
        print(f"Verified runtime copy: {path}")
    for path in report.package_files:
        print(f"Verified package: {path}!/{CUDA_PACKAGE_MEMBER}")
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
