"""
test_validate_models.py
Tests for the validate_models.py script.

Uses synthetic (minimal) ONNX stubs — no GPU/download required in CI.
Tests the validation pipeline: graph check, manifest integrity, summary table.
"""

import json
import hashlib
import sys
from pathlib import Path
from unittest.mock import patch, MagicMock

import pytest

sys.path.insert(0, str(Path(__file__).parent.parent.parent / "factory"))

from validate_models import (
    check_manifest,
    build_manifest,         # if exported — otherwise test indirectly
    validate_model_dir,
)


class TestCheckManifest:
    def test_valid_manifest_returns_true(self, tmp_model_dir_with_manifest):
        result = check_manifest(tmp_model_dir_with_manifest)
        assert result is True

    def test_missing_manifest_returns_false(self, tmp_model_dir):
        # No manifest.json in this fixture
        result = check_manifest(tmp_model_dir)
        assert result is False

    def test_corrupt_checksums_returns_false(self, corrupt_manifest_dir):
        result = check_manifest(corrupt_manifest_dir)
        assert result is False

    def test_manifest_with_missing_file_returns_false(self, tmp_path):
        model_dir = tmp_path / "missing-model"
        model_dir.mkdir()
        # Manifest references a file that doesn't exist
        manifest = {
            "modelId": "ghost-model",
            "version": "1.0",
            "segments": [
                {
                    "fileName": "nonexistent.onnx",
                    "url": "models/ghost/nonexistent.onnx",
                    "sizeBytes": 1000,
                    "sha256": "a" * 64
                }
            ]
        }
        (model_dir / "manifest.json").write_text(json.dumps(manifest))

        result = check_manifest(model_dir)
        assert result is False


class TestValidateModelDir:
    def test_dir_without_onnx_file_is_invalid(self, tmp_path):
        model_dir = tmp_path / "empty-model"
        model_dir.mkdir()
        # No .onnx file

        result = validate_model_dir(model_dir)
        assert result["valid"] is False

    def test_valid_dir_returns_model_name(self, tmp_model_dir_with_manifest):
        # The ONNX stub won't pass actual ORT checks, but the dict structure is correct
        result = validate_model_dir(tmp_model_dir_with_manifest)

        assert "model" in result
        assert result["model"] == tmp_model_dir_with_manifest.name

    def test_result_contains_all_expected_keys(self, tmp_model_dir_with_manifest):
        result = validate_model_dir(tmp_model_dir_with_manifest)

        expected_keys = {"model", "file", "graph_valid", "session_loaded", "manifest_ok"}
        assert expected_keys.issubset(result.keys())


class TestSummaryOutput:
    def test_all_valid_results_exits_zero(self, capsys):
        from validate_models import print_summary

        results = [
            {"model": "phi4-mini-q4", "graph_valid": True, "session_loaded": True,
             "forward_ok": True, "manifest_ok": True, "latency_ms": 120.0, "valid": True},
            {"model": "qwen25-1b5-q4", "graph_valid": True, "session_loaded": True,
             "forward_ok": True, "manifest_ok": True, "latency_ms": 45.0, "valid": True},
        ]

        success = print_summary(results)
        assert success is True

    def test_any_failure_exits_nonzero(self, capsys):
        from validate_models import print_summary

        results = [
            {"model": "phi4-mini-q4", "graph_valid": False, "session_loaded": False,
             "forward_ok": False, "manifest_ok": False, "latency_ms": 0.0, "valid": False},
        ]

        success = print_summary(results)
        assert success is False
