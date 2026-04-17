# DotReport EdgeCore

**Sovereign document intelligence — upload any document, ask anything, export structured reports. All processing runs in your browser. No data ever leaves your machine.**

---

## AI Agent Architecture

DotReport uses a **3-tier circuit-breaker inference engine**. Each tier is an independent AI Agent. The system automatically selects the best available agent and falls back gracefully if a higher tier is unavailable.

| Agent | Tier | Backend | Status | Description |
|-------|------|---------|--------|-------------|
| **AI Agent 1** | Tier 1 | Cloud LLM — Server Proxy | Optional | Highest quality. Requires `DotReport.Server` deployed with Azure OpenAI or Anthropic API keys. Streams via SSE (`/api/inference/stream`). Detected automatically — health check must return `application/json`. |
| **AI Agent 2** | Tier 2 | ONNX Runtime — Local Model | Optional | Local inference via WebGPU / WASM. Provision **Phi-4 Mini** (~3.8 GB VRAM) or **Qwen 2.5** (~1.4 GB VRAM) via the Provision page. Hardware recommendations auto-applied. |
| **AI Agent 3** | Tier 3 | Built-in Pattern Engine | **Always Ready** | Guaranteed fallback. No download, no setup. Regex-based field extraction — emails, dates, phone numbers, monetary amounts, key-value pairs. |

### Agent Status in the UI

The **AI Agent status bar** in the top navigation shows all three agents at all times:

- `●` **Green** — agent is loaded and available
- `●` **Grey** — agent is not available (not deployed / not provisioned)
- `●` **Accent ring** — agent is currently handling your inference request
- `● LIVE` — inference is actively streaming

Use the **↺ Refresh** button on the home page to re-probe all agents and update their status.

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

### Ingestion Diagnostics

Each document card in the sidebar shows a live chunk count:

| Indicator | Meaning |
|-----------|---------|
| `90 chunks` | File parsed successfully; 90 text chunks indexed |
| `⚠ 0 chunks` | File loaded but no text extracted — scanned/encrypted PDF, unsupported encoding |

Each AI reply shows retrieval feedback below the response:

| Indicator | Meaning |
|-----------|---------|
| `3 fragments retrieved` | BM25 found 3 relevant chunks; response grounded in document content |
| Amber warning: *0 fragments retrieved* | BM25 found nothing matching the query — try rephrasing, or check chunk count |

> **Note:** DotReport uses **BM25 keyword search**, not vector embeddings. There is no embedding model, no vector database, and no background worker. Ingestion is synchronous and fully in-process.

---

## Getting Started

```bash
# Run locally (no setup required — AI Agent 3 is always ready)
dotnet run --project src/DotReport.Client/DotReport.Client.csproj

# Open
http://localhost:5000
```

Navigate to **Intelligence Chat** → upload documents → ask anything.

### Activating AI Agent 2 (Local ONNX — optional)

1. Go to `/provision`
2. The page **auto-detects your device** (VRAM, WebGPU) and recommends the optimal model assignment
3. Override the selection if needed: Phi-4 Mini ↔ Qwen 2.5 in either slot, or skip secondary entirely
4. Click **Load Selected Models**
5. AI Agent 2 status pill turns green in the nav bar once loading completes

**Hardware guidance:**
- ≥ 5.2 GB VRAM + WebGPU → Phi-4 Mini (primary) + Qwen 2.5 (backup) — full dual-model setup
- ≥ 3.8 GB VRAM + WebGPU → Phi-4 Mini (primary) only
- ≥ 1.4 GB VRAM → Qwen 2.5 (primary) only; no WebGPU required
- < 1.4 GB VRAM → Built-in engine recommended; local models may not run reliably

### Activating AI Agent 1 (Cloud LLM — optional)

1. Deploy `DotReport.Server` (ASP.NET Core project — coming soon)
2. Set `AZURE_OPENAI_KEY` or `ANTHROPIC_KEY` environment variables
3. The server must expose:
   - `GET /api/inference/health` → `200 application/json` (e.g. `{"status":"ok"}`)
   - `POST /api/inference/stream` → SSE token stream
4. AI Agent 1 status pill turns green automatically on first successful health check

> **Important:** The health check validates `Content-Type: application/json`. A 200 response returning HTML (e.g. from a static host or SPA fallback) is treated as unavailable. This prevents the 405 error that occurs when a static host rejects POST requests.

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
      ModelStatusBar.razor    # Nav bar: AI Agent 1/2/3 live status pills
      MultiFileUploader.razor # Drag-and-drop file ingestion
      ThemeToggle.razor
    Models/                   # Domain models
      KnowledgeModels.cs      # KnowledgeDocument, DocumentChunk, ChatMessage
      DeviceCapabilities.cs   # VRAM / WebGPU detection results
      ModelConfig.cs          # Phi4Mini + Qwen25 configs, ModelStatus enum
    Pages/
      Index.razor             # Home: AI Engine Status panel + Refresh button
      Chat.razor              # Intelligence Chat with RAG + diagnostics
      Report.razor            # Single-document structured report
      Provision.razor         # Engine provisioning with hardware recommendations
    Services/
      Inference/
        IInferenceBackend.cs       # Interface + BackendTier enum + AgentInfo record
        ServerProxyBackend.cs      # AI Agent 1 — Cloud LLM via SSE proxy
        OnnxBackend.cs             # AI Agent 2 — Local ONNX models
        RuleBasedBackend.cs        # AI Agent 3 — Built-in pattern extraction
        InferenceCircuitBreaker.cs # Tier selection, backoff, RefreshAsync
      Parsers/                # PDF / DOCX / XLSX / CSV / TXT parsers
      KnowledgeBase.cs        # BM25 search, chunk store, RAG context builder
      CrossDocIntelligenceService.cs  # Amount matching, discrepancy, date clustering
      ConsolidatorProxy.cs    # Thin wrapper — delegates to InferenceCircuitBreaker
      VRAMDetector.cs         # JS interop: WebGPU adapter query
    Interop/                  # JS interop bridges (ONNX, IndexedDB, Babylon)
  DotReport.Shared/           # Shared DTOs
```

---

## License

Proprietary — All rights reserved. This software is the intellectual property of XyOPST Inc.
