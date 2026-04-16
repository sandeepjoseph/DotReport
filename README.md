# DotReport EdgeCore

**NetForge: EdgeCore** — A zero-server-compute AI document analysis engine built on Blazor WebAssembly, ONNX Runtime Web, and Babylon.js. All inference runs on the user's GPU in the browser. No cloud. No data transmission. Sovereign by design.

---

## Table of Contents

- [Architecture Overview](#architecture-overview)
- [Technology Stack](#technology-stack)
- [Prerequisites](#prerequisites)
- [Getting Started](#getting-started)
- [Phase 1 — Model Factory (Python)](#phase-1--model-factory-python)
- [Phase 2 — Running the App (.NET)](#phase-2--running-the-app-net)
- [Project Structure](#project-structure)
- [Testing](#testing)
- [UAC Compliance Matrix](#uac-compliance-matrix)
- [UI Themes](#ui-themes)
- [Deployment](#deployment)
- [Hardware Requirements](#hardware-requirements)
- [Security & Data Sovereignty](#security--data-sovereignty)

---

## Architecture Overview

```
┌─────────────────────────────────────────────────────────────────┐
│                    USER'S BROWSER (CLIENT GPU)                  │
│                                                                 │
│   ┌──────────────────────────────────────────────────────────┐  │
│   │              Blazor WebAssembly (.NET 9)                 │  │
│   │                                                          │  │
│   │   ┌──────────────────┐   ┌──────────────────────────┐   │  │
│   │   │  ConsolidatorProxy│   │     Babylon.js Scene     │   │  │
│   │   │  ─────────────── │   │  ───────────────────────  │   │  │
│   │   │  Primary (Phi-4) │   │  Dodecahedron Assembly   │   │  │
│   │   │  Backup (Qwen)   │   │  Drafting Animation      │   │  │
│   │   │  500ms Watchdog  │   │  Kinetic Structuralism   │   │  │
│   │   └────────┬─────────┘   └──────────────────────────┘   │  │
│   │            │                                              │  │
│   │   ┌────────▼─────────────────────────────────────────┐   │  │
│   │   │         ONNX Runtime Web  (WebGPU / WASM)        │   │  │
│   │   │   Phi-4 Mini 4-bit  ║  Qwen 2.5 1.5B 4-bit      │   │  │
│   │   └──────────────────────────────────────────────────┘   │  │
│   │                                                          │  │
│   │   ┌──────────────────────────────────────────────────┐   │  │
│   │   │            Browser IndexedDB Storage             │   │  │
│   │   │        (Model segments cached after first run)   │   │  │
│   │   └──────────────────────────────────────────────────┘   │  │
│   └──────────────────────────────────────────────────────────┘  │
└─────────────────────────────────────────────────────────────────┘
          ▲
          │  One-time download only
          │
┌─────────┴───────────────────────────────────────────────────────┐
│                PYTHON MODEL FACTORY  (Dev / CI only)            │
│                                                                 │
│   Phi-4 Mini 3.8B  →  4-bit GPTQ  →  ONNX  →  manifest.json   │
│   Qwen 2.5 1.5B    →  4-bit GPTQ  →  ONNX  →  manifest.json   │
└─────────────────────────────────────────────────────────────────┘
```

**Zero server compute.** After the initial one-time model download into browser IndexedDB, the app operates fully offline. No document data ever leaves the device.

---

## Technology Stack

| Layer | Technology | Role |
|---|---|---|
| **App Framework** | Blazor WebAssembly (.NET 9) | C# UI, business logic, proxy orchestration |
| **AI Inference** | ONNX Runtime Web 1.20+ | WebGPU-accelerated model execution |
| **Primary Model** | Microsoft Phi-4 Mini (3.8B, 4-bit ONNX) | Complex logic, report mapping |
| **Backup Model** | Qwen 2.5 1.5B (4-bit ONNX) | Speed-optimised safety net, immediate feedback |
| **3D Engine** | Babylon.js | Dodecahedron provisioning animation, drafting UI |
| **PDF Output** | QuestPDF (.NET) | Branded report generation, 100% in-process |
| **Model Factory** | Python + PyTorch + AutoGPTQ + Optimum | One-time quantization pipeline |
| **Storage** | Browser IndexedDB | Persistent model segment cache |

---

## Prerequisites

### For running the app

| Requirement | Version | Notes |
|---|---|---|
| .NET SDK | 9.0+ | `brew install --cask dotnet` on macOS |
| Modern browser | Chrome 113+ / Edge 113+ | WebGPU required for GPU inference |

### For running the Python Model Factory

| Requirement | Version | Notes |
|---|---|---|
| Python | 3.11+ | |
| CUDA | 11.8+ (optional) | GPU quantization is faster; CPU works too |
| Disk space | ~20 GB | For raw model weights + ONNX outputs |
| RAM | 16 GB minimum | 32 GB recommended for Phi-4 |

---

## Getting Started

### 1. Clone the repository

```bash
git clone https://github.com/sandeepjoseph/DotReport.git
cd DotReport
```

### 2. Run the Model Factory (Phase 1)

> Skip this step if you already have the ONNX models in `src/DotReport.Client/wwwroot/models/`.

```bash
cd factory
pip install -r requirements.txt

# Quantize Phi-4 Mini (Primary — ~3.8 GB output, takes 20–40 min first run)
python quantize_phi4.py --output ./output --calibration-samples 128

# Quantize Qwen 2.5 1.5B (Backup — ~1.4 GB output, takes 5–10 min)
python quantize_qwen.py --output ./output --calibration-samples 64

# Validate both models before deploying
python validate_models.py --all --output ./output

# Copy to wwwroot
cp -r ./output/phi4-mini-q4  ../src/DotReport.Client/wwwroot/models/
cp -r ./output/qwen25-1b5-q4 ../src/DotReport.Client/wwwroot/models/
```

**Fast path for development** (FP16, no quantization, no GPU needed):

```bash
python export_onnx.py --model microsoft/Phi-4-mini-instruct
python export_onnx.py --model Qwen/Qwen2.5-1.5B-Instruct
```

### 3. Run the Blazor app (Phase 2)

```bash
dotnet run --project src/DotReport.Client/DotReport.Client.csproj
```

Open `https://localhost:5001` in Chrome or Edge with WebGPU enabled.

---

## Phase 1 — Model Factory (Python)

The factory is a one-time pipeline that converts large HuggingFace models into compact, browser-runnable ONNX assets.

### Scripts

| Script | Purpose | When to run |
|---|---|---|
| `quantize_phi4.py` | 4-bit GPTQ quantization of Phi-4 Mini → ONNX | Production build |
| `quantize_qwen.py` | 4-bit GPTQ quantization of Qwen 2.5 1.5B → ONNX | Production build |
| `export_onnx.py` | Direct FP16 ONNX export, no quantization | Development / CI |
| `validate_models.py` | Graph check, ORT load, forward pass, manifest SHA-256 | Before every deploy |

### Accuracy baseline (UAC requirement)

After quantizing, run the accuracy check against the 50-document baseline:

```bash
python validate_models.py --model-dir ./output/phi4-mini-q4
# Verify: latency shown, forward pass OK, all checksums match
```

If accuracy drop exceeds 2% compared to full-precision baseline, re-run quantization with `--calibration-samples 256`.

### Output structure

```
factory/output/
  phi4-mini-q4/
    model.onnx          ← Main inference graph
    tokenizer.model     ← SentencePiece tokenizer
    tokenizer_config.json
    manifest.json       ← Segment list with SHA-256 checksums
  qwen25-1b5-q4/
    model.onnx
    tokenizer.model
    tokenizer_config.json
    manifest.json
```

The `manifest.json` is read by the C# `ModelOrchestrator` to know which segments to download, in what order, and to verify integrity.

---

## Phase 2 — Running the App (.NET)

### Development

```bash
# Hot-reload development server
dotnet watch --project src/DotReport.Client/DotReport.Client.csproj
```

### Production build

```bash
dotnet publish src/DotReport.Client/DotReport.Client.csproj \
  --configuration Release \
  --output ./publish
```

Serve `./publish/wwwroot` from any static file host (GitHub Pages, Azure Static Web Apps, Nginx).

### Application flow

```
1. User opens the app
   └─ Device capability scan (WebGPU adapter, VRAM estimate)

2. /provision — One-time model download
   ├─ 3D Dodecahedron appears in "unfolded" state
   ├─ Each model segment downloads → one face snaps into place
   ├─ Both models load into ONNX Runtime Web
   └─ Dodecahedron locks → "LOCKED & READY" button enables

3. /report — Document analysis
   ├─ User uploads document (TXT / PDF / MD / CSV)
   ├─ Both models fire concurrently:
   │   ├─ Backup (Qwen) → immediate token stream to UI
   │   └─ Primary (Phi-4) → refined background processing
   ├─ If Primary > 500ms latency → Proxy merges Backup output
   ├─ Schema Validator checks extracted JSON fields
   │   └─ If hallucination detected → Qwen "repairs" the output
   └─ User clicks "EXPORT REPORT PDF" → QuestPDF generates in-browser
```

---

## Project Structure

```
DotReport/
├── src/
│   ├── DotReport.sln
│   ├── DotReport.Client/               # Blazor WebAssembly app
│   │   ├── Components/
│   │   │   ├── Dodecahedron3D.razor    # 3D provisioning widget
│   │   │   ├── DraftingAnimation.razor # Technical drafting canvas
│   │   │   ├── DocumentUploader.razor  # File drop zone
│   │   │   ├── ModelStatusBar.razor    # LED status indicators
│   │   │   ├── ThemeToggle.razor       # Light / Dark switch
│   │   │   └── ToggleSwitch.razor      # Physical lab toggle
│   │   ├── Interop/
│   │   │   ├── BabylonInterop.cs       # C# ↔ Babylon.js bridge
│   │   │   ├── OnnxInterop.cs          # C# ↔ ONNX Runtime Web bridge
│   │   │   └── IndexedDbInterop.cs     # C# ↔ IndexedDB bridge
│   │   ├── Models/
│   │   │   ├── ModelConfig.cs          # Phi-4 / Qwen static config
│   │   │   ├── DeviceCapabilities.cs   # WebGPU / VRAM detection result
│   │   │   ├── InferenceRequest.cs     # Per-request inference params
│   │   │   ├── InferenceResponse.cs    # Streaming token response
│   │   │   ├── ProxyState.cs           # Observable dual-model state
│   │   │   └── ReportDocument.cs       # Extracted fields + sections
│   │   ├── Pages/
│   │   │   ├── Index.razor             # Device assessment + landing
│   │   │   ├── Provision.razor         # 3D assembly + download flow
│   │   │   └── Report.razor            # Inference + PDF export
│   │   ├── Services/
│   │   │   ├── ConsolidatorProxy.cs    # ★ Dual-model orchestrator
│   │   │   ├── ModelOrchestrator.cs    # Download → cache → load lifecycle
│   │   │   ├── VRAMDetector.cs         # Pre-flight hardware assessment
│   │   │   ├── IndexedDbService.cs     # Model segment persistence
│   │   │   ├── ReportGenerator.cs      # QuestPDF branded output
│   │   │   ├── SchemaValidator.cs      # Hallucination / JSON guard
│   │   │   ├── ModelWarmupService.cs   # Silent VRAM pre-warm
│   │   │   └── ThemeService.cs         # Kinetic theme management
│   │   ├── Layout/
│   │   │   └── MainLayout.razor        # Topbar, nav, footer shell
│   │   └── wwwroot/
│   │       ├── index.html              # Blazor host page
│   │       ├── css/
│   │       │   ├── app.css             # Base Kinetic Structuralism styles
│   │       │   ├── theme-dark.css      # Theme B: Stealth Monolith
│   │       │   └── theme-light.css     # Theme A: Architectural Archive
│   │       ├── js/
│   │       │   ├── babylon-scene.js    # Dodecahedron + drafting 3D
│   │       │   ├── onnx-runner.js      # WebGPU inference + token stream
│   │       │   ├── indexed-db.js       # Model segment cache
│   │       │   ├── webgpu-detector.js  # Hardware probe
│   │       │   └── app-interop.js      # PDF download, theme swap
│   │       └── models/                 # ONNX files go here (gitignored)
│   │           ├── phi4-mini-q4/
│   │           └── qwen25-1b5-q4/
│   └── DotReport.Shared/               # Cross-project DTOs
│       └── DTOs/
│           ├── InferenceRequestDto.cs
│           ├── InferenceResponseDto.cs
│           └── ModelManifest.cs
│
├── tests/
│   ├── DotReport.Proxy.Tests/          # xUnit — services & proxy logic
│   │   ├── ConsolidatorProxyTests.cs
│   │   ├── SchemaValidatorTests.cs
│   │   ├── VRAMDetectorTests.cs
│   │   ├── ProxyStateTests.cs
│   │   └── ReportGeneratorTests.cs
│   ├── DotReport.Components.Tests/     # bUnit — Blazor components
│   │   ├── ToggleSwitchTests.cs
│   │   ├── ThemeToggleTests.cs
│   │   ├── ModelStatusBarTests.cs
│   │   └── ThemeServiceTests.cs
│   ├── DotReport.E2E.Tests/            # Playwright — browser E2E
│   │   ├── PlaywrightFixture.cs
│   │   ├── ProvisioningFlowTests.cs
│   │   ├── ThemeSwitchTests.cs
│   │   ├── OfflineOperationTests.cs
│   │   └── TabSuspendTests.cs
│   └── factory/                        # pytest — Python factory
│       ├── conftest.py
│       ├── test_manifest.py
│       ├── test_validate_models.py
│       └── test_export_onnx.py
│
├── factory/                            # Python Model Factory
│   ├── requirements.txt
│   ├── quantize_phi4.py
│   ├── quantize_qwen.py
│   ├── export_onnx.py
│   └── validate_models.py
│
└── .github/
    └── workflows/
        └── build.yml                   # CI: test → build → deploy
```

---

## Testing

### Run all .NET tests

```bash
# Unit + component tests
dotnet test src/DotReport.sln --configuration Release

# Proxy tests only
dotnet test tests/DotReport.Proxy.Tests/ --configuration Release

# Component tests only
dotnet test tests/DotReport.Components.Tests/ --configuration Release
```

### Run Playwright E2E tests

```bash
# Start the app first
dotnet run --project src/DotReport.Client &

# Install Playwright browsers (one-time)
dotnet build tests/DotReport.E2E.Tests/ --configuration Release
pwsh tests/DotReport.E2E.Tests/bin/Release/net9.0/playwright.ps1 install chromium

# Run E2E suite
DOTREPORT_BASE_URL=http://localhost:5000 \
  dotnet test tests/DotReport.E2E.Tests/ --configuration Release
```

### Run Python factory tests

```bash
pip install pytest pytest-cov onnx onnxruntime rich numpy
pytest tests/factory/ -v --tb=short
```

### Test coverage summary

| Suite | Framework | Tests | Covers |
|---|---|---|---|
| Proxy Tests | xUnit + Moq | 25 | ConsolidatorProxy, SchemaValidator, VRAMDetector, ProxyState, ReportGenerator |
| Component Tests | bUnit | 22 | ToggleSwitch, ThemeToggle, ModelStatusBar, ThemeService |
| E2E Tests | Playwright | 18 | Provisioning flow, theme switch, offline operation, tab suspend |
| Factory Tests | pytest | 24 | Manifest integrity, validation pipeline, export, slug map |
| **Total** | | **89** | |

### Key test scenarios

| Test | What it proves |
|---|---|
| `ProxyStateTests` — `ProvisioningFaceCount` | Play button is **only** enabled at face 12/12 (UAC 7.3) |
| `ConsolidatorProxyTests` — latency | Backup merges when Primary > 500ms — user never sees a hang (UAC 7.2) |
| `SchemaValidatorTests` — hallucination | Phi-4 bad JSON is caught and sent to Qwen for repair before PDF |
| `OfflineOperationTests` — zero leakage | Network monitor asserts 0 bytes to external hosts during inference (UAC 7.5) |
| `ThemeSwitchTests` — anti-AI audit | Asserts zero `✨` sparkle icons or `chat-bubble` classes in markup (UAC 7.4) |
| `ThemeSwitchTests` — transition speed | Theme switch completes in under 500ms |

---

## UAC Compliance Matrix

| UAC | Requirement | Implementation | Status |
|---|---|---|---|
| **7.1** | Zero server compute — 100% client GPU via WebGPU, WASM fallback | `VRAMDetector` → `onnx-runner.js` WebGPU/WASM backend selection | ✅ |
| **7.2** | If Primary > 500ms token latency → seamlessly merge Backup output | `ConsolidatorProxy` watchdog task + channel-based merge | ✅ |
| **7.3** | Model download visualised as 3D geometric assembly. Play button enabled only when Dodecahedron fully locked | `ModelOrchestrator.OnSegmentCached` → `BabylonInterop.AssembleFaceAsync` | ✅ |
| **7.4** | Zero sparkle icons, zero chatbot bubbles. All processing shown as Technical Drafting animation | Kinetic Structuralism CSS + Babylon.js drafting grid. E2E audit test enforces this | ✅ |
| **7.5** | All source code in single Git repo, zero external AI API dependencies | This repository. `validate_models.py` scans for external keys | ✅ |

---

## UI Themes

### Theme A — Architectural Archive (Retro Light)

- **Background:** Aged drafting paper (`#f2efea`)
- **Borders:** Deep-etched (`#6a6055`), 2px solid — no rounded corners
- **Dodecahedron:** Milled aluminium with manufacture stamps (`SN-DR-001 / MFG: 2026-A`)
- **Controls:** Chunky 3px-border toggle switches with physical shadow press effect
- **Accent:** Dark ink on paper — no gradients

### Theme B — Stealth Monolith (Modern Dark)

- **Background:** Matte black (`#0d0d0d`)
- **Depth:** Varying textures — brushed metal (`#111111`) vs. sandblasted carbon (`#080808`)
- **Dodecahedron:** Translucent dark obsidian (alpha 0.85) with sharp laboratory-white core glow during inference only
- **Controls:** Inset "carved" buttons — they physically sink (`translateY(1px)`) rather than glowing
- **Accent:** Sharp white — appears only when the engine is processing

---

## Deployment

### GitHub Pages (automated via CI)

Pushing to `main` triggers the CI pipeline:

```
unit-tests + factory-tests
        ↓
    e2e-tests
        ↓
  build-blazor (publish)
        ↓
  deploy-pages → https://sandeepjoseph.github.io/DotReport/
```

### Static host (manual)

```bash
dotnet publish src/DotReport.Client/DotReport.Client.csproj \
  --configuration Release --output ./publish

# Serve ./publish/wwwroot with any static file server
# Ensure: Content-Type: application/wasm for .wasm files
#         Cross-Origin-Opener-Policy: same-origin
#         Cross-Origin-Embedder-Policy: require-corp
```

> **Note:** The `Cross-Origin-Opener-Policy` and `Cross-Origin-Embedder-Policy` headers are required for `SharedArrayBuffer`, which ONNX Runtime Web uses for WebGPU workloads.

---

## Hardware Requirements

| Profile | GPU | VRAM | Model | Tokens/sec |
|---|---|---|---|---|
| **High-End** | NVIDIA RTX 40+ | 8 GB+ | Phi-4 Mini (Primary) | ~85 t/s |
| **Enterprise** | Intel Iris Xe | 4–6 GB | Phi-4 / Qwen Hybrid | ~28 t/s |
| **Low-End / Mobile** | Integrated / none | < 4 GB | Qwen 2.5 (Backup, WASM) | ~12 t/s |

The app automatically detects VRAM on first load and switches to **Lightweight Build** (Qwen only, WASM) when VRAM is below 4 GB.

---

## Security & Data Sovereignty

| Checkpoint | Method | Result |
|---|---|---|
| **Zero data transmission** | Playwright network monitor during inference — asserts 0 bytes to external hosts | Enforced by E2E test |
| **No proprietary API keys** | `validate_models.py` codebase scan | 0 external keys |
| **Model integrity** | SHA-256 checksums in `manifest.json`, verified on every load | `IndexedDbService` + `ModelOrchestrator` |
| **Input validation** | `SchemaValidator` guards all AI output before PDF generation | Hallucinations flagged + Qwen repair triggered |
| **Local storage only** | All model segments stored in browser IndexedDB — no server-side storage | Verified by offline E2E tests |

> **Architect's Note:** By moving the processing brain into the browser, the $15,000/year cloud operating cost is eliminated. DotReport EdgeCore is a zero-cost, zero-leakage asset.

---

## Built With

- [Blazor WebAssembly](https://dotnet.microsoft.com/apps/aspnet/web-apps/blazor) — C# in the browser
- [ONNX Runtime Web](https://onnxruntime.ai/docs/get-started/with-javascript/web.html) — WebGPU AI inference
- [Babylon.js](https://www.babylonjs.com/) — Professional 3D engine
- [QuestPDF](https://www.questpdf.com/) — .NET PDF generation
- [Microsoft Phi-4 Mini](https://huggingface.co/microsoft/Phi-4-mini-instruct) — Primary inference model
- [Qwen 2.5 1.5B](https://huggingface.co/Qwen/Qwen2.5-1.5B-Instruct) — Backup inference model
- [AutoGPTQ](https://github.com/AutoGPTQ/AutoGPTQ) — Model quantization
- [Optimum](https://huggingface.co/docs/optimum) — ONNX export pipeline

---

*NetForge / EdgeCore / REV-2026-A — ALL COMPUTE: CLIENT-SIDE*
