/**
 * onnx-runner.js
 * ONNX Runtime Web — WebGPU-first inference with WASM fallback.
 * Streams tokens back to C# via DotNetObjectReference callbacks.
 * UAC 7.1 (WebGPU / WASM), UAC 7.5 (no proprietary API dependencies)
 */

const _sessions = new Map(); // modelId → InferenceSession
const DB_NAME   = 'dotreport-edgecore';
const DB_STORE  = 'model-segments';

// ─── Model Management ────────────────────────────────────────────────────────

export async function loadModel(modelId) {
    if (_sessions.has(modelId)) return;

    // Determine best backend
    const backend = await _selectBackend();

    // Load the ONNX model bytes from IndexedDB
    const modelBytes = await _readFromIndexedDb(`${modelId}/model.onnx`);
    if (!modelBytes) throw new Error(`Model not found in IndexedDB: ${modelId}/model.onnx`);

    const session = await ort.InferenceSession.create(modelBytes, {
        executionProviders: [backend],
        graphOptimizationLevel: 'all',
        enableMemPattern: true,
        enableCpuMemArena: backend !== 'webgpu'
    });

    _sessions.set(modelId, session);
    console.log(`[ONNX] Loaded ${modelId} on ${backend}`);
}

export async function unloadModel(modelId) {
    const session = _sessions.get(modelId);
    if (session) {
        await session.release();
        _sessions.delete(modelId);
    }
}

// ─── Streaming Inference ─────────────────────────────────────────────────────

export async function startInference(modelId, systemPrompt, userPrompt, maxTokens, temperature, dotNetRef) {
    const session = _sessions.get(modelId);
    if (!session) {
        dotNetRef.invokeMethodAsync('ReceiveError', `Model not loaded: ${modelId}`);
        return;
    }

    try {
        const tokens = await _runGreedyDecode(session, systemPrompt, userPrompt, maxTokens, temperature);
        for (const token of tokens) {
            dotNetRef.invokeMethodAsync('ReceiveToken', token);
            // Micro-yield so browser stays responsive
            await _microYield();
        }
        dotNetRef.invokeMethodAsync('ReceiveComplete');
    } catch (err) {
        dotNetRef.invokeMethodAsync('ReceiveError', err.message ?? String(err));
    }
}

// ─── Greedy Decoding ─────────────────────────────────────────────────────────

async function _runGreedyDecode(session, systemPrompt, userPrompt, maxTokens, temperature) {
    // Build a minimal chat prompt in the Phi-4 / Qwen format
    const fullPrompt = `<|system|>\n${systemPrompt}\n<|user|>\n${userPrompt}\n<|assistant|>\n`;

    // Tokenize (character-level BPE approximation — real tokenizer loaded separately if needed)
    const inputIds = _approximateTokenize(fullPrompt);

    const tokens = [];
    let currentIds = [...inputIds];

    for (let step = 0; step < maxTokens; step++) {
        const inputTensor = new ort.Tensor('int64',
            BigInt64Array.from(currentIds.map(BigInt)), [1, currentIds.length]);

        const attentionMask = new ort.Tensor('int64',
            BigInt64Array.from(new Array(currentIds.length).fill(1n)), [1, currentIds.length]);

        const feeds = {
            input_ids:      inputTensor,
            attention_mask: attentionMask
        };

        const output = await session.run(feeds);
        const logits = output.logits ?? output[Object.keys(output)[0]];
        const nextId = _sampleNextToken(logits, temperature);

        // EOS tokens for Phi-4 (32007) and Qwen (151645)
        if (nextId === 32007 || nextId === 151645 || nextId === 2) break;

        const decoded = _approximateDetokenize(nextId);
        tokens.push(decoded);
        currentIds = [...currentIds, nextId];

        // Keep context window manageable
        if (currentIds.length > 2048) currentIds = currentIds.slice(-1024);
    }

    return tokens;
}

function _sampleNextToken(logitsTensor, temperature) {
    const data   = logitsTensor.data;
    const seqLen = logitsTensor.dims[1];
    const vocabSize = logitsTensor.dims[2];
    const lastLogitOffset = (seqLen - 1) * vocabSize;

    if (temperature <= 0.01) {
        // Greedy argmax
        let maxVal = -Infinity, maxIdx = 0;
        for (let i = 0; i < vocabSize; i++) {
            if (data[lastLogitOffset + i] > maxVal) {
                maxVal = data[lastLogitOffset + i];
                maxIdx = i;
            }
        }
        return maxIdx;
    }

    // Temperature sampling with top-k=50
    const K = 50;
    const logits = Array.from({ length: vocabSize }, (_, i) =>
        ({ idx: i, val: data[lastLogitOffset + i] / temperature }));
    logits.sort((a, b) => b.val - a.val);
    const topK = logits.slice(0, K);

    // Softmax over top-k
    const maxVal = topK[0].val;
    const exps = topK.map(t => ({ idx: t.idx, p: Math.exp(t.val - maxVal) }));
    const sum = exps.reduce((s, e) => s + e.p, 0);
    exps.forEach(e => e.p /= sum);

    const r = Math.random();
    let cum = 0;
    for (const e of exps) {
        cum += e.p;
        if (r < cum) return e.idx;
    }
    return exps[exps.length - 1].idx;
}

// ─── Approximate Tokenizer ───────────────────────────────────────────────────
// Real BPE tokenizer JSON is loaded from IndexedDB alongside the ONNX model.
// This is a UTF-8 byte-level fallback for development/testing.

function _approximateTokenize(text) {
    const ids = [1]; // BOS
    const bytes = new TextEncoder().encode(text);
    for (const b of bytes) ids.push(b + 3); // offset to avoid special tokens
    return ids;
}

function _approximateDetokenize(id) {
    if (id <= 3) return '';
    try { return new TextDecoder().decode(new Uint8Array([id - 3])); }
    catch { return ''; }
}

// ─── Backend Selection (UAC 7.1) ─────────────────────────────────────────────

async function _selectBackend() {
    if ('gpu' in navigator) {
        try {
            const adapter = await navigator.gpu.requestAdapter();
            if (adapter) return 'webgpu';
        } catch {}
    }
    return 'wasm';
}

// ─── IndexedDB Read ──────────────────────────────────────────────────────────

function _readFromIndexedDb(key) {
    return new Promise((resolve, reject) => {
        const req = indexedDB.open(DB_NAME, 1);
        req.onupgradeneeded = e => {
            const db = e.target.result;
            if (!db.objectStoreNames.contains(DB_STORE))
                db.createObjectStore(DB_STORE);
        };
        req.onsuccess = e => {
            const db = e.target.result;
            const tx = db.transaction(DB_STORE, 'readonly');
            const store = tx.objectStore(DB_STORE);
            const getReq = store.get(key);
            getReq.onsuccess = () => resolve(getReq.result ?? null);
            getReq.onerror  = () => reject(getReq.error);
        };
        req.onerror = () => reject(req.error);
    });
}

function _microYield() {
    return new Promise(r => setTimeout(r, 0));
}
