from pathlib import PurePosixPath


ALLOWED_RUNTIME_PREFIXES = (
    "runtimes/win/",
    "runtimes/win-x64/",
)


def normalize_archive_relative_path(path_value: str) -> str:
    return PurePosixPath(path_value.replace("\\", "/")).as_posix()


def should_keep_runtime_path(path_value: str) -> bool:
    normalized = normalize_archive_relative_path(path_value).lower()
    if not normalized.startswith("runtimes/"):
        return True

    return normalized.startswith(ALLOWED_RUNTIME_PREFIXES)
