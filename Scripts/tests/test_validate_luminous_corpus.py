"""Protect the corpus gate against false passes and altered historical evidence."""

import copy
from argparse import Namespace
from contextlib import nullcontext
import hashlib
import importlib.util
import json
import struct
import sys
import tempfile
import unittest
from unittest.mock import patch
from pathlib import Path

import numpy as np


REPO_ROOT = Path(__file__).resolve().parents[2]
SCRIPT = REPO_ROOT / "Test/opencv_helper_test/validate_luminous_corpus.py"
SPEC = importlib.util.spec_from_file_location("validate_luminous_corpus", SCRIPT)
assert SPEC is not None and SPEC.loader is not None
MODULE = importlib.util.module_from_spec(SPEC)
sys.modules[SPEC.name] = MODULE
SPEC.loader.exec_module(MODULE)


def _cvraw(*, version=2, width=2, height=2, channels=1, bits=16, payload=None, declared_length=None):
    if payload is None:
        payload = np.array([100, 200, 300, 400], dtype="<u2").tobytes()
    header = b"CVCIE" + struct.pack("<Ii", version, 0)
    if version == 3:
        header += struct.pack("<i", 2)
    header += struct.pack("<fI", 1.0, channels & 0xFFFFFFFF)
    header += struct.pack("<" + "f" * max(channels, 0), *([1.0] * max(channels, 0)))
    header += struct.pack("<III", width & 0xFFFFFFFF, height & 0xFFFFFFFF, bits)
    header += struct.pack("<q" if version == 2 else "<i", len(payload) if declared_length is None else declared_length)
    return header + payload


def _result(*, success=True):
    return {
        "Algorithm": "RobustV2",
        "Success": success,
        "Confidence": 0.9 if success else 0.0,
        "Corners": [{"X": x, "Y": y} for x, y in [(0.0, 0.0), (1.0, 0.0), (1.0, 1.0), (0.0, 1.0)]] if success else [],
        "SideQuality": [
            {"Name": name, "Coverage": 1.0, "InlierRatio": 1.0, "ContrastP10": 0.8,
             "FitRms": 0.1, "MaxGap": 0.0, "Confidence": 0.9, "SampleCount": 10, "InlierCount": 10}
            for name in ("Top", "Right", "Bottom", "Left")
        ],
        "FailureReason": "" if success else "NoSignal",
        "Warnings": [],
    }


def _row(name="image.tiff", *, success=True):
    return {
        "file": name,
        "sha256": "a" * 64,
        "sizeBytes": 8,
        "shape": [2, 2],
        "dtype": "uint16",
        "returnCode": 1000,
        "elapsedMs": 3.0,
        "result": _result(success=success),
    }


class CvRawDecodingTests(unittest.TestCase):
    def test_reads_all_supported_header_versions_without_reordering_channels(self):
        pixels = np.arange(12, dtype="<u2").reshape(2, 2, 3)
        for version in (1, 2, 3):
            with self.subTest(version=version):
                actual = MODULE.read_cvraw(_cvraw(version=version, channels=3, payload=pixels.tobytes()))
                np.testing.assert_array_equal(pixels, actual)
                self.assertEqual(np.dtype("uint16"), actual.dtype)

    def test_rejects_malformed_lengths_formats_and_dimensions_before_native_call(self):
        cases = {
            "truncated header": b"CVCIE\x02",
            "wrong magic": b"WRONG" + _cvraw()[5:],
            "unknown version": _cvraw(version=9),
            "negative name length": b"CVCIE" + struct.pack("<Ii", 2, -1),
            "name extends past EOF": b"CVCIE" + struct.pack("<Ii", 2, 100),
            "truncated payload": _cvraw()[:-1],
            "wrong declared payload": _cvraw(declared_length=7),
            "negative payload length": _cvraw(declared_length=-1),
            "unsupported depth": _cvraw(bits=12),
            "zero channels": _cvraw(channels=0),
            "unsupported channels": _cvraw(channels=2),
            "negative channels": _cvraw(version=3, channels=-1),
            "zero width": _cvraw(width=0),
            "negative width": _cvraw(version=3, width=-1),
            "negative height": _cvraw(version=3, height=-1),
        }
        for name, data in cases.items():
            with self.subTest(case=name), self.assertRaises(ValueError):
                MODULE.read_cvraw(data)

    def test_float32_grayscale_keeps_its_pixel_type_and_values(self):
        pixels = np.array([[0.25, 0.5], [1.0, 2.0]], dtype="<f4")
        actual = MODULE.read_cvraw(_cvraw(bits=32, payload=pixels.tobytes()))
        np.testing.assert_array_equal(pixels, actual)
        self.assertEqual(np.dtype("float32"), actual.dtype)

    def test_optional_metadata_tail_does_not_change_the_declared_pixel_payload(self):
        pixels = np.arange(12, dtype="<u2").reshape(2, 2, 3)
        tail = b"CVMD0001" + bytes(56) + b"opaque camera acquisition metadata"
        for version in (1, 2, 3):
            with self.subTest(version=version):
                data = _cvraw(version=version, channels=3, payload=pixels.tobytes()) + tail
                actual = MODULE.read_cvraw(data)
                np.testing.assert_array_equal(pixels, actual)


class ResultContractTests(unittest.TestCase):
    def test_accepts_both_detection_and_explicit_algorithm_rejection(self):
        MODULE.validate_result(_result(success=True))
        MODULE.validate_result(_result(success=False))

    def test_low_confidence_rejection_may_preserve_candidate_corners(self):
        result = _result(success=False)
        result["FailureReason"] = "LowConfidence"
        result["Corners"] = _result()["Corners"]
        MODULE.validate_result(result)

    def test_rejects_nonfinite_diagnostics_and_invalid_success_geometry(self):
        for location in ("Confidence", "corner", "diagnostic"):
            for value in (float("nan"), float("inf"), float("-inf")):
                result = _result()
                if location == "corner":
                    result["Corners"][0]["X"] = value
                elif location == "diagnostic":
                    result["SideQuality"][0]["FitRms"] = value
                else:
                    result[location] = value
                with self.subTest(location=location, value=value), self.assertRaises(ValueError):
                    MODULE.validate_result(result)
        result = _result()
        result["Corners"].pop()
        with self.assertRaises(ValueError):
            MODULE.validate_result(result)


class CorpusComparisonTests(unittest.TestCase):
    def test_unchanged_known_rejection_passes_and_observation_timing_is_ignored(self):
        expected = [_row(success=False)]
        actual = copy.deepcopy(expected)
        actual[0]["elapsedMs"] = 900.0
        self.assertEqual([], MODULE.compare_rows(expected, actual))

    def test_detects_replaced_input_even_when_result_is_identical(self):
        expected = [_row()]
        actual = copy.deepcopy(expected)
        actual[0]["sha256"] = "b" * 64
        changes = MODULE.compare_rows(expected, actual)
        self.assertEqual(["image.tiff"], [item["file"] for item in changes])
        self.assertIn("sha256", changes[0]["changes"])

    def test_missing_input_identity_requires_review_even_when_both_sides_lack_it(self):
        expected = [_row()]
        del expected[0]["sha256"]
        changes = MODULE.compare_rows(expected, copy.deepcopy(expected))
        self.assertEqual(["image.tiff"], [item["file"] for item in changes])
        self.assertTrue(changes[0]["changes"])

    def test_detects_sample_set_metadata_and_complete_result_changes(self):
        expected = [_row("missing.tiff"), _row()]
        actual = [_row("new.tiff"), _row()]
        actual[1]["shape"] = [1, 4]
        actual[1]["result"]["SideQuality"][0]["SampleCount"] = 9
        changes = {item["file"]: item["changes"] for item in MODULE.compare_rows(expected, actual)}
        self.assertEqual({"missing.tiff", "new.tiff", "image.tiff"}, set(changes))
        self.assertTrue(any(field.startswith("shape") for field in changes["image.tiff"]))
        self.assertTrue(any(field.startswith("result") for field in changes["image.tiff"]))

    def test_duplicate_sample_names_cannot_hide_a_result(self):
        for expected, actual in (([_row(), _row()], [_row()]), ([_row()], [_row(), _row()])):
            with self.subTest(side="reference" if len(expected) == 2 else "candidate"), self.assertRaises(ValueError):
                MODULE.compare_rows(expected, actual)


class ReferenceBindingTests(unittest.TestCase):
    def test_binds_current_input_identity_without_rewriting_historical_expectations(self):
        with tempfile.TemporaryDirectory(prefix="luminous-reference-") as directory:
            root = Path(directory)
            samples = root / "samples"
            samples.mkdir()
            sample = samples / "image.tiff"
            sample.write_bytes(b"pixels")
            reference = root / "accepted.json"
            original = {"dll": "historical.dll", "dllSha256": "b" * 64, "rows": [_row()]}
            original["rows"][0].pop("sha256")
            original["rows"][0].pop("sizeBytes")
            reference.write_text(json.dumps(original), encoding="utf-8")
            historical_bytes = reference.read_bytes()
            output = root / "bound.json"

            bound = MODULE.bind_reference(samples, reference, output)

            self.assertEqual(historical_bytes, reference.read_bytes())
            self.assertEqual(1, bound["schemaVersion"])
            self.assertEqual("reviewed-behavior", bound["referenceKind"])
            self.assertEqual("bound-at-import", bound["provenance"]["inputIdentity"])
            self.assertEqual(original["rows"][0]["result"], bound["rows"][0]["result"])
            self.assertEqual(hashlib.sha256(b"pixels").hexdigest(), bound["rows"][0]["sha256"])
            self.assertEqual(6, bound["rows"][0]["sizeBytes"])
            self.assertEqual(bound, json.loads(output.read_text(encoding="utf-8")))
            with self.assertRaises((FileExistsError, ValueError)):
                MODULE.bind_reference(samples, reference, output)
            self.assertEqual(bound, json.loads(output.read_text(encoding="utf-8")))

    def test_refuses_binding_a_changed_or_ambiguous_sample_set(self):
        for scenario in ("missing", "extra", "duplicate"):
            with self.subTest(scenario=scenario), tempfile.TemporaryDirectory(prefix="luminous-reference-") as directory:
                root = Path(directory)
                samples = root / "samples"
                samples.mkdir()
                if scenario != "missing":
                    (samples / "image.tiff").write_bytes(b"pixels")
                if scenario == "extra":
                    (samples / "extra.cvraw").write_bytes(b"pixels")
                rows = [_row()]
                if scenario == "duplicate":
                    rows.append(_row())
                reference = root / "accepted.json"
                reference.write_text(json.dumps({"rows": rows}), encoding="utf-8")
                output = root / "bound.json"
                with self.assertRaises(ValueError):
                    MODULE.bind_reference(samples, reference, output)
                self.assertFalse(output.exists())

    def test_existing_input_identity_cannot_be_rebound_to_changed_bytes(self):
        with tempfile.TemporaryDirectory(prefix="luminous-reference-") as directory:
            root = Path(directory)
            samples = root / "samples"
            samples.mkdir()
            (samples / "image.tiff").write_bytes(b"new")
            row = _row()
            row["sha256"] = hashlib.sha256(b"old").hexdigest()
            row["sizeBytes"] = 3
            reference = root / "accepted.json"
            reference.write_text(json.dumps({"rows": [row]}), encoding="utf-8")
            original_bytes = reference.read_bytes()
            output = root / "bound.json"
            with self.assertRaises(ValueError):
                MODULE.bind_reference(samples, reference, output)
            self.assertFalse(output.exists())
            self.assertEqual(original_bytes, reference.read_bytes())


class CorpusRunTests(unittest.TestCase):
    def _inputs(self, root, names, *, success=True):
        samples = root / "samples"
        samples.mkdir()
        encoded, content = MODULE.cv2.imencode(".png", np.array([[0, 1], [2, 3]], dtype=np.uint8))
        self.assertTrue(encoded)
        data = content.tobytes()
        rows = []
        for name in names:
            (samples / name).write_bytes(data)
            row = _row(name, success=success)
            row.update(sha256=hashlib.sha256(data).hexdigest(), sizeBytes=len(data), dtype="uint8")
            rows.append(row)
        reference = root / "reference.json"
        reference.write_text(json.dumps({"schemaVersion": 1, "referenceKind": "reviewed-behavior",
                                         "config": {}, "rows": rows}), encoding="utf-8")
        dll = root / "fixture.dll"
        dll.write_bytes(b"fixture DLL; NativeDetector is mocked")
        return Namespace(samples=samples, reference=reference, dll=dll,
                         output=root / "output", dependency_dir=[])

    def test_records_native_error_and_continues_to_remaining_sample(self):
        with tempfile.TemporaryDirectory(prefix="luminous-run-") as directory:
            args = self._inputs(Path(directory), ["a.png", "b.png"])
            with patch.object(MODULE, "NativeDetector") as detector, patch.object(
                    MODULE.os, "add_dll_directory", side_effect=lambda _: nullcontext(), create=True):
                detector.return_value.loaded_path = str(args.dll)
                detector.return_value.detect.side_effect = [
                    ValueError("fixture native failure"), (1000, 1.0, _result())]
                self.assertEqual(1, MODULE.run(args))
            report = json.loads((args.output / "report.json").read_text(encoding="utf-8"))
            self.assertEqual(2, len(report["rows"]))
            self.assertEqual(1, report["technicalErrors"])
            self.assertIn("fixture native failure", report["rows"][0]["error"])
            self.assertEqual(_result(), report["rows"][1]["result"])
            summary = (args.output / "summary.md").read_text(encoding="utf-8")
            for text in ("a.png", "b.png", "fixture native failure"):
                self.assertIn(text, summary)

    def test_unchanged_reviewed_rejection_returns_zero(self):
        with tempfile.TemporaryDirectory(prefix="luminous-run-") as directory:
            args = self._inputs(Path(directory), ["rejected.png"], success=False)
            with patch.object(MODULE, "NativeDetector") as detector, patch.object(
                    MODULE.os, "add_dll_directory", side_effect=lambda _: nullcontext(), create=True):
                detector.return_value.loaded_path = str(args.dll)
                detector.return_value.detect.return_value = (1000, 1.0, _result(success=False))
                self.assertEqual(0, MODULE.run(args))
            report = json.loads((args.output / "report.json").read_text(encoding="utf-8"))
            self.assertEqual([], report["differences"])
            self.assertEqual(0, report["technicalErrors"])
            self.assertFalse(report["rows"][0]["result"]["Success"])

    def test_malformed_reference_cannot_be_treated_as_no_reference_and_pass(self):
        with tempfile.TemporaryDirectory(prefix="luminous-run-") as directory:
            args = self._inputs(Path(directory), ["image.png"])
            args.reference.write_text("{}", encoding="utf-8")
            with patch.object(MODULE, "NativeDetector") as detector, patch.object(
                    MODULE.os, "add_dll_directory", side_effect=lambda _: nullcontext(), create=True):
                detector.return_value.loaded_path = str(args.dll)
                detector.return_value.detect.return_value = (1000, 1.0, _result())
                code = MODULE.main(["run", "--samples", str(args.samples), "--reference", str(args.reference),
                                    "--dll", str(args.dll), "--output", str(args.output)])
                self.assertNotEqual(0, code)

    def test_empty_reference_cannot_pass_a_nonempty_corpus(self):
        with tempfile.TemporaryDirectory(prefix="luminous-run-") as directory:
            args = self._inputs(Path(directory), ["image.png"])
            args.reference.write_text(json.dumps({"rows": []}), encoding="utf-8")
            with patch.object(MODULE, "NativeDetector") as detector, patch.object(
                    MODULE.os, "add_dll_directory", side_effect=lambda _: nullcontext(), create=True):
                detector.return_value.loaded_path = str(args.dll)
                detector.return_value.detect.return_value = (1000, 1.0, _result())
                code = MODULE.main(["run", "--samples", str(args.samples), "--reference", str(args.reference),
                                    "--dll", str(args.dll), "--output", str(args.output)])
                self.assertNotEqual(0, code)

    def test_unreviewed_or_failed_reference_cannot_establish_a_pass(self):
        for scenario in ("undeclared reference kind", "technical error row"):
            with self.subTest(scenario=scenario), tempfile.TemporaryDirectory(prefix="luminous-run-") as directory:
                args = self._inputs(Path(directory), ["image.png"])
                reference = json.loads(args.reference.read_text(encoding="utf-8"))
                if scenario == "undeclared reference kind":
                    reference.pop("referenceKind")
                else:
                    reference["rows"][0]["error"] = "Prior native call failed"
                args.reference.write_text(json.dumps(reference), encoding="utf-8")
                with patch.object(MODULE, "NativeDetector") as detector, patch.object(
                        MODULE.os, "add_dll_directory", side_effect=lambda _: nullcontext(), create=True):
                    detector.return_value.loaded_path = str(args.dll)
                    detector.return_value.detect.return_value = (1000, 1.0, _result())
                    code = MODULE.main(["run", "--samples", str(args.samples), "--reference", str(args.reference),
                                        "--dll", str(args.dll), "--output", str(args.output)])
                    self.assertNotEqual(0, code)


if __name__ == "__main__":
    unittest.main()
