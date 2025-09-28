# MCP and Prompts Module Plan

## Scope & Goals
Task T10 extracts MCP tooling and prompt library features from `MyToolWindowControl` into dedicated services and view-models. The module will surface MCP tool metadata, track in-flight runs, manage prompt insertion, and coordinate refresh, telemetry, and accessibility behaviors. This document satisfies every Task T10 subtask.

## Current Implementation Snapshot
- MCP tool list and run history live in the control via `_mcpTools`, `_mcpToolRuns`, `_mcpToolRunIndex`, and `MaxMcpToolRuns` (`ToolWindows/MyToolWindowControl.xaml.cs:201`, `ToolWindows/MyToolWindowControl.xaml.cs:205`, `ToolWindows/MyToolWindowControl.xaml.cs:329`). Metadata is modeled by `McpToolInfo` while run state relies on `McpToolRun` (`ToolWindows/MyToolWindowControl.Types.cs:62`, `ToolWindows/MyToolWindowControl.Types.cs:96`).
- Prompt library data binds directly to `_customPrompts` and `CustomPromptInfo` (`ToolWindows/MyToolWindowControl.xaml.cs:209`, `ToolWindows/MyToolWindowControl.Types.cs:236`). Selection inserts text through WPF event handlers (`ToolWindows/MyToolWindowControl.xaml.cs:5849`).
- Refresh buttons rely on per-control timestamps and the constant `RefreshDebounceSeconds = 2` (`ToolWindows/MyToolWindowControl.xaml.cs:353`, `ToolWindows/MyToolWindowControl.xaml.cs:5601`, `ToolWindows/MyToolWindowControl.xaml.cs:5797`).
- Option persistence stores `LastUsedTool` and `LastUsedPrompt` on `CodexOptions` (`Options/CodexOptions.cs:94`, `Options/CodexOptions.cs:101`).
- Error handling is limited to diagnostics pane writes (`ToolWindows/MyToolWindowControl.Mcp.cs:34`) with no user-facing banner when list commands fail or when MCP servers are missing.
- Telemetry events `tool_selected` and `prompt_inserted` plus the `TelemetryTracker.RecordToolInvocation` call remain embedded in the view (`ToolWindows/MyToolWindowControl.xaml.cs:6163`, `ToolWindows/MyToolWindowControl.xaml.cs:6004`, `ToolWindows/MyToolWindowControl.xaml.cs:5969`).
- Keyboard and accessibility rely on `Border` mouse events in XAML, leaving gaps for keyboard activation (`ToolWindows/MyToolWindowControl.xaml:119`).

These shortcomings guide the refactor plan.

## 1. MCP Tool Metadata & Run Models
- Introduce immutable DTOs under `Modules/Mcp/Models/`:
  - `McpToolDescriptor` { `Id`, `DisplayName`, `Description`, `Server`, `IsOnline`, `Capabilities`, `SupportsInputSchema` } derived from CLI payloads. Provide normalization helpers accepting raw JSON tokens similar to `ExtractMcpTools` (`ToolWindows/MyToolWindowControl.xaml.cs:10897`).
  - `McpServerDescriptor` capturing server identifier, connection status, latency, and error state for multi-server scenarios.
  - `McpToolRunSnapshot` representing progress of a single invocation with fields { `RunId`, `ToolId`, `ServerId`, `StartedUtc`, `CompletedUtc`, `Status`, `Summary`, `OutputLines`, `ErrorDetails` } mirroring `McpToolRun` but detached from WPF, enabling background processing.
  - Enumerations `McpToolRunStatus` (Queued, Running, Completed, Failed, Cancelled) and `McpToolInvocationOutcome` to unify telemetry and UI states.
- Maintain collections in an `IMcpRunStore` backed by thread-safe `ImmutableDictionary` snapshots. UI subscribes to change streams instead of mutating `ObservableCollection` on the control thread.
- Provide adapters converting snapshots into lightweight view-model rows so virtualization and diffing are handled in the MVVM layer instead of manual `Insert`/`Trim` logic.

## 2. Prompt Library View-Model Interactions
- Create `PromptLibraryViewModel` exposing `ReadOnlyObservableCollection<PromptItemViewModel>` plus commands `InsertPromptCommand`, `RefreshPromptsCommand`, and `FilterPromptsCommand` to replace `OnCustomPromptClick` and manual insertion.
- `PromptItemViewModel` holds `Id`, `Title`, `Description`, `BodyPreview`, `SourceLabel`, `KeyboardShortcutHint`, and implements `ICommand` or `IAsyncRelayCommand` for activation. Provide hover text and automation names via properties instead of relying on status bar updates.
- Bind view-model to a new `PromptLibraryView` that supports list virtualization, keyboard navigation, search/filter box, and screen reader semantics. Replace `MouseLeftButtonDown` with `Button` or `ListBoxItem` commands to ensure keyboard support.
- Integrate with `ChatTranscriptViewModel` by emitting `PromptInserted` events so chat input services manage caret placement, gating on authentication, and streaming state rather than manipulating the `TextBox` directly.

## 3. Refresh Debounce & Caching Strategy
- Move refresh orchestration into `IMcpDirectoryService` with policies:
  - Maintain per-source cache entries (`ToolListCache`, `PromptListCache`) keyed by server set and user identity. Each entry stores payload, ETag, and fetched-at timestamp.
  - Apply configurable debounce windows (default 2 seconds matching `RefreshDebounceSeconds`) plus TTL (e.g., 60 seconds) with override flags for forced refresh.
  - Expose `Task<McpDirectorySnapshot> GetToolsAsync(RefreshRequest request)` returning cached data when within TTL and no invalidation reason (e.g., workspace change, server restart) exists.
  - Provide `RefreshDiagnostics` result capturing whether data came from cache, was throttled, or required remote fetch, enabling status messages and telemetry.
- Debounce signals at service level using an `AsyncBatchingWorkQueue` so multiple refresh requests coalesce. UI receives progress states (Idle, Throttled, Updating) to display inline banners instead of status bar messages.

## 4. Option Persistence for Selections
- Replace direct `_options.LastUsedTool` / `_options.LastUsedPrompt` access with an `IOptionsCache` abstraction already planned in T6/T11. `McpToolsViewModel` observes the cache to persist selection changes on the background thread.
- For multi-solution support, map option storage to `WorkspaceContextStore` entries so last-used values can differ per workspace. Provide fallback to global defaults when no solution context exists.
- When the user selects a tool or prompt that no longer exists, automatically clear the persisted value and surface a soft notification explaining the change.
- Ensure updates are transactional: view-model raises `SelectionChanged` event, service writes to options store via `await optionsStore.UpdateAsync(opts => opts with { LastUsedTool = ... })`, preventing `ThreadHelper` marshaling in UI code.

## 5. Error Handling for Missing Servers
- Extend `IMcpDirectoryService` to surface structured errors (`McpDirectoryErrorKind.NoServersConfigured`, `ServerUnreachable`, `HandshakeFailed`, `InvalidResponse`).
- `McpToolsViewModel` exposes `StatusBanner` data containing severity, message, and optional actions (e.g., "Open MCP settings"). When errors occur, show inline banner in the MCP pane instead of only logging.
- Provide retry guidance: on `NoServersConfigured`, display instructions similar to current XAML copy but with actionable buttons ("Open codex.json", "Learn more"). For server outages, allow user to dismiss banner and automatically schedule background retry with exponential backoff.
- Log diagnostics with correlation IDs tying CLI request IDs to user-visible errors to support telemetry and debugging.

## 6. Telemetry for Tool & Prompt Usage
- Define events published via `ITelemetryService`:
  - `McpToolsRefreshRequested` (properties: `Reason`, `UsedCache`, `Throttled`, `ServerCount`).
  - `McpToolSelected` (`ToolId`, `ServerId`, `HasSchema`, `Source`).
  - `McpToolRunStarted` / `Completed` / `Failed` capturing durations, success, output size, error code, and invocation context (chat vs. approval).
  - `PromptLibraryRefreshRequested` (`Reason`, `PromptCount`).
  - `PromptInserted` (`PromptId`, `Source`, `InsertedChars`, `CaretMode`).
- Route all telemetry through module-level aggregator to avoid direct `LogTelemetryAsync` calls. Aggregator pushes structured payloads and automatically links to session metadata (workspace, CLI version) provided by `ICodexSessionCoordinator`.
- Continue incrementing `TelemetryTracker` counters by feeding aggregated stats (e.g., tool invocation count) through events rather than calling `_telemetry.RecordToolInvocation()` directly.

## 7. Integration Tests with Mocked CLI
- Create integration test suite under `CodexVS22.Tests.Modules.Mcp` using the planned `CliEventHub` to replay CLI JSON:
  - `ListMcpTools` event populates descriptors and ensures cache/diffing works.
  - Tool run lifecycle (begin/output/end) verifies snapshots update sequentially and run trimming honors `MaxMcpToolRuns` equivalent policy.
  - Prompt list event verifies prompt view-model updates and selection persistence.
  - Error scenario tests simulate CLI returning `error` property or empty arrays, expecting service to emit `McpDirectoryErrorKind` and UI banner states.
  - Refresh throttling test ensures two rapid refresh calls collapse into one request and produce `Throttled=true` telemetry.
- Provide contract tests for `PromptInsertionService` to confirm inserted text interacts with chat input via mediator, including undo support.

## 8. Accessibility & Keyboard Support
- Replace `ItemsControl` + `Border` templates with `ListView` / `ListBox` items using `ButtonBase` or `ListBoxItem` focus scopes so arrow keys and Enter/Space trigger commands without mouse events.
- Set `AutomationProperties.Name` and `AutomationProperties.HelpText` from model metadata (`DisplayName`, `Description`, `Server`) for screen readers. Provide `AutomationProperties.ItemStatus` reflecting run status (Running, Completed, Failed).
- Ensure prompt insertion has keyboard shortcuts (e.g., Alt+1..9 for quick prompts) and exposes `AccessKey` text. Provide live-region updates when runs start/complete to announce status changes.
- Document requirements to test with Narrator and high-contrast themes. Coordinate with Task T15 to ensure global focus visuals are consistent.

## 9. Hover Help & Data Providers
- Replace manual status bar hover (`OnMcpToolMouseEnter`) with explicit `ToolTip` bound to `ToolDescriptor.TooltipText` derived from aggregated server metadata (description + capability tags).
- Introduce `IMcpDocumentationService` that resolves per-tool documentation URLs or help topics, enabling the view to display inline links. Service consumes data from CLI `metadata` payloads when available and falls back to docs site used in `OnMcpHelpClick`.
- Provide unified hover help for prompts via `PromptItemViewModel.HoverSummary`, including last-modified timestamp and scope (workspace/global). Tooltips should be keyboard-accessible and not rely on pointer hover exclusively.
- Ensure hover help fetches data lazily and caches results to avoid blocking UI when user moves focus rapidly.

## 10. Deliverable & Handoff
- This plan (`docs/mcp-module-plan.md`) fulfills Task T10. Implementation should create a `Modules/Mcp` folder housing services, models, and view-models described above. Coordinate with Task T6 (CLI Host), Task T11 (Options), Task T12 (Telemetry), and Task T13 (Approvals) during execution.

## Next Steps
1. Scaffold `IMcpDirectoryService`, `IMcpPromptService`, `McpToolsViewModel`, and `PromptLibraryViewModel` with dependency injection registrations.
2. Design caching/refresh policies and integrate with the shared session coordinator.
3. Update UI views to consume view-models, ensuring accessibility and telemetry hooks align with this plan.
4. Author integration tests described above using mocked CLI payloads.
