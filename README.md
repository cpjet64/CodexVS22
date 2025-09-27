# Codex for Visual Studio 2022

Codex for Visual Studio 2022 brings the Codex CLI into Microsoft Visual Studio so that you can chat with the Codex agent, run guided commands, review diffs, and interact with MCP tools without leaving the editor.

> **Unofficial project**: CodexVS22 is community maintained and is not an official product of Anthropic or Microsoft. The extension is under active development and APIs may change.

## Project Status
- Actively developed with frequent in-progress checkpoints.
- Requires the Codex CLI to be installed locally.
- Expect rough edges while release tasks in `todo-release.md` remain open.

## Features
- AI chat tool window with full conversation history and code selection context.
- Diff review and patch application workflow with approval gates.
- Execution console for running shell commands through Codex with optional WSL routing.
- MCP (Model Context Protocol) tool discovery, invocation, and prompt insertion helpers.
- Persistent solution scoped settings plus import/export for sharing configurations.
- Telemetry hooks (opt in) for tracking prompt and tool usage.

## Requirements
- Visual Studio 2022 version 17.6 or newer with the **Visual Studio extension development** workload.
- Codex CLI installed and available on `PATH`, or configure the absolute path in the Options page.
- Optional: Windows Subsystem for Linux if you prefer to run the CLI inside WSL.

## Installation
1. Download the latest `.vsix` package from the [releases](../../releases) page.
2. Double-click the package to launch the Visual Studio extension installer.
3. Restart Visual Studio after the installer completes.
4. Open the Codex tool window via `Tools -> Open Codex`.

## Getting Started
1. Authenticate: use the **Login** button in the Codex tool window (runs `codex login`).
2. Send context: right-click a selection and choose **Add to Codex chat** to preload snippets.
3. Adjust settings: visit `Tools -> Options -> Codex` for CLI path, approval modes, sandbox policy, and default models.
4. Try prompts: insert predefined prompts from the prompt palette or create your own.

## Configuration Notes
- Settings support per-solution overrides stored alongside the `.sln` file.
- WSL integration shells out to `wsl.exe -- codex ...` when enabled.
- MCP servers are declared in `codex.json`; use the tool window refresh action after editing the file.
- Execution safeguards route through approval workflows; see `PROMPTS.txt` for built-in command templates.

## Building From Source
```powershell
# Restore dependencies
msbuild CodexVS22.sln /t:Restore

# Build the VSIX (Release configuration recommended)
msbuild CodexVS22.sln /p:Configuration=Release
```
Generated artifacts land in `bin/` and can be installed by double-clicking the resulting `.vsix`.

## Testing
- Unit tests reside in `CodexVS22.Tests` and can be executed with `dotnet test`.
- Integration validation relies on manual smoke tests noted in `RELEASE_CHECKLIST_v0.1.0.md` and `POST_TEST_SUMMARY_v0.1.0.md`.

## Contributing
Contributions are welcome. Please:
1. Open an issue to discuss significant feature ideas.
2. Follow the provided issue templates for bug reports and feature requests.
3. Submit pull requests using the template to document testing and rationale.

## Support and Feedback
- File issues on the [GitHub tracker](../../issues).
- Check the `/docs` directory for build logs, release notes, and troubleshooting guides.
- Discussions and Q&A can happen via GitHub Discussions (if enabled) or by opening an issue.

## License
This repository is provided under the MIT License (see `LICENSE`). The VSIX package also includes the project-specific `EULA.md`; consult both documents before redistribution.
