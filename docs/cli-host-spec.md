# Codex CLI Host Extraction Specification (Task T6)

## 1. Goals and Non-Goals
- Provide a dependency-injectable CLI gateway so view-models consume a contract instead of instantiating `CodexCliHost` directly (`ToolWindows/MyToolWindowControl.xaml.cs:657`).
- Centralize process lifecycle, reconnection, and heartbeat logic currently scattered across partial classes (`ToolWindows/MyToolWindowControl.Heartbeat.cs:18`, `ToolWindows/MyToolWindowControl.xaml.cs:585`).
- Surface structured events to downstream modules (chat, exec, diff, MCP) without `async void` handlers on UI threads (`ToolWindows/MyToolWindowControl.Exec.cs:18`, `ToolWindows/MyToolWindowControl.Mcp.cs:19`).
- Preserve feature parity (authentication, login/logout, reconnect) while enabling unit/integration testing.
- Out of scope: replacing CLI binary invocation semantics or redesigning high-level UX.

## 2. Proposed Abstractions
### 2.1 Interface Contract
Define `ICodexCliHost` (DI registered singleton) with:
- `Task<CliConnectionResult> ConnectAsync(CliConnectionRequest request, CancellationToken ct)` – resolves CLI path, starts process, wires pumps.
- `Task DisconnectAsync(CancellationToken ct)` – graceful shutdown; safe to call when disconnected.
- `Task<bool> SendAsync(string payload, CancellationToken ct = default)` – validates JSON, queues to stdin channel.
- `IAsyncEnumerable<CliEnvelope> ReadAllAsync(CancellationToken ct)` – typed stream of stdout messages.
- `IAsyncEnumerable<CliDiagnostic> ReadDiagnosticsAsync(CancellationToken ct)` – stderr + host-level notifications (login prompts, reconnect attempts).
- `ValueTask<CodexAuthenticationResult> CheckAuthenticationAsync(CancellationToken ct)` and `Task<bool> LoginAsync(...)`, `Task<bool> LogoutAsync(...)` mirroring existing helpers (`Core/CodexCliHost.cs:37`, `Core/CodexCliHost.cs:70`).
- `CliHostState State { get; }` event for transitions (Connecting, Connected, Reconnecting, Faulted, Stopped).
- `Task<CliHeartbeatInfo> EnsureHeartbeatAsync(CancellationToken ct)` to expose negotiated heartbeat template (`ToolWindows/MyToolWindowControl.Heartbeat.Helpers.cs:12`).

### 2.2 Supporting Contracts
- `ICliMessageSerializer` – owns submission factories (`Core/ApprovalSubmissionFactory.cs:7`, `ToolWindows/MyToolWindowControl.xaml.cs:12575`).
- `ICliDiagnosticsSink` – injected sink for diagnostics/throttling replacing static `DiagnosticsPane` calls (`Core/CodexCliHost.cs:438`).
- `ICodexOptionsProvider` – abstraction that provides effective options snapshot and change notifications (see §6).

## 3. Lifetime and Ownership Semantics
- Host lives at package scope (singleton) and is requested by feature modules; tool window no longer owns `_host` field (`ToolWindows/MyToolWindowControl.xaml.cs:369`).
- Connection is reference-counted: first consumer `ConnectAsync` triggers process start; subsequent callers await existing state. `DisconnectAsync` decrements usage; background inactivity timer triggers auto-stop after configurable idle timeout (default 5 minutes) to avoid runaway processes.
- Reconnect attempts are serialized; host exposes `CliReconnectionPolicy` (max retries, backoff) configurable through options.
- Heartbeat scheduler owned by host; UI can observe `CliHeartbeatInfo` but must not manage timers (`ToolWindows/MyToolWindowControl.Heartbeat.cs:48`).

## 4. Message Pump Decoupling
- Replace `Task.Run(() => PumpAsync(...))` with a dedicated background `Channel<string>` per stream; pump loops read from process stream into channel and push `CliEnvelope` instances tagged with source.
- Event consumers subscribe via `ReadAllAsync`, which emits on background threads; UI adapters (e.g., chat VM) dispatch onto main thread via dispatcher at composition boundary.
- Standardize JSON parsing upstream with `ICliMessageRouter` responsible for parsing `EventMsg` from raw JSON, avoiding duplication inside `HandleStdout` (`ToolWindows/MyToolWindowControl.xaml.cs:1629`).
- Provide backpressure by bounding channel capacity (e.g., 100 messages) and dropping with diagnostics when exceeded to keep VS responsive.

## 5. Serialization Helper Relocation
Move the following into `CodexVS22.Core.Cli` namespace:
- User input submission builder (`ToolWindows/MyToolWindowControl.xaml.cs:12575`) → `CliSubmissionFactory.CreateUserInput`.
- Exec cancel builder (`ToolWindows/MyToolWindowControl.xaml.cs:12429`) → `CliSubmissionFactory.CreateExecCancel`.
- Heartbeat submission template helpers (`ToolWindows/MyToolWindowControl.Heartbeat.Helpers.cs:12-272`).
- Approval submissions already under `Core/ApprovalSubmissionFactory.cs:7`; expose via `ICliMessageSerializer`.
- JSON access utilities (`ToolWindows/MyToolWindowControl.JsonHelpers.cs:11-103`) to shared serializer namespace for reuse in message router.

## 6. Options Integration & Host Factory
- Introduce `ICodexOptionsProvider` that merges global/solution overrides (see `Options/CodexOptions.cs:20-120`).
- Host factory consumes snapshot when connecting; store `CliSessionConfig` containing `ExecutablePath`, `UseWsl`, `WorkingDirectory`, `SandboxPolicy`, heartbeat debounce, etc.
- Listen for option changes via event; when CLI-critical properties mutate (`CliExecutable`, `UseWsl`, sandbox), signal `CliHostState.NeedsRestart` event. Consumers can opt-in to auto-restart.
- Provide `IWorkingDirectoryResolver` to encapsulate `EnsureWorkingDirectoryUpToDateAsync` logic currently coupled to tool window (`ToolWindows/MyToolWindowControl.WorkingDirectory.cs:17`).

## 7. Reconnect & Heartbeat Responsibilities
- Host tracks connectivity; on write failure `SendAsync` triggers internal reconnection once (`Core/CodexCliHost.cs:214`). New policy: expose `CliReconnectRequested` event and require consumer acknowledgement before restart to avoid conflicting UI state (e.g., pending approvals).
- Heartbeat scheduler resides in host: when CLI emits `SessionConfigured`, router provides template; host schedules `CliHeartbeatTick` events; view-model just listens for connection state rather than manipulating timers (`ToolWindows/MyToolWindowControl.Heartbeat.cs:48-185`).
- On failure to send heartbeat, host transitions to `Faulted` and emits diagnostic payload; consumer may call `ConnectAsync` to attempt manual recovery.

## 8. Diagnostics Contract
- Replace direct `DiagnosticsPane` usage with `ICliDiagnosticsSink.LogAsync(CliDiagnostic diagnostic)`. Diagnostic payload fields: severity, category (ProcessStart, StdErr, Heartbeat, Auth), message, exception, correlationId.
- Sink default implementation bridges to Diagnostics tool window; alternative sink for tests can assert logs.
- Maintain throttling semantics (max 20/sec) inside sink, not host (`Core/CodexCliHost.cs:450`).

## 9. Error Propagation Strategy
- Host categorizes failures:
  - `CliErrorKind.ProcessStart` – emits `CliConnectionResult` with failure details and recommended remediation.
  - `CliErrorKind.WriteFailure` – includes original payload id; consumer can mark request as failed and notify user (`ToolWindows/MyToolWindowControl.xaml.cs:12325`).
  - `CliErrorKind.Authentication` – produced by `CheckAuthenticationAsync` with message lines (`Core/CodexCliHost.cs:79`).
- All errors surface through `CliDiagnostic` stream plus state transitions; UI layer should not depend on exceptions thrown on background threads.
- Provide `Task<CliHealthSnapshot>` API for status polling (process id, uptime, heartbeat latency) to drive status banners.

## 10. Required Test Coverage
- Unit tests for path and argument resolution (native vs WSL) covering `ResolveCli`, `ResolveCodexCommand` obsoleted functions (`Core/CodexCliHost.cs:224`).
- Pump tests using fake process streams verifying channel consumption order and backpressure handling.
- Reconnect tests ensuring only single restart attempt and state transitions align with spec.
- Heartbeat tests verifying scheduler uses negotiated interval and stops on disconnect.
- Serialization tests for each submission factory (user input, exec cancel, approvals, heartbeat) ensuring JSON matches protocol contract.
- Diagnostics sink tests verifying throttling and severity tagging.
- Integration tests with mock CLI process (named pipe or in-memory) confirming message router dispatch to chat/exec modules.

## 11. Deliverables & Migration Plan
- Implement `Core/Cli/ICodexCliHost.cs`, concrete `ProcessCodexCliHost`, and supporting contracts.
- Update tool window and upcoming view-models to consume interfaces via DI container introduced in refactor strategy (`docs/refactor-strategy.md`).
- Migrate heartbeat/approval/exec modules incrementally: wrap existing `CodexCliHost` logic with adapter, then replace call sites.
- Deprecate static properties `CodexCliHost.LastVersion` and `LastRolloutPath`; move to `CliMetadataCache` emitted via diagnostics stream (`Core/CodexCliHost.cs:476`).
- Provide temporary shim `LegacyCodexCliHostAdapter` to keep existing code compiling during transition.

## 12. Open Questions / Follow-Ups
- Determine whether CLI stdout JSON parsing should live in host or dedicated message router module (leaning router to keep host transport-focused).
- Clarify ownership of authentication prompts in new UI (tie-in with Task T13 approval service and Task T11 options plan).
- Align telemetry hooks for connection lifecycle with Task T12 deliverable.


## 13. Task Checklist Mapping
- Design interface for DI → Section 2.1/2.2.
- Lifetime & ownership semantics → Section 3.
- Message pump decoupling → Section 4.
- Serialization helpers relocation → Section 5.
- Reconnect & heartbeat responsibilities → Section 7.
- Diagnostics channel contract → Section 8.
- Required tests for new CLI service → Section 10.
- Error propagation strategy → Section 9.
- Options integration and host factory → Section 6.
- Specification deliverable (this document).
