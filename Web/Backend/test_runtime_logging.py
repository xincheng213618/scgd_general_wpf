import io
import tempfile
import unittest
from pathlib import Path

from services.runtime_logging import (
    _RotatingTextSink,
    _TeeTextStream,
    close_runtime_logging,
    install_runtime_logging,
)


class RuntimeLoggingTests(unittest.TestCase):
    def setUp(self):
        self.temp_dir = tempfile.TemporaryDirectory()
        self.log_path = Path(self.temp_dir.name) / "ColorVisionWeb.log"

    def tearDown(self):
        self.temp_dir.cleanup()

    def test_tee_preserves_console_output_and_persists_utf8(self):
        sink = _RotatingTextSink(self.log_path, max_bytes=1024, backup_count=2)
        console = io.StringIO()
        stream = _TeeTextStream(console, sink)

        stream.write("启动完成\n")
        stream.flush()
        sink.close()

        self.assertEqual(console.getvalue(), "启动完成\n")
        self.assertEqual(self.log_path.read_text(encoding="utf-8"), "启动完成\n")

    def test_sink_rotates_without_exceeding_backup_limit(self):
        sink = _RotatingTextSink(self.log_path, max_bytes=24, backup_count=2)

        sink.write("first-entry\n")
        sink.write("second-entry\n")
        sink.write("third-entry\n")
        sink.close()

        self.assertTrue(self.log_path.exists())
        self.assertTrue(self.log_path.with_name("ColorVisionWeb.log.1").exists())
        self.assertFalse(self.log_path.with_name("ColorVisionWeb.log.3").exists())
        combined = "".join(
            path.read_text(encoding="utf-8")
            for path in sorted(self.log_path.parent.glob("ColorVisionWeb.log*"))
        )
        self.assertIn("first-entry", combined)
        self.assertIn("third-entry", combined)

    def test_install_and_close_restore_streams_and_release_log(self):
        import sys

        original_stdout = sys.stdout
        original_stderr = sys.stderr
        try:
            path = install_runtime_logging(Path(self.temp_dir.name))
            sys.stdout.write("stdout-probe\n")
            sys.stderr.write("stderr-probe\n")
        finally:
            close_runtime_logging()

        self.assertIs(sys.stdout, original_stdout)
        self.assertIs(sys.stderr, original_stderr)
        text = path.read_text(encoding="utf-8")
        self.assertIn("process_start pid=", text)
        self.assertIn("stdout-probe", text)
        self.assertIn("stderr-probe", text)


if __name__ == "__main__":
    unittest.main()
