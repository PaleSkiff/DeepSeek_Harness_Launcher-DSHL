# DeepSeek Harness Launcher

> [English](README.en.md) | [简体中文](README.md)

A Windows desktop launcher for **starting, stopping, restarting and monitoring a local DeepSeek Harness service** with one click, plus dependency installation, configuration management, real-time logs and a system tray.

> Tech stack: C# / WPF / .NET 8 (Windows 10/11 x64 only) · Pure black theme · Chinese & English UI

---

## 1. What problem does it solve?

DeepSeek Harness (`@deepseek-ai/dsh`) is a local AI-agent framework normally run from a command line:

```text
npx --verbose @deepseek-ai/dsh web
```

This is unfriendly to non-command-line users: you have to remember the command, install node.js yourself, watch the startup state, and debug port conflicts / timeouts / missing dependencies. This project puts all of that into a GUI:

- **No command line needed**: click "Start" to run the local DSH service; the browser opens automatically once ready.
- **Auto environment setup**: install node.js (offline installer preferred) and DSH with one click.
- **Visible status**: five states (Stopped / Starting / Running / Stopping / Faulted) shown live, with the tray icon color synced.
- **Easy troubleshooting**: service output streams in real time and is saved to daily log files; config and logs live in the program folder (portable).

## 2. Main features

| Module | Description |
|---|---|
| **Service Control** (home) | One-click Start / Stop / Restart. Clicking Start immediately shows "Starting" (state transitions first, then environment prep); opens `http://127.0.0.1:3080` in the browser automatically when ready |
| **Status monitoring** | Five-state machine (Stopped/Starting/Running/Stopping/Faulted) with port probing + HTTP health checks; start/stop timeouts; detects unexpected process exit |
| **Logs** | Live capture of service stdout/stderr, auto-scroll, level filter (All/INFO/WARN/ERROR/DEBUG), clear/copy/export `.log/.txt`; daily files under `logs\` cleaned by retention days |
| **Configuration** | Form + JSON text dual-mode editing of command/arguments/working dir/env vars/port/timeouts/health check; saved to `config.json` in the program folder (portable) |
| **Environment / Install** | Auto parallel detection of node.js and DSH; **one-click node.js install** (local offline MSI preferred → winget → official download, UAC elevation + progress heartbeat); **one-click DSH install** (hidden console runs `npx --yes --verbose @deepseek-ai/dsh --help`, real-time detailed log + package verification, elevated npm global install fallback); install buttons auto-disabled once installed |
| **System tray** | Status-colored icon + context menu (Start/Stop/Restart/Open/Exit), double-click to restore; rounded toast card at the top-right when minimized to tray |
| **Settings** | Launch on boot (to tray), auto-start service on launch, close behavior (minimize to tray / exit + ask on first close), log retention days, stop service on exit, **Chinese/English language switch** |
| **Guidance & dialogs** | First-run onboarding dialog (intro + steps + environment status); clear prompts for port conflicts, install results, etc. |

## 3. System requirements

- Windows 10 / 11 (64-bit)
- No .NET runtime required (self-contained build)
- **node.js**: recommended to install in-app via the Environment / Install page (the installer bundles an offline MSI, no internet needed); you may also install it yourself first
- DeepSeek Harness is installed/downloaded by the app (installing DSH requires internet access to the npm registry)

## 4. Installation

### Option A: Setup program (recommended)

Run `installer\DeepSeekHarnessLauncher_Setup_v1.0.0.exe` (Inno Setup, per-user install, no admin required):

- Installs to `%LOCALAPPDATA%\DeepSeek Harness Launcher`
- Bundles the offline node.js MSI (`nodejs\`), so node.js can be installed without internet
- Optional desktop shortcut; uninstalling also removes `logs\` and `config.json` from the program folder

### Option B: Green release (full folder)

In the project root (requires .NET 8 SDK):

```powershell
powershell -ExecutionPolicy Bypass -File publish.ps1
```

Output goes to `publish\`; copy the whole folder to a Windows 10/11 x64 machine and double-click the exe (self-contained, no .NET runtime needed).

## 5. Usage

1. **First launch**: an onboarding dialog appears (intro, steps, environment status). Click "Get Started".
2. **Check environment**: open the "Environment / Install" page (auto-detection runs). If node.js or DSH is missing, click the corresponding Install button (approve the UAC prompt for node.js; the DSH install log streams live and shows `[Success]` when done).
3. **Start the service**: back on the "Service Control" page, click "▶ Start" — the status immediately becomes "Starting", then "Running", and the browser opens `http://127.0.0.1:3080`.
4. **Everyday use**:
   - Stop / Restart: click "■ Stop" / "↻ Restart";
   - Stay in background: click "—" in the title bar to minimize to tray (a toast card appears at the top-right); double-click the tray icon to restore;
   - Tray right-click: Start / Stop / Restart / Open Main Window / Exit;
   - View output: "Logs" page (live scrolling, filter/export);
   - Change the port etc.: "Config" page, save and restart the service;
   - Auto-start, close behavior, language: "Settings" page.
5. **Exit**: if the service is running you will be prompted to stop it, avoiding orphan processes.

## 6. Input / output examples

### 6.1 Environment check (output box on the Environment / Install page)

Input: click "Re-check" (or the page auto-detects)

Output (example):

```text
> node -v
v24.19.0

> npx --no-install @deepseek-ai/dsh --help
Usage: dsh [options] [command] ...

> npm view @deepseek-ai/dsh version
0.1.0
```

Status shown: `node.js ✓ v24.19.0`, `DeepSeek Harness ✓ 0.1.0` (or ✗ missing / unavailable, with the install button enabled).

### 6.2 Installing node.js (local offline package)

Input: click "Auto install (winget)" → approve the UAC prompt

Output (install log, with elevation notice and silent-install heartbeat):

```text
[Installing Node.js]
Searching for local offline installer…
Using local offline installer: node-v24.19.0-x64.msi
Command: msiexec /i "…\node-v24.19.0-x64.msi" /qn /norestart
Requesting administrator privileges (UAC), click "Yes" in the prompt…
Installing silently, please wait (10s elapsed)…
[Success] Node.js installation complete
```

The environment is re-checked automatically after installation.

### 6.3 Installing DeepSeek Harness (hidden console, lightweight verification)

Input: click "Install DeepSeek Harness"

Output (install log, streaming, batched):

```text
[Installing DeepSeek Harness]
Installing & verifying via npx --yes --verbose @deepseek-ai/dsh --help (detailed log)
Command: npx --yes --verbose @deepseek-ai/dsh --help
npm verbose cli C:\Program Files\nodejs\node.exe C:\Program Files\nodejs\node_modules\npm\bin\npm-cli.js
npm http fetch GET 200 https://registry.npmjs.org/@deepseek-ai%2fdsh 855ms (cache revalidated)
Usage: dsh [options] [command] ...
[Success] DeepSeek Harness installation complete
```

> Note: installation uses a lightweight `--help` verification (it does NOT start the full web service, so low-spec machines won't run out of memory and crash); the actual service is started by the "Start" button when needed.

### 6.4 Starting / stopping the service (Service Control page)

Input: click "▶ Start"

Output (state transition):

```text
Stopped → Starting → Running (PID 12345 · 127.0.0.1:3080)
```

The browser then opens `http://127.0.0.1:3080`; the status bar and tray icon turn green "Running".

Input: click "■ Stop"

Output:

```text
Running → Stopping → Stopped
```

### 6.5 Config file `config.json` (program folder, portable)

Defaults are used on first run and the file is written on first save; full example:

```json
{
  "service": {
    "command": "npx",
    "arguments": "--verbose @deepseek-ai/dsh web",
    "workingDirectory": "",
    "environmentVariables": {}
  },
  "network": {
    "port": 3080,
    "healthCheckUrl": "",
    "healthCheckIntervalSeconds": 5
  },
  "timeout": {
    "startSeconds": 60,
    "stopSeconds": 15
  },
  "behavior": {
    "autoStartServiceOnLaunch": false,
    "closeToTray": true,
    "askOnFirstClose": true,
    "stopServiceOnExit": true
  },
  "startup": {
    "autoStartOnBoot": false
  },
  "logging": {
    "retentionDays": 7
  },
  "language": "zh-CN"
}
```

If corrupted, it is backed up to `config.json.bad` and the app continues with defaults; deleting it restores defaults on next launch.

## 7. Data files (all in the program folder, portable)

- `config.json`: all launcher settings (above).
- `logs\`: daily rolling service logs (`yyyy-MM-dd.log`), auto-cleaned by retention days.

## 8. Build & test

```powershell
# Build
dotnet build src/DeepSeekHarnessLauncher/DeepSeekHarnessLauncher.csproj -c Debug

# Run all tests (xUnit, 200+)
dotnet test DeepSeekHarnessLauncher.sln

# Publish (self-contained full folder → publish\)
powershell -ExecutionPolicy Bypass -File publish.ps1

# Build the setup program (requires Inno Setup 6; point ISCC.exe to your install location)
ISCC.exe installer\dshl-setup.iss
```

## 9. Directory structure

```text
DeepSeek Harness Launcher/
├── README.md / README.en.md / LICENSE
├── publish.ps1                  # self-contained publish script
├── installer/                   # Inno Setup script + output setup exe
├── logo/                        # app icon source images
└── src/
    ├── DeepSeekHarnessLauncher/          # WPF main project
    └── DeepSeekHarnessLauncher.Tests/    # xUnit test project
```

## License

Released under the **MIT License** — see [LICENSE](LICENSE).
