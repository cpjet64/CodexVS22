# UI Shell and Window Orchestration Plan

## Scope & Current State
Task T15 extracts the Visual Studio tool window “shell” responsibilities from `MyToolWindowControl` into a dedicated shell/view-model pair. Today the control owns layout, environment banners, toolbar buttons, streaming indicators, MCP/prompt panes, and dialog prompts while directly manipulating WPF elements and window chrome (`ToolWindows/MyToolWindowControl.xaml:1`, `ToolWindows/MyToolWindowControl.xaml.cs:1737`). Window sizing is persisted via option mutations inside the control (`ToolWindows/MyToolWindowControl.Windowing.cs:10`). Actions such as “Copy All” and “Clear” are wired to code-behind handlers (`ToolWindows/MyToolWindowControl.xaml.cs:1909`, `ToolWindows/MyToolWindowControl.xaml.cs:5969`). This plan outlines the refactor strategy for a composable shell aligned with Tasks T5/T6/T7 and satisfies every Task T15 subtask.

## 1. Tool Window Composition Root
- Create `CodexToolWindowView` containing only shell chrome, placeholders for feature panes, and resource dictionaries. The new root hosts regions: `HeaderBarRegion`, `BannerRegion`, `BodyRegion`, and `FooterRegion` to be populated via DataTemplates resolved from view-models.
- Introduce `ToolWindowShellViewModel` (already defined at a high level in `docs/refactor-strategy.md:4`) responsible for orchestrating child view-models: `ChatTranscriptViewModel`, `DiffReviewViewModel`, `ExecConsoleViewModel`, `ApprovalsBannerViewModel`, `McpToolsViewModel`, and `PromptLibraryViewModel`.
- The composition root binds to `ToolWindowShellViewModel` via DI and uses `ContentControl` placeholders with implicit DataTemplates so modules can render their own views without shell-level knowledge of their internals.
- Remove direct control references to `FindName` lookups; the shell exposes strongly typed properties (e.g., `IObservable<bool> IsExecConsoleVisible`) and commands for toolbar buttons.

## 2. Dispatcher / Thread Usage Plan
- Adopt the `IUiDispatcher` abstraction proposed in Task T5 so shell operations (status banner updates, focus changes) marshal via a single injected dispatcher rather than ad-hoc `ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync()` calls (`ToolWindows/MyToolWindowControl.xaml.cs:5613`, `:7375`).
- `ToolWindowShellViewModel` exposes `UiActionQueue` (e.g., `IObservable<UiAction>`) consumed by the view, ensuring non-UI services schedule work without violating thread-affinity. All UI updates (status text, streaming indicator visibility, banner messages) are delivered through dispatcher-friendly state rather than direct `BeginInvoke` usage (`ToolWindows/MyToolWindowControl.Windowing.cs:13`).
- Background services (CLI event hub, approval coordinator) communicate via mediator events; the shell subscribes through asynchronous handlers returning `Task` to avoid `async void` entry points.

## 3. Host Window Event Hooks
- Wrap host window interactions in `IToolWindowHostService` responsible for subscribing to `IVsWindowFrame` events instead of hooking WPF `Window.SizeChanged/LocationChanged` directly (`ToolWindows/MyToolWindowControl.Windowing.cs:30`).
- The service emits events (`HostMoved`, `HostResized`, `HostStateChanged`, `HostClosing`) consumed by `ToolWindowShellViewModel`, which updates layout state store. When the view initializes, it requests current geometry from the service and applies stored preferences.
- Ensure host hooks unsubscribe automatically when tool window closes or view-model disposes, removing the need for manual `UnhookWindowEvents()` calls scattered in the control.

## 4. Layout Persistence Coordination
- Replace option mutation side-effects with `ILayoutPersistenceStore` residing in state layer (aligned with Task T6/T11). Shell view-model reads/writes layout snapshots `{ Width, Height, Left, Top, WindowState, ExecConsoleHeight, PaneVisibility }`.
- Writes occur on throttled schedule (e.g., 500 ms debounce) to avoid repeated option saves triggered by `SizeChanged` storms (`ToolWindows/MyToolWindowControl.Windowing.cs:74`).
- Layout snapshots are scoped per workspace using `WorkspaceContextStore`, allowing different solutions to maintain distinct window positions. Provide fallback defaults when running in floating or docked states.
- Introduce serialization guards that detect invalid positions (off-screen) and restore to primary monitor bounds when necessary.

## 5. Spinner and Status Indicator Bindings
- Move streaming indicator (`StreamingIndicator`, `ToolWindows/MyToolWindowControl.xaml:608`) and status text to dedicated `ShellStatusViewModel`. Properties: `IsStreaming`, `StatusMessage`, `TokenSummary`, `TelemetrySummary`.
- Bind `ProgressBar` visibility to `IsStreaming` via BooleanToVisibility converter; remove imperative `UpdateStreamingIndicator` (`ToolWindows/MyToolWindowControl.xaml.cs:9391`).
- Provide `StatusSeverity` enum to style foreground colors (info/warn/error). Use `VisualStateManager` or `DataTriggers` instead of manual status bar messages routed through `VS.StatusBar`.
- Expose asynchronous command states (send in-progress, prompt insertion) through `IAsyncCommand` metadata enabling automatic spinner toggling.

## 6. Accessibility Automation Peer Ownership
- Define an `AutomationPeer` implementation, `CodexToolWindowAutomationPeer`, controlling ownership of automation elements for header buttons, banners, and body regions. The peer aggregates child peers contributed by subviews.
- Ensure toolbar items use `Button`/`ToggleButton` with proper `AccessText` while declaring `AutomationProperties.Name/HelpText/AcceleratorKey`. Move hover-driven status messages to accessible tooltips with keyboard access (replacing `OnMcpToolMouseEnter`, `ToolWindows/MyToolWindowControl.xaml.cs:6205`).
- Spinner and status messages raise `AutomationProperties.LiveSetting=Polite` updates when state changes, making streaming or error updates audible.
- Provide high-contrast assets and ensure focus scopes transition between header, body, and footer without trapping screen readers.

## 7. Align “Clear Chat” and “Copy All” Actions with View-Models
- Replace `OnClearClick` (`ToolWindows/MyToolWindowControl.xaml.cs:1909`) and `OnCopyAllClick` (`ToolWindows/MyToolWindowControl.Transcript.cs:18`) handlers with shell commands exposed by `ChatTranscriptViewModel`.
- Buttons bind to `ChatTranscriptViewModel.ClearTranscriptCommand` and `CopyTranscriptCommand`, which internally coordinate with approval and exec modules to determine availability (e.g., disable when exec turns active or clipboard operation not permitted).
- Use command `CanExecute` state to toggle button enablement instead of manual gating logic sprinkled across the control.
- Emit telemetry via the chat module rather than the shell, ensuring consistent analytics.

## 8. Resizing & Layout Persistence Logic
- Provide `ShellLayoutController` that orchestrates split view sizes (chat vs. exec console) and listens to modules publishing preferred heights. Replace `ApplyExecConsoleToggleState` and `_execConsolePreferredHeight` manipulations (`ToolWindows/MyToolWindowControl.Exec.Helpers.cs:11`, `ToolWindows/MyToolWindowControl.xaml.cs:273`).
- Use WPF `GridLengthAnimation` or built-in row definitions with `GridSplitter` to allow user-driven resizing; shell controller records persistent splitter ratios.
- When host window state changes (docked/floating/tabbed), controller recalculates layout metrics ensuring diff/exec panes remain usable at small widths.
- Provide adaptive layout states (compact vs. expanded) so modules can hide optional adorners when window width < threshold.

## 9. Tests for Window Open/Close Sequences
- Author integration tests leveraging Visual Studio integration harness (or UI-less harness using `WindowStub`) verifying:
  - View-model initialization applies persisted layout, and shell registers for host events exactly once.
  - Closing/reopening tool window disposes subscriptions without leaking dispatcher callbacks (replacing manual `UnhookWindowEvents`).
  - `ShellStatusViewModel` state resets between sessions while layout persistence survives process restarts.
  - Commands `ClearTranscript`, `CopyTranscript`, `ToggleExecConsole` remain enabled/disabled appropriately across open/close cycles.
  - Accessibility peer enumeration includes header controls, banners, and body content, validated via UI Automation tree inspection.

## 10. Deliverable
This document (`docs/ui-shell-plan.md`) fulfils Task T15 by documenting the orchestration strategy, covering composition root, dispatcher approach, host window hooks, layout persistence, status bindings, accessibility, command alignment, resizing logic, and test plan.

## Next Steps
1. Scaffold `CodexToolWindowView` and `ToolWindowShellViewModel` with DI registrations.
2. Implement `IToolWindowHostService`, `ShellLayoutController`, and `ShellStatusViewModel` with dispatcher abstraction.
3. Update toolbar XAML to bind to new commands and status properties; remove direct code-behind handlers.
4. Author integration tests as outlined and validate accessibility with Narrator/high-contrast modes.
