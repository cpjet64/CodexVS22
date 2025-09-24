Keybindings (T10.6)

Defaults
- No default keybindings assigned to avoid conflicts.
  - Open Codex (menu: Tools → Open Codex)
  - Add to Codex chat (context menu in editor)
  - Codex Diagnostics (menu: Tools → Codex Diagnostics)

Recommendations (user-configurable)
- Open Codex: Ctrl+Alt+C (if unassigned)
- Add to Codex chat: Ctrl+Alt+A (if unassigned)

How to customize in Visual Studio
- Tools → Options → Environment → Keyboard
- Filter by command names defined in VSCommandTable.vsct:
  - OpenCodexCommand
  - AddToChatCommand
  - DiagnosticsCommand

Notes
- Conflicts vary by environment; leave unassigned by default and let users bind per preference.
