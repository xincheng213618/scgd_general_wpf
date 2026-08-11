import hashlib
from dataclasses import dataclass
from pathlib import Path


class PreparedArtifactError(RuntimeError):
    pass


@dataclass(frozen=True)
class PreparedArtifact:
    path: Path
    size: int
    sha256: str

    @classmethod
    def capture(cls, path: str | Path) -> "PreparedArtifact":
        artifact_path = Path(path).resolve()
        if not artifact_path.is_file():
            raise PreparedArtifactError(f"Prepared release artifact is not a file: {artifact_path}")
        before = artifact_path.stat()
        digest = _sha256_file(artifact_path)
        after = artifact_path.stat()
        if (before.st_size, before.st_mtime_ns) != (after.st_size, after.st_mtime_ns):
            raise PreparedArtifactError(f"Prepared release artifact changed while hashing: {artifact_path}")
        return cls(artifact_path, after.st_size, digest)

    def verify(self) -> None:
        try:
            current_size = self.path.stat().st_size
        except OSError as exc:
            raise PreparedArtifactError(f"Prepared release artifact is unavailable: {self.path}: {exc}") from exc
        if current_size != self.size:
            raise PreparedArtifactError(
                f"Prepared release artifact size changed: {self.path} "
                f"(expected {self.size}, found {current_size})"
            )
        current_hash = _sha256_file(self.path)
        if current_hash != self.sha256:
            raise PreparedArtifactError(
                f"Prepared release artifact hash changed: {self.path} "
                f"(expected {self.sha256}, found {current_hash})"
            )

    def stage_to(self, destination: str | Path) -> Path:
        destination_path = Path(destination)
        destination_path.parent.mkdir(parents=True, exist_ok=True)
        digest = hashlib.sha256()
        bytes_written = 0
        try:
            with self.path.open("rb") as source, destination_path.open("xb") as output:
                while chunk := source.read(1024 * 1024):
                    output.write(chunk)
                    digest.update(chunk)
                    bytes_written += len(chunk)
        except OSError as exc:
            destination_path.unlink(missing_ok=True)
            raise PreparedArtifactError(
                f"Could not stage prepared release artifact {self.path}: {exc}"
            ) from exc

        staged_hash = digest.hexdigest()
        if bytes_written != self.size or staged_hash != self.sha256:
            destination_path.unlink(missing_ok=True)
            raise PreparedArtifactError(
                f"Prepared release artifact changed before publication: {self.path} "
                f"(expected {self.size}/{self.sha256}, found {bytes_written}/{staged_hash})"
            )
        return destination_path


def _sha256_file(path: Path) -> str:
    digest = hashlib.sha256()
    try:
        with path.open("rb") as file_handle:
            while chunk := file_handle.read(1024 * 1024):
                digest.update(chunk)
    except OSError as exc:
        raise PreparedArtifactError(f"Could not hash prepared release artifact {path}: {exc}") from exc
    return digest.hexdigest()
