import argparse
import hashlib
import re
import struct
import sys
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
    declarations = re.findall(
        r'extern\s+"C"\s+COLORVISIONCORE_API\s+'
        r"(?P<prefix>[^;()]+?)\s+(?P<name>[A-Za-z_]\w*)\s*"
        r"\((?P<parameters>[^;()]*)\)\s*;",
        source,
        flags=re.MULTILINE,
    )
    if not declarations:
        raise NativeContractError("No CUDA exports were found in cuda_export.h.")
    functions: dict[str, AbiFunction] = {}
    for prefix, name, parameters in declarations:
        if name in functions:
            raise NativeContractError(f"cuda_export.h contains duplicate export declaration: {name}.")
        functions[name] = _parse_cpp_function(name, prefix, parameters)
    return functions


def read_header_callback(source: str) -> AbiFunction:
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


def _read_const_string(source: str, name: str, context: str) -> str:
    matches = re.findall(
        rf"\bconst\s+string\s+{re.escape(name)}\s*=\s*\"([^\"]+)\"\s*;",
        source,
    )
    if len(matches) != 1:
        raise NativeContractError(f"{context} must declare exactly one const string {name}.")
    return matches[0]


def read_managed_import_contract(source: str) -> tuple[str, dict[str, AbiFunction]]:
    library_name = _read_const_string(source, "LibPath", "OpenCVCuda")
    declarations = re.findall(
        r"\[DllImport\((?P<arguments>.*?)\)\]\s*"
        r"private\s+static\s+extern\s+"
        r"(?P<return>[A-Za-z_]\w*(?:\[\])?)\s+"
        r"(?P<method>[A-Za-z_]\w*)\s*"
        r"\((?P<parameters>[^;()]*)\)\s*;",
        source,
        flags=re.DOTALL,
    )
    import_attributes = re.findall(r"\[DllImport\((.*?)\)\]", source, flags=re.DOTALL)
    if not declarations or len(declarations) != len(import_attributes):
        raise NativeContractError("Could not pair every CUDA DllImport attribute with its declaration.")

    functions: dict[str, AbiFunction] = {}
    for arguments, return_type, method_name, parameters in declarations:
        argument_parts = _split_top_level(arguments)
        if not argument_parts or argument_parts[0].strip() != "LibPath":
            raise NativeContractError(f"CUDA DllImport {method_name} must reference OpenCVCuda.LibPath.")
        convention_match = re.search(
            r"\bCallingConvention\s*=\s*CallingConvention\.([A-Za-z_]+)", arguments
        )
        if not convention_match or convention_match.group(1) != "Cdecl":
            raise NativeContractError(f"CUDA DllImport {method_name} is not declared Cdecl.")
        charset_match = re.search(r"\bCharSet\s*=\s*CharSet\.([A-Za-z_]+)", arguments)
        if charset_match and charset_match.group(1) != "Ansi":
            raise NativeContractError(
                f"CUDA DllImport {method_name} must use ANSI string marshalling for const char*."
            )
        entry_point_match = re.search(r'\bEntryPoint\s*=\s*"([A-Za-z_]\w*)"', arguments)
        export_name = entry_point_match.group(1) if entry_point_match else method_name
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
    library_name = _read_const_string(source, "CudaLib", "NativeLogBridge")
    delegate_matches = re.findall(
        r"\[UnmanagedFunctionPointer\(\s*CallingConvention\.([A-Za-z_]+)\s*\)\]\s*"
        r"(?:public|private)\s+delegate\s+"
        r"(?P<return>[A-Za-z_]\w*)\s+(?P<name>[A-Za-z_]\w*)\s*"
        r"\((?P<parameters>[^;()]*)\)\s*;",
        source,
        flags=re.DOTALL,
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

    binding_matches = re.findall(
        r"GetExport<(?P<delegate>[A-Za-z_]\w*)>\(\s*module\s*,\s*"
        r'\$"\{exportPrefix\}(?P<suffix>[A-Za-z_]\w*)"\s*\)',
        source,
    )
    bindings: dict[str, str] = {}
    for delegate_name, suffix in binding_matches:
        if suffix in bindings:
            raise NativeContractError(f"Duplicate NativeLogBridge export binding: {suffix}.")
        bindings[suffix] = delegate_name

    prefix_match = re.search(
        r"return\s+source\s*==\s*NativeLogSource\.OpencvCuda\s*"
        r'\?\s*"([^"]+)"\s*:\s*"([^"]+)"\s*;',
        source,
    )
    if not prefix_match or prefix_match.groups() != ("CM_", "M_"):
        raise NativeContractError("NativeLogBridge must map OpencvCuda exports to the CM_ prefix.")
    return library_name, delegates, bindings


def _extract_braced_body(source: str, opening_brace: int, context: str) -> str:
    depth = 0
    for index in range(opening_brace, len(source)):
        character = source[index]
        if character == "{":
            depth += 1
        elif character == "}":
            depth -= 1
            if depth == 0:
                return source[opening_brace + 1:index]
    raise NativeContractError(f"Unterminated braced declaration for {context}.")


def _native_pack_at(source: str, position: int) -> int:
    current = 0
    stack: list[int] = []
    for match in re.finditer(r"^\s*#\s*pragma\s+pack\s*\(([^)]*)\)", source[:position], re.MULTILINE):
        parts = [part.strip() for part in match.group(1).split(",") if part.strip()]
        if not parts:
            current = 0
        elif parts[0] == "push":
            stack.append(current)
            if len(parts) > 1:
                current = int(parts[-1])
        elif parts[0] == "pop":
            if not stack:
                raise NativeContractError("custom_structs.h contains an unmatched #pragma pack(pop).")
            current = stack.pop()
        elif parts[0].isdigit():
            current = int(parts[0])
        else:
            raise NativeContractError(f"Unsupported #pragma pack form before HImage: {match.group(0)!r}.")
    return current


def _top_level_lines(body: str):
    depth = 0
    for line in body.splitlines():
        stripped = line.strip()
        if depth == 0:
            yield stripped
        depth += line.count("{") - line.count("}")
        if depth < 0:
            raise NativeContractError("Unexpected closing brace while parsing ABI structure.")


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
    match = re.search(r"typedef\s+struct\s+HImage\s*\{", source)
    if not match:
        raise NativeContractError("custom_structs.h does not declare typedef struct HImage.")
    opening_brace = source.find("{", match.start())
    body = _extract_braced_body(source, opening_brace, "native HImage")
    fields: list[AbiField] = []
    for line in _top_level_lines(body):
        field_match = re.fullmatch(
            r"(?P<type>(?:unsigned\s+char|int|bool)\s*\*?)\s*"
            r"(?P<name>[A-Za-z_]\w*)(?:\s*=\s*[^;]+)?\s*;",
            line,
        )
        if field_match:
            fields.append(
                AbiField(_normalize_cpp_type(field_match.group("type")), field_match.group("name"))
            )
    return _calculate_layout(
        "native HImage",
        _native_pack_at(source, match.start()),
        tuple(fields),
        {"int": (4, 4), "bool": (1, 1), "unsigned char*": (8, 8)},
    )


def read_managed_himage_layout(source: str) -> AbiStructLayout:
    match = re.search(
        r"\[StructLayout\((?P<layout>[^]]+)\)\]\s*"
        r"public\s+struct\s+HImage\b[^\{]*\{",
        source,
        flags=re.DOTALL,
    )
    if not match:
        raise NativeContractError("HImage.cs does not declare a StructLayout for HImage.")
    layout_parts = _split_top_level(match.group("layout"))
    if not layout_parts or layout_parts[0].strip() != "LayoutKind.Sequential":
        raise NativeContractError("Managed HImage must use LayoutKind.Sequential.")
    pack = 0
    for part in layout_parts[1:]:
        pack_match = re.fullmatch(r"Pack\s*=\s*(\d+)", part)
        if pack_match:
            pack = int(pack_match.group(1))
        else:
            raise NativeContractError(f"Unsupported managed HImage StructLayout option: {part!r}.")

    opening_brace = source.find("{", match.start())
    body = _extract_braced_body(source, opening_brace, "managed HImage")
    fields: list[AbiField] = []
    pending_attributes: list[str] = []
    for line in _top_level_lines(body):
        if not line:
            continue
        attribute_match = re.fullmatch(r"\[(.+)\]", line)
        if attribute_match:
            pending_attributes.append(_normalize_cs_attribute(attribute_match.group(1)))
            continue
        field_match = re.fullmatch(
            r"public\s+(?P<type>int|bool|IntPtr)\s+(?P<name>[A-Za-z_]\w*)\s*;"
            r"(?:\s*//.*)?",
            line,
        )
        if field_match:
            fields.append(
                AbiField(
                    field_match.group("type"),
                    field_match.group("name"),
                    tuple(pending_attributes),
                )
            )
            pending_attributes.clear()
        elif not line.startswith("//"):
            pending_attributes.clear()
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
    if native_layout.pack != 0 or native_layout.fields != EXPECTED_NATIVE_HIMAGE_FIELDS:
        raise NativeContractError(f"Native HImage layout/pack drift: found={native_layout!r}.")
    if managed_layout.pack != 0 or managed_layout.fields != EXPECTED_MANAGED_HIMAGE_FIELDS:
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
) -> NativeContractReport:
    root = Path(repository_root).resolve()
    header_path = root / CUDA_HEADER
    native_struct_path = root / CUDA_NATIVE_STRUCTS
    managed_path = root / CUDA_MANAGED_WRAPPER
    managed_struct_path = root / CUDA_MANAGED_STRUCTS
    log_bridge_path = root / CUDA_NATIVE_LOG_BRIDGE
    project_path = root / CUDA_PROJECT
    tracked_path = root / CUDA_TRACKED_DLL
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


def _packages_in_directory(directory: Path) -> list[Path]:
    packages = sorted(directory.glob("ColorVision.Core.*.nupkg"))
    if len(packages) != 1:
        raise NativeContractError(
            f"Expected exactly one ColorVision.Core nupkg in {directory}; found {len(packages)}."
        )
    return packages


def parse_args() -> argparse.Namespace:
    parser = argparse.ArgumentParser(
        description="Verify the source, managed, tracked-PE, runtime, and package opencv_cuda contract."
    )
    parser.add_argument("--repository-root", type=Path, default=Path(__file__).resolve().parents[1])
    parser.add_argument("--runtime", action="append", type=Path, default=[])
    parser.add_argument("--package", action="append", type=Path, default=[])
    parser.add_argument("--package-directory", action="append", type=Path, default=[])
    return parser.parse_args()


def main() -> int:
    args = parse_args()
    packages = list(args.package)
    try:
        for directory in args.package_directory:
            packages.extend(_packages_in_directory(directory))
        report = validate_native_contracts(
            args.repository_root,
            runtime_files=tuple(args.runtime),
            package_files=tuple(packages),
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
    for path in report.runtime_files:
        print(f"Verified runtime copy: {path}")
    for path in report.package_files:
        print(f"Verified package: {path}!/{CUDA_PACKAGE_MEMBER}")
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
