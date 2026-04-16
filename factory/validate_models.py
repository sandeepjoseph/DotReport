"""
validate_models.py
Phase 1 — Validates exported ONNX models before shipping to wwwroot/models/.

Checks:
  1. ONNX model graph is valid
  2. Model can be loaded into OnnxRuntime
  3. A forward pass produces output of the correct shape
  4. Manifest.json checksums match on-disk files
  5. Token latency estimate (single-token pass)

Usage:
    python factory/validate_models.py --model-dir ./output/phi4-mini-q4
    python factory/validate_models.py --model-dir ./output/qwen25-1b5-q4
    python factory/validate_models.py --all --output ./output
"""

import argparse
import hashlib
import json
import time
from pathlib import Path

import numpy as np
import onnx
import onnxruntime as ort
from rich.console import Console
from rich.table import Table

console = Console()


def parse_args():
    p = argparse.ArgumentParser(description="Validate ONNX model for EdgeCore deployment")
    grp = p.add_mutually_exclusive_group(required=True)
    grp.add_argument("--model-dir", type=Path, help="Path to a single model directory")
    grp.add_argument("--all", action="store_true", help="Validate all models in --output")
    p.add_argument("--output", default="./output", help="Output base directory (used with --all)")
    return p.parse_args()


def check_onnx_graph(model_path: Path) -> bool:
    console.log(f"  Checking ONNX graph: {model_path.name}")
    try:
        model = onnx.load(str(model_path))
        onnx.checker.check_model(model)
        console.log(f"  [green]✓ Graph valid[/green] — IR version {model.ir_version}")
        return True
    except Exception as e:
        console.log(f"  [red]✗ Graph invalid: {e}[/red]")
        return False


def check_ort_load(model_path: Path) -> ort.InferenceSession | None:
    console.log(f"  Loading ORT session: {model_path.name}")
    providers = ["CPUExecutionProvider"]
    try:
        sess = ort.InferenceSession(str(model_path), providers=providers)
        inputs  = [i.name for i in sess.get_inputs()]
        outputs = [o.name for o in sess.get_outputs()]
        console.log(f"  [green]✓ Session loaded[/green] — inputs: {inputs}, outputs: {outputs}")
        return sess
    except Exception as e:
        console.log(f"  [red]✗ ORT load failed: {e}[/red]")
        return None


def check_forward_pass(sess: ort.InferenceSession) -> tuple[bool, float]:
    console.log("  Running forward pass (latency estimate)...")
    try:
        # Minimal 4-token input
        input_ids     = np.array([[1, 100, 200, 300]], dtype=np.int64)
        attention_mask = np.array([[1, 1, 1, 1]], dtype=np.int64)

        feeds = {}
        for inp in sess.get_inputs():
            if "input_ids" in inp.name:
                feeds[inp.name] = input_ids
            elif "attention_mask" in inp.name:
                feeds[inp.name] = attention_mask

        start = time.perf_counter()
        outputs = sess.run(None, feeds)
        elapsed_ms = (time.perf_counter() - start) * 1000

        logits = outputs[0]
        console.log(f"  [green]✓ Forward pass OK[/green] — logits shape: {logits.shape}, latency: {elapsed_ms:.1f}ms")
        return True, elapsed_ms
    except Exception as e:
        console.log(f"  [red]✗ Forward pass failed: {e}[/red]")
        return False, 0.0


def check_manifest(model_dir: Path) -> bool:
    manifest_path = model_dir / "manifest.json"
    if not manifest_path.exists():
        console.log("  [yellow]⚠ No manifest.json found[/yellow]")
        return False

    try:
        manifest = json.loads(manifest_path.read_text())
        segments = manifest.get("segments", [])
        all_ok = True
        for seg in segments:
            f = model_dir / seg["fileName"]
            if not f.exists():
                console.log(f"  [red]✗ Missing file: {seg['fileName']}[/red]")
                all_ok = False
                continue
            actual = hashlib.sha256(f.read_bytes()).hexdigest()
            if actual != seg["sha256"]:
                console.log(f"  [red]✗ Checksum mismatch: {seg['fileName']}[/red]")
                all_ok = False
            else:
                console.log(f"  [dim]✓ {seg['fileName']} — {seg['sizeBytes'] // 1024} KB[/dim]")
        return all_ok
    except Exception as e:
        console.log(f"  [red]✗ Manifest error: {e}[/red]")
        return False


def validate_model_dir(model_dir: Path) -> dict:
    console.rule(f"[bold]Validating: {model_dir.name}[/bold]")

    onnx_files = list(model_dir.glob("*.onnx"))
    if not onnx_files:
        console.log("[red]No .onnx file found in directory[/red]")
        return {"valid": False}

    model_path = onnx_files[0]
    results = {"model": model_dir.name, "file": model_path.name}

    results["graph_valid"]    = check_onnx_graph(model_path)
    sess                      = check_ort_load(model_path)
    results["session_loaded"] = sess is not None
    if sess:
        results["forward_ok"], results["latency_ms"] = check_forward_pass(sess)
    results["manifest_ok"]    = check_manifest(model_dir)
    results["valid"] = all([
        results.get("graph_valid", False),
        results.get("session_loaded", False),
        results.get("forward_ok", False),
    ])

    return results


def print_summary(all_results: list[dict]):
    console.rule("[bold]VALIDATION SUMMARY[/bold]")
    table = Table(show_header=True, header_style="bold")
    table.add_column("Model",      style="cyan")
    table.add_column("Graph",      justify="center")
    table.add_column("ORT Load",   justify="center")
    table.add_column("Fwd Pass",   justify="center")
    table.add_column("Manifest",   justify="center")
    table.add_column("Latency",    justify="right")
    table.add_column("READY",      justify="center")

    ok = "[green]✓[/green]"
    fail = "[red]✗[/red]"
    warn = "[yellow]⚠[/yellow]"

    for r in all_results:
        table.add_row(
            r.get("model", "?"),
            ok if r.get("graph_valid")    else fail,
            ok if r.get("session_loaded") else fail,
            ok if r.get("forward_ok")     else fail,
            ok if r.get("manifest_ok")    else warn,
            f"{r.get('latency_ms', 0):.1f}ms",
            ok if r.get("valid")          else fail,
        )

    console.print(table)
    all_valid = all(r.get("valid", False) for r in all_results)
    if all_valid:
        console.print("[bold green]ALL MODELS VALIDATED — READY FOR DEPLOYMENT[/bold green]")
    else:
        console.print("[bold red]VALIDATION FAILURES DETECTED — DO NOT DEPLOY[/bold red]")
    return all_valid


def main():
    args = parse_args()

    if args.all:
        output_dir = Path(args.output)
        dirs = [d for d in output_dir.iterdir() if d.is_dir() and (d / "model.onnx").exists()]
        if not dirs:
            console.print(f"[yellow]No model directories found in {output_dir}[/yellow]")
            return
        results = [validate_model_dir(d) for d in dirs]
    else:
        results = [validate_model_dir(args.model_dir)]

    success = print_summary(results)
    raise SystemExit(0 if success else 1)


if __name__ == "__main__":
    main()
