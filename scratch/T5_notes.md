# T5 Threading Risk Assessment Notes

## Async void methods
- TBD

## ThreadHelper usage
- TBD

## Background ops needing service layer
- TBD

## Shared collections w/o synchronization
- TBD

## Heartbeat & timer
- TBD

## UI access requirements
- TBD

## Task factory recommendations
- TBD

## CLI reconnect path risks
- TBD

## Logging & telemetry thread-safety
- TBD


## Async void methods
- `ToolWindows/MyToolWindowControl.Transcript.cs:18` `OnCopyAllClick` – WPF `RoutedEventHandler`; must remain `void` but marshals to UI via `Switch`.
- `ToolWindows/MyToolWindowControl.Approvals.cs:17` `OnResetApprovalsClick` – WPF button click; uses `Switch` for UI updates.
- `ToolWindows/MyToolWindowControl.Authentication.cs:120` `OnLoginClick` – WPF button; orchestrates CLI login/restart; signature constrained by event wiring.
- `ToolWindows/MyToolWindowControl.Authentication.cs:161` `OnLogoutClick` – WPF button; same constraints as login.
- `ToolWindows/MyToolWindowControl.Authentication.cs:202` `HandleStderr` – subscribed to `CodexCliHost.OnStderrLine` delegate returning `void`; writes diagnostics.
- `ToolWindows/MyToolWindowControl.Exec.cs:18` `HandleExecApproval` – invoked from `HandleStdout` dispatcher; currently fire-and-forget; consider returning `Task` post-refactor.
- `ToolWindows/MyToolWindowControl.Exec.cs:67` `HandleExecCommandBegin` – CLI event handler; same event pipeline requirements.
- `ToolWindows/MyToolWindowControl.Exec.cs:127` `HandleExecCommandOutputDelta` – CLI event handler; updates UI after `Switch`.
- `ToolWindows/MyToolWindowControl.Exec.cs:163` `HandleExecCommandEnd` – CLI event handler; completes exec turn.
- `ToolWindows/MyToolWindowControl.Mcp.cs:19` `HandleListMcpTools` – CLI event; updates ObservableCollection under UI thread.
- `ToolWindows/MyToolWindowControl.Mcp.cs:42` `HandleListCustomPrompts` – CLI event; updates UI list.
- `ToolWindows/MyToolWindowControl.Mcp.cs:69` `HandleToolCallBegin` – CLI event; updates run state.
- `ToolWindows/MyToolWindowControl.Mcp.cs:97` `HandleToolCallOutput` – CLI event.
- `ToolWindows/MyToolWindowControl.Mcp.cs:122` `HandleToolCallEnd` – CLI event.
- `ToolWindows/MyToolWindowControl.Mcp.cs:152` `HandleTurnDiff` – CLI event driving diff pipeline.
- `ToolWindows/MyToolWindowControl.xaml.cs:813` `HandleAgentMessageDelta` – streaming CLI event.
- `ToolWindows/MyToolWindowControl.xaml.cs:893` `HandleAgentMessage` – final message CLI event.
- `ToolWindows/MyToolWindowControl.xaml.cs:1029` `HandleTokenCount` – CLI event updating telemetry.
- `ToolWindows/MyToolWindowControl.xaml.cs:1105` `HandleStreamError` – CLI error event.
- `ToolWindows/MyToolWindowControl.xaml.cs:1241` `HandleApplyPatchApproval` – CLI approval event; triggers UI + CLI responses.
- `ToolWindows/MyToolWindowControl.xaml.cs:1409` `HandlePatchApplyBegin` – CLI patch progress event.
- `ToolWindows/MyToolWindowControl.xaml.cs:1473` `HandlePatchApplyEnd` – CLI patch completion event.
- `ToolWindows/MyToolWindowControl.xaml.cs:1545` `HandleTaskComplete` – CLI task completion event.
- `ToolWindows/MyToolWindowControl.xaml.cs:3609` `OnDiscardPatchClick` – WPF button.
- `ToolWindows/MyToolWindowControl.xaml.cs:5169` `OnExecCancelClick` – WPF button.
- `ToolWindows/MyToolWindowControl.xaml.cs:5297` `OnExecCopyAllClick` – WPF button.
- `ToolWindows/MyToolWindowControl.xaml.cs:5429` `OnExecClearClick` – WPF button.
- `ToolWindows/MyToolWindowControl.xaml.cs:6555` `OnExecExportClick` – WPF button.
- `ToolWindows/MyToolWindowControl.xaml.cs:9475` `OnCopyMessageMenuItemClick` – context-menu handler.
- `ToolWindows/MyToolWindowControl.xaml.cs:12123` `OnSendClick` – primary send button.

Rationale: UI event handlers must remain `void`; CLI pipeline currently dispatches via synchronous switch in `HandleStdout`, so conversions to `Task` would require upstream changes. All functions guard with `try/catch` to avoid unobserved exceptions. Recommend future refactor to expose async `Task` handlers via dispatcher abstraction so exceptions can propagate.

## ThreadHelper usage
- UI marshaling: Methods that update WPF controls call `ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync()` before touching UI (e.g., `RefreshAuthUiAsync` in `ToolWindows/MyToolWindowControl.Authentication.cs:28`, `DisplayNextApprovalAsync` in `ToolWindows/MyToolWindowControl.Approvals.cs:72`, `HandleExecCommandBegin` in `ToolWindows/MyToolWindowControl.Exec.cs:67`, `HandleListMcpTools` in `ToolWindows/MyToolWindowControl.Mcp.cs:19`, `HandleAgentMessage` in `ToolWindows/MyToolWindowControl.xaml.cs:893`).
- DTE/VSSDK operations: Working-directory helpers (`ToolWindows/MyToolWindowControl.WorkingDirectory.Environment.cs:26`, `:64`, `:190`) and subscription setup tear-down ensure main thread before interacting with `IVsSolution` and `UIContext` APIs. They also assert via `ThreadHelper.ThrowIfNotOnUIThread()` in `WorkingDirectory.Environment.cs:138`, `:151`, `:213`.
- Fire-and-forget background work: The control frequently uses `ThreadHelper.JoinableTaskFactory.RunAsync` to queue non-blocking tasks (e.g., to issue CLI requests in `ToolWindows/MyToolWindowControl.xaml.cs:1661`, `:1665`, `Options.cs:87`, `Lifecycle.cs:115`). These tasks typically call `Switch` internally when they reach UI.
- Synchronous hookup: `OnLoaded` uses `ThreadHelper.JoinableTaskFactory.Run` (`ToolWindows/MyToolWindowControl.Lifecycle.cs:60`) to avoid reentrancy when awaiting async initialization from a WPF `Loaded` event.
- Heartbeat logging and approvals banner clearing rely on `RunAsync` to perform UI updates without blocking callback threads (`ToolWindows/MyToolWindowControl.Heartbeat.cs:73`, `Appovals.cs:171`).
- Exec/tokens/diff flows rely on `SwitchToMainThreadAsync` to synchronize updates to dictionaries/ObservableCollections that back the UI before altering state (`ToolWindows/MyToolWindowControl.xaml.cs:829`, `:909`, `:1257`).

Observations:
- `SwitchToMainThreadAsync` is consistently used before UI access, but many callers immediately re-enter the UI thread from non-UI contexts (CLI callbacks, timers). Refactor should consider central dispatcher abstraction to reduce scattered `Switch` calls.
- `RunAsync` usage lacks cancellation or exception surfacing; results are effectively fire-and-forget. Consider wrapping in dedicated task scheduler with logging for failures.
## Background operations needing services layer
- CLI command dispatch: `SendUserInputAsync` (`ToolWindows/MyToolWindowControl.xaml.cs:12179`), `SendExecCancelAsync` (`ToolWindows/MyToolWindowControl.xaml.cs:24773`), and approval submissions in `ResolveActiveApprovalAsync` (`ToolWindows/MyToolWindowControl.Approvals.cs:139`) all issue host RPCs from the control. These should move behind a dedicated CLI service to avoid UI class orchestrating process I/O.
- Resource fetches: `RequestMcpToolsAsync` (`ToolWindows/MyToolWindowControl.xaml.cs:7843`) and `RequestCustomPromptsAsync` (`ToolWindows/MyToolWindowControl.xaml.cs:7971`) run on background threads yet live in control; candidate for CLI data service.
- Heartbeat pipeline: `SendHeartbeatAsync` (`ToolWindows/MyToolWindowControl.Heartbeat.cs:159`) executes on timer callbacks and would belong in a connectivity service so timer lifecycle is decoupled from UI.
- Working directory + CLI restart: `EnsureWorkingDirectoryUpToDateAsync` (`ToolWindows/MyToolWindowControl.WorkingDirectory.Subscriptions.cs:108`) and `RestartCliAsync` (`ToolWindows/MyToolWindowControl.xaml.cs:737`) perform environment inspection and host restarts; these cross responsibilities (DTE + process) and should migrate to workspace/host services.
- Diff application: `ApplySelectedDiffsAsync` (`ToolWindows/MyToolWindowControl.xaml.cs:8129`) and downstream helpers (`ApplyDocumentTextAsync` at `ToolWindows/MyToolWindowControl.xaml.cs:8945`) mix UI state and file mutations. Moving patch execution to a diff service would isolate file IO from UI threading concerns.
- Diagnostics + telemetry writes: `LogTelemetryAsync` (`ToolWindows/MyToolWindowControl.xaml.cs:12727`) and `WriteExecDiagnosticsAsync` (`ToolWindows/MyToolWindowControl.xaml.cs:12791`) perform logging on background threads; central logging service could serialize writes and handle failure policy.
## Shared collections lacking synchronization
- `_assistantTurns` (`ToolWindows/MyToolWindowControl.xaml.cs:189`) and `_execTurns` (`ToolWindows/MyToolWindowControl.xaml.cs:193`) are plain dictionaries updated from CLI event handlers after a UI thread switch. They assume single-threaded access; concurrent mutation during host restarts (`RestartCliAsync`) would race. Recommendation: encapsulate within dispatcher-bound store or guard with `AsyncLock`.
- `_execConsoleTurns` list (`ToolWindows/MyToolWindowControl.xaml.cs:197`) is appended/trimmed when exec output arrives and when toggling console visibility (`ToolWindows/MyToolWindowControl.Exec.Helpers.cs:21`). Needs UI-thread confinement or conversion to `ObservableCollection` with dispatcher check.
- Approval tracking structures `_rememberedExecApprovals` (`ToolWindows/MyToolWindowControl.xaml.cs:209`), `_rememberedPatchApprovals` (`ToolWindows/MyToolWindowControl.xaml.cs:213`), and `_approvalQueue` (`ToolWindows/MyToolWindowControl.xaml.cs:217`) are unsynchronized. `RestartCliAsync` clears them from whichever thread invoked the restart; simultaneous approval events could observe inconsistent state.
- Diff backing stores `_diffTreeRoots` (`ToolWindows/MyToolWindowControl.xaml.cs:401`) and `_diffDocuments` (`ToolWindows/MyToolWindowControl.xaml.cs:405`) are manipulated in diff handlers and patch pipelines. They rely on upstream switches—any future background diff processing must avoid touching them without dispatcher marshal.
- MCP caches `_mcpTools` (`ToolWindows/MyToolWindowControl.xaml.cs:201`) and `_customPrompts` (`ToolWindows/MyToolWindowControl.xaml.cs:205`), plus index dictionaries (`ToolWindows/MyToolWindowControl.xaml.cs:221`, `:225`), are mutated after CLI responses. Ensure updates stay on UI thread or wrap in thread-safe containers if data access moves to services.
- `TelemetryTracker._execStarts` dictionary (`ToolWindows/MyToolWindowControl.Telemetry.cs:19`) collects timings across exec lifecycle events. It presumes sequential access from UI thread; host timers or background completions would need synchronization.
## Heartbeat and timer risks
- Timer lifecycle: `StartHeartbeatTimer` (`ToolWindows/MyToolWindowControl.Heartbeat.cs:48`) replaces `_heartbeatTimer` under `_heartbeatLock`, disposing the prior timer outside the lock. If `StopHeartbeatTimer` (`ToolWindows/MyToolWindowControl.Heartbeat.cs:88`) runs concurrently, callback threads could observe `null` state just after disposal; current guard resets `_heartbeatSending`, but we should ensure callbacks exit gracefully.
- Callback coordination: `OnHeartbeatTimer` (`ToolWindows/MyToolWindowControl.Heartbeat.cs:127`) uses `Interlocked.Exchange` to prevent reentrancy and reads `_heartbeatState` under lock. It queues async work via `ThreadHelper.JoinableTaskFactory.RunAsync` without awaiting; failures surface only through diagnostics logging.
- Host disposal race: During `DisposeHost` (`ToolWindows/MyToolWindowControl.xaml.cs:693`), `StopHeartbeatTimer` is called but in-flight callbacks may still attempt to send via `_host`. They guard against `host == null` and catch `ObjectDisposedException`, yet there remains a small window before `_host` is nulled where `SendHeartbeatAsync` might run against disposed process; consider using cancellation tokens.
- Interval changes: `ConfigureHeartbeat` (`ToolWindows/MyToolWindowControl.Heartbeat.cs:17`) only restarts timer when interval/opType change. If CLI sends updated template with same metadata but different payload, we reuse old timer without updating `_heartbeatState.OpTemplate`? (Although inside lock they set `_heartbeatState = state`, so new template is stored.)
- Logging fan-out: Timer stop/start logging uses `RunAsync` to schedule output; if the tool window closes, those queued log tasks may attempt to use `DiagnosticsPane`. Handling is already wrapped in try/catch but consider centralizing logging to ensure deterministic disposal.
## UI access requirements
- Authentication banner updates (`ToolWindows/MyToolWindowControl.Authentication.cs:30`) call `FindName` and update WPF controls; must execute on dispatcher thread.
- Approval banner rendering (`ToolWindows/MyToolWindowControl.Approvals.cs:95`) manipulates `Border`, `TextBlock`, `Button` state; requires UI thread.
- Diff tree updates (`ToolWindows/MyToolWindowControl.Mcp.cs:189`) rely on WPF `TreeView` binding (`DiffTreeView`, `DiffSelectionSummary`), so diff view models should expose dispatcher-ready collections post-refactor.
- Transcript and exec consoles update `StackPanel`/`ScrollViewer` content within `AppendAssistantText` (`ToolWindows/MyToolWindowControl.xaml.cs:9223`) and `AppendExecText` (`ToolWindows/MyToolWindowControl.xaml.cs:10505`); these must stay on UI thread or use dispatcher operations.
- Status/telemetry updates call `VS.StatusBar.ShowMessageAsync` (`ToolWindows/MyToolWindowControl.Exec.cs:47`, `Approvals.cs:31`) which expects UI context.
- Working directory selectors and combo boxes manipulated in `InitializeSelectorsAsync` (`ToolWindows/MyToolWindowControl.Options.cs:16`) rely on UI thread for `ComboBox` data binding.

Guidance: Future view-model extraction should centralize UI-affecting logic behind dispatcher-aware adapters so background services emit state changes that the UI layer marshals via `Dispatcher.InvokeAsync` or `IJoinableTaskContext`. Avoid calling `ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync()` deep in services; confine switching to view layer entry points.
## Task factory / scheduler recommendations
- **CLI event processing**: Introduce a dedicated background queue (e.g., `Channel<EventMsg>`) processed by a hosted service so CLI stdout handlers (`ToolWindows/MyToolWindowControl.xaml.cs:1629`) dispatch onto a single-threaded context. UI layer would await tasks exposed via `Task` instead of relying on `async void`.
- **UI synchronization**: Retain `IJoinableTaskFactory`/`Dispatcher` usage at module boundaries. Consider wrapping in a helper (`IUiThreadDispatcher`) to replace the scattered `ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync()` calls.
- **Workspace operations**: Running DTE queries in `ResolveWorkingDirectoryAsync` (`ToolWindows/MyToolWindowControl.WorkingDirectory.cs:34`) should leverage `JoinableTaskFactory` collections to avoid reentrancy while still permitting background execution when safe.
- **Timers and polling**: Replace raw `System.Threading.Timer` heartbeat with `IHostedService`-style background loop or `JoinableTaskFactory.RunAsync` + `JoinableTaskScope` to simplify cancellation when the tool window disposes.
- **Exec/diff pipelines**: Use `TaskScheduler.Default` or dedicated `TaskFactory` to perform parsing (`ProcessDiffDocumentsAsync` at `ToolWindows/MyToolWindowControl.Mcp.cs:232`) and file IO, then marshal results to UI scheduler.
## CLI reconnect path assessment
- `RestartCliAsync` (`ToolWindows/MyToolWindowControl.xaml.cs:737`) disposes and recreates the host without guarding against concurrent invocations. Login/logout (`ToolWindows/MyToolWindowControl.Authentication.cs:120`, `:161`) and workspace updates (`ToolWindows/MyToolWindowControl.WorkingDirectory.Subscriptions.cs:108`) can trigger restarts simultaneously; introduce an `AsyncLock` or serialized command queue around host lifecycle.
- `EnsureWorkingDirectoryUpToDateAsync` holds `_workingDirLock` while switching to the UI thread to update authentication banners (`ToolWindows/MyToolWindowControl.WorkingDirectory.Subscriptions.cs:129`). This prevents concurrent workspace updates but still allows other UI handlers to hit `_host` mid-restart. Consider exposing a host state machine that broadcasts "restarting" to disable send buttons.
- `DisposeHost` (`ToolWindows/MyToolWindowControl.xaml.cs:681`) detaches stdout/stderr handlers, but CLI events already queued may target the previous instance; ensure new host creation resets event dispatcher and flushes pending `_assistantTurns`/`_approvalQueue` (`ToolWindows/MyToolWindowControl.xaml.cs:1933`, `:1953`) atomically.
- `_cliStarted` flag (`ToolWindows/MyToolWindowControl.xaml.cs:585`) is set per start but not protected; racy with async start failures or multiple restarts. Recommend storing host lifecycle state in thread-safe enum and exposing transitions through service.
- Heartbeat timer is not gated during restart beyond `StopHeartbeatTimer`; ensure restart path calls `ConfigureHeartbeat` only after new host is fully running to avoid sending heartbeats to stale process.
## Logging & telemetry thread-safety
- Diagnostics logging: Many handlers call `DiagnosticsPane.GetAsync()` from background contexts without switching threads (e.g., `HandleStderr` at `ToolWindows/MyToolWindowControl.Authentication.cs:202`, `WriteExecDiagnosticsAsync` at `ToolWindows/MyToolWindowControl.xaml.cs:12791`). Verify `DiagnosticsPane` is thread-safe or funnel writes through a logger service that serializes access on UI thread.
- TelemetryTracker (`ToolWindows/MyToolWindowControl.Telemetry.cs:10-67`) is not synchronized. `BeginExec`/`CompleteExec` are invoked from exec handlers after UI switch, but ensure only one thread mutates `_execStarts`. When exec messages arrive from background threads, guarantee UI dispatch before touching tracker.
- Status bar updates (`VS.StatusBar.ShowMessageAsync` calls at `ToolWindows/MyToolWindowControl.Exec.cs:43`, `ToolWindows/MyToolWindowControl.Approvals.cs:30`) rely on `ThreadHelper` to enforce main thread; encapsulate within UI service to avoid forgetting the switch.
- For refactor, introduce structured logging abstraction (with `ILogger`-style API) that is thread-safe and can record context (thread ID, event type) while avoiding repeated `try/catch` logging blocks across UI control.
