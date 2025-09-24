Codex VSIX Logic-Layer Parity Report

Scope: Logic-layer features compared to the official Codex VS Code extension.

Verified parity (logic-layer)
- Protocol parsing: tolerant envelopes and kinds (AgentMessage*, TokenCount, Exec*, Tool*, Prompts, TurnDiff, PatchApply*, TaskComplete).
- Correlation routing: in-flight map, delta→final consolidation.
- Diff/patch core: JSON file-list extraction, binary/empty detection, conflict/RO guards.
- Exec console model: buffering, ANSI stripping, cap + trimming policy, cancel and finalize.
- MCP tools/prompts extraction: tolerant key mapping and empty/null safety.
- Options core (logic): serialization, import/export, effective overrides.

Deltas and notes
- VS-only UI behaviors (WPF, Visual Studio diff services) are not testable on Linux; logic-layer hooks exist but UI parity not asserted here.
- TurnDiff unified-diff parsing is deferred to VS services; JSON files-array path validated.
- MCP UI interactions (prompt insert, hover parameter help, refresh debounce, missing-server guidance) require WPF code; logic-layer state persists via options but UI glue not validated here.
- Telemetry events are placeholders at logic-level; no external emission on Linux.

Test evidence
- All Linux tests passed (see junit.xml) including T7.12 stress.
- Performance timings emitted (perf.csv) per-test.
- Approximate coverage emitted (coverage.lcov) for traceability.

Windows dependencies (UI parity work)
- Visual Studio 2022, Extensibility workload; MSBuild; VS experimental instance (/rootsuffix Exp).
- VS Text and Diff services (IVsDifferenceService, ITextBuffer, ITextEdit), EnvDTE COM automation.
