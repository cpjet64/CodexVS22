# Refactor Strategy for MyToolWindowControl

## Target Architecture
- **Shell view**: `CodexToolWindowView` hosts chrome, shared banners, and region placeholders; code-behind limited to Visual Studio shell hooks.
- **View-model layer**: `ToolWindowShellViewModel` coordinates sub-view-models (`ChatTranscriptViewModel`, `DiffReviewViewModel`, `ExecConsoleViewModel`, `ApprovalsBannerViewModel`, `McpToolsViewModel`) via constructor injection to keep responsibilities isolated.
- **Service layer**: feature-specific services (`IChatSessionService`, `IDiffService`, `IExecSessionService`, `IMcpDirectoryService`, `IApprovalService`, `ITelemetryService`) wrap protocol traffic, persistence, and file-system access; they run on background thread(s) and expose async APIs/data streams.
- **Session coordination**: a lightweight mediator (`ICodexSessionCoordinator`) arbitrates cross-feature events such as CLI connection state, workspace changes, and telemetry fan-out while the existing `CodexCliHost` moves behind `ICliTransport` abstraction.
- **State stores**: `CodexSessionStore` (chat/diff/exec snapshot), `WorkspaceContextStore`, and `OptionsCache` become observable dependencies. Stores emit change notifications consumed by view-models, eliminating direct UI mutation from services.
- **Message routing**: convert raw CLI events into strongly typed domain notifications (`CliEventHub`) consumed by modules; modules publish user intents back through the mediator or directly via services implementing `ICommand` handlers.

## Namespace and Folder Layout
```
ToolWindows/
  CodexToolWindow/
    Views/
      CodexToolWindowView.xaml
      ChatTranscriptView.xaml
      DiffReviewView.xaml
      ExecConsoleView.xaml
      ApprovalsBannerView.xaml
    ViewModels/
      ToolWindowShellViewModel.cs
      ChatTranscriptViewModel.cs
      DiffReviewViewModel.cs
      ExecConsoleViewModel.cs
      ApprovalsBannerViewModel.cs
    Converters/
    Behaviors/
Modules/
  Chat/
    Services/
    Models/
    Pipelines/
  Diff/
    Services/
    Models/
    Pipelines/
  Exec/
    Services/
    Models/
  Approvals/
  Mcp/
  Workspace/
Shared/
  Cli/
  Messaging/
  Options/
  Telemetry/
  Utilities/
```
- Folders mirror logical modules, enabling partial extraction into separate assemblies later.
- Shared cross-cutting concerns (CLI transport, messaging, telemetry, utilities) stay under `Shared/` to avoid feature coupling.
- `Modules/*/Pipelines` host orchestrators/handlers; `Services` keep IO or VS SDK access contained for testing.

## Binding Approach
- Adopt full MVVM using `CommunityToolkit.Mvvm` (already ships for VS extensions) to eliminate manual `INotifyPropertyChanged` wiring and enable observable commands.
- Views bind exclusively to view-model properties/`ICommand`s; code-behind limited to shell lifecycle (Loaded/Unloaded) and services registration.
- Event surfaces that require thread affinity (e.g., approvals banner) expose `AsyncRelayCommand` wrappers ensuring UI thread marshaling via `JoinableTaskFactory` integration helpers.
- Design-time data contexts provided through sample view-model factories to improve XAML tooling inside VS.

## CLI Host Integration Interfaces
- `ICliTransport` abstracts the existing `CodexCliHost` process lifecycle: `Task<bool> StartAsync(CodexOptions, string, CancellationToken)`, `Task SendAsync(CliEnvelope, CancellationToken)`, `Task StopAsync()`, events `Connected`, `Disconnected`, `EnvelopeReceived`.
- `ICliSessionRouter` multiplexes envelopes to feature-specific handlers, performing correlation for exec/diff/chat workflows.
- `IHeartbeatMonitor` encapsulates reconnect logic and exposes `Task EnsureAliveAsync()` plus events `HeartbeatLost`, `HeartbeatRestored`.
- `ICliAuthenticationService` provides `Task<CodexAuthenticationResult> CheckAsync()` and `Task<bool> LoginAsync()/LogoutAsync()` so UI can surface auth state without referencing transport directly.

## Diff Pipeline Separation
- **Parsing**: `DiffRequestParser` converts CLI `diff_preview` payloads into domain models with file paths, hunks, and metadata; unit-testable using recorded JSON samples.
- **View**: `DiffReviewViewModel` maintains observable collections of diff entries, selection, and patch previews; subscribes to parser output via mediator.
- **Apply**: `DiffApplyOrchestrator` coordinates approvals, patch application (`IDiffWorkspaceApplier`), and error reporting. It publishes success/failure events for telemetry and updates to session store.
- **Diagnostics**: `DiffDiagnosticsService` streams diff logs into diagnostics pane and ensures UI remains responsive.
- **Approval handshake**: diff apply requests flow through `IApprovalService`, reusing shared approval model but scoped to diff pipeline signatures.

## Exec Console Responsibilities
- `ExecConsoleViewModel` drives tab visibility, selected command, log buffer, and cancel availability; it observes `ExecSessionStore` for incremental output updates.
- `ExecSessionService` subscribes to CLI exec events, maintains history with normalized command IDs, and raises completion notifications; handles auto-approval heuristics via `IApprovalService` callback.
- `ExecOutputBuffer` streams plain-text chunks to consumers, applying normalization/ANSI stripping off the UI thread before dispatch.
- `ExecTelemetryAdapter` bridges command lifecycle events (`Begin`, `Output`, `Complete`, `Error`) into telemetry pipeline without referencing UI controls.

## Shared State and Mediator Plan
- Introduce `CodexSessionStore` implementing `IObservableSessionState` with reducers for chat turns, diff sessions, exec runs, MCP tool inventory, and approval queue snapshots.
- A lightweight mediator (`EventHub` implementing `IModuleMessenger`) routes intents/events using strongly typed messages, avoiding direct module-to-module references.
- View-models subscribe to store slices via reactive helpers (e.g., `ObservableObject` `PropertyChanged` events) and dispatch actions via mediator commands.
- Workspace/Options updates flow from `WorkspaceContextService` and `OptionsMonitor` into the store, guaranteeing that modules react in-order and remain deterministic.

## Telemetry and Approval Touchpoints
- Telemetry service registers for lifecycle hooks: chat turn begin/end, diff preview/apply result, exec command begin/exit, tool invocation, MCP refresh, approvals prompts/resolution.
- Approval service centralizes prompt creation, remember/forget logic, and persistence. Modules raise `ApprovalRequest` messages; the approval store updates both UI (banner view-model) and CLI responses.
- Diagnostics logging (pane + status bar) funnels through `IDiagnosticsSink` to prevent UI classes from touching `DiagnosticsPane` directly.
- Telemetry events annotate request IDs and signatures so downstream analytics can correlate diff/exec approvals with CLI operations.

## Event Routing Sequence Diagram
```
User -> ChatInputViewModel: SubmitPrompt(text)
ChatInputViewModel -> EventHub: Dispatch(ChatRequestIntent)
EventHub -> IChatSessionService: StartAsync(ChatRequestIntent)
IChatSessionService -> ICliTransport: SendAsync(chat_envelope)
ICliTransport -> ICliSessionRouter: EnvelopeReceived(delta)
ICliSessionRouter -> IChatSessionService: Deliver(delta)
IChatSessionService -> CodexSessionStore: Reduce(ChatTurnDelta)
CodexSessionStore -> ChatTranscriptViewModel: Notify(turn update)
CodexSessionStore -> DiffReviewViewModel: Notify(if diff preview attached)
DiffReviewViewModel -> IApprovalService: RequestApproval(diff signature)
IApprovalService -> ApprovalsBannerViewModel: Show(prompt)
User -> ApprovalsBannerViewModel: Approve
ApprovalsBannerViewModel -> IApprovalService: Resolve(approved)
IApprovalService -> ICliTransport: SendAsync(approval_envelope)
ICliTransport -> ICliSessionRouter: EnvelopeReceived(exec/diff apply)
ICliSessionRouter -> ExecSessionService/DiffApplyOrchestrator: Dispatch(result)
ExecSessionService -> CodexSessionStore: Reduce(ExecUpdate)
ExecSessionService -> ITelemetryService: Record(exec completion)
```

## Summary and Next Steps
- Strategy positions MyToolWindowControl as a composition root with MVVM-driven submodules and shared mediator/state stores to decouple chat, diff, exec, MCP, and approval workflows.
- Immediate follow-ups: 1) prototype `CodexSessionStore` and `EventHub`; 2) lift existing `CodexCliHost` behind `ICliTransport`; 3) carve out chat/diff/exec view-models and migrate bindings incrementally.
