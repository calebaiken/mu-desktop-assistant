"""Reviewer-owned exact audio arithmetic tests; no inference/import/download."""
import struct
import unittest

from inspect_public_samples import EXPECTED_RATE, inspect_wave


def wave(samples=(1, -32768, 32767, 0), *, channels=1, rate=EXPECTED_RATE, bits=16,
         data=None, extra=b""):
    pcm = struct.pack("<" + "h" * len(samples), *samples) if data is None else data
    alignment = channels * bits // 8
    form = struct.pack("<4sIHHIIHH", b"fmt ", 16, 1, channels, rate,
                       rate * alignment, alignment, bits)
    body = form + extra + struct.pack("<4sI", b"data", len(pcm)) + pcm
    return b"RIFF" + struct.pack("<I", len(body) + 4) + b"WAVE" + body


class IndependentWaveTests(unittest.TestCase):
    def test_known_signed_pcm_and_independent_duration(self):
        result = inspect_wave(wave())
        self.assertEqual(result["dataBytes"], 8)
        self.assertEqual(result["frames"], 4)
        self.assertEqual(result["durationSeconds"], 1 / 6000)
        self.assertEqual((result["minimumSample"], result["maximumSample"]), (-32768, 32767))
        self.assertEqual(result["endpointSamples"], 2)
        self.assertEqual(result["nonzeroSamples"], 3)

    def test_exact_thirty_seconds_and_next_frame(self):
        exact = wave(data=b"\x01\x00" * (EXPECTED_RATE * 30))
        self.assertEqual(inspect_wave(exact)["durationSeconds"], 30)
        with self.assertRaisesRegex(ValueError, "duration_limit"):
            inspect_wave(wave(data=b"\x01\x00" * (EXPECTED_RATE * 30 + 1)))

    def test_malformed_riff_and_declared_chunk_lengths(self):
        valid = wave()
        cases = [valid[:43], b"NOPE" + valid[4:], valid[:8] + b"NOPE" + valid[12:],
                 valid[:4] + struct.pack("<I", 0xFFFFFFFF) + valid[8:], valid + b"\x00",
                 valid[:40] + struct.pack("<I", 0xFFFFFFFF) + valid[44:]]
        for raw in cases:
            with self.subTest(length=len(raw)), self.assertRaises(ValueError):
                inspect_wave(raw)

    def test_wrong_rate_channels_bits_or_format_arithmetic(self):
        for arguments in [{"rate": 16000}, {"channels": 2}, {"bits": 32}]:
            with self.subTest(arguments=arguments), self.assertRaisesRegex(ValueError, "pcm_format"):
                inspect_wave(wave(**arguments))
        valid = bytearray(wave())
        struct.pack_into("<I", valid, 28, EXPECTED_RATE)
        with self.assertRaisesRegex(ValueError, "pcm_format"):
            inspect_wave(valid)

    def test_silence_empty_and_partial_sample_fail(self):
        for pcm in [b"", b"\x00\x00" * 10, b"\x01"]:
            with self.subTest(pcm=pcm), self.assertRaises(ValueError):
                inspect_wave(wave(data=pcm))

    def test_valid_odd_chunk_padding_and_chunk_bounds(self):
        extra = b"JUNK" + struct.pack("<I", 1) + b"X\x00"
        result = inspect_wave(wave(extra=extra))
        self.assertEqual(result["chunks"][1], {"id": "JUNK", "offset": 36, "length": 1})
        valid = wave()
        for extra in [b"fmt " + struct.pack("<I", 0), b"data" + struct.pack("<I", 0),
                      (b"JUNK" + struct.pack("<I", 0)) * 31,
                      b"\x00BAD" + struct.pack("<I", 0)]:
            with self.subTest(extra=extra[:8]), self.assertRaises(ValueError):
                inspect_wave(wave(extra=extra))
        with self.assertRaises(ValueError):
            inspect_wave(valid + b"JUNK")

    def test_data_before_format_fails(self):
        body = b"data" + struct.pack("<I", 4) + b"\x01\x00\x02\x00"
        body += wave()[12:36]
        raw = b"RIFF" + struct.pack("<I", len(body) + 4) + b"WAVE" + body
        with self.assertRaisesRegex(ValueError, "data_order_alignment"):
            inspect_wave(raw)


if __name__ == "__main__":
    unittest.main()
