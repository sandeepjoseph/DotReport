# DotReport EdgeCore

**Sovereign document intelligence — upload any document, ask anything, export structured reports. All processing runs in your browser. No data ever leaves your machine.**

---

## AI Agent Architecture

DotReport uses a **3-tier circuit-breaker inference engine**. Each tier is an independent AI Agent. The system automatically selects the best available agent and falls back gracefully if a higher tier is unavailable.

| Agent | Tier | Backend | Status | Description |
|-------|------|---------|--------|-------------|
| **AI Agent 1** | Tier 1 | Cloud LLM — Server Proxy | Optional | Highest quality. Requires `DotReport.Server` deployed with Azure OpenAI or Anthropic API keys. Streams via SSE (`/api/inference/stream`). |
| **AI Agent 2** | Tier 2 | ONNX Runtime — Local Model | Optional | Local inference via WebGPU / WASM. Requires provisioning Phi-4 Mini (~3.8 GB VRAM) or Qwen 2.5 (~1.4 GB VRAM) via the Provision page. |
| **AI Agent 3** | Tier 3 | Built-in Pattern Engine | **Always Ready** | Guaranteed fallback. No download, no setup. Regex-based field extraction — emails, dates, phone numbers, monetary amounts, key-value pairs. |

### Agent Status in the UI

The **AI Agent status bar** in the top navigation shows all three agents at all times:

- `●` **Green** — agent is loaded and available
- `●` **Grey** — agent is not available (not deployed / not provisioned)
- `●` **Accent ring** — agent is currently handling your inference request
- `● LIVE` — inference is actively streaming

### Circuit Breaker Behaviour

```
Request arrives
      ↓
  AI Agent 1 available? ──Yes──→ use it → success → done
      │ No / circuit open
  AI Agent 2 available? ──Yes──→ use it → success → done
      │ No / circuit open
  AI Agent 3 (always available) → use it → done
```

If an agent fails **3 consecutive times**, its circuit opens with **decorrelated jitter backoff** (2 s base → 60 s cap). It enters a half-open probe state and recovers automatically.

---

## Document Support

| Format | Parser | Notes |
|--------|--------|-------|
| PDF | PdfPig (pure .NET) | Filters PDF stream commands and binary-encoded glyphs; falls back gracefully on custom-encoded fonts |
| DOCX | BCL ZipArchive + XDocument | Extracts `<w:t>` paragraphs and 2-column table rows |
| XLSX | BCL ZipArchive + shared strings | Up to 50 rows per sheet, column-header key-value output |
| CSV / TSV | Custom parser | Quoted-field support; 50-row cap; header row auto-detection |
| TXT / MD / LOG / JSON / XML | StreamReader | Plain text passthrough |

---

## Intelligence Chat (`/chat`)

The primary interface. Upload up to 20 documents, then ask questions in plain English.

- **BM25 full-text search** across all uploaded document chunks (k₁=1.5, b=0.75)
- **RAG pipeline** — top-4 chunks injected into every system prompt
- **Cross-document intelligence** — automatically runs when 2+ documents are loaded:
  - Matching monetary amounts across documents
  - Discrepancy detection (same label, different values)
  - Date clusters linking documents to shared event windows
- **Report generation** — type "build report" or use the quick action; exports as PDF

---

## Getting Started

```bash
# Run locally (no setup required — AI Agent 3 is always ready)
dotnet run --project src/DotReport.Client/DotReport.Client.csproj

# Open
http://localhost:5000
```

Navigate to **Intelligence Chat** → upload documents → ask anything.

### Activating AI Agent 2 (Local ONNX)

1. Go to `/provision`
2. Click **Load Local AI Models**
3. Wait for Phi-4 Mini and Qwen 2.5 to download and load
4. AI Agent 2 status pill turns green in the nav bar

### Activating AI Agent 1 (Cloud LLM)

1. Deploy `DotReport.Server` (ASP.NET Core project — coming soon)
2. Set `AZURE_OPENAI_KEY` / `ANTHROPIC_KEY` environment variables
3. The server exposes `GET /api/inference/health` and `POST /api/inference/stream`
4. AI Agent 1 status pill turns green automatically on first successful health check

---

## Technology Stack

| Layer | Technology |
|-------|-----------|
| UI Framework | Blazor WebAssembly · .NET 9 |
| Inference Tier 1 | Azure OpenAI / Anthropic (via server proxy) |
| Inference Tier 2 | ONNX Runtime Web · WebGPU / WASM |
| Inference Tier 3 | .NET regex — always available |
| PDF Parsing | PdfPig 0.1.9 |
| PDF Generation | QuestPDF 2024.12 |
| Search | BM25 in-memory (custom implementation) |
| 3D Visuals | Babylon.js (non-critical, progressive) |

---

## Project Structure

```
src/
  DotReport.Client/           # Blazor WASM app
    Components/               # Reusable UI components
    Models/                   # Domain models and DTOs
    Pages/                    # Routable pages (/chat, /report, /provision)
    Services/
      Inference/              # 3-tier inference engine
        IInferenceBackend.cs  # Interface + AgentInfo record
        ServerProxyBackend.cs # AI Agent 1 — Cloud LLM
        OnnxBackend.cs        # AI Agent 2 — Local ONNX
        RuleBasedBackend.cs   # AI Agent 3 — Built-in patterns
        InferenceCircuitBreaker.cs
      Parsers/                # Document format parsers
      KnowledgeBase.cs        # BM25 search + RAG context builder
      CrossDocIntelligenceService.cs
    Interop/                  # JS interop (ONNX, IndexedDB, Babylon)
  DotReport.Shared/           # Shared DTOs
```

---

## License

MIT © 2026 XyOPST Intelligence
