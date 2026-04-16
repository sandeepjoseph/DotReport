"""
quantize_phi4.py
Phase 1 — Model Factory: Download and quantize Microsoft Phi-4 Mini to 4-bit ONNX.

Target:
    - Model:   microsoft/Phi-4-mini-instruct (3.8B)
    - Format:  4-bit GPTQ → ONNX (FP16/INT4 mixed)
    - Output:  ./output/phi4-mini-q4/

Usage:
    python factory/quantize_phi4.py [--output ./output] [--calibration-samples 128]
"""

import argparse
import json
import os
import hashlib
from pathlib import Path

import torch
from datasets import load_dataset
from optimum.onnxruntime import ORTModelForCausalLM
from transformers import AutoTokenizer, AutoModelForCausalLM, GPTQConfig
from rich.console import Console
from rich.progress import Progress, SpinnerColumn, TextColumn, BarColumn

console = Console()
MODEL_ID = "microsoft/Phi-4-mini-instruct"


def parse_args():
    p = argparse.ArgumentParser(description="Quantize Phi-4 Mini to 4-bit ONNX")
    p.add_argument("--output", default="./output", help="Output directory")
    p.add_argument("--calibration-samples", type=int, default=128)
    p.add_argument("--bits", type=int, default=4)
    p.add_argument("--group-size", type=int, default=128)
    p.add_argument("--skip-hf-download", action="store_true",
                   help="Skip HuggingFace download (model already cached)")
    return p.parse_args()


def load_calibration_dataset(tokenizer, n_samples: int):
    console.log("[dim]Loading calibration dataset (wikitext-2)...[/dim]")
    dataset = load_dataset("wikitext", "wikitext-2-raw-v1", split="train")
    texts = [t for t in dataset["text"] if len(t.strip()) > 64][:n_samples]
    return texts


def quantize_with_gptq(model_id: str, output_dir: Path, args):
    console.rule("[bold]PHASE 1A — GPTQ Quantization[/bold]")

    tokenizer = AutoTokenizer.from_pretrained(model_id, trust_remote_code=True)
    calibration_data = load_calibration_dataset(tokenizer, args.calibration_samples)

    gptq_config = GPTQConfig(
        bits=args.bits,
        dataset=calibration_data,
        tokenizer=tokenizer,
        group_size=args.group_size,
        desc_act=False,   # faster inference, slight accuracy trade-off acceptable
    )

    console.log(f"[bold]Loading {model_id}...[/bold] (this may take 10–20 min on first run)")
    model = AutoModelForCausalLM.from_pretrained(
        model_id,
        quantization_config=gptq_config,
        device_map="auto",
        torch_dtype=torch.float16,
        trust_remote_code=True,
    )

    gptq_dir = output_dir / "phi4-mini-gptq"
    gptq_dir.mkdir(parents=True, exist_ok=True)
    model.save_pretrained(gptq_dir)
    tokenizer.save_pretrained(gptq_dir)
    console.log(f"[green]GPTQ model saved → {gptq_dir}[/green]")
    return gptq_dir


def export_to_onnx(gptq_dir: Path, output_dir: Path):
    console.rule("[bold]PHASE 1B — ONNX Export[/bold]")

    onnx_dir = output_dir / "phi4-mini-q4"
    onnx_dir.mkdir(parents=True, exist_ok=True)

    console.log("Exporting to ONNX via Optimum...")
    ort_model = ORTModelForCausalLM.from_pretrained(
        gptq_dir,
        export=True,
        provider="CPUExecutionProvider",   # export on CPU, run on WebGPU
    )
    ort_model.save_pretrained(onnx_dir)

    tokenizer = AutoTokenizer.from_pretrained(gptq_dir)
    tokenizer.save_pretrained(onnx_dir)

    console.log(f"[green]ONNX model saved → {onnx_dir}[/green]")
    return onnx_dir


def build_manifest(onnx_dir: Path):
    """
    Build a manifest.json that the Blazor ModelOrchestrator reads
    to know which segments to download and their checksums.
    """
    console.rule("[bold]PHASE 1C — Manifest Generation[/bold]")

    segments = []
    for f in sorted(onnx_dir.iterdir()):
        if f.is_file() and f.suffix in {".onnx", ".json", ".model", ".bin"}:
            sha256 = hashlib.sha256(f.read_bytes()).hexdigest()
            segments.append({
                "fileName": f.name,
                "url":      f"models/phi4-mini-q4/{f.name}",
                "sizeBytes": f.stat().st_size,
                "sha256":    sha256
            })

    manifest = {
        "modelId":  "phi4-mini-q4",
        "version":  "2026-A",
        "segments": segments
    }

    manifest_path = onnx_dir / "manifest.json"
    manifest_path.write_text(json.dumps(manifest, indent=2))
    console.log(f"[green]Manifest → {manifest_path}[/green]")
    console.log(f"  Total segments: {len(segments)}")
    total_mb = sum(s["sizeBytes"] for s in segments) / (1024 * 1024)
    console.log(f"  Total size:     {total_mb:.1f} MB")


def main():
    args = parse_args()
    output_dir = Path(args.output)

    console.rule("[bold cyan]DotReport EdgeCore — Phi-4 Mini Quantization[/bold cyan]")
    console.print(f"Model:       {MODEL_ID}")
    console.print(f"Bits:        {args.bits}-bit GPTQ")
    console.print(f"Group size:  {args.group_size}")
    console.print(f"Cal samples: {args.calibration_samples}")
    console.print(f"Output:      {output_dir.resolve()}")
    console.print()

    gptq_dir = quantize_with_gptq(MODEL_ID, output_dir, args)
    onnx_dir = export_to_onnx(gptq_dir, output_dir)
    build_manifest(onnx_dir)

    console.rule("[bold green]COMPLETE[/bold green]")
    console.print(f"[bold]Output ready → {onnx_dir}[/bold]")
    console.print("Copy the phi4-mini-q4/ folder to DotReport.Client/wwwroot/models/")


if __name__ == "__main__":
    main()
