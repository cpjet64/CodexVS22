# XAML Binding Map for MyToolWindowControl

## Scope
- Task T4 from `todo-refactor-mytoolwindowcontrol.md` covers the binding inventory for `MyToolWindowControl` and its partial classes.
- Source files reviewed: `ToolWindows/MyToolWindowControl.xaml`, `ToolWindows/MyToolWindowControl.xaml.cs`, and partials under `ToolWindows/MyToolWindowControl.*.cs`.

## Subtask Coverage
- [x] Audit XAML bindings and code-behind lookups (see **Current Bindings and Element Lookups**).
- [x] List commands, click handlers, and routed events (see **Event and Command Inventory**).
- [x] Determine bindings needing conversion to ICommand (see **ICommand Conversion Targets**).
- [x] Identify controls requiring view-model properties (see **View-Model Property Requirements**).
- [x] Mark UI elements referencing static helpers (see **Static Helper and Service Touchpoints to Abstract**).
- [x] Note data templates and item sources in use (see **Data Templates and Item Sources**).
- [x] Capture validation rules or converters referenced (see **Validation Rules and Converters**).
- [x] Flag accessibility automation peers to preserve (see **Accessibility**).
- [x] Plan binding changes for diff tree and exec console (see **Diff Tree Binding Refactor Plan** and **Exec Console Binding Refactor Plan**).
- [x] Commit inventory to docs/xaml-binding-map.md (this document).

## Current Bindings and Element Lookups
### Top Command Bar (ToolWindows/MyToolWindowControl.xaml:17-68)
- `LogoutButton`, `CopyAllButton`, `ResetApprovalsButton`, and `_Clear` buttons fire routed `Click` events handled in code-behind (`OnLogoutClick`, `OnCopyAllClick`, `OnResetApprovalsClick`, `OnClearClick`).
- `ExecConsoleToggle` raises `Checked`/`Unchecked` events (`OnExecConsoleToggleChanged`) and is synchronized manually with `_options.ExecConsoleVisible` via `FindName`.
- `StackPanel` relies on dynamic resources for theme brushes; no direct data bindings beyond static text.

### MCP Tools and Activity (ToolWindows/MyToolWindowControl.xaml:80-240)
- `McpToolsContainer` and `McpToolsEmptyText` visibility managed in code (`UpdateMcpToolsUi`). `ItemsControl` `McpToolsList` uses a `DataTemplate` binding to `Name`, `Description`, `Server`; `ItemsSource` assigned via `InitializeMcpToolsUi` and maintained as `_mcpTools`.
- Event handlers on template root `Border`: `OnMcpToolClick`, `OnMcpToolMouseEnter`, `OnMcpToolMouseLeave`.
- `McpToolRunsContainer` binds template fields to `McpToolRun` properties (`ToolName`, `StatusDisplay`, `Server`, `TimingDisplay`, `Detail`). `ItemsSource` is `_mcpToolRuns` populated in `InitializeMcpToolsUi` and refreshed through `UpdateMcpToolRunsUi`.

### Custom Prompts (ToolWindows/MyToolWindowControl.xaml:260-344)
- `CustomPromptsList` `ItemsControl` template binds to `Name`, `SourceDisplay`, `Description`, `Body`. `ItemsSource` set to `_customPrompts` via `InitializeMcpToolsUi` → `UpdateCustomPromptsUi`.
- Template `Border` hooks `OnCustomPromptClick`, `OnCustomPromptMouseEnter`, `OnCustomPromptMouseLeave`.

### Authentication & Status Banners (ToolWindows/MyToolWindowControl.xaml:345-430)
- `AuthBanner`, `AuthMessage`, and `LoginButton` toggled in `RefreshAuthUiAsync` using `FindName`.
- `FullAccessBanner` and text managed in `UpdateFullAccessBannerAsync` / `ApplyExecConsoleToggleState`.
- `ApprovalPromptBanner` includes `ApprovalPromptText`, `ApprovalRememberCheckBox`, and buttons (`OnApprovalApproveClick`, `OnApprovalDenyClick`) orchestrated via `ShowApprovalBanner`.

### Options Row (ToolWindows/MyToolWindowControl.xaml:431-482)
- `ApprovalCombo`, `ModelCombo`, `ReasoningCombo` rely on `SelectionChanged` handlers (`OnApprovalModeChanged`, `OnModelSelectionChanged`, `OnReasoningSelectionChanged`).
- Combo items populated imperatively in `InitializeSelectorsAsync`; selections mirrored into `_options`.

### Diff Tree (ToolWindows/MyToolWindowControl.xaml:483-548)
- `DiffTreeContainer` and `DiffSelectionSummary` visibility controlled in `UpdateDiffTreeAsync`, `UpdateDiffSelectionSummary`.
- `TreeView` uses `ItemsSource` set to `_diffTreeRoots` (ObservableCollection<DiffTreeItem>). Template binds `IsChecked`, `IsExpanded`, `Name`, `RelativePath` with two-way checkbox binding managed by `DiffTreeItem.SetIsChecked` and `OnDiffTreeCheckBoxClick`.
- `DiscardPatchButton` invokes `OnDiscardPatchClick`.

### Transcript and Input (ToolWindows/MyToolWindowControl.xaml:549-720)
- `Transcript` `StackPanel` populated entirely via code-behind (`AppendAssistantTurnAsync`, etc.); only initial welcome `TextBlock` declared in XAML.
- `TokenUsageText`, `TelemetryText`, `StreamingIndicator`, `StatusText` updated directly via `FindName` in helper methods.
- `SendButton` uses `OnSendClick`; `InputBox` handles `PreviewKeyDown` (`OnInputPreviewKeyDown`).
- Stream error banner elements (`StreamErrorText`, `StreamRetryButton`) toggled via `ShowStreamErrorBanner`/`HideStreamErrorBanner`.

### Exec Console (No static XAML definition)
- Exec turns are built in code (see `CreateExecTurnAsync` around `ToolWindows/MyToolWindowControl.xaml.cs:10380-10500`). Buttons (`cancelButton`, `copyButton`, `clearButton`, `exportButton`) assigned handlers (`OnExecCancelClick`, `OnExecCopyAllClick`, `OnExecClearClick`, `OnExecExportClick`). Container subscribes to `PreviewMouseWheel` (`OnExecContainerPreviewMouseWheel`).

## Event and Command Inventory
- Global lifecycle: `OnLoaded`, `OnUnloaded`, window size/location state handlers in `MyToolWindowControl.Windowing.cs`.
- Authentication & approvals: `OnLoginClick`, `OnLogoutClick`, `OnResetApprovalsClick`, `OnApprovalApproveClick`, `OnApprovalDenyClick`.
- Options combo events: `OnApprovalModeChanged`, `OnModelSelectionChanged`, `OnReasoningSelectionChanged`.
- MCP & prompts: `OnRefreshMcpToolsClick`, `OnMcpHelpClick`, `OnMcpToolClick`, `OnMcpToolMouseEnter`, `OnMcpToolMouseLeave`, `OnRefreshPromptsClick`, `OnCustomPromptClick`, `OnCustomPromptMouseEnter`, `OnCustomPromptMouseLeave`.
- Diff management: `OnDiscardPatchClick`, `OnDiffTreeCheckBoxClick`.
- Transcript & streaming: `OnCopyAllClick`, `OnClearClick`, `OnSendClick`, `OnInputPreviewKeyDown`, `OnStreamRetryClick`, `OnStreamDismissClick`, `OnCopyMessageMenuItemClick`.
- Exec console: `OnExecConsoleToggleChanged`, `OnExecCancelClick`, `OnExecCopyAllClick`, `OnExecClearClick`, `OnExecExportClick`, `OnExecContainerPreviewMouseWheel`.

## ICommand Conversion Targets
- Replace button `Click` handlers with `ICommand` bindings: login/logout, copy transcript, reset approvals, clear chat, refresh tools/prompts, send message, approve/deny, discard patch, stream retry/dismiss, exec actions, transcript copy context menu.
- Bind `ExecConsoleToggle.IsChecked` two-way to `IsExecConsoleVisible` property with `ICommand` for state persistence only if side-effects remain.
- Convert combo selections to two-way bindings on `SelectedApprovalMode`, `SelectedModel`, `SelectedReasoning`; no event handler needed once property setters handle persistence.
- Replace `PreviewKeyDown` send flow with `KeyBinding` to `SendMessageCommand` (Enter key) to avoid manual `ThreadHelper` dispatch.
- Diff tree checkboxes should bind `IsChecked` to view-model properties; consider `ICommand` for explicit toggle if additional logic is needed beyond property setter.

## View-Model Property Requirements
- Authentication: `IsAuthBannerVisible`, `AuthMessage`, `CanLogin`, `CanLogout`.
- Session options: `ApprovalModes`, `SelectedApprovalMode`, `ModelOptions`, `SelectedModel`, `ReasoningOptions`, `SelectedReasoning`, `IsExecConsoleVisible`, `ExecConsoleHeight`.
- MCP/prompts: collections (`ObservableCollection<McpToolViewModel>`, `McpToolRunViewModel`, `CustomPromptViewModel`), plus banner visibility states.
- Diff tree: `ObservableCollection<DiffNodeViewModel>`, `DiffSelectionSummary`, `IsDiscardVisible`, `HasDiffs`.
- Transcript: `ObservableCollection<TranscriptItemViewModel>` backing the transcript stack, `TokenUsageSummary`, `TelemetrySummary`, `StatusText`, `IsStreaming`, `StreamErrorMessage`, `CanRetryStream`, `ChatInput`, `IsStreamErrorVisible`.
- Approvals: `ApprovalPrompt` composite with `Message`, `CanRemember`, `RememberDecision`, `ApproveCommand`, `DenyCommand`.
- Exec console: `ObservableCollection<ExecTurnViewModel>` with properties for output text, ANSI-stripped text, running state, available actions, command metadata.

## Static Helper and Service Touchpoints to Abstract
- `ThreadHelper.JoinableTaskFactory` for UI thread switching (ubiquitous across handlers).
- `VS.StatusBar` updates triggered by most UI interactions (copy, prompts, tools, exec, etc.).
- `DiagnosticsPane` logging used for error reporting (`OnCopyAllClick`, exec actions, approvals, diff, auth).
- `Clipboard` access for transcript/execution copy operations.
- `System.Windows.MessageBox` usage in `OnClearClick`.
- Codex-specific helpers: `CodexVS22Package.OptionsInstance`, `ApprovalSubmissionFactory`, `DiffUtilities`, `ChatTextUtilities`, `DiagnosticsPane.GetAsync`.
- File and telemetry helpers inside partial classes (`_telemetry`, `LogTelemetryAsync`, option persistence). These should become injected services or view-model collaborators.

## Data Templates and Item Sources
- `McpToolsList` template (ToolWindows/MyToolWindowControl.xaml:107-164) binds to `McpToolInfo`, uses hover/click events.
- `McpToolRunsList` template (ToolWindows/MyToolWindowControl.xaml:190-238) binds to `McpToolRun` observable with property change notifications.
- `CustomPromptsList` template (ToolWindows/MyToolWindowControl.xaml:287-332) binds to `CustomPromptInfo` data.
- `DiffTreeView` hierarchical template (ToolWindows/MyToolWindowControl.xaml:520-544) binds to `DiffTreeItem.Children` with tri-state checkbox binding to `IsChecked`.
- Exec console currently lacks XAML templates; plan to introduce `ItemsControl` with DataTemplate for `ExecTurnViewModel` representing header, output, and action buttons.

## Validation Rules and Converters
- No explicit `ValidationRule`, `Converter`, or `Multibinding` usage detected. Future refactor may introduce `BooleanToVisibilityConverter` or dedicated converters for banner visibility and exec state.

## Accessibility
- Existing `AutomationProperties.Name` assignments on interactive elements (buttons, combos, diff tree components, transcript, stream controls) must be preserved. When migrating to data templates, ensure attached properties remain on template roots (`AutomationProperties.Name="{Binding ...}"` for diff checkboxes, etc.).
- `AccessText` accelerators (`Lo_gout`, `_Send`) need to remain aligned with command bindings.

## Diff Tree Binding Refactor Plan
- Expose `DiffTreeNodes` `ObservableCollection` on view-model; bind `TreeView.ItemsSource` directly without `FindName`.
- Convert `DiffTreeItem` into dedicated view-model implementing `INotifyPropertyChanged` with `IsChecked`, `IsExpanded`, `Name`, `RelativePath`, child collection. Move `HandleDiffSelectionChanged` logic into view-model or mediator service.
- Bind `DiscardPatchCommand` to button, with `IsEnabled`/`Visibility` driven by `HasSelectableDiffs` property.
- Update summary text binding to view-model property (`DiffSelectionSummaryText`), eliminating manual `FindName` updates.
- Replace manual `UpdateDiffTreeAsync` UI updates with asynchronous dispatcher that only mutates view-model collections; view handles rest via binding.

## Exec Console Binding Refactor Plan
- Introduce `ExecConsoleTurns` observable bound to `ItemsControl` (likely `ListView`/`ItemsControl`) with DataTemplate for header (status + metadata) and output text block using `TextBlock.Inlines` or `RichTextBox` for ANSI rendering.
- Replace dynamic button creation with commands bound to `ExecTurnViewModel` (`CancelExecCommand`, `CopyExecOutputCommand`, `ClearExecOutputCommand`, `ExportExecLogCommand`). Use `CommandParameter` to pass the selected turn.
- Four actions currently depend on button `Tag` storing `ExecTurn`; swap to binding and command parameters, removing `Tag` usage.
- Manage console visibility through binding to `IsExecConsoleVisible`; toggle should control host view-model property with persisted options update handled in view-model/service.
- Encapsulate ANSI rendering into a behavior or converter invoked by binding rather than direct `RenderAnsiText` call; consider presenting raw text plus formatted spans in the view-model.

## Additional Notes
- Transcript rendering currently relies on stacking `Border` and `TextBlock` instances created in code. Future phases should introduce an `ItemsControl` with data templates for user/assistant/exec turns to align with MVVM goals identified in other tasks.
- No validation errors are surfaced today; binding migration should consider user feedback for invalid states (e.g., missing input on send) via `IDataErrorInfo` or `NotifyDataErrorInfo`.
- Preserve dispatcher-affinity operations (focus management, scrolling) possibly via interaction behaviors once core bindings are in place.
