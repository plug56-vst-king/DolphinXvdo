const http = require('http');
const fs = require('fs');
const path = require('path');
const { exec, spawn } = require('child_process');

const PORT = 52345;
const PUBLIC_DIR = path.join(__dirname, 'public');
const ENGINE_DIR = fs.existsSync(path.join(__dirname, 'engine')) ? path.join(__dirname, 'engine') : path.join(__dirname, 'DolphinXVDO', 'engine');
const CONFIG_DIR = path.join(__dirname, 'config');
const LOCAL_CONFIG_PATH = path.join(CONFIG_DIR, 'config.txt');
const LOCAL_PLUGIN_PATH = path.join(__dirname, 'plugins', 'reacomp-standalone.dll');

// Fallback/System APO paths
const SYSTEM_APO_DIRS = [
    path.join(__dirname, 'config', 'config.txt'),
    'C:\\Program Files\\DolphinXVDO\\config\\config.txt',
    'C:\\Program Files\\EqualizerAPO\\config\\config.txt'
];

// Ensure required directories exist
if (!fs.existsSync(CONFIG_DIR)) fs.mkdirSync(CONFIG_DIR, { recursive: true });
if (!fs.existsSync(path.join(__dirname, 'plugins'))) fs.mkdirSync(path.join(__dirname, 'plugins'), { recursive: true });

// Initial default config if empty
if (!fs.existsSync(LOCAL_CONFIG_PATH)) {
    const defaultCfg = `# ========================================================
# DolphinXVDO - Audio Processing Object & ReaComp VST
# ========================================================
Preamp: 0.0 dB

# --- Equalizer Bands ---
Filter 1: ON LS Fc 31 Hz Gain 0.0 dB Q 1.41
Filter 2: ON PK Fc 63 Hz Gain 0.0 dB Q 1.41
Filter 3: ON PK Fc 125 Hz Gain 0.0 dB Q 1.41
Filter 4: ON PK Fc 250 Hz Gain 0.0 dB Q 1.41
Filter 5: ON PK Fc 500 Hz Gain 0.0 dB Q 1.41
Filter 6: ON PK Fc 1000 Hz Gain 0.0 dB Q 1.41
Filter 7: ON PK Fc 2000 Hz Gain 0.0 dB Q 1.41
Filter 8: ON PK Fc 4000 Hz Gain 0.0 dB Q 1.41
Filter 9: ON PK Fc 8000 Hz Gain 0.0 dB Q 1.41
Filter 10: ON HS Fc 16000 Hz Gain 0.0 dB Q 1.41

# --- ReaComp VST Compressor ---
VSTPlugin: Library "${LOCAL_PLUGIN_PATH.replace(/\\/g, '/')}" Thresh 0.12589 Ratio 0.06122 Attack 0.00600 Release 0.05000 Knee 0.12500 Wet 1.00000 Dry 0.00000 AutoMkUp 1.0
`;
    fs.writeFileSync(LOCAL_CONFIG_PATH, defaultCfg, 'utf-8');
}

// Find DeviceSelector.exe
function getDeviceSelectorPath() {
    const candidates = [
        path.join(ENGINE_DIR, 'DeviceSelector.exe'),
        path.join(__dirname, 'DeviceSelector.exe'),
        'C:\\Program Files\\DolphinXVDO\\engine\\DeviceSelector.exe',
        'C:\\Program Files\\EqualizerAPO\\DeviceSelector.exe'
    ];
    for (const c of candidates) {
        if (fs.existsSync(c)) return c;
    }
    return null;
}

// MIME types
const MIME_TYPES = {
    '.html': 'text/html; charset=utf-8',
    '.css': 'text/css; charset=utf-8',
    '.js': 'text/javascript; charset=utf-8',
    '.json': 'application/json; charset=utf-8',
    '.png': 'image/png',
    '.ico': 'image/x-icon',
    '.svg': 'image/svg+xml'
};

// Execute PowerShell device manager helper
function runPowerShell(args, callback) {
    const psScript = path.join(ENGINE_DIR, 'device-manager.ps1');
    const ps = spawn('powershell', ['-NoProfile', '-ExecutionPolicy', 'Bypass', '-File', psScript, ...args], {
        windowsHide: true
    });
    let stdout = '';
    let stderr = '';
    ps.stdout.on('data', d => stdout += d.toString());
    ps.stderr.on('data', d => stderr += d.toString());
    ps.on('close', code => {
        callback(code, stdout.trim(), stderr.trim());
    });
}

// HTTP Request Handler
const server = http.createServer((req, res) => {
    // API: App Info
    if (req.method === 'GET' && req.url === '/api/app-info') {
        res.writeHead(200, { 'Content-Type': 'application/json' });
        res.end(JSON.stringify({
            appName: 'DolphinXVDO',
            version: '1.1.0',
            pluginPath: LOCAL_PLUGIN_PATH,
            configPath: LOCAL_CONFIG_PATH,
            deviceSelectorAvailable: !!getDeviceSelectorPath()
        }));
        return;
    }

    // API: Get Audio Devices List
    if (req.method === 'GET' && req.url === '/api/audio-devices') {
        runPowerShell(['-Action', 'list'], (code, stdout, stderr) => {
            if (code === 0 && stdout) {
                res.writeHead(200, { 'Content-Type': 'application/json' });
                res.end(stdout);
            } else {
                res.writeHead(500, { 'Content-Type': 'application/json' });
                res.end(JSON.stringify({ error: stderr || 'Failed to list audio devices' }));
            }
        });
        return;
    }

    // API: Toggle Device APO
    if (req.method === 'POST' && req.url === '/api/toggle-device-apo') {
        let body = '';
        req.on('data', chunk => body += chunk);
        req.on('end', () => {
            try {
                const { deviceId, type, enable } = JSON.parse(body);
                runPowerShell([
                    '-Action', 'set',
                    '-DeviceId', deviceId,
                    '-Type', type || 'Playback',
                    '-Enable', enable ? '$true' : '$false'
                ], (code, stdout, stderr) => {
                    if (code === 0 && stdout) {
                        res.writeHead(200, { 'Content-Type': 'application/json' });
                        res.end(stdout);
                    } else {
                        res.writeHead(500, { 'Content-Type': 'application/json' });
                        res.end(JSON.stringify({ error: stderr || 'Failed to update device APO state' }));
                    }
                });
            } catch (err) {
                res.writeHead(400, { 'Content-Type': 'application/json' });
                res.end(JSON.stringify({ error: err.message }));
            }
        });
        return;
    }

    // API: Restart Windows Audio Service
    if (req.method === 'POST' && req.url === '/api/restart-audio') {
        runPowerShell(['-Action', 'restart-audio'], (code, stdout, stderr) => {
            if (code === 0 && stdout) {
                res.writeHead(200, { 'Content-Type': 'application/json' });
                res.end(stdout);
            } else {
                res.writeHead(500, { 'Content-Type': 'application/json' });
                res.end(JSON.stringify({ error: stderr || 'Failed to restart audio service' }));
            }
        });
        return;
    }

    // API: Launch DeviceSelector (Legacy fallback)
    if (req.method === 'POST' && req.url === '/api/open-device-selector') {
        const dsPath = getDeviceSelectorPath();
        if (dsPath) {
            try {
                let args = [];
                const qss = path.join(path.dirname(dsPath), 'DeviceSelector.qss');
                if (fs.existsSync(qss)) {
                    args.push('-stylesheet', qss);
                }
                const child = spawn(dsPath, args, {
                    detached: true,
                    stdio: 'ignore'
                });
                child.unref();

                // Apply Modern Dark DWM Window Frame & Chrome (UI Presentation Only)
                const psCmd = `
Add-Type -TypeDefinition @"
using System;
using System.Runtime.InteropServices;
public class DwmHelper {
    [DllImport("dwmapi.dll")] public static extern int DwmSetWindowAttribute(IntPtr h, int a, ref int v, int s);
    [DllImport("user32.dll", EntryPoint = "GetWindowLongPtr")] public static extern IntPtr GetWindowLongPtr(IntPtr h, int idx);
    [DllImport("user32.dll", EntryPoint = "SetWindowLongPtr")] public static extern IntPtr SetWindowLongPtr(IntPtr h, int idx, IntPtr v);
    [DllImport("user32.dll")] public static extern bool SetWindowPos(IntPtr h, IntPtr a, int x, int y, int cx, int cy, uint f);
    [DllImport("user32.dll")] public static extern IntPtr FindWindow(string c, string n);
}
"@
for ($i = 0; $i -lt 40; $i++) {
    Start-Sleep -Milliseconds 50
    $h = [DwmHelper]::FindWindow("DeviceSelectorClass", $null)
    if ($h -ne [IntPtr]::Zero) {
        $s = [DwmHelper]::GetWindowLongPtr($h, -16).ToInt64() -bor 0x00020000L -bor 0x00010000L
        [DwmHelper]::SetWindowLongPtr($h, -16, [IntPtr]$s) | Out-Null
        [DwmHelper]::SetWindowPos($h, [IntPtr]::Zero, 0, 0, 0, 0, 0x0027) | Out-Null
        $one = 1; [DwmHelper]::DwmSetWindowAttribute($h, 20, [ref]$one, 4) | Out-Null
        $c = 0x0015100B; [DwmHelper]::DwmSetWindowAttribute($h, 35, [ref]$c, 4) | Out-Null
        $t = 0x00F5F0E8; [DwmHelper]::DwmSetWindowAttribute($h, 36, [ref]$t, 4) | Out-Null
        $b = 0x003E3426; [DwmHelper]::DwmSetWindowAttribute($h, 34, [ref]$b, 4) | Out-Null
        $r = 2; [DwmHelper]::DwmSetWindowAttribute($h, 33, [ref]$r, 4) | Out-Null
        break
    }
}
`;
                spawn('powershell.exe', ['-NoProfile', '-WindowStyle', 'Hidden', '-Command', psCmd], {
                    detached: true,
                    stdio: 'ignore'
                }).unref();
                res.writeHead(200, { 'Content-Type': 'application/json' });
                res.end(JSON.stringify({ success: true, path: dsPath }));
            } catch (err) {
                res.writeHead(500, { 'Content-Type': 'application/json' });
                res.end(JSON.stringify({ success: false, error: err.message }));
            }
        } else {
            res.writeHead(404, { 'Content-Type': 'application/json' });
            res.end(JSON.stringify({ success: false, error: 'DeviceSelector.exe not found' }));
        }
        return;
    }

    // API: Save Config
    if (req.method === 'POST' && req.url === '/api/save-config') {
        let body = '';
        req.on('data', chunk => body += chunk);
        req.on('end', () => {
            try {
                const { config } = JSON.parse(body);
                
                // 1. Always write to local config
                fs.writeFileSync(LOCAL_CONFIG_PATH, config, 'utf-8');

                // 2. Also sync to system APO locations if they exist
                const synced = [LOCAL_CONFIG_PATH];
                for (const targetPath of SYSTEM_APO_DIRS) {
                    if (targetPath !== LOCAL_CONFIG_PATH && fs.existsSync(path.dirname(targetPath))) {
                        try {
                            fs.writeFileSync(targetPath, config, 'utf-8');
                            synced.push(targetPath);
                        } catch (e) {
                            // Non-critical if permission denied
                        }
                    }
                }

                res.writeHead(200, { 'Content-Type': 'application/json' });
                res.end(JSON.stringify({
                    success: true,
                    syncedPaths: synced,
                    message: 'Config updated in real-time'
                }));
            } catch (err) {
                res.writeHead(500, { 'Content-Type': 'application/json' });
                res.end(JSON.stringify({ success: false, error: err.message }));
            }
        });
        return;
    }

    // Static Files
    let reqUrl = req.url === '/' ? '/index.html' : req.url;
    let filePath = path.join(PUBLIC_DIR, reqUrl.split('?')[0]);

    if (!fs.existsSync(filePath)) {
        res.writeHead(404, { 'Content-Type': 'text/plain' });
        res.end('404 Not Found');
        return;
    }

    const ext = path.extname(filePath).toLowerCase();
    const contentType = MIME_TYPES[ext] || 'application/octet-stream';

    fs.readFile(filePath, (err, data) => {
        if (err) {
            res.writeHead(500, { 'Content-Type': 'text/plain' });
            res.end('500 Internal Error');
            return;
        }
        const headers = { 'Content-Type': contentType };
        if (ext === '.js' || ext === '.css' || ext === '.png' || ext === '.ico' || ext === '.svg') {
            headers['Cache-Control'] = 'public, max-age=86400';
        }
        res.writeHead(200, headers);
        res.end(data);
    });
});

// Launch Desktop App Window (App mode in Chromium-based browser or default browser)
function launchAppWindow(appUrl) {
    const browserCandidates = [
        'C:\\Program Files\\BraveSoftware\\Brave-Browser\\Application\\brave.exe',
        'C:\\Program Files\\Google\\Chrome\\Application\\chrome.exe',
        'C:\\Program Files (x86)\\Microsoft\\Edge\\Application\\msedge.exe',
        'C:\\Program Files\\Microsoft\\Edge\\Application\\msedge.exe'
    ];

    let launched = false;
    for (const bPath of browserCandidates) {
        if (fs.existsSync(bPath)) {
            try {
                spawn(bPath, [`--app=${appUrl}`, '--window-size=1120,860', '--disable-features=TranslateUI'], {
                    detached: true,
                    stdio: 'ignore'
                }).unref();
                launched = true;
                console.log(`[DolphinXVDO] Standalone window launched with: ${bPath}`);
                break;
            } catch (err) {
                console.error('[DolphinXVDO] Browser spawn failed:', err);
            }
        }
    }

    if (!launched) {
        exec(`start ${appUrl}`);
        console.log(`[DolphinXVDO] Launched in default system browser.`);
    }
}

// Handle Server Errors (e.g. port already in use by background instance)
server.on('error', (err) => {
    if (err.code === 'EADDRINUSE') {
        const appUrl = `http://127.0.0.1:${PORT}`;
        console.log(`[DolphinXVDO] Port ${PORT} already active. Launching interface directly...`);
        launchAppWindow(appUrl);
        setTimeout(() => {
            process.exit(0);
        }, 800);
    } else {
        console.error('[DolphinXVDO] Server error:', err);
    }
});

// Start Server & Launch Desktop App Window
server.listen(PORT, '127.0.0.1', () => {
    const appUrl = `http://127.0.0.1:${PORT}`;
    console.log(`[DolphinXVDO] Audio Controller Service running at ${appUrl}`);
    launchAppWindow(appUrl);
});
