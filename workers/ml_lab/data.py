"""Bounded, non-destructive data readers and sample-based inspection."""
from __future__ import annotations

import hashlib
import csv
import io
import itertools
import json
import math
from pathlib import Path
import zipfile

import numpy as np
import pandas as pd

from common import MAX_COLUMNS, MAX_TEXT, bounded_int, clean, fail, local_path

FORMATS = {".csv", ".tsv", ".json", ".jsonl", ".npy", ".npz", ".h5", ".hdf5", ".parquet", ".xlsx"}
MAX_CHANNELS = 8
MAX_WAVEFORM_SAMPLES = 16384
WAVEFORM_STAT_NAMES = (
    "mean", "std", "min", "max", "ptp", "rms", "energy", "abs_mean",
    "median", "iqr", "zero_crossings", "crest_factor", "peak_index",
    "spectral_centroid", "dominant_bin",
)


def parse_dataset_key(raw_key):
    """Parse optional @channel:<index> and @waveform suffixes from a dataset key."""
    if raw_key is None:
        return None, None, False
    if not isinstance(raw_key, str) or len(raw_key) > 1024 or any(ord(c) < 32 for c in raw_key):
        fail("Dataset key must be a valid string of at most 1024 characters without control characters.")
    text = raw_key.strip()
    if not text:
        return None, None, False
    waveform_mode = False
    channel_index = None
    if text.endswith("@waveform"):
        waveform_mode = True
        text = text[:-len("@waveform")]
    if "@channel:" in text:
        base, _, ch_str = text.rpartition("@channel:")
        if not ch_str.isdigit():
            fail("Channel selector must be a non-negative integer in @channel:<index>.")
        channel_index = int(ch_str)
        if channel_index < 0 or channel_index >= MAX_CHANNELS:
            fail(f"Channel selector must be between 0 and {MAX_CHANNELS - 1}.", "resource_limit")
        text = base
    if "@" in text:
        fail("Unsupported dataset key selector. Use @waveform, @channel:<index> or @channel:<index>@waveform.")
    base_key = text if text else None
    return base_key, channel_index, waveform_mode


def format_canonical_key(base_key, channel_index, waveform_mode):
    suffix = ""
    if channel_index is not None:
        suffix += f"@channel:{channel_index}"
    if waveform_mode:
        suffix += "@waveform"
    if not suffix:
        return base_key
    return (base_key or "") + suffix


def _extract_waveform_features(matrix, prefix: str = "wf") -> pd.DataFrame:
    arr = np.asarray(matrix)
    if arr.dtype.hasobject or arr.dtype.kind not in "biuf":
        fail("Waveform extraction requires numeric arrays.")
    if arr.ndim != 2:
        fail("Waveform feature extraction requires a 2D (events, samples) slice.")
    n_rows, n_samples = arr.shape
    if n_samples < 1 or n_samples > MAX_WAVEFORM_SAMPLES:
        fail(f"Waveform sample length must be between 1 and {MAX_WAVEFORM_SAMPLES}.", "resource_limit")
    data = arr.astype(np.float64, copy=False)
    if not np.all(np.isfinite(data)):
        fail("Waveform samples contain NaN or infinite values; clean or filter invalid samples before feature extraction.")
    mean = np.mean(data, axis=1)
    std = np.std(data, axis=1, ddof=0)
    minimum = np.min(data, axis=1)
    maximum = np.max(data, axis=1)
    ptp = maximum - minimum
    sq = np.square(data)
    energy = np.sum(sq, axis=1)
    rms = np.sqrt(energy / n_samples)
    abs_data = np.abs(data)
    abs_mean = np.mean(abs_data, axis=1)
    median = np.median(data, axis=1)
    q25, q75 = np.percentile(data, [25.0, 75.0], axis=1)
    iqr = q75 - q25
    if n_samples >= 2:
        signs = np.sign(data)
        zero_crossings = np.sum(signs[:, 1:] * signs[:, :-1] < 0, axis=1).astype(np.float64)
    else:
        zero_crossings = np.zeros(n_rows, dtype=np.float64)
    peak_abs = np.max(abs_data, axis=1)
    safe_rms = np.where(rms > 0.0, rms, 1.0)
    crest_factor = np.where(rms > 0.0, peak_abs / safe_rms, 0.0)
    peak_index = np.argmax(abs_data, axis=1).astype(np.float64)
    centered = data - mean[:, None]
    spectrum = np.abs(np.fft.rfft(centered, axis=1))
    bins = np.arange(spectrum.shape[1], dtype=np.float64)
    spec_sum = np.sum(spectrum, axis=1)
    safe_spec_sum = np.where(spec_sum > 0.0, spec_sum, 1.0)
    spectral_centroid = np.where(spec_sum > 0.0, np.sum(spectrum * bins[None, :], axis=1) / safe_spec_sum, 0.0)
    if spectrum.shape[1] > 1:
        ac_mag = spectrum[:, 1:]
        ac_max = np.max(ac_mag, axis=1)
        dominant_bin = np.where(ac_max > 0.0, (np.argmax(ac_mag, axis=1) + 1).astype(np.float64), 0.0)
    else:
        dominant_bin = np.zeros(n_rows, dtype=np.float64)
    columns = {
        f"{prefix}_mean": mean,
        f"{prefix}_std": std,
        f"{prefix}_min": minimum,
        f"{prefix}_max": maximum,
        f"{prefix}_ptp": ptp,
        f"{prefix}_rms": rms,
        f"{prefix}_energy": energy,
        f"{prefix}_abs_mean": abs_mean,
        f"{prefix}_median": median,
        f"{prefix}_iqr": iqr,
        f"{prefix}_zero_crossings": zero_crossings,
        f"{prefix}_crest_factor": crest_factor,
        f"{prefix}_peak_index": peak_index,
        f"{prefix}_spectral_centroid": spectral_centroid,
        f"{prefix}_dominant_bin": dominant_bin,
    }
    return pd.DataFrame(columns)


def _csv_frame(path, delimiter, start, count, budget):
    """Bound physical lines, logical records and retained bytes before pandas parses."""
    class Lines:
        def __init__(self, stream):
            self.stream = stream
            self.record_bytes = 0
            self.total_bytes = 0
        def __iter__(self):
            return self
        def __next__(self):
            line = self.stream.readline(65537)
            if not line:
                raise StopIteration
            size = len(line.encode("utf-8"))
            self.record_bytes += size
            self.total_bytes += size
            if len(line) > 65536 or self.record_bytes > 1024 * 1024 or self.total_bytes > max(64 * 1024 * 1024, budget // 4):
                fail("CSV scan exceeds the bounded line/record/byte budget. Choose a smaller selection or convert to NumPy/HDF5.", "resource_limit")
            return line
    rows = []
    retained_bytes = 0
    with path.open(encoding="utf-8-sig", newline="") as stream:
        lines = Lines(stream)
        reader = csv.reader(lines, delimiter=delimiter, strict=True)
        try:
            header = next(reader)
        except StopIteration:
            fail("CSV has no header row.")
        if len(header) > MAX_COLUMNS or len(set(header)) != len(header):
            fail("CSV columns must be unique and within the 128-column limit.")
        lines.record_bytes = 0
        scanned = 0
        for row in reader:
            lines.record_bytes = 0
            if not row:
                continue
            if len(row) != len(header):
                fail("CSV rows have inconsistent column counts.")
            if any(len(value) > 4096 for value in row):
                fail("A CSV cell exceeds 4096 characters.", "resource_limit")
            if scanned >= start:
                retained_bytes += sum(len(value.encode("utf-8")) for value in row)
                if retained_bytes * 20 > budget:
                    fail("CSV decoded selection exceeds the RAM estimate. Select fewer rows.", "resource_limit")
                rows.append(row)
            scanned += 1
            if len(rows) > count:
                break
    buffer = io.StringIO(newline="")
    writer = csv.writer(buffer, delimiter=delimiter)
    writer.writerow(header)
    writer.writerows(rows[:count])
    buffer.seek(0)
    return pd.read_csv(buffer, sep=delimiter, low_memory=False), len(rows) > count, None if len(rows) > count else scanned


def source_identity(path: Path) -> dict:
    """Bounded fingerprint: full SHA256 for <=16MiB, sampled SHA256 otherwise."""
    stat = path.stat()
    digest = hashlib.sha256()
    with path.open("rb") as stream:
        if stat.st_size <= 16 * 1024 * 1024:
            for block in iter(lambda: stream.read(1024 * 1024), b""):
                digest.update(block)
            kind = "sha256-full"
        else:
            for offset in sorted({0, max(0, stat.st_size // 2 - 524288), max(0, stat.st_size - 1048576)}):
                stream.seek(offset)
                digest.update(str(offset).encode("ascii"))
                digest.update(stream.read(1048576))
            digest.update(str(stat.st_size).encode("ascii"))
            digest.update(str(stat.st_mtime_ns).encode("ascii"))
            kind = "sha256-sampled-size-mtime"
    return {"fileSize": stat.st_size, "modifiedAt": stat.st_mtime_ns, "fingerprint": digest.hexdigest(), "fingerprintKind": kind}


def _array_frame(array, start: int, count: int, budget: int, columns=None, *, channel_index=None, waveform_mode: bool = False, pre_sliced_channel: bool = False):
    if array.dtype.hasobject or array.dtype.kind not in "biufSU":
        fail("Arrays must contain numeric, boolean or fixed-width text values; object/pickle arrays are refused.")
    ndim = len(array.shape)
    if channel_index is not None or waveform_mode:
        if array.dtype.kind not in "biuf":
            fail("Waveform feature extraction and channel slicing require numeric arrays.")
        if pre_sliced_channel:
            if ndim != 2:
                fail("Pre-sliced channel data must be two-dimensional (events, samples).")
            nsamples = array.shape[1]
            if nsamples < 1 or nsamples > MAX_WAVEFORM_SAMPLES:
                fail(f"Waveform sample length must be between 1 and {MAX_WAVEFORM_SAMPLES}.", "resource_limit")
            if count * nsamples * max(8, array.dtype.itemsize) * 8 > budget:
                fail("This waveform selection exceeds the RAM estimate. Reduce the selected row range.", "resource_limit")
            selected = np.asarray(array[start:start + count])
            if len(selected) == 0:
                return pd.DataFrame(columns=[f"ch{channel_index}_{name}" for name in WAVEFORM_STAT_NAMES] if waveform_mode else [f"ch{channel_index}_sample_{i}" for i in range(nsamples)])
            if waveform_mode:
                return _extract_waveform_features(selected, prefix=f"ch{channel_index}")
            if nsamples > MAX_COLUMNS:
                fail(f"Channel slice has {nsamples} samples, exceeding {MAX_COLUMNS} columns. Append @waveform to extract summary features.", "resource_limit")
            return pd.DataFrame(selected, columns=[f"ch{channel_index}_sample_{i}" for i in range(nsamples)])
        if ndim == 3:
            nchannels, nsamples = array.shape[1], array.shape[2]
            if nchannels < 1 or nchannels > MAX_CHANNELS:
                fail(f"3D waveform datasets support 1 to {MAX_CHANNELS} channels.", "resource_limit")
            if nsamples < 1 or nsamples > MAX_WAVEFORM_SAMPLES:
                fail(f"Waveform sample length must be between 1 and {MAX_WAVEFORM_SAMPLES}.", "resource_limit")
            if channel_index is not None:
                if channel_index < 0 or channel_index >= nchannels:
                    fail(f"Channel index {channel_index} is out of bounds for dataset with {nchannels} channels.")
                if count * nsamples * max(8, array.dtype.itemsize) * 8 > budget:
                    fail("This channel selection exceeds the RAM estimate. Reduce the selected row range.", "resource_limit")
                selected = np.asarray(array[start:start + count, channel_index, :])
                if len(selected) == 0:
                    return pd.DataFrame(columns=[f"ch{channel_index}_{name}" for name in WAVEFORM_STAT_NAMES] if waveform_mode else [f"ch{channel_index}_sample_{i}" for i in range(nsamples)])
                if waveform_mode:
                    return _extract_waveform_features(selected, prefix=f"ch{channel_index}")
                if nsamples > MAX_COLUMNS:
                    fail(f"Channel slice has {nsamples} samples, exceeding {MAX_COLUMNS} columns. Append @waveform to extract summary features.", "resource_limit")
                return pd.DataFrame(selected, columns=[f"ch{channel_index}_sample_{i}" for i in range(nsamples)])
            if count * nchannels * nsamples * max(8, array.dtype.itemsize) * 8 > budget:
                fail("This multi-channel waveform selection exceeds the RAM estimate. Select a single channel or fewer rows.", "resource_limit")
            selected = np.asarray(array[start:start + count])
            if len(selected) == 0:
                return pd.DataFrame(columns=[f"ch{ch}_{name}" for ch in range(nchannels) for name in WAVEFORM_STAT_NAMES])
            frames = [_extract_waveform_features(selected[:, ch, :], prefix=f"ch{ch}") for ch in range(nchannels)]
            return pd.concat(frames, axis=1)
        if ndim in (1, 2):
            if channel_index is not None:
                fail("Channel selector @channel:<index> requires a 3D (events, channels, samples) dataset.")
            nsamples = 1 if ndim == 1 else array.shape[1]
            if nsamples < 1 or nsamples > MAX_WAVEFORM_SAMPLES:
                fail(f"Waveform sample length must be between 1 and {MAX_WAVEFORM_SAMPLES}.", "resource_limit")
            if count * nsamples * max(8, array.dtype.itemsize) * 8 > budget:
                fail("This waveform selection exceeds the RAM estimate. Reduce the selected row range.", "resource_limit")
            selected = np.asarray(array[start:start + count])
            if ndim == 1:
                selected = selected.reshape(-1, 1)
            if len(selected) == 0:
                return pd.DataFrame(columns=[f"wf_{name}" for name in WAVEFORM_STAT_NAMES])
            return _extract_waveform_features(selected, prefix="wf")
        fail("Select a 1D, 2D or 3D (events, channels, samples) array.")
    if ndim not in (1, 2):
        fail("Select a one- or two-dimensional array, or append @waveform / @channel:<index> for 3D waveform datasets.")
    ncols = 1 if ndim == 1 else array.shape[1]
    if ncols < 1 or ncols > MAX_COLUMNS:
        fail(f"Select at most {MAX_COLUMNS} columns, or append @waveform to extract summary features.", "resource_limit")
    if count * max(1, ncols) * max(8, array.dtype.itemsize) * 8 > budget:
        fail("This array selection exceeds the RAM estimate. Reduce the selected row range.", "resource_limit")
    selected = array[start:start + count]
    if ndim == 1:
        selected = selected.reshape(-1, 1)
    if columns is None:
        columns = [f"feature_{index}" for index in range(ncols)]
    return pd.DataFrame(np.asarray(selected), columns=columns)


def _validate_frame(frame: pd.DataFrame, budget: int):
    if frame.shape[1] < 1 or frame.shape[1] > MAX_COLUMNS:
        fail(f"Dataset must have 1 to {MAX_COLUMNS} columns.", "resource_limit")
    names = [str(c) for c in frame.columns]
    if len(set(names)) != len(names) or any(not c or len(c) > 120 or any(ord(x) < 32 for x in c) for c in names):
        fail("Column names must be unique, nonempty and at most 120 characters without control characters.")
    frame.columns = names
    if frame.memory_usage(deep=True).sum() * 8 > budget:
        fail("The decoded dataset exceeds the working-memory estimate. Select fewer rows.", "resource_limit")
    for name in frame.select_dtypes(include=["object", "string"]).columns:
        values = frame[name].dropna()
        if any(not isinstance(x, (str, bool, int, float)) for x in values):
            fail("Nested JSON objects and array-valued cells are not supported in tabular training.")
        if any(isinstance(x, str) and len(x) > 4096 for x in values):
            fail("A text cell exceeds 4096 characters. Select a table with shorter scalar values.", "resource_limit")
    return frame


def _h5_dataset(handle, key):
    import h5py
    if not isinstance(key, str) or not key or len(key) > 1024:
        fail("Choose an HDF5 dataset key from Browse before importing.")
    current = handle
    for part in key.strip("/").split("/"):
        if part in ("", ".", "..") or not isinstance(current, h5py.Group):
            fail("Choose a valid HDF5 dataset key.")
        link = current.get(part, getlink=True)
        if not isinstance(link, h5py.HardLink):
            fail("HDF5 external and symbolic links are not followed.", "unsafe_path")
        current = current[part]
    if not isinstance(current, h5py.Dataset):
        fail("The selected HDF5 item is a group, not a dataset.")
    if current.is_virtual or current.external:
        fail("Virtual and externally stored HDF5 datasets are not admitted.", "unsafe_path")
    return current


def _admit_npz_array(path, key, budget, *, channel_index=None, waveform_mode: bool = False):
    """Read a bounded .npy header before np.load may allocate its declared shape."""
    if path.stat().st_size * 8 > budget:
        fail("NPZ archive size exceeds the RAM estimate. Use memory-mapped NPY or sliced HDF5.", "resource_limit")
    with zipfile.ZipFile(path) as archive:
        entries = archive.infolist()
        if len(entries) > 128 or sum(x.file_size for x in entries) * 8 > budget:
            fail("NPZ decompression exceeds the RAM estimate; use NPY or HDF5.", "resource_limit")
        if any(x.flag_bits & 1 for x in entries):
            fail("Encrypted NPZ files are not supported.")
        arrays = {entry.filename.removesuffix(".npy"): entry for entry in entries if entry.filename.endswith(".npy")}
        if len(arrays) != len(entries):
            fail("NPZ must contain only uniquely named NumPy arrays.")
        selected = key or (next(iter(arrays)) if len(arrays) == 1 else None)
        if selected not in arrays:
            fail("Choose an array key from the NPZ file using Browse.")
        entry = arrays[selected]
        with archive.open(entry) as stream:
            version = np.lib.format.read_magic(stream)
            if version == (1, 0):
                shape, _, dtype = np.lib.format.read_array_header_1_0(stream, max_header_size=10000)
            elif version == (2, 0):
                shape, _, dtype = np.lib.format.read_array_header_2_0(stream, max_header_size=10000)
            else:
                fail("This NPZ header version is not supported. Save numeric arrays in standard NPY or HDF5 format.")
            if dtype.hasobject:
                fail("NPZ arrays must be bounded numeric/fixed-text arrays; object arrays are refused.")
            if channel_index is not None or waveform_mode:
                if dtype.kind not in "biuf" or len(shape) not in (1, 2, 3):
                    fail("NPZ waveform/channel selection requires a 1D, 2D or 3D numeric array.")
                if len(shape) == 3 and (shape[1] < 1 or shape[1] > MAX_CHANNELS or shape[2] < 1 or shape[2] > MAX_WAVEFORM_SAMPLES):
                    fail("NPZ 3D array exceeds channel or sample bounds.", "resource_limit")
                if len(shape) == 2 and (shape[1] < 1 or shape[1] > MAX_WAVEFORM_SAMPLES):
                    fail("NPZ 2D waveform array exceeds sample bounds.", "resource_limit")
            else:
                if len(shape) not in (1, 2) or (len(shape) == 2 and shape[1] > MAX_COLUMNS):
                    fail("NPZ arrays must be bounded numeric/fixed-text one- or two-dimensional tables.")
            elements = math.prod(shape)
            size = elements * dtype.itemsize
            if size * 8 > budget or stream.tell() + size > entry.file_size:
                fail("The NPZ array's declared shape exceeds its file or memory bounds.", "resource_limit")
    return selected


def read_dataset(spec: dict, resource: dict, *, preview=False):
    if not isinstance(spec, dict):
        fail("Dataset selection must be an object.")
    path = local_path(spec.get("path"))
    suffix = path.suffix.lower()
    if suffix not in FORMATS:
        fail("Unsupported format. Choose CSV, TSV, JSON, JSONL, NPY, NPZ, HDF5, Parquet or XLSX.", "unsupported_format")
    requested = bounded_int(spec.get("rowLimit"), resource["max_rows"], 1, resource["max_rows"], "rowLimit")
    count = min(requested, resource["preview_rows"]) if preview else requested
    start = bounded_int(spec.get("start"), 0, 0, 2_000_000_000, "start")
    raw_key = spec.get("key")
    key = raw_key
    budget = resource["memory_mb"] * 1024 * 1024
    before = source_identity(path)
    # The worker job has an external hard cap too. This is a conservative admission
    # estimate, not a promise that arbitrary parser inputs cannot allocate memory.
    shape = None
    total_rows = None
    warnings = []
    if suffix in (".csv", ".tsv"):
        delimiter = "\t" if suffix == ".tsv" else ","
        frame, more, total_rows = _csv_frame(path, delimiter, start, count, budget)
        warnings.append("Preview and diagnostics use the selected leading row range; they are not a random or representative sample.")
    elif suffix in (".json", ".jsonl"):
        if suffix == ".json":
            if path.stat().st_size * 20 > budget or path.stat().st_size > 16 * 1024 * 1024:
                fail("JSON requires full parsing. Use JSONL or a smaller file for bounded reads.", "resource_limit")
            with path.open(encoding="utf-8-sig") as stream:
                records = json.load(stream)
            if not isinstance(records, list):
                fail("JSON must contain an array of row objects.")
            total_rows = len(records)
            frame = pd.DataFrame(records[start:start + count])
            more = total_rows > start + count
        else:
            records = []
            scanned = 0
            retained_bytes = 0
            with path.open(encoding="utf-8-sig") as stream:
                while True:
                    line = stream.readline(65537)
                    if not line:
                        total_rows = scanned
                        break
                    if len(line) > 65536:
                        fail("A JSONL record exceeds 64 KiB.", "resource_limit")
                    if not line.strip():
                        continue
                    if scanned >= start:
                        retained_bytes += len(line.encode("utf-8"))
                        if retained_bytes * 20 > budget:
                            fail("JSONL selection exceeds the RAM estimate. Select fewer rows.", "resource_limit")
                        record = json.loads(line)
                        if not isinstance(record, dict):
                            fail("JSONL records must be row objects.")
                        records.append(record)
                    scanned += 1
                    if len(records) > count:
                        break
            more = len(records) > count
            frame = pd.DataFrame(records[:count])
    elif suffix == ".npy":
        base_key, channel_index, waveform_mode = parse_dataset_key(raw_key)
        if base_key is not None:
            fail("NPY files contain a single array; leave key empty or use @waveform / @channel:<index>.")
        key = format_canonical_key(None, channel_index, waveform_mode)
        array = np.load(path, mmap_mode="r", allow_pickle=False, max_header_size=10000)
        shape = list(array.shape)
        total_rows = shape[0] if shape else 0
        frame = _array_frame(array, start, count, budget, channel_index=channel_index, waveform_mode=waveform_mode)
        more = total_rows > start + count
        if waveform_mode:
            warnings.append("Waveform mode extracted 15 time- and frequency-domain summary features per channel.")
    elif suffix == ".npz":
        base_key, channel_index, waveform_mode = parse_dataset_key(raw_key)
        selected = _admit_npz_array(path, base_key, budget, channel_index=channel_index, waveform_mode=waveform_mode)
        with np.load(path, allow_pickle=False, max_header_size=10000) as archive:
            array = archive[selected]
            key = format_canonical_key(selected, channel_index, waveform_mode)
            shape = list(array.shape)
            total_rows = shape[0] if shape else 0
            frame = _array_frame(array, start, count, budget, channel_index=channel_index, waveform_mode=waveform_mode)
            more = total_rows > start + count
            if waveform_mode:
                warnings.append("Waveform mode extracted 15 time- and frequency-domain summary features per channel.")
    elif suffix in (".h5", ".hdf5"):
        import h5py
        base_key, channel_index, waveform_mode = parse_dataset_key(raw_key)
        key = format_canonical_key(base_key, channel_index, waveform_mode)
        with h5py.File(path, "r") as handle:
            array = _h5_dataset(handle, base_key)
            if array.chunks and math.prod(array.chunks) * array.dtype.itemsize * 4 > budget:
                fail("An HDF5 storage chunk exceeds the memory estimate even for this small slice. Rechunk the source externally.", "resource_limit")
            shape = list(array.shape)
            total_rows = shape[0] if shape else 0
            if len(shape) == 3 and channel_index is not None:
                nchannels, nsamples = shape[1], shape[2]
                if nchannels < 1 or nchannels > MAX_CHANNELS:
                    fail(f"3D waveform datasets support 1 to {MAX_CHANNELS} channels.", "resource_limit")
                if channel_index < 0 or channel_index >= nchannels:
                    fail(f"Channel index {channel_index} is out of bounds for dataset with {nchannels} channels.")
                if nsamples < 1 or nsamples > MAX_WAVEFORM_SAMPLES:
                    fail(f"Waveform sample length must be between 1 and {MAX_WAVEFORM_SAMPLES}.", "resource_limit")
                if count * nsamples * max(8, array.dtype.itemsize) * 8 > budget:
                    fail("This channel selection exceeds the RAM estimate. Reduce the selected row range.", "resource_limit")
                take = max(0, min(count, total_rows - start))
                sliced = array[start:start + take, channel_index, :] if take > 0 else np.empty((0, nsamples), dtype=array.dtype)
                frame = _array_frame(sliced, 0, take, budget, channel_index=channel_index, waveform_mode=waveform_mode, pre_sliced_channel=True)
            else:
                # Validate shape/type/memory before slicing; never coerce the whole dataset.
                frame = _array_frame(array, start, count, budget, channel_index=channel_index, waveform_mode=waveform_mode)
            more = total_rows > start + count
            if waveform_mode:
                warnings.append("Waveform mode extracted 15 time- and frequency-domain summary features per channel.")
    elif suffix == ".parquet":
        try:
            import pyarrow.parquet as parquet
        except ImportError:
            fail("Parquet requires optional pyarrow. CSV, JSONL, NumPy and HDF5 remain available.", "missing_dependency")
        source = parquet.ParquetFile(path)
        total_rows = source.metadata.num_rows
        batches = []
        scanned = 0
        for batch in source.iter_batches(batch_size=min(count, 1000)):
            end = scanned + batch.num_rows
            if end > start:
                batches.append(batch.slice(max(0, start - scanned), max(0, min(end, start + count) - max(start, scanned))).to_pandas())
            scanned = end
            if scanned >= start + count:
                break
        frame = pd.concat(batches, ignore_index=True) if batches else pd.DataFrame()
        more = total_rows > start + count
    else:
        if path.stat().st_size * 30 > budget or path.stat().st_size > 16 * 1024 * 1024:
            fail("Excel parsing exceeds the RAM estimate. Export the selected worksheet to CSV.", "resource_limit")
        try:
            frame = pd.read_excel(path, sheet_name=key or 0, skiprows=range(1, start + 1), nrows=count + 1)
        except ImportError:
            fail("Excel requires optional openpyxl. Export the worksheet to CSV.", "missing_dependency")
        more = len(frame) > count
        frame = frame.iloc[:count].copy()
    _validate_frame(frame, budget)
    if frame.empty:
        fail("The selected row range is empty.")
    after = source_identity(path)
    if before != after:
        fail("The source changed while it was being read. Import it again.", "source_changed")
    identity = hashlib.sha256((str(path) + json.dumps([key, start, requested], separators=(",", ":")) + before["fingerprint"]).encode()).hexdigest()
    metadata = {"id": identity[:32], "name": path.stem, "path": str(path), "format": suffix[1:], **before,
                "version": before["fingerprint"][:16], "rows": total_rows, "columns": len(frame.columns),
                "shape": shape or ([total_rows, len(frame.columns)] if total_rows is not None else None),
                "selectedRows": len(frame), "start": start, "key": key,
                "rowLimit": requested, "memoryBytes": int(frame.memory_usage(deep=True).sum())}
    if more:
        warnings.append(f"Only {len(frame)} selected rows starting at {start} were materialized. Remaining rows were not used.")
    if before["fingerprintKind"] != "sha256-full":
        warnings.append("Large-file identity uses sampled SHA256 plus size and modification time, not a complete content hash.")
    return frame.reset_index(drop=True), metadata, warnings, bool(more)


def _safe_attr_value(item, attr_name, aid):
    """Extract small scalar or 1D attribute values only after low-level shape/dtype admission."""
    import h5py
    shape = aid.shape
    dtype = aid.dtype
    if shape is None or len(shape) > 1:
        return None
    elements = math.prod(shape) if shape else 1
    if elements < 1 or elements > 16:
        return None
    if h5py.check_dtype(ref=dtype) is not None:
        return None
    vlen_type = h5py.check_dtype(vlen=dtype)
    if vlen_type is not None and vlen_type not in (str, bytes):
        return None
    if dtype.hasobject and vlen_type not in (str, bytes):
        return None
    try:
        storage_size = aid.get_storage_size()
    except Exception:
        storage_size = 0
    if storage_size > 256:
        return None
    if not dtype.hasobject and elements * dtype.itemsize > 256:
        return None
    try:
        raw = item.attrs[attr_name]
    except Exception:
        return None
    def _format_scalar(val):
        if isinstance(val, (bytes, np.bytes_)):
            return val.decode("utf-8", errors="replace")[:120]
        if isinstance(val, str):
            return val[:120]
        if isinstance(val, (bool, np.bool_)):
            return bool(val)
        if isinstance(val, (int, np.integer)):
            return int(val)
        if isinstance(val, (float, np.floating)):
            fval = float(val)
            return fval if math.isfinite(fval) else None
        return str(val)[:120]
    if shape == ():
        if isinstance(raw, np.ndarray) and raw.shape == ():
            return _format_scalar(raw.item())
        return _format_scalar(raw)
    arr = np.asarray(raw)
    if arr.ndim != 1 or len(arr) > 16:
        return None
    return [_format_scalar(x) for x in arr.tolist()]


def _recommended_selectors(key: str, shape):
    if not shape:
        return []
    if len(shape) == 3:
        nchannels, nsamples = shape[1], shape[2]
        if 1 <= nchannels <= MAX_CHANNELS and 1 <= nsamples <= MAX_WAVEFORM_SAMPLES:
            selectors = [f"{key}@waveform"]
            for ch in range(min(nchannels, 4)):
                selectors.append(f"{key}@channel:{ch}@waveform")
            if nsamples <= MAX_COLUMNS:
                selectors.append(f"{key}@channel:0")
            return selectors
    elif len(shape) == 2 and MAX_COLUMNS < shape[1] <= MAX_WAVEFORM_SAMPLES:
        return [f"{key}@waveform"]
    return []


def browse(spec: dict, resource: dict):
    path = local_path(spec.get("path"))
    entries = []
    warnings = []
    if path.suffix.lower() == ".npz":
        if path.stat().st_size * 8 > resource["memory_mb"] * 1024 * 1024:
            fail("NPZ archive size exceeds the browser admission limit. Use NPY or HDF5 for large arrays.", "resource_limit")
        with zipfile.ZipFile(path) as archive:
            for entry in archive.infolist()[:128]:
                entries.append({"key": entry.filename.removesuffix(".npy"), "kind": "array", "shape": None,
                                "dtype": None, "attributes": {"uncompressedBytes": entry.file_size}, "selectors": []})
        return {"path": str(path), "entries": entries, "warnings": warnings}
    if path.suffix.lower() not in (".h5", ".hdf5"):
        fail("Browse is available for HDF5 and NPZ containers.")
    import h5py
    with h5py.File(path, "r") as handle:
        def visit(group, prefix, depth):
            if depth > 12 or len(entries) >= 256:
                return
            for name in group:
                if len(entries) >= 256:
                    break
                key = prefix + "/" + name
                link = group.get(name, getlink=True)
                if not isinstance(link, h5py.HardLink):
                    entries.append({"key": key, "kind": "link-refused", "shape": None, "dtype": None, "attributes": {}, "selectors": []})
                    continue
                item = group[name]
                is_dataset = isinstance(item, h5py.Dataset)
                attrs = {}
                for attr_name in itertools.islice(item.attrs, 16):
                    # Attribute identifiers/shapes are inspected first; only bounded scalar/1D
                    # attributes (<=16 elements, <=256 bytes, no refs/objects) are materialized.
                    aid = item.attrs.get_id(attr_name)
                    try:
                        attr_meta = {"shape": list(aid.shape) if aid.shape is not None else None, "dtype": str(aid.dtype)}
                        val = _safe_attr_value(item, attr_name, aid)
                        if val is not None:
                            attr_meta["value"] = val
                        attrs[str(attr_name)[:120]] = attr_meta
                    finally:
                        aid.close()
                item_shape = list(item.shape) if is_dataset and item.shape is not None else None
                entries.append({"key": key, "kind": "dataset" if is_dataset else "group",
                                "shape": item_shape,
                                "dtype": str(item.dtype) if is_dataset else None,
                                "attributes": attrs,
                                "selectors": _recommended_selectors(key, item_shape) if is_dataset else []})
                if not is_dataset:
                    visit(item, key, depth + 1)
        visit(handle, "", 0)
    if len(entries) >= 256:
        warnings.append("Container browser stops after 256 entries and 12 levels.")
    return {"path": str(path), "entries": entries, "warnings": warnings}


def inspect_frame(frame: pd.DataFrame):
    features = []
    warnings = []
    for name in frame.columns:
        series = frame[name]
        unique = int(series.nunique(dropna=True))
        missing = int(series.isna().sum())
        numeric = pd.api.types.is_numeric_dtype(series) and not pd.api.types.is_bool_dtype(series)
        kind = "numeric" if numeric else "boolean" if pd.api.types.is_bool_dtype(series) else "datetime" if pd.api.types.is_datetime64_any_dtype(series) else "categorical"
        possible_id = unique == len(frame) and (name.lower().endswith("id") or name.lower() in ("index", "event", "event_number"))
        stats = {}
        if numeric:
            numbers = series.replace([np.inf, -np.inf], np.nan).dropna()
            q = numbers.quantile([.25, .5, .75])
            if len(numbers):
                iqr = q.iloc[2] - q.iloc[0]
                stats = {"min": numbers.min(), "max": numbers.max(), "mean": numbers.mean(), "std": numbers.std(),
                         "median": q.iloc[1], "variance": numbers.var(), "skewness": numbers.skew(),
                         "outlierCountIqr": int(((numbers < q.iloc[0] - 1.5 * iqr) | (numbers > q.iloc[2] + 1.5 * iqr)).sum()),
                         "nonFinite": int(np.isinf(series.to_numpy(dtype=float, na_value=np.nan)).sum())}
        else:
            stats = {"valueCounts": {str(k)[:MAX_TEXT]: int(v) for k, v in series.value_counts(dropna=False).head(12).items()}}
        top_share = float(series.value_counts(normalize=True, dropna=False).iloc[0]) if len(series) else 0
        features.append({"name": name, "type": kind, "missing": missing, "unique": unique, "constant": unique <= 1,
                         "nearConstant": top_share >= .99, "identifier": possible_id, "statistics": clean(stats)})
        if possible_id:
            warnings.append(f"{name} looks like an identifier. Review whether it belongs in the feature set; it was not removed.")
        if not numeric and unique > 100:
            warnings.append(f"{name} has high cardinality ({unique} sampled values). Encoding may consume substantial RAM.")
        if unique <= 1:
            warnings.append(f"{name} is constant in the selected sample.")
    duplicates = int(frame.duplicated().sum())
    if duplicates:
        warnings.append(f"The selected sample contains {duplicates} duplicate rows. None were removed.")
    preview = [{k: (str(v)[:MAX_TEXT] if isinstance(v, str) else v) for k, v in row.items()}
               for row in frame.head(30).to_dict(orient="records")]
    return features, clean(preview), warnings
