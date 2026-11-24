// Dolphin Audio - Main Application Logic
// Handles EQ Bands, Q Factors, Canvas Drawing, ReaComp VST Mapping & APO Config

const DEFAULT_FREQS = [31, 63, 125, 250, 500, 1000, 2000, 4000, 8000, 16000];

// Application State
const state = {
    preamp: 0.0,
    masterBypass: false,
    bands: DEFAULT_FREQS.map((f, i) => ({
        id: i + 1,
        freq: f,
        gain: 0.0,
        q: 1.41,
        type: i === 0 ? 'LS' : (i === DEFAULT_FREQS.length - 1 ? 'HS' : 'PK'),
        enabled: true
    })),
    comp: {
        enabled: true,
        threshDb: -18.0,
        ratio: 4.0,
        attackMs: 3.0,
        releaseMs: 100.0,
        kneeDb: 3.0,
        precompMs: 0.0,
        rmsMs: 5.0,
        dryDb: -60.0,
        wetDb: 0.0,
        autoMakeup: true,
        hipassHz: 0,
        lowpassHz: 20000,
        autoRelease: false,
        classicAttack: false,
        antiAlias: false
    },
    autoApply: true
};

let biquadFilters = [];
let canvas, ctx;
let serverAppInfo = null;

// Initialization
document.addEventListener('DOMContentLoaded', async () => {
    try {
        const infoRes = await fetch('/api/app-info');
        serverAppInfo = await infoRes.json();
    } catch (e) {
        console.log('Running without server API');
    }

    initEqBands();
    initCanvas();
    initReaCompEvents();
    initHeaderAndFooter();
    rebuildBiquadFilters();
    renderCanvas();
    syncConfig();
});

// Build 10 EQ Bands UI
function initEqBands() {
    const container = document.getElementById('eqBandsContainer');
    container.innerHTML = '';

    state.bands.forEach((band, index) => {
        const col = document.createElement('div');
        col.className = 'eq-band-col';

        const freqDisplay = band.freq >= 1000 ? `${band.freq / 1000}k` : `${band.freq}`;
        
        col.innerHTML = `
            <span class="freq-label">${freqDisplay}Hz</span>
            <select class="filter-type-select" data-id="${band.id}">
                <option value="PK" ${band.type === 'PK' ? 'selected' : ''}>Peak</option>
                <option value="LS" ${band.type === 'LS' ? 'selected' : ''}>LowShelf</option>
                <option value="HS" ${band.type === 'HS' ? 'selected' : ''}>HighShelf</option>
            </select>
            <div class="slider-container">
                <input type="range" class="vertical-slider band-slider" data-id="${band.id}" 
                       min="-30" max="30" step="0.1" value="${band.gain}">
            </div>
            <input type="text" class="gain-val-box" data-id="${band.id}" value="${band.gain.toFixed(1)} dB">
            <div class="q-box-wrapper" title="Quality Factor (Q) - Bandwidth">
                <span>Q:</span>
                <input type="number" class="q-val-box" data-id="${band.id}" 
                       min="0.1" max="10.0" step="0.1" value="${band.q}">
            </div>
        `;

        container.appendChild(col);
    });

    // Attach Event Listeners to Band elements
    container.querySelectorAll('.band-slider').forEach(slider => {
        const id = parseInt(slider.dataset.id);
        const band = state.bands.find(b => b.id === id);
        const gainBox = container.querySelector(`.gain-val-box[data-id="${id}"]`);

        slider.addEventListener('input', (e) => {
            const val = parseFloat(e.target.value);
            band.gain = val;
            gainBox.value = `${val.toFixed(1)} dB`;
            updateFilter(id);
        });

        // Double click slider to reset to 0 dB
        slider.addEventListener('dblclick', () => {
            slider.value = 0;
            band.gain = 0;
            gainBox.value = '0.0 dB';
            updateFilter(id);
        });
    });

    // Gain Text Boxes
    container.querySelectorAll('.gain-val-box').forEach(box => {
        const id = parseInt(box.dataset.id);
        const band = state.bands.find(b => b.id === id);
        const slider = container.querySelector(`.band-slider[data-id="${id}"]`);

        box.addEventListener('change', (e) => {
            let val = parseFloat(e.target.value.replace(/[^0-9.-]/g, ''));
            if (isNaN(val)) val = 0;
            val = Math.max(-30, Math.min(30, val));
            band.gain = val;
            box.value = `${val.toFixed(1)} dB`;
            slider.value = val;
            updateFilter(id);
        });
    });

    // Q Factor Boxes
    container.querySelectorAll('.q-val-box').forEach(qBox => {
        const id = parseInt(qBox.dataset.id);
        const band = state.bands.find(b => b.id === id);

        qBox.addEventListener('change', (e) => {
            let val = parseFloat(e.target.value);
            if (isNaN(val) || val < 0.1) val = 0.1;
            if (val > 10.0) val = 10.0;
            band.q = val;
            qBox.value = val.toFixed(2);
            updateFilter(id);
        });
    });

    // Filter Type Selects
    container.querySelectorAll('.filter-type-select').forEach(sel => {
        const id = parseInt(sel.dataset.id);
        const band = state.bands.find(b => b.id === id);

        sel.addEventListener('change', (e) => {
            band.type = e.target.value;
            updateFilter(id);
        });
    });
}

// Preamp controls
const preampSlider = document.getElementById('preampSlider');
const preampVal = document.getElementById('preampVal');

preampSlider.addEventListener('input', (e) => {
    state.preamp = parseFloat(e.target.value);
    preampVal.value = `${state.preamp.toFixed(1)} dB`;
    renderCanvas();
    triggerSync();
});

preampSlider.addEventListener('dblclick', () => {
    preampSlider.value = 0;
    state.preamp = 0;
    preampVal.value = '0.0 dB';
    renderCanvas();
    triggerSync();
});

preampVal.addEventListener('change', (e) => {
    let val = parseFloat(e.target.value.replace(/[^0-9.-]/g, ''));
    if (isNaN(val)) val = 0;
    val = Math.max(-30, Math.min(30, val));
    state.preamp = val;
    preampSlider.value = val;
    preampVal.value = `${val.toFixed(1)} dB`;
    renderCanvas();
    triggerSync();
});

// Update single filter in DSP array
function updateFilter(id) {
    const band = state.bands.find(b => b.id === id);
    const filter = biquadFilters[id - 1];
    if (filter) {
        filter.type = band.type;
        filter.gain = band.gain;
        filter.q = band.q;
        filter.calculateCoefficients();
    }
    renderCanvas();
    triggerSync();
}

function rebuildBiquadFilters() {
    biquadFilters = state.bands.map(b => new BiquadFilter(b.type, b.freq, b.gain, b.q));
}

// Canvas Initialization & High-DPI handling
function initCanvas() {
    canvas = document.getElementById('freqCanvas');
    ctx = canvas.getContext('2d');

    function resizeCanvas() {
        const rect = canvas.getBoundingClientRect();
        const dpr = window.devicePixelRatio || 1;
        canvas.width = rect.width * dpr;
        canvas.height = rect.height * dpr;
        ctx.scale(dpr, dpr);
        renderCanvas();
    }

    window.addEventListener('resize', resizeCanvas);
    resizeCanvas();

    // Canvas Tooltip on mousemove (throttled to screen refresh rate via requestAnimationFrame)
    let canvasRafId = null;
    canvas.addEventListener('mousemove', (e) => {
        const rect = canvas.getBoundingClientRect();
        const mouseX = e.clientX - rect.left;
        const mouseY = e.clientY - rect.top;
        if (canvasRafId) cancelAnimationFrame(canvasRafId);
        canvasRafId = requestAnimationFrame(() => {
            renderCanvas(mouseX, mouseY);
            canvasRafId = null;
        });
    });

    canvas.addEventListener('mouseleave', () => {
        if (canvasRafId) {
            cancelAnimationFrame(canvasRafId);
            canvasRafId = null;
        }
        renderCanvas();
    });
}

// Frequency Response Curve Renderer
function renderCanvas(hoverX = null, hoverY = null) {
    if (!canvas || !ctx) return;
    const rect = canvas.getBoundingClientRect();
    const w = rect.width;
    const h = rect.height;

    ctx.clearRect(0, 0, w, h);

    const padLeft = 36;
    const padRight = 14;
    const padTop = 14;
    const padBottom = 20;
    const plotW = w - padLeft - padRight;
    const plotH = h - padTop - padBottom;

    const minF = 20;
    const maxF = 20000;
    const logMin = Math.log10(minF);
    const logMax = Math.log10(maxF);

    const minDb = -30;
    const maxDb = 30;

    function xFromFreq(f) {
        const ratio = (Math.log10(f) - logMin) / (logMax - logMin);
        return padLeft + ratio * plotW;
    }

    function freqFromX(x) {
        const ratio = (x - padLeft) / plotW;
        return Math.pow(10, logMin + ratio * (logMax - logMin));
    }

    function yFromDb(db) {
        const ratio = (maxDb - db) / (maxDb - minDb);
        return padTop + ratio * plotH;
    }

    function dbFromY(y) {
        const ratio = (y - padTop) / plotH;
        return maxDb - ratio * (maxDb - minDb);
    }

    // 1. Grid Lines - Frequency (Vertical)
    const gridFreqs = [20, 50, 100, 200, 500, 1000, 2000, 5000, 10000, 20000];
    ctx.font = '9px Consolas, monospace';
    ctx.textAlign = 'center';

    gridFreqs.forEach(f => {
        const x = xFromFreq(f);
        ctx.strokeStyle = (f === 100 || f === 1000 || f === 10000) ? '#282d34' : '#1b1e22';
        ctx.lineWidth = 1;
        ctx.beginPath();
        ctx.moveTo(x, padTop);
        ctx.lineTo(x, padTop + plotH);
        ctx.stroke();

        // Labels
        ctx.fillStyle = '#687280';
        const label = f >= 1000 ? `${f / 1000}k` : `${f}`;
        ctx.fillText(label, x, h - 6);
    });

    // 2. Grid Lines - dB (Horizontal)
    const gridDbs = [-30, -20, -10, 0, 10, 20, 30];
    ctx.textAlign = 'right';

    gridDbs.forEach(db => {
        const y = yFromDb(db);
        if (db === 0) {
            // Prominent 0 dB Flat Reference Line
            ctx.strokeStyle = '#4b5563';
            ctx.lineWidth = 1.5;
            ctx.setLineDash([4, 4]);
        } else {
            ctx.strokeStyle = '#1b1e22';
            ctx.lineWidth = 1;
            ctx.setLineDash([]);
        }

        ctx.beginPath();
        ctx.moveTo(padLeft, y);
        ctx.lineTo(padLeft + plotW, y);
        ctx.stroke();
        ctx.setLineDash([]);

        // Labels
        ctx.fillStyle = db === 0 ? '#9ca3af' : '#4b5563';
        const label = db > 0 ? `+${db}` : `${db}`;
        ctx.fillText(label, padLeft - 6, y + 3);
    });

    // 3. Compute and Draw Active Response Curve
    const points = computeResponseCurve(state.masterBypass ? [] : biquadFilters, state.masterBypass ? 0 : state.preamp, 300);

    // Gradient fill under the curve
    const zeroY = yFromDb(0);
    const grad = ctx.createLinearGradient(0, padTop, 0, padTop + plotH);
    grad.addColorStop(0, 'rgba(0, 229, 255, 0.25)');
    grad.addColorStop(0.5, 'rgba(0, 229, 255, 0.05)');
    grad.addColorStop(1, 'rgba(0, 229, 255, 0.0)');

    ctx.beginPath();
    points.forEach((pt, i) => {
        const x = padLeft + pt.xRatio * plotW;
        const y = yFromDb(Math.max(minDb, Math.min(maxDb, pt.gain)));
        if (i === 0) ctx.moveTo(x, y);
        else ctx.lineTo(x, y);
    });
    ctx.lineTo(padLeft + plotW, zeroY);
    ctx.lineTo(padLeft, zeroY);
    ctx.closePath();
    ctx.fillStyle = grad;
    ctx.fill();

    // The Stroke Curve
    ctx.beginPath();
    points.forEach((pt, i) => {
        const x = padLeft + pt.xRatio * plotW;
        const y = yFromDb(Math.max(minDb, Math.min(maxDb, pt.gain)));
        if (i === 0) ctx.moveTo(x, y);
        else ctx.lineTo(x, y);
    });
    ctx.strokeStyle = '#00e5ff';
    ctx.lineWidth = 2;
    ctx.stroke();

    // 4. Hover Tooltip
    if (hoverX !== null && hoverX >= padLeft && hoverX <= padLeft + plotW) {
        const f = freqFromX(hoverX);
        let totalGain = state.masterBypass ? 0 : state.preamp;
        if (!state.masterBypass) {
            biquadFilters.forEach(filt => { totalGain += filt.getMagnitudeDb(f); });
        }
        const hoverYPoint = yFromDb(totalGain);

        // Crosshair
        ctx.strokeStyle = 'rgba(255, 255, 255, 0.3)';
        ctx.setLineDash([2, 2]);
        ctx.beginPath();
        ctx.moveTo(hoverX, padTop);
        ctx.lineTo(hoverX, padTop + plotH);
        ctx.stroke();
        ctx.setLineDash([]);

        // Hover circle
        ctx.fillStyle = '#00e5ff';
        ctx.beginPath();
        ctx.arc(hoverX, hoverYPoint, 4, 0, 2 * Math.PI);
        ctx.fill();

        // Tooltip box
        const tipText = `${Math.round(f)} Hz : ${totalGain > 0 ? '+' : ''}${totalGain.toFixed(1)} dB`;
        ctx.font = '10px Consolas, monospace';
        const textW = ctx.measureText(tipText).width;
        let boxX = hoverX + 8;
        if (boxX + textW + 12 > padLeft + plotW) boxX = hoverX - textW - 14;

        ctx.fillStyle = 'rgba(0, 0, 0, 0.85)';
        ctx.fillRect(boxX, hoverYPoint - 18, textW + 10, 16);
        ctx.strokeStyle = '#00e5ff';
        ctx.lineWidth = 1;
        ctx.strokeRect(boxX, hoverYPoint - 18, textW + 10, 16);

        ctx.fillStyle = '#ffffff';
        ctx.textAlign = 'left';
        ctx.fillText(tipText, boxX + 5, hoverYPoint - 6);
    }
}

// ReaComp Compressor Event Handling
function initReaCompEvents() {
    const comp = state.comp;

    // Power toggle
    const compPower = document.getElementById('compPower');
    compPower.addEventListener('change', (e) => {
        comp.enabled = e.target.checked;
        triggerSync();
    });

    // Simple Controls
    const sliderThresh = document.getElementById('sliderThresh');
    const lblThresh = document.getElementById('lblThresh');
    sliderThresh.addEventListener('input', (e) => {
        comp.threshDb = parseFloat(e.target.value);
        lblThresh.textContent = `${comp.threshDb.toFixed(1)} dB`;
        triggerSync();
    });

    const sliderWet = document.getElementById('sliderWet');
    const lblWet = document.getElementById('lblWet');
    sliderWet.addEventListener('input', (e) => {
        comp.wetDb = parseFloat(e.target.value);
        lblWet.textContent = `${comp.wetDb.toFixed(1)} dB`;
        triggerSync();
    });

    const chkAutoMakeup = document.getElementById('chkAutoMakeup');
    chkAutoMakeup.addEventListener('change', (e) => {
        comp.autoMakeup = e.target.checked;
        triggerSync();
    });

    // Advanced Drawer Toggle
    const btnToggleAdv = document.getElementById('btnToggleAdv');
    const advancedDrawer = document.getElementById('advancedDrawer');
    btnToggleAdv.addEventListener('click', () => {
        advancedDrawer.classList.toggle('open');
        btnToggleAdv.textContent = advancedDrawer.classList.contains('open') ? '▲ Hide Advanced' : '⚙️ Advanced VST';
    });

    // Advanced Slider Mappings
    const advSliders = [
        { id: 'sliderRatio', prop: 'ratio', lbl: 'lblRatio', fmt: v => `${v.toFixed(1)}:1` },
        { id: 'sliderAttack', prop: 'attackMs', lbl: 'lblAttack', fmt: v => `${v.toFixed(1)} ms` },
        { id: 'sliderRelease', prop: 'releaseMs', lbl: 'lblRelease', fmt: v => `${Math.round(v)} ms` },
        { id: 'sliderKnee', prop: 'kneeDb', lbl: 'lblKnee', fmt: v => `${v.toFixed(1)} dB` },
        { id: 'sliderPrecomp', prop: 'precompMs', lbl: 'lblPrecomp', fmt: v => `${v.toFixed(1)} ms` },
        { id: 'sliderRms', prop: 'rmsMs', lbl: 'lblRms', fmt: v => `${v.toFixed(1)} ms` },
        { id: 'sliderDry', prop: 'dryDb', lbl: 'lblDry', fmt: v => v <= -60 ? '-inf dB' : `${v.toFixed(1)} dB` },
        { id: 'sliderHipass', prop: 'hipassHz', lbl: 'lblHipass', fmt: v => `${Math.round(v)} Hz` },
        { id: 'sliderLowpass', prop: 'lowpassHz', lbl: 'lblLowpass', fmt: v => `${Math.round(v)} Hz` },
    ];

    advSliders.forEach(item => {
        const elem = document.getElementById(item.id);
        const lbl = document.getElementById(item.lbl);
        elem.addEventListener('input', (e) => {
            comp[item.prop] = parseFloat(e.target.value);
            lbl.textContent = item.fmt(comp[item.prop]);
            triggerSync();
        });
    });

    // Advanced Checkboxes
    document.getElementById('chkAutoRel').addEventListener('change', (e) => { comp.autoRelease = e.target.checked; triggerSync(); });
    document.getElementById('chkClassicAttack').addEventListener('change', (e) => { comp.classicAttack = e.target.checked; triggerSync(); });
    document.getElementById('chkAntiAlias').addEventListener('change', (e) => { comp.antiAlias = e.target.checked; triggerSync(); });

    // Comp Presets
    document.getElementById('compPresetSelect').addEventListener('change', (e) => {
        applyCompPreset(e.target.value);
    });
}

function applyCompPreset(presetName) {
    const c = state.comp;
    switch (presetName) {
        case 'glue':
            c.threshDb = -12; c.ratio = 2.0; c.attackMs = 15; c.releaseMs = 120; c.kneeDb = 6; c.autoMakeup = true;
            break;
        case 'punch':
            c.threshDb = -16; c.ratio = 4.0; c.attackMs = 30; c.releaseMs = 80; c.kneeDb = 2; c.autoMakeup = true;
            break;
        case 'tamer':
            c.threshDb = -20; c.ratio = 3.5; c.attackMs = 2; c.releaseMs = 60; c.kneeDb = 4; c.autoMakeup = true;
            break;
        case 'brickwall':
            c.threshDb = -3; c.ratio = 50.0; c.attackMs = 0.5; c.releaseMs = 50; c.kneeDb = 0; c.autoMakeup = false;
            break;
        case 'clean':
        default:
            c.threshDb = -18; c.ratio = 4.0; c.attackMs = 3; c.releaseMs = 100; c.kneeDb = 3; c.autoMakeup = true;
            break;
    }
    updateCompUI();
    triggerSync();
}

function updateCompUI() {
    const c = state.comp;
    document.getElementById('sliderThresh').value = c.threshDb;
    document.getElementById('lblThresh').textContent = `${c.threshDb.toFixed(1)} dB`;
    document.getElementById('sliderWet').value = c.wetDb;
    document.getElementById('lblWet').textContent = `${c.wetDb.toFixed(1)} dB`;
    document.getElementById('chkAutoMakeup').checked = c.autoMakeup;

    document.getElementById('sliderRatio').value = c.ratio;
    document.getElementById('lblRatio').textContent = `${c.ratio.toFixed(1)}:1`;
    document.getElementById('sliderAttack').value = c.attackMs;
    document.getElementById('lblAttack').textContent = `${c.attackMs.toFixed(1)} ms`;
    document.getElementById('sliderRelease').value = c.releaseMs;
    document.getElementById('lblRelease').textContent = `${Math.round(c.releaseMs)} ms`;
    document.getElementById('sliderKnee').value = c.kneeDb;
    document.getElementById('lblKnee').textContent = `${c.kneeDb.toFixed(1)} dB`;
}

// Header & Footer Actions
function initHeaderAndFooter() {
    initDeviceSelectorModal();

    // Reset EQ
    document.getElementById('btnResetEq').addEventListener('click', () => {
        state.preamp = 0;
        preampSlider.value = 0;
        preampVal.value = '0.0 dB';
        state.bands.forEach(b => {
            b.gain = 0;
            const slider = document.querySelector(`.band-slider[data-id="${b.id}"]`);
            const box = document.querySelector(`.gain-val-box[data-id="${b.id}"]`);
            if (slider) slider.value = 0;
            if (box) box.value = '0.0 dB';
        });
        rebuildBiquadFilters();
        renderCanvas();
        triggerSync();
    });

    // Master Bypass
    document.getElementById('masterBypass').addEventListener('change', (e) => {
        state.masterBypass = e.target.checked;
        renderCanvas();
        triggerSync();
    });

    // Presets
    document.getElementById('presetSelect').addEventListener('change', (e) => {
        applyPreset(e.target.value);
    });

    // Live Sync Checkbox
    const chkAutoApply = document.getElementById('chkAutoApply');
    chkAutoApply.addEventListener('change', (e) => {
        state.autoApply = e.target.checked;
    });

    // Apply Now Button
    document.getElementById('btnApplyNow').addEventListener('click', () => {
        syncConfig();
    });

    // View config.txt Dialog
    const configDialog = document.getElementById('configDialog');
    const configTextarea = document.getElementById('configTextarea');
    document.getElementById('btnExportConfig').addEventListener('click', () => {
        configTextarea.value = generateApoConfigText();
        configDialog.showModal();
    });

    document.getElementById('btnCloseDialog').addEventListener('click', () => {
        configDialog.close();
    });

    document.getElementById('btnCopyConfig').addEventListener('click', () => {
        configTextarea.select();
        navigator.clipboard.writeText(configTextarea.value);
        alert('Copied to clipboard!');
    });
}

function applyPreset(presetName) {
    const resetGains = (gains) => {
        gains.forEach((g, i) => {
            if (state.bands[i]) {
                state.bands[i].gain = g;
                const slider = document.querySelector(`.band-slider[data-id="${state.bands[i].id}"]`);
                const box = document.querySelector(`.gain-val-box[data-id="${state.bands[i].id}"]`);
                if (slider) slider.value = g;
                if (box) box.value = `${g.toFixed(1)} dB`;
            }
        });
    };

    switch (presetName) {
        case 'bass_boost':
            state.preamp = -3.0;
            resetGains([6.0, 5.0, 3.5, 1.5, 0.0, 0.0, 0.0, 0.5, 1.0, 1.5]);
            break;
        case 'vocal':
            state.preamp = -1.5;
            resetGains([-2.0, -1.0, 0.0, 1.0, 2.5, 3.5, 3.0, 2.0, 1.0, 0.0]);
            break;
        case 'gaming':
            state.preamp = -2.0;
            resetGains([2.0, 1.0, 0.0, 0.5, 1.5, 3.0, 4.0, 3.5, 2.0, 1.0]);
            break;
        case 'music_warm':
            state.preamp = -2.0;
            resetGains([4.0, 3.5, 2.0, 1.0, 0.0, 0.5, 1.0, 2.0, 3.0, 3.5]);
            break;
        case 'flat':
        default:
            state.preamp = 0.0;
            resetGains([0, 0, 0, 0, 0, 0, 0, 0, 0, 0]);
            break;
    }

    preampSlider.value = state.preamp;
    preampVal.value = `${state.preamp.toFixed(1)} dB`;
    rebuildBiquadFilters();
    renderCanvas();
    triggerSync();
}

// Convert state to valid Equalizer APO text syntax
function generateApoConfigText() {
    let out = `# ========================================================\n`;
    out += `# DolphinXVDO v1.3.10 - Audio Processing Object & ReaComp VST\n`;
    out += `# Generated: ${new Date().toLocaleString()}\n`;
    out += `# ========================================================\n\n`;

    if (state.masterBypass) {
        out += `# DOLPHINXVDO_BYPASS_ALL: ON\n`;
        out += `Preamp: 0.0 dB\n\n`;
        out += `# --- Equalizer Bands (Master Bypassed) ---\n`;
        state.bands.forEach((b, idx) => {
            out += `# Filter ${idx + 1}: ON ${b.type} Fc ${b.freq} Hz Gain ${b.gain.toFixed(1)} dB Q ${b.q.toFixed(2)}\n`;
        });
        out += `\n`;
        if (state.comp.enabled) {
            out += `# --- ReaComp VST Compressor (Master Bypassed) ---\n`;
            const threshVal = Math.pow(10, state.comp.threshDb / 20).toFixed(5);
            const ratioVal = Math.min(1.0, Math.max(0.0, (state.comp.ratio - 1) / 49)).toFixed(5);
            const attackVal = Math.min(1.0, state.comp.attackMs / 500).toFixed(5);
            const releaseVal = Math.min(1.0, state.comp.releaseMs / 2000).toFixed(5);
            const wetVal = Math.pow(10, state.comp.wetDb / 20).toFixed(5);
            const dryVal = state.comp.dryDb <= -60 ? '0.00000' : Math.pow(10, state.comp.dryDb / 20).toFixed(5);
            const autoMkUpVal = state.comp.autoMakeup ? '1.0' : '0.0';
            const kneeVal = (state.comp.kneeDb / 24).toFixed(5);
            const pluginLib = serverAppInfo ? serverAppInfo.pluginPath.replace(/\\/g, '/') : 'reacomp-standalone.dll';
            out += `# VSTPlugin: Library "${pluginLib}" Thresh ${threshVal} Ratio ${ratioVal} Attack ${attackVal} Release ${releaseVal} Knee ${kneeVal} Wet ${wetVal} Dry ${dryVal} AutoMkUp ${autoMkUpVal}\n`;
        }
        return out;
    }

    out += `# DOLPHINXVDO_BYPASS_ALL: OFF\n`;
    out += `Preamp: ${state.preamp.toFixed(1)} dB\n\n`;

    // 2. EQ Bands
    out += `# --- Equalizer Bands ---\n`;
    state.bands.forEach((b, idx) => {
        const typeStr = b.type; // PK, LS, HS
        out += `Filter ${idx + 1}: ON ${typeStr} Fc ${b.freq} Hz Gain ${b.gain.toFixed(1)} dB Q ${b.q.toFixed(2)}\n`;
    });
    out += `\n`;

    // 3. ReaComp VST Compressor
    if (state.comp.enabled) {
        out += `# --- ReaComp VST Compressor ---\n`;
        const threshVal = Math.pow(10, state.comp.threshDb / 20).toFixed(5);
        const ratioVal = Math.min(1.0, Math.max(0.0, (state.comp.ratio - 1) / 49)).toFixed(5);
        const attackVal = Math.min(1.0, state.comp.attackMs / 500).toFixed(5);
        const releaseVal = Math.min(1.0, state.comp.releaseMs / 2000).toFixed(5);
        const wetVal = Math.pow(10, state.comp.wetDb / 20).toFixed(5);
        const dryVal = state.comp.dryDb <= -60 ? '0.00000' : Math.pow(10, state.comp.dryDb / 20).toFixed(5);
        const autoMkUpVal = state.comp.autoMakeup ? '1.0' : '0.0';
        const kneeVal = (state.comp.kneeDb / 24).toFixed(5);

        const pluginLib = serverAppInfo ? serverAppInfo.pluginPath.replace(/\\/g, '/') : 'reacomp-standalone.dll';
        out += `VSTPlugin: Library "${pluginLib}" Thresh ${threshVal} Ratio ${ratioVal} Attack ${attackVal} Release ${releaseVal} Knee ${kneeVal} Wet ${wetVal} Dry ${dryVal} AutoMkUp ${autoMkUpVal}\n`;
    }

    return out;
}

let syncTimeout = null;
function triggerSync() {
    if (!state.autoApply) return;
    clearTimeout(syncTimeout);
    syncTimeout = setTimeout(() => {
        syncConfig();
    }, 150);
}

// Send config to local Node server API
async function syncConfig() {
    const configText = generateApoConfigText();
    const statusMsg = document.getElementById('statusMessage');

    try {
        const res = await fetch('/api/save-config', {
            method: 'POST',
            headers: { 'Content-Type': 'application/json' },
            body: JSON.stringify({ config: configText })
        });
        const data = await res.json();
        if (data.success) {
            statusMsg.innerHTML = `<span style="color: var(--accent-green);">● DolphinXVDO APO: Active ✓ (0ms Latency)</span> <span style="color: #888;">[${data.syncedPaths ? data.syncedPaths.join(', ') : 'Synced'}]</span>`;
        } else {
            statusMsg.innerHTML = `<span style="color: var(--accent-amber);">▲ Saved locally (${data.message})</span>`;
        }
    } catch (e) {
        // Running standalone without server
        localStorage.setItem('dolphinxvdo_config', configText);
        statusMsg.innerHTML = `<span style="color: #aaa;">● DolphinXVDO Portable Mode</span>`;
    }
}

// ----------------------------------------------------
// DolphinXVDO Native In-App Device Configurator
// ----------------------------------------------------
let cachedAudioDevices = [];

function initDeviceSelectorModal() {
    const btnOpenDevice = document.getElementById('btnOpenDeviceSelector');
    const deviceModal = document.getElementById('deviceModal');
    const btnClose = document.getElementById('btnCloseDeviceModal');
    const btnDone = document.getElementById('btnDoneDevice');
    const tabPlayback = document.getElementById('tabPlayback');
    const tabRecording = document.getElementById('tabRecording');
    const listPlayback = document.getElementById('deviceListPlayback');
    const listRecording = document.getElementById('deviceListRecording');
    const btnRestartAudio = document.getElementById('btnRestartAudio');
    const btnLegacy = document.getElementById('btnLegacyDeviceSelector');

    if (!btnOpenDevice || !deviceModal) return;

    // Open Modal
    btnOpenDevice.addEventListener('click', () => {
        deviceModal.showModal();
        loadAudioDevices();
    });

    // Close Modal
    const closeModal = () => {
        if (deviceModal.open) deviceModal.close();
    };
    if (btnClose) btnClose.addEventListener('click', closeModal);
    if (btnDone) btnDone.addEventListener('click', closeModal);
    deviceModal.addEventListener('click', (e) => {
        if (e.target === deviceModal) closeModal();
    });

    // Tab Switching
    if (tabPlayback && tabRecording) {
        tabPlayback.addEventListener('click', () => {
            tabPlayback.classList.add('active');
            tabRecording.classList.remove('active');
            listPlayback.style.display = 'flex';
            listRecording.style.display = 'none';
        });
        tabRecording.addEventListener('click', () => {
            tabRecording.classList.add('active');
            tabPlayback.classList.remove('active');
            listPlayback.style.display = 'none';
            listRecording.style.display = 'flex';
        });
    }

    // Restart Audio Service Button
    if (btnRestartAudio) {
        btnRestartAudio.addEventListener('click', async () => {
            const originalText = btnRestartAudio.textContent;
            const statusEl = document.getElementById('deviceModalStatus');
            try {
                btnRestartAudio.disabled = true;
                btnRestartAudio.textContent = '⏳ Restarting service...';
                if (statusEl) statusEl.innerHTML = '<span style="color: var(--accent-amber);">⏳ Restarting Windows Audio Service...</span>';

                const res = await fetch('/api/restart-audio', { method: 'POST' });
                const data = await res.json();
                if (data.success) {
                    if (statusEl) statusEl.innerHTML = '<span style="color: var(--accent-green);">✓ Windows Audio restarted successfully. Audio ready.</span>';
                } else {
                    if (statusEl) statusEl.innerHTML = `<span style="color: var(--accent-red);">▲ ${data.error || 'Failed to restart audio service'}</span>`;
                }
            } catch (err) {
                if (statusEl) statusEl.innerHTML = `<span style="color: var(--accent-red);">▲ Unable to connect to service</span>`;
            } finally {
                setTimeout(() => {
                    btnRestartAudio.disabled = false;
                    btnRestartAudio.textContent = originalText;
                }, 1500);
            }
        });
    }

    // Done Button
    const btnDoneDevice = document.getElementById('btnDoneDevice');
    if (btnDoneDevice) {
        btnDoneDevice.addEventListener('click', () => {
            closeDeviceModal();
        });
    }

    // Legacy Button
    const btnLegacy = document.getElementById('btnLegacyDeviceSelector');
    if (btnLegacy) {
        btnLegacy.addEventListener('click', async () => {
            try {
                await fetch('/api/launch-device-selector', { method: 'POST' });
            } catch (err) {
                console.error('Failed to launch legacy selector:', err);
            }
        });
    }
}

// Fetch & Load Devices
async function loadAudioDevices() {
    const loadingEl = document.getElementById('deviceLoading');
    const statusEl = document.getElementById('deviceModalStatus');

    try {
        if (loadingEl) loadingEl.style.display = 'block';
        if (statusEl) statusEl.innerHTML = '<span>● Scanning devices...</span>';

        const res = await fetch('/api/audio-devices');
        const data = await res.json();

        if (loadingEl) loadingEl.style.display = 'none';

        if (!data.success) {
            if (statusEl) statusEl.innerHTML = `<span style="color: var(--accent-red);">▲ ${data.error || 'Failed to scan devices'}</span>`;
            return;
        }

        cachedAudioDevices = data.devices || [];
        if (statusEl) statusEl.innerHTML = '<span style="color: var(--accent-green);">● Scan complete</span>';

        renderAudioDevices(cachedAudioDevices);
    } catch (err) {
        if (loadingEl) loadingEl.textContent = 'Failed to load audio devices: ' + err.message;
        if (statusEl) statusEl.innerHTML = '<span style="color: var(--accent-red);">▲ Failed to read Windows Audio Registry</span>';
    }
}

// Render Devices into Lists
function renderAudioDevices(devices) {
    const listPlayback = document.getElementById('deviceListPlayback');
    const listRecording = document.getElementById('deviceListRecording');
    const countPlayback = document.getElementById('countPlayback');
    const countRecording = document.getElementById('countRecording');

    if (!listPlayback || !listRecording) return;

    listPlayback.innerHTML = '';
    listRecording.innerHTML = '';

    const playbackDevs = devices.filter(d => d.type === 'Playback');
    const recordingDevs = devices.filter(d => d.type === 'Recording');

    if (countPlayback) countPlayback.textContent = playbackDevs.length;
    if (countRecording) countRecording.textContent = recordingDevs.length;

    // Helper to build a card
    const createDeviceCard = (dev) => {
        const card = document.createElement('div');
        card.className = `device-card ${dev.installed ? 'installed' : ''}`;
        card.id = `devcard-${dev.id.replace(/[^a-zA-Z0-9]/g, '')}`;

        const info = document.createElement('div');
        info.className = 'device-info';

        const nameRow = document.createElement('div');
        nameRow.className = 'device-name-row';

        const title = document.createElement('span');
        title.className = 'device-title';
        title.textContent = dev.name;

        const tag = document.createElement('span');
        tag.className = `device-tag ${dev.installed ? 'tag-active' : 'tag-off'}`;
        tag.id = `tag-${dev.id.replace(/[^a-zA-Z0-9]/g, '')}`;
        tag.textContent = dev.installed ? 'APO ACTIVE' : 'OFF';

        nameRow.appendChild(title);
        nameRow.appendChild(tag);

        const sub = document.createElement('div');
        sub.className = 'device-sub';
        sub.textContent = dev.adapter ? dev.adapter : dev.id;

        info.appendChild(nameRow);
        info.appendChild(sub);

        const toggleWrap = document.createElement('div');
        toggleWrap.className = 'device-toggle-wrap';

        const switchLabel = document.createElement('label');
        switchLabel.className = 'switch';

        const chk = document.createElement('input');
        chk.type = 'checkbox';
        chk.checked = !!dev.installed;

        chk.addEventListener('change', async (e) => {
            const wantEnable = e.target.checked;
            await handleDeviceToggle(dev, wantEnable, card, tag, chk);
        });

        const slider = document.createElement('span');
        slider.className = 'slider-round';

        switchLabel.appendChild(chk);
        switchLabel.appendChild(slider);
        toggleWrap.appendChild(switchLabel);

        card.appendChild(info);
        card.appendChild(toggleWrap);
        return card;
    };

    playbackDevs.forEach(d => listPlayback.appendChild(createDeviceCard(d)));
    recordingDevs.forEach(d => listRecording.appendChild(createDeviceCard(d)));

    if (playbackDevs.length === 0) {
        listPlayback.innerHTML = '<div style="color: #888; text-align: center; padding: 20px;">No playback devices found</div>';
    }
    if (recordingDevs.length === 0) {
        listRecording.innerHTML = '<div style="color: #888; text-align: center; padding: 20px;">No recording devices found</div>';
    }
}

// Handle Device APO Toggle
async function handleDeviceToggle(dev, enable, card, tag, chk) {
    const statusEl = document.getElementById('deviceModalStatus');
    if (statusEl) statusEl.innerHTML = `<span style="color: var(--accent-cyan);">⏳ Configuring ${dev.name}...</span>`;

    chk.disabled = true;

    try {
        const res = await fetch('/api/toggle-device-apo', {
            method: 'POST',
            headers: { 'Content-Type': 'application/json' },
            body: JSON.stringify({
                deviceId: dev.id,
                type: dev.type,
                enable: enable
            })
        });
        const data = await res.json();

        if (data.success) {
            dev.installed = enable;
            if (enable) {
                card.classList.add('installed');
                tag.className = 'device-tag tag-active';
                tag.textContent = 'APO ACTIVE';
                if (statusEl) statusEl.innerHTML = `<span style="color: var(--accent-green);">✓ DolphinXVDO enabled on [${dev.name}]</span>`;
            } else {
                card.classList.remove('installed');
                tag.className = 'device-tag tag-off';
                tag.textContent = 'OFF';
                if (statusEl) statusEl.innerHTML = `<span style="color: var(--text-secondary);">● Disabled on [${dev.name}]</span>`;
            }
        } else {
            // Revert checkbox on error
            chk.checked = !enable;
            if (statusEl) statusEl.innerHTML = `<span style="color: var(--accent-red);">▲ Error: ${data.error || 'Failed to update Registry'}</span>`;
        }
    } catch (err) {
        chk.checked = !enable;
        if (statusEl) statusEl.innerHTML = `<span style="color: var(--accent-red);">▲ Connection error: ${err.message}</span>`;
    } finally {
        chk.disabled = false;
    }
}

