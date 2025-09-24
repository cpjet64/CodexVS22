Codex VSIX Logic-Layer Parity Report

Scope: Logic-layer features compared to the official Codex VS Code extension.

Verified parity (logic-layer)
- Protocol parsing: tolerant envelopes and kinds (AgentMessage*, TokenCount, Exec*, Tool*, Prompts, TurnDiff, PatchApply*, TaskComplete).
- Correlation routing: in-flight map, delta→final consolidation.
- Diff/patch core: JSON file-list extraction, binary/empty detection, conflict/RO guards.
- Exec console model: buffering, ANSI stripping, cap + trimming policy, cancel and finalize.
- MCP tools/prompts extraction: tolerant key mapping and empty/null safety.
- MCP tools/prompts persistence: stored in options (LastUsedTool/LastUsedPrompt) and round-trip via JSON.
- Options validation: default/clamp rules verified (model, reasoning, dimensions, buffer limits, window state).
- Options core (logic): serialization, import/export, effective overrides.

Deltas and notes
- VS-only UI behaviors (WPF, Visual Studio diff services) are not testable on Linux; logic-layer hooks exist but UI parity not asserted here.
- TurnDiff unified-diff parsing is deferred to VS services; JSON files-array path validated.
- MCP UI interactions (prompt insert, hover parameter help, refresh debounce, missing-server guidance) require WPF code; logic-layer state persists via options but UI glue not validated here.
Dependencies: WPF (XAML), dispatcher, event handlers, VS toolwindow hosting.

MCP UI-linked items (documented)
- T8.6 Missing MCP servers guidance: Requires UI banner/empty-state in the tool window, hyperlink to MCP server configuration docs, and retry affordance. Dependency: WPF visual tree, binding to model state, VS threading (dispatcher).
- T8.7 Refresh Tools debounce: Requires UI command handler with a debounced async invoker (e.g., 300–500 ms) to avoid spamming the CLI. Dependency: WPF command routing/async patterns. Logic-layer tolerant parsing validated.
- T8.11 Hover help for tool parameters: Requires tooltip templates bound to tool metadata and parameter schemas from MCP responses. Dependency: WPF DataTemplates/ToolTip, access to MCP schema in the UI layer.
- Telemetry events are placeholders at logic-level; no external emission on Linux.

Test evidence
- All Linux tests passed (see junit.xml) including T7.12 stress.
- Performance timings emitted (perf.csv) per-test.
- Approximate coverage emitted (coverage.lcov) for traceability.
- T8.8 validated with 2000 tools/prompts (<800ms). T8.12 validated with 5000 tools (<1.2s).
- T8.5 verified last-used tool/prompt persistence via JSON round-trip.
- T9.3 verified validation defaults/limits; CLI version check documented as Windows-only.
- T9.7 health thresholds refined and wired to a status bar logic model (labels: Healthy/Degraded/Unstable).

Windows dependencies (UI parity work)
- Visual Studio 2022, Extensibility workload; MSBuild; VS experimental instance (/rootsuffix Exp).
- VS Text and Diff services (IVsDifferenceService, ITextBuffer, ITextEdit), EnvDTE COM automation.
