"""Small native-runtime tests; run with the optional LAYA environment.

These verify the numeric execution policy, not pretrained model performance.
No network or model assets are required.
"""
import importlib.util
import unittest


@unittest.skipUnless(importlib.util.find_spec("laya"), "Optional LAYA runtime is not installed")
class RuntimePrecisionTests(unittest.TestCase):
    def test_checkpoint_config_policy_builds_half_parameters_and_finite_cpu_output(self):
        import torch
        from transformers import AutoModel, ModernBertConfig
        from laya.common import DecisionModel

        self.assertEqual(torch.__version__, "2.6.0+cpu")
        torch.set_num_threads(2)
        torch.manual_seed(42)
        previous_dtype = torch.get_default_dtype()
        try:
            torch.set_default_dtype(torch.float16)
            # The real pinned encoder config stores dtype, not torch_dtype.
            config = ModernBertConfig(vocab_size=32, hidden_size=32, intermediate_size=64,
                                      num_hidden_layers=2, num_attention_heads=4,
                                      local_attention=8, reference_compile=False,
                                      pad_token_id=0, dtype="float32")
            encoder = AutoModel.from_config(config, attn_implementation="sdpa")
            model = DecisionModel(encoder, head_layers=1, n_act=2).eval()
        finally:
            torch.set_default_dtype(previous_dtype)
        self.assertTrue(all(parameter.dtype == torch.float16 for parameter in model.parameters()))
        self.assertEqual(torch.get_default_dtype(), previous_dtype)
        with torch.inference_mode(), torch.autocast(device_type="cpu", dtype=torch.float16):
            logits, action_logits = model(torch.tensor([[1, 2, 3, 4]]),
                                          torch.ones((1, 4), dtype=torch.long),
                                          torch.tensor([[1, 2]]),
                                          torch.ones((1, 2), dtype=torch.bool),
                                          torch.tensor([0]))
        self.assertTrue(torch.isfinite(logits).all().item())
        self.assertTrue(torch.isfinite(action_logits).all().item())
        self.assertEqual(tuple(logits.shape), (1, 2))


if __name__ == "__main__":
    unittest.main()
