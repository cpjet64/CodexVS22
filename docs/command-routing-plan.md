# Command Routing and Integration Plan (Task T16)

## 1. Overview
Codex VS currently registers three primary commands via `VSCommandTable.vsct`: `OpenCodexCommand`, `AddToChatCommand`, and `DiagnosticsCommand`. The refactor introduces modular view-models and services, requiring updated routing and ownership. This plan aligns commands with the new architecture while preserving VS integration points.

## 2. Command Surface Audit
- `Open Codex` (Tools menu) – toggles the tool window (`Commands/OpenCodexCommand.cs:6`).
- `Add to Codex chat` (editor context) – forwards active text selection to chat input (`Commands/AddToChatCommand.cs:10`).
- `Codex Diagnostics` (Tools menu) – activates diagnostics pane (`Commands/DiagnosticsCommand.cs:9`).
Future refactor may add context-menu items for diff/exec actions and command palette entries.

## 3. Target Command Architecture
- Encapsulate command registrations in a dedicated `CommandRoutingModule` that binds `OleMenuCommandService` to `ICommandRouter` abstractions.
- Map each VS command ID to a `CodexCommandDescriptor` (name, category, roles, required services) stored in configuration for telemetry alignment.
- Compose command handlers using DI: `IOpenShellCommandHandler`, `IAddSelectionCommandHandler`, `IShowDiagnosticsCommandHandler` with async methods returning `CommandResult`.
- Provide `ICommandAvailabilityService` that evaluates enablement/visibility based on application state (e.g., chat availability, CLI connectivity). Avoid `async void` `BeforeQueryStatus` blocks.

## 4. AsyncPackage Refactor Plan
- Replace direct `Commands.X.InitializeAsync` registration in `CodexVS22Package.InitializeAsync` with modular bootstrap:
  1. During package init, resolve `ICommandRouter` from container.
  2. Router registers commands by enumerating descriptors and invoking `OleMenuCommandService.AddCommand`.
  3. Router wires `BeforeQueryStatus` to `ICommandAvailabilityService` results, executing on UI thread with `JoinableTaskFactory`.
- Ensures new commands can be added without editing package class.

## 5. Add to Chat Data Flow (Post-Refactor)
```
VS Command -> ICommandRouter -> IAddSelectionCommandHandler.ExecuteAsync()
  -> IEditorSelectionService.GetSelectionAsync()
    -> returns `EditorSelectionSnapshot`
  -> IChatInputCoordinator.EnqueueSelectionAsync(snapshot, context)
    -> updates `ChatInputViewModel` via state store
```
- Splits responsibilities: selection retrieval, chat input mutation, diagnostics logging.
- Supports future sources (solution explorer context, diff viewer) via `EditorSelectionService` abstraction.

## 6. Diagnostics Command Routing
- Replace direct diagnostics pane calls (`Commands/DiagnosticsCommand.cs:9`) with `IDiagnosticsService.ShowAsync()` to centralize logging and surface other sinks (e.g., output window).
- Command handler logs telemetry event `command_diagnostics_opened` with user role (tools menu vs command palette) using new telemetry module.
- Provide fallback messaging when diagnostics infrastructure fails (display InfoBar to prompt user to open output window).

## 7. Toolbars and Context Menus
- Maintain Tools menu group but plan to add command palette entries via `KnownCommands` registration.
- Introduce context menu commands for diff/exec modules post-split (e.g., “Apply current diff with Codex”). Commands will live under new groups referencing module-specific handler services.
- Provide `CommandContributionBuilder` helper to register XAML-based toolbars once MVVM shell is in place.

## 8. Keyboard Shortcuts
- Evaluate adding shortcuts for key flows:
  - `Ctrl+Alt+@` to open Codex window.
  - `Ctrl+Alt+A` to invoke Add to Chat on selection.
- Document requirement to avoid conflicts with default VS assignments and surface in Options page for customization.
- Ensure `ICommandBindingService` exposes a central registry so view-models and help docs stay in sync.

## 9. Command Ownership and Responsibilities
| Command | New Owner Service | Responsibilities |
| --- | --- | --- |
| Open Codex | `IToolWindowOrchestrator` | Ensure tool window/view-model is initialized, handle multi-instance scenarios, track telemetry. |
| Add to Chat | `IChatInputCoordinator` + `IEditorSelectionService` | Retrieve selection, sanitize, append to chat state, handle errors/feedback. |
| Diagnostics | `IDiagnosticsService` | Focus diagnostics surface, stream logs, provide fallback when not available. |
| Future Diff Apply | `IDiffReviewCommandHandler` | Trigger diff view-model apply pipeline through approvals service. |
| Future Exec Cancel | `IExecCommandHandler` | Cancel exec tasks using new CLI host service. |

## 10. Testing Strategy
- Unit tests for `CommandRouter` verifying registration, enablement rules, and exception handling.
- Integration tests (VS host) using `ThreadedWaitDialog` to ensure commands appear in menus and respect state gating.
- Playwright-style UI automation (if available) to assert keyboard shortcuts trigger expected flows.
- Mocked services for selection retrieval and diagnostics to test Add to Chat command without DTE dependency.

## 11. Telemetry Alignment
- Emit telemetry events per command invocation with context: command id, source (menu, keyboard), duration, success/failure.
- `OptionsTelemetryAdapter` (Task T11) integrates to capture command-related option changes (e.g., enabling shortcuts).
- Ensure telemetry categories mirror CLI service events for cross-correlation.

## 12. Pending Tasks / Dependencies
- Requires completion of CLI host and options services to provide dependencies for command handlers.
- Diff/exec command additions dependent on module decomposition tasks (T8/T9).
- Accessibility review for new toolbar contributions (ensuring automation peers naming consistent).

## 13. Checklist Traceability
- Audit VSCT vs module APIs → §2 & §9.
- AsyncPackage handler refactor → §4.
- Add to Chat data flow post-split → §5.
- Diagnostics command integration → §6.
- Toolbar/context menu updates → §7.
- Keyboard shortcuts → §8.
- Command ownership documentation → §9.
- Tests for command paths → §10.
- Command telemetry alignment → §11.
- Deliverable (this document) → §13.

