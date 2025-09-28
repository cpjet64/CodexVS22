# Changelog

All notable changes to this project will be documented in this file.

The format is based on [Keep a Changelog](https://keepachangelog.com/en/1.0.0/),
and this project adheres to [Semantic Versioning](https://semver.org/spec/v2.0.0.html).

## [0.1.0] - 2024-12-19

### Added

#### Core Features
- **AI Chat Interface**: Interactive chat with Codex AI directly in Visual Studio 2022
- **Code Context Integration**: Right-click to add selected code to chat with automatic context detection
- **Diff and Patch Support**: View and apply code changes with approval workflows
- **Command Execution**: Run commands with AI assistance and approval
- **MCP Tools Integration**: Integration with Model Context Protocol tools and servers
- **Custom Prompts**: Predefined prompts for common coding tasks with click-to-insert functionality

#### User Interface
- **Tool Window**: Dedicated Codex tool window accessible via Tools → Open Codex
- **Diagnostics Pane**: Real-time logging and error information for debugging
- **Status Bar Integration**: Visual feedback for operations and connection status
- **Responsive Design**: Adaptive UI that works with different window sizes and states

#### Configuration & Options
- **Comprehensive Settings**: Full options page with 20+ configurable settings
- **Solution-Specific Overrides**: Per-solution CLI path and WSL settings
- **Sandbox Policies**: Security presets (Strict, Moderate, Permissive) for code execution
- **Approval Modes**: Chat, Agent, and Agent (Full Access) modes for different use cases
- **Window State Persistence**: Remembers window position, size, and state across sessions

#### CLI Integration
- **Codex CLI Integration**: Seamless integration with `codex proto` JSON lines protocol
- **WSL Support**: Run Codex CLI through Windows Subsystem for Linux
- **Authentication Management**: Login/logout functionality with session persistence
- **Connection Validation**: Automatic CLI path and version validation
- **Error Handling**: Graceful handling of CLI connection issues and timeouts

#### Data Management
- **JSON Export/Import**: Export and import settings as JSON for backup and sharing
- **Last Used Tracking**: Remembers last used tools and prompts across sessions
- **Telemetry**: Anonymous usage tracking for tool invocations and prompt usage
- **Debounced Operations**: Prevents excessive API calls with intelligent debouncing

#### Testing & Quality
- **Unit Tests**: Comprehensive test suite for core functionality
- **Options Validation**: Input validation with clear error messages and hints
- **Error Recovery**: Robust error handling and recovery mechanisms
- **Performance Optimization**: Async operations to keep Visual Studio responsive
 - **Artifacts**: Linux junit.xml, perf.csv, coverage.lcov emitted by tests
 - **Stress/Perf**: Exec stream and MCP large list tests (2k–5k items)

### Technical Details

#### Architecture
- **VSIX Extension**: Built using Visual Studio Extension SDK 17.x
- **WPF UI**: Modern WPF-based user interface with Material Design principles
- **Async/Await**: Non-blocking operations throughout the codebase
- **Dependency Injection**: Clean separation of concerns with proper DI patterns

#### Dependencies
- **Community.VisualStudio.Toolkit**: 17.17.0.507
- **Community.VisualStudio.VSCT**: 16.0.29.6
- **Microsoft.VSSDK.BuildTools**: 17.9.3168
- **Newtonsoft.Json**: 13.0.1

#### Supported Platforms
- **Visual Studio 2022**: Version 17.0 and above
- **Windows**: Windows 10/11 with .NET Framework 4.8 or .NET 8.0
- **WSL**: Windows Subsystem for Linux (optional)

### Known Limitations

#### Current Limitations
- **VS Code Parity**: Some advanced features from the VS Code extension are not yet implemented
- **Custom UI for Options**: Options page uses standard property grid (no custom test connection button)
- **MCP Parameter Help**: Hover help for MCP tool parameters requires WPF templates; logic-layer documented in parity-report.md
- **Refresh Debounce**: Debounced tool refresh is UI-bound; logic-layer tolerant parsing validated
- **Missing Server Guidance**: Empty-state guidance requires WPF UI; dependencies documented in parity-report.md
- **Demo Content**: No demo GIFs or screenshots included in this release
- **Marketplace**: Not yet published to Visual Studio Marketplace

#### Performance Considerations
- **Large Lists**: UI may become less responsive with very large MCP tool lists (1000+ tools)
- **Memory Usage**: Exec console output is limited to prevent excessive memory usage
- **CLI Startup**: Initial CLI connection may take 2-3 seconds on first launch

#### Security Notes
- **Sandbox Policies**: Code execution respects configured sandbox policy settings
- **CLI Path Validation**: Validates CLI executable path but doesn't verify authenticity
- **WSL Security**: WSL execution inherits WSL security model and permissions

### Migration Notes

#### From VS Code Extension
- Settings are not automatically migrated from VS Code extension
- MCP configuration format is compatible with VS Code extension
- Custom prompts need to be recreated (no import functionality yet)

#### From Previous Versions
- This is the initial release (0.1.0)
- No migration needed from previous versions

### Breaking Changes

- None (initial release)

### Deprecated

- None (initial release)

### Removed

- None (initial release)

### Fixed

- None (initial release)

### Security

- **Input Validation**: All user inputs are validated before processing
- **CLI Path Security**: CLI executable path is validated before execution
- **Sandbox Enforcement**: Code execution respects configured security policies
- **Session Management**: Secure handling of authentication tokens and sessions

---

## [Unreleased]

### Added
- Documentation: architecture overview, troubleshooting guide, onboarding checklist, and updated telemetry/privacy notes for the refactor.
- Planning: blog/release outline captured in `docs/docs-onboarding-plan.md`.

### Changed
- README highlights module owners, VS Code parity, and configuration toggles.
- CHANGELOG ready to record incremental refactor milestones toward v0.2.0.

### Notes
- See `docs/session-persistence-plan.md` and `docs/telemetry-diagnostics-plan.md` for the new session resume telemetry events.

---

## Release Notes

### v0.1.0 Release Notes

This is the initial release of Codex for Visual Studio 2022, providing AI-powered coding assistance directly within Visual Studio. The extension offers comprehensive integration with the Codex CLI, including chat functionality, code context integration, diff/exec/approvals, MCP tools support, and extensive configuration options.

**Key Highlights:**
- Full parity with core VS Code extension features
- Native Visual Studio 2022 integration
- Comprehensive options and configuration system
- Robust error handling and user feedback
- Extensive testing and validation

**Getting Started:**
1. Install the extension from the .vsix file
2. Configure your Codex CLI path in Tools → Options → Codex
3. Open the tool window via Tools → Open Codex
4. Authenticate and start coding with AI assistance

**Support:**
- Report issues on GitHub
- Check the Diagnostics pane for detailed error information
- Review the README for configuration guidance

---

*This changelog is automatically generated and maintained as part of the development process.*
