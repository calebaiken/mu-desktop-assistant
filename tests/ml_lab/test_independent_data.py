"""Independent adversarial dataset checks; synthetic fixtures never use personal data."""
from __future__ import annotations

import hashlib
import io
import json
import math
import os
from pathlib import Path
import sys
import tempfile
import unittest
import zipfile
from unittest.mock import patch

sys.path.insert(0, str(Path(__file__).resolve().parents[2] / "workers" / "ml_lab"))

import h5py
import numpy as np
import pandas as pd
from common import LabError, limits, local_path
from data import browse, inspect_frame, read_dataset, source_identity


RESOURCE = {"memory_mb": 1024, "max_rows": 10000, "preview_rows": 1000, "workers": 2}


class IndependentDataTests(unittest.TestCase):
    def setUp(self):
        self.scratch = Path(__file__).resolve().parents[2] / "artifacts" / "qa" / "ml-independent-scratch"
        self.scratch.mkdir(parents=True, exist_ok=True)
        self.directory = tempfile.TemporaryDirectory(prefix="data-", dir=self.scratch)
        self.root = Path(self.directory.name)

    def tearDown(self):
        self.assertEqual(self.root.resolve().parent, self.scratch.resolve())
        self.directory.cleanup()

    def read(self, name, **selection):
        return read_dataset({"path": str(self.root / name), **selection}, RESOURCE)

    def test_tabular_formats_select_exact_rows_and_preserve_bytes(self):
        source = pd.DataFrame({"x": [1, 2, 3, 4], "label": ["alpha", "beta", "gamma", "delta"]})
        source.to_csv(self.root / "data.csv", index=False)
        source.to_csv(self.root / "data.tsv", sep="\t", index=False)
        (self.root / "data.json").write_text(source.to_json(orient="records"), encoding="utf-8")
        (self.root / "data.jsonl").write_text(source.to_json(orient="records", lines=True), encoding="utf-8")
        for name in ["data.csv", "data.tsv", "data.json", "data.jsonl"]:
            with self.subTest(format=name):
                path = self.root / name
                before = path.read_bytes()
                frame, metadata, warnings, sampled = self.read(name, start=1, rowLimit=2)
                self.assertEqual(frame.to_dict("list"), {"x": [2, 3], "label": ["beta", "gamma"]})
                self.assertEqual(metadata["selectedRows"], 2)
                self.assertEqual(metadata["start"], 1)
                self.assertTrue(sampled)
                self.assertTrue(warnings)
                self.assertEqual(path.read_bytes(), before)
                self.assertEqual(metadata["fingerprint"], hashlib.sha256(before).hexdigest())

    def test_numpy_numeric_selection_and_container_key(self):
        array = np.arange(24, dtype=np.float64).reshape(8, 3)
        np.save(self.root / "data.npy", array)
        np.savez_compressed(self.root / "data.npz", events=array, other=np.ones((3, 2)))
        for name, selection in [("data.npy", {}), ("data.npz", {"key": "events"})]:
            with self.subTest(format=name):
                frame, metadata, _, sampled = self.read(name, start=2, rowLimit=3, **selection)
                np.testing.assert_array_equal(frame.to_numpy(), array[2:5])
                self.assertEqual(metadata["shape"], [8, 3])
                self.assertTrue(sampled)
        with self.assertRaises(LabError):
            self.read("data.npz")

    def test_object_numpy_cannot_enable_pickle(self):
        array = np.array([{"private": "sentinel"}], dtype=object)
        np.save(self.root / "unsafe.npy", array)
        np.savez(self.root / "unsafe.npz", objects=array)
        for name in ["unsafe.npy", "unsafe.npz"]:
            with self.subTest(format=name), self.assertRaises((LabError, ValueError)):
                self.read(name)

    def test_npz_expansion_refused_before_numpy_load(self):
        np.savez_compressed(self.root / "expanded.npz", data=np.zeros((200000, 2), dtype=np.float64))
        with patch("data.np.load", side_effect=AssertionError("Decompression must not begin")):
            with self.assertRaises(LabError) as caught:
                read_dataset({"path": str(self.root / "expanded.npz")}, {**RESOURCE, "memory_mb": 1})
        self.assertEqual(caught.exception.code, "resource_limit")

    def test_npz_tiny_member_with_huge_declared_shape_is_refused_before_loading(self):
        header = io.BytesIO()
        np.lib.format.write_array_header_1_0(header, {"descr": "<f8", "fortran_order": False, "shape": (100_000_000, 3)})
        with zipfile.ZipFile(self.root / "declared.npz", "w", compression=zipfile.ZIP_DEFLATED) as archive:
            archive.writestr("events.npy", header.getvalue())
        self.assertLess((self.root / "declared.npz").stat().st_size, 10000)
        with patch("data.np.load", side_effect=AssertionError("Array materialization must not begin")):
            with self.assertRaises(LabError):
                self.read("declared.npz", key="events", rowLimit=10)

    def test_csv_multiline_records_use_logical_row_selection(self):
        frame = pd.DataFrame({"x": [1, 2, 3, 4], "text": ["first\ncontinued", "second", "third\ncontinued", "fourth"]})
        frame.to_csv(self.root / "multiline.csv", index=False)
        selected, metadata, _, _ = self.read("multiline.csv", start=1, rowLimit=2)
        self.assertEqual(selected.to_dict("list"), frame.iloc[1:3].to_dict("list"))
        self.assertEqual(metadata["start"], 1)

    def test_hdf5_giant_logical_dataset_reads_only_requested_range(self):
        path = self.root / "large.h5"
        with h5py.File(path, "w") as handle:
            ds = handle.create_dataset("detector/events", shape=(10_000_000, 2), dtype="f8", chunks=(64, 2), fillvalue=7)
            ds.attrs["units"] = "arbitrary units"
        self.assertLess(path.stat().st_size, 100000)
        original = path.read_bytes()
        result = browse({"path": str(path)}, RESOURCE)
        entries = {entry["key"]: entry for entry in result["entries"]}
        self.assertEqual(entries["/detector/events"]["shape"], [10_000_000, 2])
        self.assertIn("units", entries["/detector/events"]["attributes"])
        frame, metadata, _, sampled = self.read("large.h5", key="/detector/events", start=99, rowLimit=10)
        np.testing.assert_array_equal(frame.to_numpy(), np.full((10, 2), 7.0))
        self.assertEqual(metadata["rows"], 10_000_000)
        self.assertTrue(sampled)
        self.assertEqual(path.read_bytes(), original)

    def test_hdf5_links_and_external_storage_are_refused(self):
        target = self.root / "target.h5"
        with h5py.File(target, "w") as handle:
            handle["rows"] = np.arange(12).reshape(6, 2)
        with h5py.File(self.root / "links.h5", "w") as handle:
            handle["external"] = h5py.ExternalLink(str(target), "/rows")
            handle["soft"] = h5py.SoftLink("/external")
            handle.create_dataset("raw_external", (6, 2), dtype="f8", external=[(str(self.root / "raw.bin"), 0, h5py.h5f.UNLIMITED)])
        listing = browse({"path": str(self.root / "links.h5")}, RESOURCE)
        self.assertEqual({x["key"] for x in listing["entries"] if x["kind"] == "link-refused"}, {"/external", "/soft"})
        for key in ["/external", "/soft", "/raw_external"]:
            with self.subTest(key=key), self.assertRaises(LabError) as caught:
                self.read("links.h5", key=key)
            self.assertEqual(caught.exception.code, "unsafe_path")

    def test_hdf5_virtual_dataset_is_refused(self):
        source = self.root / "source.h5"
        with h5py.File(source, "w") as handle:
            handle["rows"] = np.arange(12).reshape(6, 2)
        layout = h5py.VirtualLayout(shape=(6, 2), dtype="i8")
        layout[:] = h5py.VirtualSource(str(source), "/rows", shape=(6, 2))
        with h5py.File(self.root / "virtual.h5", "w", libver="latest") as handle:
            handle.create_virtual_dataset("events", layout)
        with self.assertRaises(LabError) as caught:
            self.read("virtual.h5", key="/events")
        self.assertEqual(caught.exception.code, "unsafe_path")

    def test_nested_and_empty_ranges_fail_without_source_change(self):
        path = self.root / "nested.json"
        path.write_text(json.dumps([{"x": {"nested": 1}}]), encoding="utf-8")
        with self.assertRaises(LabError):
            self.read("nested.json")
        np.save(self.root / "tensor.npy", np.zeros((2, 2, 2)))
        with self.assertRaises(LabError):
            self.read("tensor.npy")
        (self.root / "short.csv").write_text("x,y\n1,2\n", encoding="utf-8")
        with self.assertRaises(LabError):
            self.read("short.csv", start=100)

    def test_preview_limits_are_explicit_and_original_not_modified(self):
        path = self.root / "preview.csv"
        pd.DataFrame({"event_id": np.arange(1500), "x": np.arange(1500) * 2}).to_csv(path, index=False)
        frame, metadata, warnings, sampled = read_dataset({"path": str(path), "rowLimit": 1400}, RESOURCE, preview=True)
        self.assertEqual(len(frame), 1000)
        self.assertEqual(metadata["rowLimit"], 1400)
        self.assertTrue(sampled)
        self.assertIn("not a random or representative sample", " ".join(warnings))

    def test_inspector_flags_identifiers_missing_constants_and_duplicates_without_mutation(self):
        frame = pd.DataFrame({"event_id": [1, 2, 3, 4], "value": [1.0, None, 1.0, 3.0], "constant": [7, 7, 7, 7]})
        before = frame.copy(deep=True)
        features, preview, warnings = inspect_frame(frame)
        indexed = {item["name"]: item for item in features}
        self.assertTrue(indexed["event_id"]["identifier"])
        self.assertEqual(indexed["value"]["missing"], 1)
        self.assertTrue(indexed["constant"]["constant"])
        self.assertEqual(indexed["value"]["statistics"]["mean"], 5 / 3)
        self.assertEqual(len(preview), 4)
        self.assertTrue(warnings)
        pd.testing.assert_frame_equal(frame, before)
        duplicated = pd.DataFrame({"x": [1, 1, 2]})
        self.assertIn("1 duplicate rows", " ".join(inspect_frame(duplicated)[2]))

    def test_changed_content_with_same_size_changes_full_fingerprint(self):
        path = self.root / "identity.csv"
        path.write_text("x,y\n1,2\n", encoding="utf-8")
        initial = source_identity(path)
        stamp = path.stat()
        path.write_text("x,y\n2,1\n", encoding="utf-8")
        os.utime(path, ns=(stamp.st_atime_ns, stamp.st_mtime_ns))
        changed = source_identity(path)
        self.assertEqual(changed["fileSize"], initial["fileSize"])
        self.assertEqual(changed["modifiedAt"], initial["modifiedAt"])
        self.assertNotEqual(changed["fingerprint"], initial["fingerprint"])

    def test_header_and_column_limits_are_enforced(self):
        (self.root / "header.csv").write_text("x" * 65537 + "\n1\n", encoding="utf-8")
        with patch("data.pd.read_csv", side_effect=AssertionError("Parser must not begin")):
            with self.assertRaises(LabError) as caught:
                self.read("header.csv")
        self.assertEqual(caught.exception.code, "resource_limit")
        np.save(self.root / "wide.npy", np.zeros((3, 129)))
        with self.assertRaises(LabError):
            self.read("wide.npy")

    def test_compute_bounds_match_application_contract(self):
        self.assertEqual(limits({"limits": {"maxRows": 10, "memoryMiB": 256, "threads": 1}})["max_rows"], 10)
        self.assertEqual(limits({"limits": {"maxRows": 100000, "memoryMiB": 4096, "threads": 8}})["workers"], 8)
        for field, value in [("maxRows", 9), ("maxRows", 100001), ("memoryMiB", 255), ("memoryMiB", 4097), ("threads", 0), ("threads", 9)]:
            with self.subTest(field=field, value=value), self.assertRaises(LabError):
                limits({"limits": {field: value}})

    def test_remote_relative_and_device_paths_are_rejected(self):
        for path in ["relative.csv", "//server/share/data.csv", "\\\\server\\share\\data.csv", "\\\\?\\C:\\data.csv", "\\\\.\\C:\\data.csv", "C:\\data.csv:stream"]:
            with self.subTest(path=path), self.assertRaises(LabError):
                local_path(path, exists=False)



    def test_waveform_feature_extraction_matches_hand_computed_oracles(self):
        n_samples = 16
        t = np.arange(n_samples, dtype=np.float64)
        sine = 2.0 * np.sin(2.0 * np.pi * 3.0 * t / n_samples)
        constant = np.full(n_samples, 5.0, dtype=np.float64)
        zeros = np.zeros(n_samples, dtype=np.float64)
        impulse = np.zeros(n_samples, dtype=np.float64)
        impulse[4] = -8.0
        matrix = np.vstack([sine, constant, zeros, impulse])
        np.save(self.root / "oracles.npy", matrix)

        frame, meta, warnings, more = self.read("oracles.npy", key="@waveform")
        self.assertFalse(more)
        self.assertEqual(meta["key"], "@waveform")
        self.assertEqual(frame.shape, (4, 15))
        self.assertTrue(any("Waveform mode" in w for w in warnings))

        # Row 0: pure sine wave (amplitude 2, 3 cycles in 16 samples)
        self.assertAlmostEqual(frame.loc[0, "wf_mean"], 0.0, places=12)
        self.assertAlmostEqual(frame.loc[0, "wf_std"], math.sqrt(2.0), places=12)
        self.assertAlmostEqual(frame.loc[0, "wf_rms"], math.sqrt(2.0), places=12)
        self.assertAlmostEqual(frame.loc[0, "wf_energy"], 32.0, places=11)
        self.assertAlmostEqual(frame.loc[0, "wf_dominant_bin"], 3.0, places=12)
        self.assertAlmostEqual(frame.loc[0, "wf_spectral_centroid"], 3.0, places=11)

        # Row 1: constant 5.0 signal
        self.assertAlmostEqual(frame.loc[1, "wf_mean"], 5.0, places=12)
        self.assertAlmostEqual(frame.loc[1, "wf_std"], 0.0, places=12)
        self.assertAlmostEqual(frame.loc[1, "wf_ptp"], 0.0, places=12)
        self.assertAlmostEqual(frame.loc[1, "wf_rms"], 5.0, places=12)
        self.assertAlmostEqual(frame.loc[1, "wf_energy"], 400.0, places=12)
        self.assertAlmostEqual(frame.loc[1, "wf_crest_factor"], 1.0, places=12)
        self.assertAlmostEqual(frame.loc[1, "wf_zero_crossings"], 0.0, places=12)
        self.assertAlmostEqual(frame.loc[1, "wf_spectral_centroid"], 0.0, places=12)
        self.assertAlmostEqual(frame.loc[1, "wf_dominant_bin"], 0.0, places=12)

        # Row 2: all-zero signal (no NaN/Inf on zero RMS or zero spectrum)
        for col in frame.columns:
            self.assertEqual(frame.loc[2, col], 0.0, msg=f"Expected 0.0 for {col} on zero signal")

        # Row 3: negative impulse at index 4 (-8.0)
        self.assertAlmostEqual(frame.loc[3, "wf_min"], -8.0, places=12)
        self.assertAlmostEqual(frame.loc[3, "wf_max"], 0.0, places=12)
        self.assertAlmostEqual(frame.loc[3, "wf_ptp"], 8.0, places=12)
        self.assertAlmostEqual(frame.loc[3, "wf_energy"], 64.0, places=12)
        self.assertAlmostEqual(frame.loc[3, "wf_rms"], 2.0, places=12)
        self.assertAlmostEqual(frame.loc[3, "wf_crest_factor"], 4.0, places=12)
        self.assertAlmostEqual(frame.loc[3, "wf_peak_index"], 4.0, places=12)

    def test_hdf5_3d_waveform_channel_slicing_and_attribute_previews(self):
        path = self.root / "detector_events.h5"
        t = np.linspace(0.0, 1.0, 64, endpoint=False)
        waveforms = np.zeros((20, 3, 64), dtype=np.float64)
        for i in range(20):
            waveforms[i, 0, :] = (i + 1) * np.sin(2.0 * np.pi * 2.0 * t)
            waveforms[i, 1, :] = (i + 1) * np.cos(2.0 * np.pi * 5.0 * t)
            waveforms[i, 2, :] = float(i)
        wide_2d = np.ones((12, 200), dtype=np.float64)

        with h5py.File(path, "w") as handle:
            grp = handle.create_group("detector")
            ds = grp.create_dataset("waveforms", data=waveforms, chunks=(5, 1, 64))
            ds.attrs["sampling_rate_hz"] = 2000.0
            ds.attrs["detector_name"] = "calorimeter-A"
            ds.attrs["calibrated"] = np.bool_(True)
            ds.attrs["gains"] = np.array([1.0, 2.5, 5.0], dtype=np.float64)
            ds.attrs["non_finite_cal"] = np.nan
            ds.attrs["oversized_lut"] = np.arange(64, dtype=np.int32)
            grp.create_dataset("wide_pulses", data=wide_2d)

        info = browse({"path": str(path)}, limits({"limits": {}}))
        entries = {entry["key"]: entry for entry in info["entries"]}
        wf_entry = entries["/detector/waveforms"]
        self.assertEqual(wf_entry["shape"], [20, 3, 64])
        self.assertIn("/detector/waveforms@waveform", wf_entry["selectors"])
        self.assertIn("/detector/waveforms@channel:0@waveform", wf_entry["selectors"])
        self.assertIn("/detector/waveforms@channel:0", wf_entry["selectors"])
        self.assertEqual(wf_entry["attributes"]["sampling_rate_hz"]["value"], 2000.0)
        self.assertEqual(wf_entry["attributes"]["detector_name"]["value"], "calorimeter-A")
        self.assertIs(wf_entry["attributes"]["calibrated"]["value"], True)
        self.assertEqual(wf_entry["attributes"]["gains"]["value"], [1.0, 2.5, 5.0])
        self.assertIsNone(wf_entry["attributes"]["non_finite_cal"].get("value"))
        self.assertNotIn("value", wf_entry["attributes"]["oversized_lut"])

        wide_entry = entries["/detector/wide_pulses"]
        self.assertEqual(wide_entry["selectors"], ["/detector/wide_pulses@waveform"])

        # Multi-channel waveform extraction (3 channels * 15 features = 45 columns)
        all_ch_frame, all_meta, _, _ = self.read("detector_events.h5", key="/detector/waveforms@waveform", start=2, rowLimit=6)
        self.assertEqual(all_ch_frame.shape, (6, 45))
        self.assertEqual(all_meta["key"], "/detector/waveforms@waveform")
        self.assertAlmostEqual(all_ch_frame.loc[0, "ch0_dominant_bin"], 2.0, places=12)
        self.assertAlmostEqual(all_ch_frame.loc[0, "ch1_dominant_bin"], 5.0, places=12)
        self.assertAlmostEqual(all_ch_frame.loc[0, "ch2_mean"], 2.0, places=12)

        # Single-channel waveform extraction (15 features)
        ch1_frame, ch1_meta, _, _ = self.read("detector_events.h5", key="/detector/waveforms@channel:1@waveform", start=0, rowLimit=5)
        self.assertEqual(ch1_frame.shape, (5, 15))
        self.assertEqual(ch1_meta["key"], "/detector/waveforms@channel:1@waveform")
        self.assertNotEqual(all_meta["id"], ch1_meta["id"])

        # Single-channel raw sample slice (64 columns)
        raw_ch2_frame, _, _, _ = self.read("detector_events.h5", key="/detector/waveforms@channel:2", start=1, rowLimit=4)
        self.assertEqual(raw_ch2_frame.shape, (4, 64))
        self.assertEqual(raw_ch2_frame.columns[0], "ch2_sample_0")
        self.assertAlmostEqual(raw_ch2_frame.loc[0, "ch2_sample_0"], 1.0, places=12)

        # Wide 2D dataset (>128 columns) works with @waveform
        wide_wf_frame, _, _, _ = self.read("detector_events.h5", key="/detector/wide_pulses@waveform", start=0, rowLimit=5)
        self.assertEqual(wide_wf_frame.shape, (5, 15))
        self.assertAlmostEqual(wide_wf_frame.loc[0, "wf_mean"], 1.0, places=12)

    def test_waveform_and_selector_negative_controls(self):
        path = self.root / "neg_waveforms.h5"
        with h5py.File(path, "w") as handle:
            handle.create_dataset("wf3d", data=np.zeros((10, 2, 32), dtype=np.float64))
            handle.create_dataset("wf2d", data=np.zeros((10, 32), dtype=np.float64))
            bad = np.zeros((10, 2, 32), dtype=np.float64)
            bad[0, 0, 5] = np.nan
            handle.create_dataset("wf_nan", data=bad)
            handle.create_dataset("huge_3d", shape=(100_000_000, 4, 2048), chunks=(10, 1, 256), dtype="f8")

        for bad_key in (
            "/wf3d@unknown",
            "/wf3d@channel:-1",
            "/wf3d@channel:abc",
            "/wf3d@channel:2",
            "/wf3d@channel:99",
            "/wf2d@channel:0",
            "/wf_nan@waveform",
            "/wf3d\x00@waveform",
        ):
            with self.assertRaises(LabError, msg=f"Expected failure for {bad_key}"):
                self.read("neg_waveforms.h5", key=bad_key)

        # Logical huge 3D dataset succeeds on small bounded slice, refuses oversized slice before materialization
        small_slice, _, _, more = self.read("neg_waveforms.h5", key="/huge_3d@channel:0@waveform", start=0, rowLimit=5)
        self.assertTrue(more)
        self.assertEqual(small_slice.shape, (5, 15))
        with self.assertRaises(LabError):
            read_dataset(
                {"path": str(path), "key": "/huge_3d@waveform", "start": 0, "rowLimit": 50000},
                limits({"limits": {"memoryMb": 256, "maxRows": 100000}}),
            )


if __name__ == "__main__":
    unittest.main(verbosity=2)
