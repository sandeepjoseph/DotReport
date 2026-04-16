"""
conftest.py — Pytest fixtures for DotReport Model Factory tests.
No real models are downloaded in CI — all tests use synthetic ONNX stubs.
"""

import json
import hashlib
import struct
import pytest
from pathlib import Path


# ── Minimal valid ONNX binary ─────────────────────────────────────────────────
# An ONNX file is a protobuf-encoded ModelProto.
# This is the smallest valid IR-version-8 ONNX graph (single Add node).
_MINIMAL_ONNX_BYTES = bytes([
    0x08, 0x08,             # field 1 (ir_version) = 8
    0x12, 0x09,             # field 2 (opset_import), length 9
    0x0A, 0x00,             # domain = "" (default)
    0x10, 0x13,             # version = 19
    0x00, 0x00, 0x00,       # padding
])


@pytest.fixture
def tmp_model_dir(tmp_path: Path) -> Path:
    """A temp directory pre-populated with a minimal ONNX model + manifest."""
    model_dir = tmp_path / "test-model-q4"
    model_dir.mkdir()

    # Write minimal ONNX file
    onnx_file = model_dir / "model.onnx"
    onnx_file.write_bytes(_MINIMAL_ONNX_BYTES)

    # Write tokenizer config
    tokenizer = model_dir / "tokenizer_config.json"
    tokenizer.write_text(json.dumps({"model_type": "phi", "bos_token": "<|endoftext|>"}))

    return model_dir


@pytest.fixture
def tmp_model_dir_with_manifest(tmp_model_dir: Path) -> Path:
    """tmp_model_dir extended with a valid manifest.json."""
    onnx_bytes = (tmp_model_dir / "model.onnx").read_bytes()
    sha256 = hashlib.sha256(onnx_bytes).hexdigest()

    manifest = {
        "modelId": "test-model-q4",
        "version": "test-1.0",
        "segments": [
            {
                "fileName": "model.onnx",
                "url": "models/test-model-q4/model.onnx",
                "sizeBytes": len(onnx_bytes),
                "sha256": sha256
            },
            {
                "fileName": "tokenizer_config.json",
                "url": "models/test-model-q4/tokenizer_config.json",
                "sizeBytes": (tmp_model_dir / "tokenizer_config.json").stat().st_size,
                "sha256": hashlib.sha256(
                    (tmp_model_dir / "tokenizer_config.json").read_bytes()
                ).hexdigest()
            }
        ]
    }
    (tmp_model_dir / "manifest.json").write_text(json.dumps(manifest))
    return tmp_model_dir


@pytest.fixture
def corrupt_manifest_dir(tmp_model_dir: Path) -> Path:
    """tmp_model_dir with a manifest containing a wrong checksum."""
    manifest = {
        "modelId": "corrupt-model",
        "version": "bad",
        "segments": [
            {
                "fileName": "model.onnx",
                "url": "models/corrupt/model.onnx",
                "sizeBytes": 100,
                "sha256": "0" * 64  # deliberately wrong
            }
        ]
    }
    (tmp_model_dir / "manifest.json").write_text(json.dumps(manifest))
    return tmp_model_dir
