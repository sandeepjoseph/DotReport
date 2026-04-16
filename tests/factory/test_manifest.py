"""
test_manifest.py
Tests for manifest.json generation in export_onnx.py and quantize_*.py.

Validates:
  - Manifest structure matches what the Blazor ModelOrchestrator expects
  - SHA-256 checksums are correct
  - All segment file sizes are accurate
  - No segment URLs point to absolute paths (must be relative)
"""

import json
import hashlib
import sys
from pathlib import Path

import pytest

# Ensure factory/ is importable
sys.path.insert(0, str(Path(__file__).parent.parent.parent / "factory"))


class TestManifestStructure:
    def test_manifest_has_required_top_level_keys(self, tmp_model_dir_with_manifest):
        manifest = json.loads((tmp_model_dir_with_manifest / "manifest.json").read_text())

        assert "modelId"  in manifest
        assert "version"  in manifest
        assert "segments" in manifest

    def test_manifest_segments_is_non_empty_list(self, tmp_model_dir_with_manifest):
        manifest = json.loads((tmp_model_dir_with_manifest / "manifest.json").read_text())

        assert isinstance(manifest["segments"], list)
        assert len(manifest["segments"]) > 0

    def test_each_segment_has_required_keys(self, tmp_model_dir_with_manifest):
        manifest = json.loads((tmp_model_dir_with_manifest / "manifest.json").read_text())
        required = {"fileName", "url", "sizeBytes", "sha256"}

        for seg in manifest["segments"]:
            assert required.issubset(seg.keys()), \
                f"Segment missing keys: {required - seg.keys()}"

    def test_segment_urls_are_relative_not_absolute(self, tmp_model_dir_with_manifest):
        manifest = json.loads((tmp_model_dir_with_manifest / "manifest.json").read_text())

        for seg in manifest["segments"]:
            assert not seg["url"].startswith("/"), \
                f"URL must be relative, got: {seg['url']}"
            assert not seg["url"].startswith("http"), \
                f"URL must not be absolute http, got: {seg['url']}"

    def test_segment_sha256_matches_actual_file(self, tmp_model_dir_with_manifest):
        manifest = json.loads((tmp_model_dir_with_manifest / "manifest.json").read_text())

        for seg in manifest["segments"]:
            file_path = tmp_model_dir_with_manifest / seg["fileName"]
            actual_sha = hashlib.sha256(file_path.read_bytes()).hexdigest()
            assert actual_sha == seg["sha256"], \
                f"SHA-256 mismatch for {seg['fileName']}"

    def test_segment_size_bytes_matches_actual_file(self, tmp_model_dir_with_manifest):
        manifest = json.loads((tmp_model_dir_with_manifest / "manifest.json").read_text())

        for seg in manifest["segments"]:
            file_path = tmp_model_dir_with_manifest / seg["fileName"]
            actual_size = file_path.stat().st_size
            assert actual_size == seg["sizeBytes"], \
                f"Size mismatch for {seg['fileName']}: expected {seg['sizeBytes']}, got {actual_size}"

    def test_manifest_model_id_is_non_empty_string(self, tmp_model_dir_with_manifest):
        manifest = json.loads((tmp_model_dir_with_manifest / "manifest.json").read_text())

        assert isinstance(manifest["modelId"], str)
        assert len(manifest["modelId"]) > 0

    def test_manifest_version_is_non_empty_string(self, tmp_model_dir_with_manifest):
        manifest = json.loads((tmp_model_dir_with_manifest / "manifest.json").read_text())

        assert isinstance(manifest["version"], str)
        assert len(manifest["version"]) > 0


class TestManifestChecksumIntegrity:
    def test_corrupt_sha256_is_detectable(self, corrupt_manifest_dir):
        manifest = json.loads((corrupt_manifest_dir / "manifest.json").read_text())
        seg = manifest["segments"][0]
        file_path = corrupt_manifest_dir / seg["fileName"]

        actual_sha = hashlib.sha256(file_path.read_bytes()).hexdigest()
        assert actual_sha != seg["sha256"], "Corrupt manifest must have wrong SHA"

    def test_all_onnx_files_are_included_in_manifest(self, tmp_model_dir_with_manifest):
        manifest   = json.loads((tmp_model_dir_with_manifest / "manifest.json").read_text())
        onnx_files = {f.name for f in tmp_model_dir_with_manifest.glob("*.onnx")}
        seg_files  = {s["fileName"] for s in manifest["segments"]}

        assert onnx_files.issubset(seg_files), \
            f"ONNX files not in manifest: {onnx_files - seg_files}"
