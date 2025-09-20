# Codex for Visual Studio (Unofficial) — VS2022 Starter

This is a minimal VSSDK scaffold for Visual Studio 2022 (17.x). It provides an AsyncPackage,
a Tool Window named "Codex", a Tools menu command to open it, and an Editor context menu
command to "Add to Codex chat". It spawns `codex proto` and streams JSON lines.

Create a VSIX Project w/Tool Window (Community) template and drop these files in. Then wire
GUIDs/IDs in your .vsct and .vsixmanifest if you keep your own. JavaScript is not required.

Prerequisites
- Visual Studio 2022 (17.x) with "Visual Studio extension development" workload.
- Codex CLI installed and on PATH, or provide an explicit path in Options.
- Optional: WSL installed if you choose to run Codex via WSL.

Debugging
- The project is configured to launch the Experimental instance (`/rootsuffix Exp`).
- Press F5 from Visual Studio to build and run the extension.
- Open "Tools → Open Codex" to show the tool window.
- Select code in the editor, right‑click, and choose "Add to Codex chat".

Options
- Tools → Options → Codex → General:
  - CLI Executable Path: Full path to `codex` (empty uses PATH).
  - Use WSL: Runs `wsl.exe -- codex proto` when enabled.
  - Open on Startup: Auto‑opens Codex tool window when VS loads.
