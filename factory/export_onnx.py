"""
export_onnx.py
Phase 1 — Direct ONNX export without GPTQ (for testing / CI).

Exports the base FP16 model to ONNX for rapid iteration.
Use quantize_phi4.py / quantize_qwen.py for production 4-bit build.

Usage:
    python factory/export_onnx.py --model microsoft/Phi-4-mini-instruct
    python factory/export_onnx.py --model Qwen/Qwen2.5-1.5B-Instruct
"""

import argparse
import json
import hashlib
from pathlib import Path

from optimum.onnxruntime import ORTModelForCausalLM
from transformers import AutoTokenizer
from rich.console import Console

console = Console()

MODEL_SLUG_MAP = {
    "microsoft/Phi-4-mini-instruct": "phi4-mini-q4",
    "Qwen/Qwen2.5-1.5B-Instruct":   "qwen25-1b5-q4",
}


def parse_args():
    p = argparse.ArgumentParser(description="Export model to ONNX (FP16, no quantization)")
    p.add_argument("--model",  required=True, help="HuggingFace model ID")
    p.add_argument("--output", default="./output", help="Output base directory")
    p.add_argument("--optimize", action="store_true", help="Apply ORT graph optimizations")
    return p.parse_args()


def export(model_id: str, output_dir: Path, optimize: bool):
    slug     = MODEL_SLUG_MAP.get(model_id, model_id.split("/")[-1].lower())
    onnx_dir = output_dir / slug
    onnx_dir.mkdir(parents=True, exist_ok=True)

    console.rule(f"[bold]Exporting {model_id} → ONNX[/bold]")

    ort_model = ORTModelForCausalLM.from_pretrained(
        model_id,
        export=True,
        provider="CPUExecutionProvider",
        optimize_model=optimize,
    )
    ort_model.save_pretrained(onnx_dir)

    tokenizer = AutoTokenizer.from_pretrained(model_id, trust_remote_code=True)
    tokenizer.save_pretrained(onnx_dir)

    console.log(f"[green]Exported → {onnx_dir}[/green]")
    return onnx_dir, slug


def build_manifest(onnx_dir: Path, slug: str, model_id: str):
    segments = []
    for f in sorted(onnx_dir.iterdir()):
        if f.is_file() and f.suffix in {".onnx", ".json", ".model", ".bin"}:
            sha256 = hashlib.sha256(f.read_bytes()).hexdigest()
            segments.append({
                "fileName": f.name,
                "url":      f"models/{slug}/{f.name}",
                "sizeBytes": f.stat().st_size,
                "sha256":    sha256
            })

    manifest = {
        "modelId":  slug,
        "version":  "dev-export",
        "sourceId": model_id,
        "segments": segments
    }

    manifest_path = onnx_dir / "manifest.json"
    manifest_path.write_text(json.dumps(manifest, indent=2))
    console.log(f"[green]Manifest → {manifest_path} ({len(segments)} segments)[/green]")


def main():
    args = parse_args()
    output_dir = Path(args.output)

    onnx_dir, slug = export(args.model, output_dir, args.optimize)
    build_manifest(onnx_dir, slug, args.model)

    console.print(f"\n[bold]Copy to:[/bold] DotReport.Client/wwwroot/models/{slug}/")


if __name__ == "__main__":
    main()
