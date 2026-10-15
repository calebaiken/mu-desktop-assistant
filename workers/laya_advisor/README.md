# Optional local LAYA task advisor

This worker uses [LAYA](https://github.com/NandhaKishorM/laya), a typed decision model, to suggest one of six ML task categories. It does not generate Mu's conversational replies, train a model, inspect datasets, approve permissions, or execute actions. Existing chat and ML controls work without it.

The reviewed optional runtime is Python 3.12, `laya==0.3.21`, CPU-only `torch==2.6.0+cpu`, and the exact dependencies and hashes in `../requirements-laya.lock`. The separate environment avoids changing the ML Lab's dependencies. The English checkpoint is `convaiinnovations/laya` at revision `55cf4c4ebb4ebe31b2550e8bdf3bd21b99753851`; `model-manifest.json` pins all five files by size and SHA256. We use safetensors, never a downloaded Python checkpoint or remote custom code. Upstream LAYA is Apache 2.0; its source and checkpoint remain subject to their published licenses.

Run `scripts/setup-laya.ps1` from the repository with a local Python 3.12 and `uv`. The setup downloads public package/model files without API credentials, verifies them, and writes `artifacts/laya-runtime.json`. A missing or modified pre-existing model file is not silently replaced. Setup requires disk headroom and preserves a 1 GiB reserve for each model download. Windows CPU PyTorch 2.6 was chosen because its wheel is substantially smaller than the tested 2.8 CPU wheel; the latter exhausted available space during copying. Setup uses hardlinks where possible and does not retain a package download cache.

After setup, inference is offline: Hugging Face offline flags and a Python network audit guard prevent online fallback. Only the user-supplied problem description is processed. The worker neither reads dataset content nor writes prompts, predictions, or telemetry to disk. All model assets are verified before importing the ML runtime and held under read-only Windows leases throughout inference. A modified checkpoint, missing dependency, unexpected file, link, or incompatible runtime is refused.

## Protocol and limits

The desktop starts `artifacts/laya-runtime/Scripts/python.exe -I -u workers/laya_advisor/worker.py`. Before sending input, it assigns the process to an owned Windows job with its memory limit and timeout. The worker imports only the standard library before admission. Send one UTF-8 JSON line followed by EOF:

```json
{"protocol":1,"id":"12345678-1234-4234-9234-123456789abc","prompt":"Find unusual detector events","modelDir":"C:\\path\\to\\Mu\\artifacts\\laya-model"}
```

The schema is closed, the request is bounded to 16 KiB, the prompt to 2,000 characters, and the checkpoint input to 512 tokens including the fixed rubric. Inference uses two CPU threads, no GPU, no compilation, and no action hooks. The worker emits exactly one bounded response line:

Before native imports, the worker requires at least 1.5 GiB available physical RAM and 3 GiB available Windows commit. It refuses admission with an explanatory error under pressure. The checkpoint's float16 parameters are constructed directly in half precision and CPU float16 autocast handles inference. This avoids a second full float32 parameter allocation. Transformers 4.51.3 is pinned because its configuration loading honors the scoped default dtype for this checkpoint; the worker also verifies the resulting parameter dtypes. Scores can differ from float32 execution and are not calibrated. This is an optional memory-conscious inference policy, not model training or quantization.

It separately requires 2.5 GiB available per-process commit inside the desktop's
3 GiB job. Windows system commit comes from `GetPerformanceInfo`; the process
headroom comes from `GlobalMemoryStatusEx`, so the job limit is not misreported as
global memory. Missing or invalid memory telemetry refuses admission. The actual
2026-09-30 production-host/native-view run declined with `memory_limit` in 1.65 s
and produced no advice. Full checkpoint inference remains unqualified despite the
verified installation and passing tiny-model precision check.

```json
{"protocol":1,"id":"12345678-1234-4234-9234-123456789abc","ok":true,"result":{"category":"anomaly","confidence":0.8,"probabilities":{"regression":0.04,"classification":0.04,"clustering":0.04,"reduction":0.04,"anomaly":0.8,"other":0.04},"truncated":false,"modelRevision":"55cf4c4ebb4ebe31b2550e8bdf3bd21b99753851","elapsedSeconds":1.0,"warnings":["Illustrative response only; actual warnings explain calibration and scope."]}}
```

Failures return `ok:false,error:{code,message}` with fixed, helpful messages and no raw input/exception logging. `confidence` is the selected category's upstream score, not a measured probability of being correct. Six upstream scores are rounded to four decimals, so their sum may differ slightly from one. Model action probabilities are intentionally ignored. Every successful response warns that this checkpoint is not calibrated for Mu's scientific tasks and cannot establish scientific validity.

Long prompts may be truncated even below the character limit because the rubric shares the token budget. The `truncated` flag is calculated with the same upstream sequence builder used for prediction; the UI must not silently use truncated advice. Classification is advisory and the user can override it. There is no claim of domain accuracy without a separate held-out evaluation; testing protocol correctness or several sample prompts does not establish it.

## Maintenance

Any checkpoint/runtime update requires a new reviewed manifest and dependency lock, source review of its local loading path, offline subprocess tests, resource measurements, and a representative domain evaluation. Do not widen deserialization trust or remove hash checks to make a different model load. Keep scientific conclusions and execution authority in Mu's existing validated action system.

The standard-library contract/security suite runs with `python -I -m unittest discover -s tests/laya -v`. The small native precision-policy check runs with `artifacts/laya-runtime/Scripts/python.exe -I -m unittest discover -s tests/laya_precision -v`; it constructs a tiny model from a fixed seed without a network or downloaded weights. Neither suite proves that the full checkpoint can answer real prompts within the desktop job's memory/time budget. That requires a separate admitted full-model run and receipt. Resource refusals must remain failures or explicit skips in such a receipt, never reported as successful inference.
