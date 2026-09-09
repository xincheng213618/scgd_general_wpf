from pathlib import Path, PurePosixPath


REQUIRED_OPERATIONS_WATCHDOG_RUNTIME_PATHS = (
    "OperationsWatchdog/ColorVisionOperationsWatchdog.exe",
    "OperationsWatchdog/ColorVisionOperationsWatchdog.dll",
    "OperationsWatchdog/ColorVisionOperationsWatchdog.deps.json",
    "OperationsWatchdog/ColorVisionOperationsWatchdog.runtimeconfig.json",
)


def validate_operations_watchdog_runtime(version_directory: str | Path) -> None:
    runtime_path = Path(version_directory)
    missing_paths = [
        relative_path
        for relative_path in REQUIRED_OPERATIONS_WATCHDOG_RUNTIME_PATHS
        if not runtime_path.joinpath(*PurePosixPath(relative_path).parts).is_file()
    ]
    if missing_paths:
        raise FileNotFoundError(
            "OperationsWatchdog runtime is incomplete: " + ", ".join(missing_paths)
        )
