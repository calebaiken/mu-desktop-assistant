# Independent QA Review — ML Lab HDF5 Attribute Previews, 3D Channel Slicing & Waveform Feature Extraction

```text
QA REVIEW
Phase: ML Lab — HDF5 Attribute Preview, Channel/Waveform Selector Parsing & 15-Feature Waveform Extraction (Branch feat/ml-lab-hdf5-waveform)
Overall Score: 9.0 / 10 (Raw: 90 / 100 = 9.0000 / 10; threshold strictly > 8.5 / 10)
Result: PASS

Category Scores:
- Code Correctness: 9 / 10 (Raw: 1 + 2 + 2 + 2 + 2 = 9)
- Architecture: 10 / 10 (Raw: 2 + 2 + 2 + 2 + 2 = 10)
- Mathematical Correctness: 10 / 10 (Raw: 2 + 2 + 2 + 2 + 2 = 10)
- Testing: 9 / 10 (Raw: 2 + 2 + 2 + 2 + 1 = 9)
- Reliability: 8 / 10 (Raw: 2 + 2 + 1 + 2 + 1 = 8)
- Security: 10 / 10 (Raw: 2 + 2 + 2 + 2 + 2 = 10)
- Performance: 9 / 10 (Raw: 2 + 2 + 2 + 2 + 1 = 9)
- Maintainability: 9 / 10 (Raw: 1 + 2 + 2 + 2 + 2 = 9)
- UX/UI: 8 / 10 (Raw: 2 + 1 + 2 + 2 + 1 = 8)
- Requirement Compliance: 8 / 10 (Raw: 2 + 2 + 1 + 2 + 1 = 8)
```

## 1. Scope and Verification Receipts

- **Branch:** `feat/ml-lab-hdf5-waveform` (base `main` @ `7fe3305b49f755a94d65362692e783ef0e60035a`)
- **Reviewed Implementation Commit:** `aaa1d80137ab3a70a5ebde2d4f640b872b963d19` (with post-review refinements for exact-zero crossings, NPZ browse header inspection, and Action v7 artifact upload)
- **GitHub Actions Verification Run:** [`38836917330`](https://github.com/calebaiken/mu-desktop-assistant/actions/runs/38836917330) (Job `116767994418`, `windows-latest`, status `completed`, conclusion `success`)
  - `scripts/verify.ps1`: Release build `0 Warning(s), 0 Error(s)`; `11/11` architecture checks passed; `398/398` .NET tests in `Companion.Tests.dll` passed (`0` failed, `0` skipped); composed WPF startup/shutdown smoke passed.
  - `scripts/verify-negative.ps1`: `9/9` adversarial architecture controls failed as expected.
  - `tests/ml_lab/test_independent_*.py`: `54/54` tests passed.
  - `workers/ml_lab/test_author.py`: `7/7` tests passed.
  - `scripts/verify-ml-lab.ps1 -SkipBuild`: `64/64` native WPF `MlLabWindow` smoke assertions passed.

## 2. Delivered Capabilities

1. **Bounded HDF5 Attribute Value Previews & NPZ Header Browsing (`workers/ml_lab/data.py`):**
   - `_safe_attr_value` inspects low-level `h5py` attribute IDs (`shape`, `dtype`, `check_dtype(ref=...)`, `check_dtype(vlen=...)`, and `get_storage_size() <= 256`) before materializing values, exposing small scalar and 1D attributes (`<= 16` elements) in `browse()` while sanitizing `NaN`/`Inf` to `None`.
   - `browse()` surfaces recommended `selectors` (`@waveform`, `@channel:<index>@waveform`, `@channel:<index>`) on 3D `(events, channels, samples)` and wide 2D (`> 128` samples) datasets in both HDF5 and NPZ containers.
2. **Deterministic 15-Feature Waveform Extraction & 3D Channel Slicing (`workers/ml_lab/data.py`, `workers/ml_lab/engine.py`):**
   - `parse_dataset_key` and `format_canonical_key` support `@waveform`, `@channel:<index>`, and `@channel:<index>@waveform` selectors while preserving strict rejection of unconfigured 3D arrays and `> 128`-column raw 2D arrays.
   - `_extract_waveform_features` extracts 15 time- and frequency-domain features per channel (`mean`, `std`, `min`, `max`, `ptp`, `rms`, `energy`, `abs_mean`, `median`, `iqr`, `zero_crossings`, `crest_factor`, `peak_index`, `spectral_centroid`, `dominant_bin`) with zero-signal and zero-spectrum guards and exact standalone reproduction script (`_EXPORT_BODY`) parity.
   - Single-channel HDF5 reads slice only the requested channel hyperslab (`array[start:start + take, channel_index, :]`) after checking pre-materialization memory bounds.
3. **Native WPF Structure Selection Auto-Fill (`src/Companion.Desktop/MlLabWindow.xaml.cs`, `MlLabSmokeChecks.cs`):**
   - Selecting a 3D or wide 2D waveform dataset in `StructureGrid` auto-populates the primary recommended selector into `DatasetKey`.

## 3. Top 5 Ranked Improvements (Post-Review Disposition)

1. **GitHub Actions `@v7`/`@v6` actions and artifact upload:** Restored `actions/checkout@v7`, `actions/setup-dotnet@v6`, `actions/setup-python@v7`, and `actions/upload-artifact@v7` in `.github/workflows/verify.yml`.
2. **Exact-zero sample handling in `zero_crossings`:** Updated `_extract_waveform_features` (`data.py`) and `_extract_wf` (`engine.py`) to count sign inversions across non-zero samples per row and added an exact-zero crossing oracle test in `tests/ml_lab/test_independent_data.py`.
3. **NPZ `browse()` shape/dtype/selector inspection:** Added bounded `.npy` header inspection inside `browse()` for `.npz` archives and verified in `tests/ml_lab/test_independent_data.py`.
4. **UI multi-selector picker (Future Increment):** Add a dedicated selector dropdown next to `DatasetKey` when multiple channel selectors exist.
5. **Single-source waveform helper injection into `_EXPORT_BODY` (Future Increment):** Share the pure NumPy helper source string directly between `data.py` and `engine.py`.
