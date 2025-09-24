# Codex for Visual Studio 2022

**AI-powered coding assistant for Visual Studio 2022**

This extension integrates with the Codex CLI to provide intelligent code suggestions, chat functionality, diff/exec/approvals, and MCP (Model Context Protocol) tool support directly within Visual Studio 2022.

## ⚠️ Important Disclaimers

- **Unofficial Extension**: This is an unofficial extension and is not affiliated with or endorsed by Anthropic or Microsoft.
- **Third-Party Dependencies**: Requires the Codex CLI to be installed and properly configured.
- **Beta Software**: This extension is in active development and may contain bugs or incomplete features.
- **Use at Your Own Risk**: The authors are not responsible for any issues, data loss, or problems that may arise from using this extension.

## 🚀 Features

- **AI Chat Interface**: Interactive chat with Codex AI directly in Visual Studio
- **Code Context Integration**: Right-click to add selected code to chat
- **Diff and Patch Support**: View and apply code changes with approval workflows
- **Command Execution**: Run commands with AI assistance and approval
- **MCP Tools**: Integration with Model Context Protocol tools and servers
- **Custom Prompts**: Predefined prompts for common coding tasks
- **WSL Support**: Run Codex CLI through Windows Subsystem for Linux
- **Persistent Settings**: User preferences and solution-specific overrides

## 📋 Prerequisites

- **Visual Studio 2022** (17.x) with "Visual Studio extension development" workload
- **Codex CLI** installed and on PATH, or provide an explicit path in Options
- **Optional**: WSL installed if you choose to run Codex via WSL

## 🛠️ Installation

1. Download the latest `.vsix` file from the [Releases](../../releases) page
2. Double-click the `.vsix` file to install the extension
3. Restart Visual Studio 2022
4. Open the Codex tool window via **Tools → Open Codex**

## 🚀 Getting Started

1. **Open Codex**: Go to **Tools → Open Codex** to show the tool window
2. **Add Code to Chat**: Select code in the editor, right-click, and choose **"Add to Codex chat"**
3. **Configure Settings**: Go to **Tools → Options → Codex** to configure CLI path and other settings
4. **Authenticate**: Click the Login button in the tool window to authenticate with Codex

## 📹 Demo Videos

*Demo videos will be added here to showcase the extension's capabilities.*

### Quick Start Demo
*[Installation and basic setup demo - Coming Soon]*

### Code Context Integration
*[Right-click code integration demo - Coming Soon]*

### MCP Tools in Action
*[MCP tools integration demo - Coming Soon]*

### Custom Prompts
*[Custom prompts usage demo - Coming Soon]*

### Diff and Patch Workflow
*[Code diff and patch approval demo - Coming Soon]*

## 📸 Screenshots

*Screenshots will be added here to show the extension's interface.*

### Main Interface
*[Main tool window screenshot - Coming Soon]*

### Options Configuration
*[Options page screenshot - Coming Soon]*

### Diff Viewer
*[Diff viewer screenshot - Coming Soon]*

### Exec Console
*[Command execution console screenshot - Coming Soon]*

## ⚙️ Configuration

### Options (Tools → Options → Codex)

- **CLI Executable Path**: Full path to `codex` (empty uses PATH)
- **Use WSL**: Runs `wsl.exe -- codex proto` when enabled
- **Open on Startup**: Auto-opens Codex tool window when VS loads
- **Approval Mode**: Chat, Agent, or Agent (Full Access) for exec/patch
- **Sandbox Policy**: Security policy for code execution (Strict, Moderate, Permissive)
- **Default Model**: Model identifier for new chats
- **Default Reasoning**: Reasoning effort level (none, medium, high)

### MCP Configuration

1. Create a `codex.json` file in your project root
2. Add MCP server configurations
3. Restart Codex or refresh tools

## 🔐 Authentication

The tool window shows a login banner if `codex login status` reports you are logged out. Click **Login** to run `codex login` (respecting the WSL setting) and restart the background CLI. Use the **Logout** button to invalidate the session when needed.

## 🐛 Debugging

- The project is configured to launch the Experimental instance (`/rootsuffix Exp`)
- Press **F5** from Visual Studio to build and run the extension
- Check the **Codex Diagnostics** window for detailed logs and error information

## 📝 License

This project is licensed under the MIT License - see the [LICENSE](LICENSE) file for details.

## 🤝 Contributing

Contributions are welcome! Please feel free to submit a Pull Request. For major changes, please open an issue first to discuss what you would like to change.

## 📞 Support

- **Issues**: [GitHub Issues](../../issues)
- **Discussions**: [GitHub Discussions](../../discussions)
- **Documentation**: [Wiki](../../wiki)

## 🔗 Related Projects

- [Codex CLI](https://github.com/anthropics/codex-cli) - The official Codex command-line interface
- [Codex VS Code Extension](https://github.com/anthropics/codex-vscode) - Official VS Code extension

## 📊 Version History

### v0.1.0 (Initial Release)
- AI chat interface with Codex integration
- Code context integration (right-click to add to chat)
- Diff and patch support with approval workflows
- Command execution with AI assistance
- MCP tools integration
- Custom prompts support
- WSL support for CLI execution
- Persistent settings and solution-specific overrides
- Comprehensive options and configuration
- Telemetry and diagnostics support
