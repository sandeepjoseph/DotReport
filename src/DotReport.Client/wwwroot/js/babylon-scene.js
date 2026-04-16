/**
 * babylon-scene.js
 * Kinetic Structuralism 3D engine — Babylon.js
 * Manages: Dodecahedron provisioning animation + Technical Drafting animation
 * UAC 7.3 (3D provisioning), UAC 7.4 (anti-AI aesthetic)
 */

let _engine = null;
let _scene  = null;
let _dodec  = null;
let _faces  = [];
let _draftLines = [];
let _draftActive = false;
let _currentTheme = 'ec-theme--dark';

// ─── Scene Lifecycle ─────────────────────────────────────────────────────────

export function initScene(canvasId, theme) {
    const canvas = document.getElementById(canvasId);
    if (!canvas) return;

    _currentTheme = theme;
    _engine = new BABYLON.Engine(canvas, true, { preserveDrawingBuffer: true, stencil: true });
    _scene  = new BABYLON.Scene(_engine);

    _applyThemeBackground();
    _setupCamera();
    _setupLighting();
    _buildDodecahedron();

    _engine.runRenderLoop(() => _scene && _scene.render());
    window.addEventListener('resize', () => _engine?.resize());
}

export function startUnfold() {
    if (!_scene || !_dodec) return;
    // Start flat — faces scattered around the central position
    _faces.forEach((face, i) => {
        const angle = (i / 12) * Math.PI * 2;
        const radius = 4;
        face.position = new BABYLON.Vector3(
            Math.cos(angle) * radius,
            Math.sin(angle) * radius,
            (i % 3) * 2 - 2
        );
        face.rotation = new BABYLON.Vector3(
            Math.random() * Math.PI,
            Math.random() * Math.PI,
            Math.random() * Math.PI
        );
        face.visibility = 0.3;
    });
}

export function assembleFace(faceIndex) {
    if (!_scene || faceIndex < 1 || faceIndex > _faces.length) return;
    const face = _faces[faceIndex - 1];
    if (!face) return;

    // Animate face snapping into dodecahedron position
    const targetPos = _getDodecFacePosition(faceIndex - 1);
    BABYLON.Animation.CreateAndStartAnimation(
        'assemble_' + faceIndex, face, 'position',
        30, 20, face.position.clone(), targetPos,
        BABYLON.Animation.ANIMATIONLOOPMODE_CONSTANT
    );
    BABYLON.Animation.CreateAndStartAnimation(
        'assemble_rot_' + faceIndex, face, 'rotation',
        30, 20, face.rotation.clone(), _getDodecFaceRotation(faceIndex - 1),
        BABYLON.Animation.ANIMATIONLOOPMODE_CONSTANT
    );
    BABYLON.Animation.CreateAndStartAnimation(
        'assemble_vis_' + faceIndex, face, 'visibility',
        30, 20, 0.3, 1.0,
        BABYLON.Animation.ANIMATIONLOOPMODE_CONSTANT
    );
}

export function lockDodecahedron() {
    if (!_scene) return;
    // Final lock animation: brief scale pulse + sustained slow rotation
    _faces.forEach(face => face.visibility = 1.0);

    const anim = new BABYLON.Animation(
        'lock_pulse', 'scaling', 30,
        BABYLON.Animation.ANIMATIONTYPE_VECTOR3,
        BABYLON.Animation.ANIMATIONLOOPMODE_CONSTANT
    );
    const keys = [
        { frame: 0,  value: new BABYLON.Vector3(1, 1, 1) },
        { frame: 8,  value: new BABYLON.Vector3(1.15, 1.15, 1.15) },
        { frame: 16, value: new BABYLON.Vector3(0.95, 0.95, 0.95) },
        { frame: 24, value: new BABYLON.Vector3(1, 1, 1) }
    ];
    anim.setKeys(keys);

    _faces.forEach(face => {
        face.animations = [anim];
        _scene.beginAnimation(face, 0, 24, false);
    });

    // Start persistent slow rotation
    _scene.registerBeforeRender(() => {
        if (_dodec) _dodec.rotation.y += 0.003;
    });
}

// ─── Drafting Animation (UAC 7.4) ────────────────────────────────────────────

export function startDraftingAnimation(canvasId) {
    if (_draftActive) return;
    _draftActive = true;
    _drawDraftingGrid();
    _animateDraftingLines();
}

export function stopDraftingAnimation() {
    _draftActive = false;
    _draftLines.forEach(l => l.dispose());
    _draftLines = [];
}

function _drawDraftingGrid() {
    if (!_scene) return;
    const gridSize = 20;
    const step = 1;
    const gridMat = new BABYLON.StandardMaterial('gridMat', _scene);
    gridMat.emissiveColor = _currentTheme.includes('dark')
        ? new BABYLON.Color3(0.15, 0.15, 0.15)
        : new BABYLON.Color3(0.85, 0.85, 0.85);
    gridMat.wireframe = true;

    for (let i = -gridSize; i <= gridSize; i += step) {
        const line = BABYLON.MeshBuilder.CreateLines('grid_v_' + i, {
            points: [
                new BABYLON.Vector3(i, -gridSize, 0),
                new BABYLON.Vector3(i,  gridSize, 0)
            ]
        }, _scene);
        line.color = _currentTheme.includes('dark')
            ? new BABYLON.Color3(0.1, 0.1, 0.1)
            : new BABYLON.Color3(0.8, 0.8, 0.8);
        _draftLines.push(line);
    }
}

function _animateDraftingLines() {
    if (!_scene || !_draftActive) return;
    const lineCount = 8;
    const isDark = _currentTheme.includes('dark');

    for (let i = 0; i < lineCount; i++) {
        const delay = i * 200;
        setTimeout(() => {
            if (!_draftActive || !_scene) return;
            const x1 = (Math.random() - 0.5) * 16;
            const y1 = (Math.random() - 0.5) * 12;
            const x2 = x1 + (Math.random() - 0.5) * 8;
            const y2 = y1 + (Math.random() - 0.5) * 6;
            const line = BABYLON.MeshBuilder.CreateLines('draft_line_' + Date.now(), {
                points: [new BABYLON.Vector3(x1, y1, 0), new BABYLON.Vector3(x2, y2, 0)]
            }, _scene);
            line.color = isDark
                ? new BABYLON.Color3(0.9, 0.9, 0.9)
                : new BABYLON.Color3(0.1, 0.1, 0.1);
            _draftLines.push(line);
            // Fade out after 2s
            setTimeout(() => { try { line.dispose(); } catch {} }, 2000);
        }, delay);
    }

    // Loop while active
    if (_draftActive)
        setTimeout(() => _animateDraftingLines(), lineCount * 200 + 500);
}

// ─── Theme ───────────────────────────────────────────────────────────────────

export function setTheme(theme) {
    _currentTheme = theme;
    _applyThemeBackground();
    const sheet = document.getElementById('theme-sheet');
    if (sheet)
        sheet.href = theme.includes('dark') ? 'css/theme-dark.css' : 'css/theme-light.css';
}

// ─── Device Capabilities (UAC 7.1) ───────────────────────────────────────────

export async function getDeviceCapabilities() {
    const caps = {
        webGpuSupported: false,
        webAssemblySupported: typeof WebAssembly !== 'undefined',
        estimatedVramMb: 0,
        gpuDescription: 'Unknown',
        adapterName: 'Unknown',
        fallbackReason: ''
    };

    if (!('gpu' in navigator)) {
        caps.fallbackReason = 'WebGPU not available in this browser';
        return caps;
    }

    try {
        const adapter = await navigator.gpu.requestAdapter({ powerPreference: 'high-performance' });
        if (!adapter) {
            caps.fallbackReason = 'No WebGPU adapter found';
            return caps;
        }
        caps.webGpuSupported = true;
        caps.adapterName = adapter.info?.description ?? 'WebGPU Adapter';
        caps.gpuDescription = adapter.info?.vendor ?? 'Unknown Vendor';

        // Estimate VRAM from adapter limits
        const limits = adapter.limits;
        const maxBufSize = limits.maxBufferSize ?? 0;
        caps.estimatedVramMb = Math.round(maxBufSize / (1024 * 1024));
        // Cap at 24GB for sanity; default to 4096 if adapter doesn't expose it
        if (caps.estimatedVramMb === 0) caps.estimatedVramMb = 4096;
        if (caps.estimatedVramMb > 24576) caps.estimatedVramMb = 16384;
    } catch (e) {
        caps.fallbackReason = `WebGPU init error: ${e.message}`;
    }

    return caps;
}

// ─── Dispose ─────────────────────────────────────────────────────────────────

export function disposeScene() {
    _draftLines.forEach(l => { try { l.dispose(); } catch {} });
    _draftLines = [];
    _faces = [];
    _dodec = null;
    if (_scene)  { _scene.dispose();  _scene = null; }
    if (_engine) { _engine.dispose(); _engine = null; }
}

// ─── Private Helpers ─────────────────────────────────────────────────────────

function _applyThemeBackground() {
    if (!_scene) return;
    const isDark = _currentTheme.includes('dark');
    _scene.clearColor = isDark
        ? new BABYLON.Color4(0.05, 0.05, 0.05, 1)
        : new BABYLON.Color4(0.96, 0.95, 0.93, 1);
}

function _setupCamera() {
    const cam = new BABYLON.ArcRotateCamera('cam', -Math.PI / 2, Math.PI / 4, 8, BABYLON.Vector3.Zero(), _scene);
    cam.lowerRadiusLimit = 4;
    cam.upperRadiusLimit = 16;
    cam.attachControl(null, true);
}

function _setupLighting() {
    const isDark = _currentTheme.includes('dark');
    const hemi = new BABYLON.HemisphericLight('hemi', new BABYLON.Vector3(0, 1, 0), _scene);
    hemi.intensity = isDark ? 0.3 : 0.8;

    const point = new BABYLON.PointLight('point', new BABYLON.Vector3(0, 0, 0), _scene);
    point.intensity = isDark ? 0.8 : 0.4;
    point.diffuse = isDark
        ? new BABYLON.Color3(1, 1, 1)       // sharp white core (Stealth Monolith)
        : new BABYLON.Color3(0.95, 0.92, 0.85); // warm incandescent (Architectural Archive)
}

function _buildDodecahedron() {
    // Babylon.js doesn't have a native dodecahedron — we use 12 pentagon proxies (planes)
    // arranged at dodecahedron face positions
    _dodec = new BABYLON.Mesh('dodec_root', _scene);

    const isDark = _currentTheme.includes('dark');
    const faceMat = new BABYLON.StandardMaterial('face_mat', _scene);

    if (isDark) {
        // Stealth Monolith: translucent dark obsidian
        faceMat.diffuseColor  = new BABYLON.Color3(0.08, 0.08, 0.1);
        faceMat.specularColor = new BABYLON.Color3(0.9, 0.9, 1.0);
        faceMat.specularPower = 128;
        faceMat.alpha         = 0.85;
    } else {
        // Architectural Archive: milled aluminum
        faceMat.diffuseColor  = new BABYLON.Color3(0.75, 0.75, 0.78);
        faceMat.specularColor = new BABYLON.Color3(1, 1, 1);
        faceMat.specularPower = 64;
    }

    _faces = [];
    for (let i = 0; i < 12; i++) {
        const face = BABYLON.MeshBuilder.CreateDisc('face_' + i, {
            radius: 0.9,
            tessellation: 5, // pentagon
            sideOrientation: BABYLON.Mesh.DOUBLESIDE
        }, _scene);
        face.material = faceMat.clone('face_mat_' + i);
        face.parent = _dodec;
        _faces.push(face);
    }
}

function _getDodecFacePosition(index) {
    // 12 face center positions of a regular dodecahedron (unit radius ≈ 1.401)
    const phi = (1 + Math.sqrt(5)) / 2;
    const positions = [
        [0,  1,  phi], [0, -1,  phi], [0,  1, -phi], [0, -1, -phi],
        [ phi, 0,  1], [-phi, 0,  1], [ phi, 0, -1], [-phi, 0, -1],
        [1,  phi, 0], [-1,  phi, 0], [1, -phi, 0], [-1, -phi, 0]
    ];
    const [x, y, z] = positions[index % 12];
    const scale = 1.2;
    return new BABYLON.Vector3(x * scale, y * scale, z * scale);
}

function _getDodecFaceRotation(index) {
    const angle = (index / 12) * Math.PI * 2;
    return new BABYLON.Vector3(angle * 0.3, angle, angle * 0.2);
}
