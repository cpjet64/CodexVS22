# Exec Console Module Plan

## Scope and Current State Snapshot
- The exec console today is rendered dynamically inside the transcript stack (see `ToolWindows/MyToolWindowControl.xaml.cs:10380` for `CreateExecTurn`). Buttons for cancel/copy/clear/export are created in code and use `Tag` fields to reach back into logic.
- Exec event handling spans `ToolWindows/MyToolWindowControl.Exec.cs` (CLI events) and shared helpers in `ToolWindows/MyToolWindowControl.xaml.cs` for buffer limits, ANSI rendering, telemetry, and diagnostics writes.
- Dependencies include `ThreadHelper`, `DiagnosticsPane`, `VS.StatusBar`, `ChatTextUtilities`, `TelemetryTracker`, and option flags (e.g., `_options.ExecConsoleVisible`, `_options.ExecOutputBufferLimit`).

## View-Model Design and Buffer Limits (Subtask 1)
- Introduce `ExecConsoleViewModel` exposing observable collections for active and historical turns, persisted height, auto-hide toggle, and aggregate state (e.g., `HasActiveExec`, `IsVisible`).
- Each turn becomes an `ExecTurnViewModel` with immutable identity (`ExecId`), command metadata (`DisplayCommand`, `NormalizedCommand`, `WorkingDirectory`), lifecycle flags (`IsRunning`, `IsCancelling`, `ExitCode`, `CompletedAtUtc`), and buffer projections (`AnsiBuffer`, `PlainTextBuffer`, `Lines`).
- Buffer management moves into view-model/service layer: maintain `StringBuilder` limited by configurable `MaxBufferLength`; push trimmed segments when exceeded rather than mutating UI controls. Expose truncation counters to surface in UI (e.g., "Output truncated" notice).
- Provide `AppendOutputAsync(string chunk)` API that handles normalization (`NormalizeExecChunk`) and dispatches ANSI transformation pipeline; ensure thread-affinity via dispatcher guard inside VM service.

## ANSI Rendering Pipeline (Subtask 2)
- Extract existing `RenderAnsiText`/`AnsiBrushes` logic into `IAnsiRenderer` service that converts ANSI text into a lightweight representation (e.g., `IReadOnlyList<FormattedSpan>` with text, brush, weight flags). This decouples rendering from `TextBlock` controls.
- View layer binds to spans via attached behavior or control template (e.g., `ItemsControl` with `Run` items, or `RichTextBox` binding). Renderer operates off-thread-safe data, returning value objects for binding.
- Provide fallback for terminals lacking ANSI by returning plain text; ensure pipeline can stream incremental updates without re-parsing the entire buffer (diff based on appended text length).

## Cancellation and Status Flow (Subtask 3)
- Model exec lifecycle as state machine: `PendingApproval → Running → (Succeeded | Failed | Cancelled)` with intermediate `Cancelling` once user requests stop. Maintain timestamps to feed telemetry and UI badges.
- Expose `CancelCommand` on `ExecTurnViewModel` that checks current state, disables itself when cancel already requested, and calls a new `IExecHost` abstraction to send cancellation (`SendExecCancelAsync`). Reflect CLI confirmations to transition state and hide cancel button when done.
- Update view-model to surface status text/messages instead of relying on `StatusText` label; align with event ingestion from `HandleExecCommandBegin/OutputDelta/End` by routing through mediator that updates VM fields.

## Output Copy and Export Services (Subtask 4)
- Replace direct `Clipboard` and file system calls with `IClipboardService` and `IFileExportService`; enable unit testing and reuse for chat transcripts. Services encapsulate failure handling and status notifications.
- Commands `CopyOutputCommand`, `ClearOutputCommand`, `ExportOutputCommand` live on `ExecTurnViewModel` (or dedicated command provider) and delegate to services. Provide asynchronous patterns returning operation results to bubble success/failure messages via notification service.

## Telemetry Alignment (Subtask 5)
- Introduce `IExecTelemetry` interface to wrap `TelemetryTracker` calls (`BeginExec`, `CompleteExec`, `CancelExec`, buffer truncations, export/copy usage). Attach to view-model via dependency injection.
- Capture normalized command hashes to aggregate metrics and correlate with approvals. Add counters for truncation, export attempts, cancellation outcomes, and CLI error codes.
- Ensure telemetry updates happen in tandem with state transitions, not ad-hoc UI code, so the module remains self-contained.

## Shared Utility Reuse Strategy (Subtask 6)
- Promote reusable helpers into shared services:
  - `NormalizeExecChunk`, `BuildExecHeader`, and fallback ID logic move into an `ExecEventAdapter` to reuse across VS extension and potential CLI host tests.
  - Reuse `ChatTextUtilities.StripAnsi` for plain-text projections but centralize trimming toggles to avoid duplication.
  - Consolidate diagnostics formatting (`WriteExecDiagnosticsAsync`) and status-bar publishing into shared `INotificationService` used by diff/chat modules to ensure consistency.
- Avoid duplicating ANSI logic by referencing the new `IAnsiRenderer`. Keep option normalization (`ExecConsoleVisible`, buffer limits) inside `OptionsStore` shared with other modules.

## Diagnostics Logging Plan (Subtask 7)
- Continue routing exec events into `DiagnosticsPane`, but through an `IExecDiagnosticsLogger` that batches lines and tags them with exec ID, command, and timestamps for easier filtering.
- Support log levels (`info`, `warn`, `error`) tied to event type; integrate with existing logging pipeline so CLI-level warnings are surfaced uniformly.
- Provide optional persistence hook (e.g., writing to `%TEMP%/Codex/exec.log`) when verbose diagnostics are enabled, to help reproduce issues without VS open.

## Test Strategy for Long-Running Output (Subtask 8)
- Unit tests for view-model buffer trimming ensure appended chunks respect `MaxBufferLength`, preserve tail text, and emit truncation notifications.
- Concurrency tests using mocked dispatcher verifying that streaming updates do not block UI thread and cancel requests flip state promptly.
- Integration tests with mocked CLI host simulating >50 turns, rapid output, and ANSI sequences to validate pipeline performance and memory usage.
- Golden-file tests comparing rendered spans for representative ANSI samples to prevent regressions in color handling.

## CLI Exec vs VS Output Window Differences (Subtask 9)
- Codex exec console emphasizes contextual commands, inline approvals, and transcript integration, while VS Output window provides global streams without per-command lifecycle control.
- Exec console supports per-turn copy/export, ANSI aware rendering, and auto-hide behavior; VS Output relies on global copy/save and limited colorization.
- Refactor should document these intentional differences and highlight convergence opportunities (e.g., sharing logging filters, honoring VS themes, keyboard shortcuts for navigation).

## Deliverable and Next Steps (Subtask 10)
- This `exec-module-plan.md` serves as the blueprint for decomposing the exec console into a standalone module aligned with the broader refactor.
- Upcoming actions: finalize interfaces (`ExecConsoleViewModel`, `IAnsiRenderer`, `IExecHost`, `IExecTelemetry`, `IClipboardService`, `IFileExportService`, `IExecDiagnosticsLogger`), then prototype bindings in a dedicated XAML view backed by MVVM infrastructure.
