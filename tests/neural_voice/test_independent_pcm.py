"""Reviewer exact conversion oracles using real NumPy, no ONNX model inference."""
import importlib.util
from pathlib import Path
import struct
import sys
import unittest

import numpy as np

SOURCE = Path(__file__).resolve().parents[2] / "workers/neural_speech"
sys.path.insert(0, str(SOURCE))
specification = importlib.util.spec_from_file_location("pcm_worker", SOURCE / "worker.py")
worker = importlib.util.module_from_spec(specification)
specification.loader.exec_module(worker)
import assets
from inspect_public_samples import inspect_wave


class IndependentPcmTests(unittest.TestCase):
    def test_known_clip_round_signed_extrema_and_byte_order(self):
        raw = worker.samples_to_wave(np.array([-2, -1, -.5, 0, .5, 1, 2], dtype=np.float32))
        self.assertEqual(raw[44:], struct.pack("<7h", -32767, -32767, -16384, 0, 16384, 32767, 32767))
        result = inspect_wave(raw)
        self.assertEqual(result["frames"], 7)
        self.assertEqual(result["durationSeconds"], 7 / 24000)

    def test_exact_thirty_second_boundary_and_next_sample(self):
        raw = worker.samples_to_wave(np.ones(720000, dtype=np.float32))
        self.assertEqual(len(raw), 1440044)
        self.assertEqual(inspect_wave(raw)["durationSeconds"], 30)
        with self.assertRaises(assets.SpeechError) as caught:
            worker.samples_to_wave(np.ones(720001, dtype=np.float32))
        self.assertEqual(caught.exception.code, "audio_limit")

    def test_nonfinite_empty_silence_shape_and_nonnumeric_rejected(self):
        for value in (np.array([1, np.nan]), np.array([np.inf]), np.array([-np.inf]), np.array([]),
                      np.zeros(10), np.array([1e-10]), np.ones((2, 2)), np.array([1j]), np.array(["one"]), np.array([True])):
            with self.subTest(dtype=str(value.dtype), shape=value.shape), self.assertRaises(assets.SpeechError) as caught:
                worker.samples_to_wave(value)
            self.assertEqual(caught.exception.code, "audio_limit")


if __name__ == "__main__":
    unittest.main()
