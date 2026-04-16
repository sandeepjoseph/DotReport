"""
test_export_onnx.py
Tests for export_onnx.py manifest generation and directory structure.

Model download is mocked — tests are safe to run in CI without GPU.
"""

import json
import hashlib
import sys
from pathlib import Path
from unittest.mock import patch, MagicMock

import pytest

sys.path.insert(0, str(Path(__file__).parent.parent.parent / "factory"))


class TestBuildManifest:
    """Tests the build_manifest() function from export_onnx.py."""

    def test_build_manifest_creates_json_file(self, tmp_model_dir):
        from export_onnx import build_manifest

        build_manifest(tmp_model_dir, "test-model-q4", "microsoft/Phi-4-mini-instruct")

        assert (tmp_model_dir / "manifest.json").exists()

    def test_build_manifest_includes_onnx_file(self, tmp_model_dir):
        from export_onnx import build_manifest

        build_manifest(tmp_model_dir, "test-model-q4", "microsoft/Phi-4-mini-instruct")

        manifest = json.loads((tmp_model_dir / "manifest.json").read_text())
        names = [s["fileName"] for s in manifest["segments"]]
        assert "model.onnx" in names

    def test_build_manifest_sha256_is_correct(self, tmp_model_dir):
        from export_onnx import build_manifest

        build_manifest(tmp_model_dir, "test-model-q4", "microsoft/Phi-4-mini-instruct")

        manifest = json.loads((tmp_model_dir / "manifest.json").read_text())
        onnx_seg = next(s for s in manifest["segments"] if s["fileName"] == "model.onnx")
        actual   = hashlib.sha256((tmp_model_dir / "model.onnx").read_bytes()).hexdigest()

        assert onnx_seg["sha256"] == actual

    def test_build_manifest_sets_source_id(self, tmp_model_dir):
        from export_onnx import build_manifest

        model_id = "microsoft/Phi-4-mini-instruct"
        build_manifest(tmp_model_dir, "phi4-mini-q4", model_id)

        manifest = json.loads((tmp_model_dir / "manifest.json").read_text())
        assert manifest.get("sourceId") == model_id

    def test_build_manifest_version_is_dev_export(self, tmp_model_dir):
        from export_onnx import build_manifest

        build_manifest(tmp_model_dir, "phi4-mini-q4", "microsoft/Phi-4-mini-instruct")

        manifest = json.loads((tmp_model_dir / "manifest.json").read_text())
        assert manifest["version"] == "dev-export"

    def test_build_manifest_url_uses_slug(self, tmp_model_dir):
        from export_onnx import build_manifest

        slug = "phi4-mini-q4"
        build_manifest(tmp_model_dir, slug, "microsoft/Phi-4-mini-instruct")

        manifest = json.loads((tmp_model_dir / "manifest.json").read_text())
        for seg in manifest["segments"]:
            assert slug in seg["url"], f"URL must contain slug: {seg['url']}"


class TestModelSlugMap:
    def test_phi4_maps_to_correct_slug(self):
        from export_onnx import MODEL_SLUG_MAP

        assert MODEL_SLUG_MAP["microsoft/Phi-4-mini-instruct"] == "phi4-mini-q4"

    def test_qwen_maps_to_correct_slug(self):
        from export_onnx import MODEL_SLUG_MAP

        assert MODEL_SLUG_MAP["Qwen/Qwen2.5-1.5B-Instruct"] == "qwen25-1b5-q4"


class TestRequirements:
    def test_requirements_file_exists(self):
        req = Path(__file__).parent.parent.parent / "factory" / "requirements.txt"
        assert req.exists(), "requirements.txt must exist"

    def test_requirements_contains_core_packages(self):
        req_path = Path(__file__).parent.parent.parent / "factory" / "requirements.txt"
        content  = req_path.read_text()

        for pkg in ["torch", "transformers", "onnx", "optimum", "auto-gptq"]:
            assert pkg in content, f"requirements.txt missing: {pkg}"
