# Telemetry and Diagnostics Plan for MyToolWindowControl Refactor

## Existing Signals Inventory
- `TelemetryTracker` counters: chat turns (count, avg tokens, tokens/sec), patch outcomes (success/failure counts + avg duration), exec runs (count, avg duration, non-zero exits), tool invocations, prompt inserts.
- Live event hooks: `ChatTextUtilities` submissions (`[debug] submission`), `HandleAgentMessage*` streaming, exec lifecycle events, diff approvals, MCP tool refresh/selection, custom prompt insertions, approvals.
- Diagnostics logging tags currently emitted to `DiagnosticsPane`: `[info]`, `[debug]`, `[error]`, `[assistant]`, `[exec]`, `[telemetry]`, `[stderr]`.
- Status UI mirrors: `StatusText`, `StatusBar`, and banners for stream errors, approvals, diff apply, exec console updates.

## Telemetry Aggregation Ownership
- Introduce `ITelemetryService` (anchored in `Shared/Telemetry/`) as single writer to VS telemetry + diagnostics.
- `TelemetryTracker` logic migrates into `TelemetrySessionAggregator` implementing `ITelemetryAggregator` and injected into feature services (chat, diff, exec, MCP).
- Tool window view-models receive telemetry summaries via `TelemetrySummaryStore`, keeping UI passive and allowing independent tests.

## Diagnostics Pane Entry Points
- Replace direct `DiagnosticsPane.GetAsync()` calls with `IDiagnosticsSink` abstraction.
- Register feature-specific sinks: `ChatDiagnosticsSink`, `DiffDiagnosticsSink`, `ExecDiagnosticsSink`, `McpDiagnosticsSink`, each mapping module log levels to standard categories.
- Composition root wires sinks into services and subscribes to mediator events (`DiagnosticsMessage`), ensuring background threads enqueue logs via `JoinableTaskFactory` helpers.

## Rate Limiting and Redaction Helpers
- Add `TelemetryThrottle` utility with per-event token bucket (default 10 events / 5 minutes) to prevent flooding from loops (e.g., deltas, rapid exec output).
- Implement `DiagnosticRedactor` with pluggable rules: strip file paths outside solution, redact potential secrets (regex for keys, tokens), collapse stack traces to hash if opt-in telemetry disabled.
- Provide `RedactedProperties` helper that logs safe summary while retaining full context in debug builds only.

## Error-to-Banner Mapping Post-Refactor
- Define `ErrorClassificationService` mapping telemetry error codes → UI surfaces:
  - `StreamError` → `ChatStatusBannerViewModel` message + retry affordance.
  - `CliRestartRequired` → shell banner + CTA to open options.
  - `DiffApplyFailed` → diff module toast + telemetry `diff.apply_failed`.
- Mediator dispatches `UserFacingAlert` messages carrying severity (`Info`, `Warning`, `Critical`) and recommended commands; shell view-model consumes and updates banners consistently.

## Opt-in Telemetry Control Flow
- Respect `CodexOptions.TelemetryEnabled` (new option) and VS global privacy settings; `TelemetryConsentService` resolves effective policy at start + on options change.
- When telemetry disabled: metrics remain in-memory, diagnostics limited to `[info]/[error]` labels without event IDs, opt-in dialog shown once per solution with persistent state in `OptionsCache`.
- Provide `TelemetrySettingsCommand` hooking into Options dialog and quick command palette entry.

## Testing Coverage
- Unit tests for `TelemetrySessionAggregator` verifying turn/patch/exec counters and throttling behaviour with deterministic clocks.
- Integration-style tests using recorded CLI transcripts to assert diagnostics/event routing (mock sinks capturing sequences).
- Privacy tests ensuring redactor removes secrets and opt-out mode suppresses external emission.
- UI automation smoke verifying banners triggered on simulated errors update view-model state (backed by mediator fakes).

## CLI Stderr Handling Alignment
- Wrap CLI stderr in `CliDiagnosticsChannel` producing structured events (`CliStdErrEvent` with timestamp, severity hint).
- `CodexCliHost` forwards stderr chunks to diagnostics mediator on background thread; redactor runs before sink receives.
- Exec/diff/chat modules subscribe for correlation (e.g., attach stderr to active exec turn) while `IDiagnosticsSink` logs `[stderr]` entries with throttling.

## CI Reporting Expectations
- CI publishes telemetry sanity report: aggregate counts from unit tests (ensuring no event name drift) and generates markdown summary attached to pipeline artifacts.
- Include diagnostics snapshot (last 200 lines) when integration tests fail for traceability.
- Enforce coverage gates on new telemetry-related tests via `testing-harness-plan.md` alignment; failing to emit required baseline events marks build unstable.


## Session Persistence Telemetry and Privacy Updates
- New events exposed in `docs/session-persistence-plan.md`: `session.persist.save`, `session.persist.failure`, `session.resume.start`, `session.resume.success`, `session.resume.partial`, `session.resume.failure`. Register them through `ITelemetryService` so they respect throttling.
- `TelemetryConsentService` must honor the upcoming `CodexOptions.TelemetryEnabled` toggle and Windows privacy settings. When disabled, persistence saves still occur but telemetry events remain local.
- Session repository writes (JSONL) use DPAPI encryption when `CodexOptions.PersistenceEncrypt` is set; redact workspace hashes before emitting diagnostics.
- Add diagnostics tags `[session]` for autosave/resume operations so troubleshooting aligns with `docs/troubleshooting.md`.
- Update CI sanity report to include counts for the new session events to catch regressions early.

## Next Steps
1. Implement `ITelemetryService`/`IDiagnosticsSink` interfaces alongside mediator message definitions.
2. Migrate existing telemetry calls from `MyToolWindowControl` partials into module services using new abstractions.
3. Add throttling/redaction helpers and wire up CLI host stderr to diagnostics hub.
