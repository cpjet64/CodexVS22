# Codex for Visual Studio (Unofficial) — VS2022 Starter

This is a minimal VSSDK scaffold for Visual Studio 2022 (17.x). It provides an AsyncPackage,
a Tool Window named "Codex", a Tools menu command to open it, and an Editor context menu
command to "Add to Codex chat". It spawns `codex proto` and streams JSON lines.

Create a VSIX Project w/Tool Window (Community) template and drop these files in. Then wire
GUIDs/IDs in your .vsct and .vsixmanifest if you keep your own. JavaScript is not required.