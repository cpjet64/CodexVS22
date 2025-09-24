# Codex for Visual Studio 2022 - Release v0.1.0

**Release Date**: December 19, 2024  
**Version**: 0.1.0  
**Visual Studio Support**: 2022 (17.x)  
**Platform**: Windows 10/11  

## 🎉 What's New

This is the initial release of Codex for Visual Studio 2022, bringing AI-powered coding assistance directly to Visual Studio developers. This extension provides comprehensive integration with the Codex CLI, offering chat functionality, code context integration, diff/exec/approvals, MCP tools support, and extensive configuration options.

## ✨ Key Features

### 🤖 AI Chat Interface
- Interactive chat with Codex AI directly in Visual Studio
- Seamless integration with your coding workflow
- Context-aware responses based on your code

### 🔗 Code Context Integration
- Right-click to add selected code to chat
- Automatic context detection and inclusion
- Smart code selection and formatting

### 🔄 Diff and Patch Support
- View code changes with side-by-side diff display
- Apply patches with approval workflows
- Support for multiple approval modes (Chat, Agent, Agent Full Access)

### ⚡ Command Execution
- Run commands with AI assistance
- Real-time output display with ANSI color support
- Configurable execution policies and sandbox settings

### 🛠️ MCP Tools Integration
- Integration with Model Context Protocol tools and servers
- Dynamic tool discovery and execution
- Support for custom MCP server configurations

### 📝 Custom Prompts
- Predefined prompts for common coding tasks
- Click-to-insert functionality
- Persistent prompt management

### ⚙️ Comprehensive Configuration
- 20+ configurable settings
- Solution-specific overrides
- JSON export/import for settings backup
- Sandbox policies for security

## 🚀 Getting Started

1. **Install**: Download the `.vsix` file and double-click to install
2. **Configure**: Set your Codex CLI path in Tools → Options → Codex
3. **Open**: Go to Tools → Open Codex to launch the tool window
4. **Authenticate**: Click Login to authenticate with Codex
5. **Start Coding**: Select code, right-click, and choose "Add to Codex chat"

## 📋 System Requirements

- **Visual Studio 2022** (17.0 or later)
- **Windows 10/11** with .NET Framework 4.8 or .NET 8.0
- **Codex CLI** installed and configured
- **Optional**: WSL for Linux-based CLI execution

## 🔧 Configuration Options

### Core Settings
- **CLI Executable Path**: Full path to `codex` executable
- **Use WSL**: Enable Windows Subsystem for Linux execution
- **Open on Startup**: Auto-open tool window when VS loads

### Security & Execution
- **Approval Mode**: Chat, Agent, or Agent (Full Access)
- **Sandbox Policy**: Strict, Moderate, or Permissive
- **Default Model**: AI model selection for new chats
- **Default Reasoning**: Effort level (none, medium, high)

### UI & Behavior
- **Window State**: Position, size, and state persistence
- **Exec Console**: Height, visibility, and output limits
- **Auto Actions**: Auto-open patched files, auto-hide console

### Advanced
- **Solution Overrides**: Per-solution CLI path and WSL settings
- **Telemetry**: Anonymous usage tracking
- **Last Used**: Remember last used tools and prompts

## 🐛 Known Issues

### Current Limitations
- **VS Code Parity**: Some advanced VS Code features not yet implemented
- **Custom Options UI**: Uses standard property grid (no test connection button)
- **MCP Parameter Help**: Basic hover help (name/description only)
- **Demo Content**: No demo GIFs included in this release

### Performance Notes
- **Large Lists**: UI may become less responsive with 1000+ MCP tools
- **Memory Usage**: Exec console output is limited to prevent excessive memory usage
- **CLI Startup**: Initial connection may take 2-3 seconds

## 🔒 Security Considerations

- **Sandbox Policies**: Code execution respects configured security settings
- **CLI Path Validation**: Validates executable path before execution
- **WSL Security**: WSL execution inherits WSL security model
- **Session Management**: Secure handling of authentication tokens

## 📊 What's Included

### Files
- `CodexVS22.vsix` - Main extension package
- `CodexVS22.pdb` - Debug symbols
- `README.md` - User documentation
- `CHANGELOG.md` - Detailed change log
- `LICENSE` - MIT License

### Dependencies
- Community.VisualStudio.Toolkit 17.17.0.507
- Community.VisualStudio.VSCT 16.0.29.6
- Microsoft.VSSDK.BuildTools 17.9.3168
- Newtonsoft.Json 13.0.1

## 🆘 Support & Troubleshooting

### Getting Help
- **GitHub Issues**: Report bugs and request features
- **Diagnostics Pane**: Check detailed logs and error information
- **README**: Comprehensive setup and configuration guide

### Common Issues
1. **CLI Not Found**: Ensure Codex CLI is installed and on PATH
2. **Authentication Failed**: Check CLI login status and credentials
3. **WSL Issues**: Verify WSL installation and configuration
4. **UI Not Responsive**: Check for large MCP tool lists or memory issues

### Debug Information
- Enable detailed logging in the Diagnostics pane
- Check Visual Studio Output window for extension messages
- Verify CLI path and version in Options

## 🔄 Migration Notes

### From VS Code Extension
- Settings are not automatically migrated
- MCP configuration format is compatible
- Custom prompts need to be recreated

### From Previous Versions
- This is the initial release (0.1.0)
- No migration needed

## 🎯 Roadmap

### Planned for v0.2.0
- Demo GIFs and screenshots
- Marketplace publication
- Advanced MCP parameter support
- Custom options UI with test connection
- Settings migration from VS Code
- Performance optimizations

### Future Considerations
- Visual Studio 2025 support
- Enhanced AI model support
- Advanced debugging integration
- Team collaboration features

## 📝 License

This project is licensed under the MIT License. See the LICENSE file for details.

## 🤝 Contributing

Contributions are welcome! Please feel free to submit Pull Requests or open Issues for bugs and feature requests.

## 🙏 Acknowledgments

- **Anthropic**: For the Codex CLI and AI capabilities
- **Microsoft**: For Visual Studio extension platform
- **Community**: For feedback and contributions

---

## 📞 Contact

- **GitHub**: [curtp/codex-vs22](https://github.com/curtp/codex-vs22)
- **Issues**: [GitHub Issues](https://github.com/curtp/codex-vs22/issues)
- **Discussions**: [GitHub Discussions](https://github.com/curtp/codex-vs22/discussions)

---

**Thank you for using Codex for Visual Studio 2022!** 🎉

*This release represents months of development and testing. We hope it enhances your coding experience with AI assistance.*
