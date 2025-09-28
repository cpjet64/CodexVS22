# Troubleshooting Guide for the Refactored Codex Tool Window

This quick-reference guide complements Task T19 by mapping common issues to the refactored architecture. Each section links back to the owning module plan so maintainers can triage efficiently.

## Chat & Streaming
- **Symptoms**: No assistant response, streaming sticks on "Streaming…", or token usage stays empty.
- **Checks**:
  - Verify the session store is receiving deltas (`TelemetryTracker` should emit `chat.delta_received`).
  - Confirm `ChatTranscriptViewModel` is subscribed to `CodexSessionStore`; see `docs/chat-viewmodel-design.md` for reducer wiring.
  - Inspect diagnostics pane for `[stream]` or `[error]` tags; the redaction rules are documented in `docs/telemetry-diagnostics-plan.md`.
- **Fixes**:
  - Re-run `codex login` from the tool window or CLI; auth gating can block streaming (`ToolWindows/MyToolWindowControl.Authentication.cs`).
  - Use the **Retry** button on the stream error banner; the view-model calls `SendUserInputAsync` with `fromRetry: true`.

## Diff Review & Patch Apply
- **Symptoms**: Diff tree empty, apply fails silently, or approvals never resolve.
- **Checks**:
  - Ensure the diff session snapshot is persisted (`docs/session-persistence-plan.md`) and the base file hash matches the workspace file.
  - Confirm approvals banner is visible; if not, verify `IApprovalService` routed the request (see `docs/diff-module-plan.md`).
- **Fixes**:
  - Use Diagnostics pane to inspect `[diff]` entries; stale patches trigger a "patch stale" banner.
  - Discard cached diff using **Discard Patch**; new preview will rehydrate the tree.

## Exec Console
- **Symptoms**: No output, ANSI color bleed, or cancel button disabled.
- **Checks**:
  - Review exec history in session store (`docs/exec-module-plan.md`) to confirm the turn exists.
  - Inspect diagnostics pane for `[exec]` lines; absence indicates CLI not emitting output.
  - Ensure the view-model respects buffer limits; `_options.ExecOutputBufferLimit` trimming happens in `ApplyExecBufferLimit`.
- **Fixes**:
  - Toggle exec console visibility to force rebind (`ExecConsoleToggle`).
  - If CLI is unresponsive, hit **Reset Approvals** then restart CLI via Options → Restart Codex.

## MCP Tools & Prompts
- **Symptoms**: Empty tool list, hover help missing, prompts fail to insert.
- **Checks**:
  - Run `Refresh` from the tool window; debounced fetch is tracked in `docs/mcp-module-plan.md`.
  - Ensure `codex.json` exists and is discoverable from the workspace root; view diagnostics for `[mcp]` lines.
- **Fixes**:
  - Check `Options -> Codex -> MCP` settings for server root overrides.
  - Reauthenticate if tool servers require tokens; CLI stderr will log auth errors.

## Session Resume
- **Symptoms**: Transcript resets on launch, exec history missing, approvals reappear unexpectedly.
- **Checks**:
  - Verify persistence toggle in Options is enabled; see `docs/session-persistence-plan.md` for file locations.
  - Inspect `%LOCALAPPDATA%\CodexVS\Sessions\` for recent `session.jsonl` writes.
  - Confirm telemetry events `session.persist.save` / `session.resume.*` fire (mock `ITelemetryService`).
- **Fixes**:
  - Delete corrupt session using the planned "Clear Saved Session" command (or manually remove the folder).
  - Re-run Visual Studio; autosave triggers on unload and should recreate cache.

## Telemetry & Privacy
- **Symptoms**: Telemetry still sending after opt-out, diagnostics missing details.
- **Checks**:
  - Ensure `CodexOptions.TelemetryEnabled` toggle (upcoming) is read by `TelemetryConsentService` (`docs/telemetry-diagnostics-plan.md`).
  - Inspect diagnostics output for redaction markers (`[redacted]`).
- **Fixes**:
  - Restart the tool window after changing telemetry settings to flush cached consent state.
  - When telemetry is disabled, enable temporary logging via the diagnostics panel for debugging, then disable again.

## Additional Resources
- Architecture: `README.md` and `docs/architecture-diagrams.md`.
- Module specs: `docs/chat-viewmodel-design.md`, `docs/diff-module-plan.md`, `docs/exec-module-plan.md`, `docs/mcp-module-plan.md`.
- Telemetry & privacy: `docs/telemetry-diagnostics-plan.md`.
- Onboarding: `docs/docs-onboarding-plan.md`.

## Build Warnings
- **MSBuild `System.Collections.Immutable` conflict**: desktop builds emit `MSB3277` while preferring VS 2022's 9.0.0 assembly. The VSIX runs against the IDE copy; no functional impact. Documented warning—do not attempt to force unified packages until VS SDK dependencies rev.
- **VSIX compatibility analyzer**: `VSIXCompatibility1001` reports the BrowserLink reference bundled with the VS workload. The extension still installs on VS 17.x; leave as-is until the dependency graph is audited.
- **VSCT analyzer (`CVSTK002`)**: current command table format already includes the standard shared includes. Analyzer warnings remain informational; suppress only if the tooling blocks builds.
