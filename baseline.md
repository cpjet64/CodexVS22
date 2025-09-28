# MyToolWindowControl Baseline

## File Anatomy and Metrics
- 25 partial class files plus XAML total 26 artifacts and 18,150 LOC; `ToolWindows/MyToolWindowControl.xaml.cs` alone carries 13,226 lines (73% of the surface).
- Partial distribution skews heavily toward working-directory, MCP, and patch handling modules; see detailed counts below for refactor planning.

| File | Lines |
| --- | ---:|
| ToolWindows/MyToolWindowControl.Approvals.cs | 287 |
| ToolWindows/MyToolWindowControl.Authentication.cs | 220 |
| ToolWindows/MyToolWindowControl.Exec.Helpers.cs | 124 |
| ToolWindows/MyToolWindowControl.Exec.cs | 217 |
| ToolWindows/MyToolWindowControl.Heartbeat.Helpers.cs | 289 |
| ToolWindows/MyToolWindowControl.Heartbeat.Models.cs | 24 |
| ToolWindows/MyToolWindowControl.Heartbeat.cs | 185 |
| ToolWindows/MyToolWindowControl.JsonHelpers.cs | 109 |
| ToolWindows/MyToolWindowControl.Lifecycle.cs | 137 |
| ToolWindows/MyToolWindowControl.Mcp.Helpers.cs | 62 |
| ToolWindows/MyToolWindowControl.Mcp.cs | 260 |
| ToolWindows/MyToolWindowControl.Options.cs | 196 |
| ToolWindows/MyToolWindowControl.Telemetry.cs | 189 |
| ToolWindows/MyToolWindowControl.Transcript.cs | 132 |
| ToolWindows/MyToolWindowControl.Types.cs | 257 |
| ToolWindows/MyToolWindowControl.Windowing.cs | 124 |
| ToolWindows/MyToolWindowControl.WorkingDirectory.Environment.cs | 269 |
| ToolWindows/MyToolWindowControl.WorkingDirectory.Helpers.cs | 188 |
| ToolWindows/MyToolWindowControl.WorkingDirectory.Models.cs | 70 |
| ToolWindows/MyToolWindowControl.WorkingDirectory.Projects.cs | 165 |
| ToolWindows/MyToolWindowControl.WorkingDirectory.Selection.cs | 299 |
| ToolWindows/MyToolWindowControl.WorkingDirectory.SolutionEvents.cs | 86 |
| ToolWindows/MyToolWindowControl.WorkingDirectory.Subscriptions.cs | 238 |
| ToolWindows/MyToolWindowControl.WorkingDirectory.cs | 144 |
| ToolWindows/MyToolWindowControl.xaml | 653 |
| ToolWindows/MyToolWindowControl.xaml.cs | 13226 |
| **total** | **18150** |

## Member Exposure Snapshot
- 116 public, 9 internal, and 382 private members across the partials (`scratch/mytoolwindowcontrol_member_inventory.md`). Public API surface clusters in:
  - UI models & telemetry helpers in `ToolWindows/MyToolWindowControl.Types.cs` (53 members) and `ToolWindows/MyToolWindowControl.Telemetry.cs` (14 members).
  - Diff tree support types in `ToolWindows/MyToolWindowControl.xaml.cs` (e.g. `DiffTreeItem` at `ToolWindows/MyToolWindowControl.xaml.cs:4121`).
  - Working directory DTOs in `ToolWindows/MyToolWindowControl.WorkingDirectory.Models.cs` (16 constructors/properties).
- Internals are limited to environment readiness helpers (`ToolWindows/MyToolWindowControl.Lifecycle.cs:10` and `ToolWindows/MyToolWindowControl.WorkingDirectory.Environment.cs:128`) plus diff-tree mutators (e.g. `ToolWindows/MyToolWindowControl.xaml.cs:4425`).
- No protected surface is exposed; the control relies on private fields accessed across partial files (see cross-partial map below).

## Async and Threading Inventory
- 84 async methods catalogued in `scratch/mytoolwindowcontrol_async_methods.md`; classification summary in `scratch/mytoolwindowcontrol_async_threading.md` shows:
  - 47 methods explicitly hop to the UI thread via `ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync`.
  - 30 `async void` handlers (UI events) including message routing and CLI callbacks.
  - 15 methods send traffic over `CodexCliHost.SendAsync` (CLI coupling) and 20 touch VS UI services while async.
  - Only 17 async routines run purely in background logic.
- Hotspots: CLI callbacks like `HandleAgentMessageDelta` (`ToolWindows/MyToolWindowControl.xaml.cs:813`) and exec handlers (`ToolWindows/MyToolWindowControl.Exec.cs:18`) are `async void` and mix UI access with host calls, complicating error handling.

## CLI / Diff / Exec / MCP Entry Points
- **CLI wiring**: host created in `CreateHost` (`ToolWindows/MyToolWindowControl.xaml.cs:657`) attaches `HandleStdout`/`HandleStderr`. `HandleStdout` (`ToolWindows/MyToolWindowControl.xaml.cs:1629`) fan-outs more than a dozen `EventKind` cases to specialized handlers across partials; a failure here cascades through diff/exec/MCP flows.
- **Chat transcript**: `HandleAgentMessageDelta` and `HandleAgentMessage` (`ToolWindows/MyToolWindowControl.xaml.cs:813`, `893`) update assistant bubbles through `GetOrCreateAssistantTurn` and `AppendAssistantText` (`ToolWindows/MyToolWindowControl.xaml.cs:18437`, `18461`).
- **Token / stream errors**: `HandleTokenCount` (`ToolWindows/MyToolWindowControl.xaml.cs:1029`) and `HandleStreamError` (`ToolWindows/MyToolWindowControl.xaml.cs:1105`) manipulate telemetry UI and error banners.
- **Patch pipeline (diff)**: `HandleTurnDiff` (`ToolWindows/MyToolWindowControl.Mcp.cs:152`) -> `ProcessDiffDocumentsAsync` (`ToolWindows/MyToolWindowControl.Mcp.cs:232`) -> `UpdateDiffTreeAsync` (`ToolWindows/MyToolWindowControl.Mcp.cs:187`). UI applications culminate in `ApplySelectedDiffsAsync` (`ToolWindows/MyToolWindowControl.xaml.cs:8129`) and file writes `ApplyDocumentTextAsync` (`ToolWindows/MyToolWindowControl.xaml.cs:8945`).
- **Exec pipeline**: approval + streaming handled via `HandleExecApproval`, `HandleExecCommandBegin`, `HandleExecCommandOutputDelta`, `HandleExecCommandEnd` (`ToolWindows/MyToolWindowControl.Exec.cs:18,67,127,163`) with helper state in `ToolWindows/MyToolWindowControl.Exec.Helpers.cs:12-121`.
- **MCP integration**: `HandleListMcpTools`/`HandleListCustomPrompts`/`HandleToolCall*` (`ToolWindows/MyToolWindowControl.Mcp.cs:19-122`) populate `_mcpTools`, `_mcpToolRuns`, `_customPrompts` and drive UI updates (`ToolWindows/MyToolWindowControl.Mcp.Helpers.cs:10-45`).
- **CLI error path**: `HandleStderr` (`ToolWindows/MyToolWindowControl.Authentication.cs:202`) routes auth failures and telemetry logging.

## UI Elements, Handlers, and Data Sources
- Named element to handler map in `scratch/mytoolwindowcontrol_ui_events.md`; highlights:
  - Top command bar: `LogoutButton` → `OnLogoutClick` (`ToolWindows/MyToolWindowControl.Authentication.cs:120`); `ExecConsoleToggle` → `OnExecConsoleToggleChanged` (`ToolWindows/MyToolWindowControl.xaml.cs:5513`); `CopyAllButton` → `OnCopyAllClick` (`ToolWindows/MyToolWindowControl.Transcript.cs:18`); `ResetApprovalsButton` → `OnResetApprovalsClick` (`ToolWindows/MyToolWindowControl.Approvals.cs:17`).
  - Approval banner controls bound by `ShowApprovalBanner` (`ToolWindows/MyToolWindowControl.Approvals.cs:85`) and `ResolveActiveApprovalAsync` (`ToolWindows/MyToolWindowControl.Approvals.cs:114`).
  - Selector combos (`ApprovalCombo`, `ModelCombo`, `ReasoningCombo`) are populated and normalized in `InitializeSelectorsAsync` (`ToolWindows/MyToolWindowControl.Options.cs:14`) using static arrays (`ToolWindows/MyToolWindowControl.xaml.cs:873-985`).
  - Data regions: `McpToolsList`/`McpToolRunsList`/`CustomPromptsList` ItemsSource wired to observable collections in `InitializeMcpToolsUi` (`ToolWindows/MyToolWindowControl.Mcp.Helpers.cs:10-21`) and refreshed in `UpdateMcpToolsUi`/`UpdateMcpToolRunsUi`/`UpdateCustomPromptsUi` (`ToolWindows/MyToolWindowControl.xaml.cs:8319-8491`).
  - Diff tree view ItemsSource toggled in `UpdateDiffTreeAsync` (`ToolWindows/MyToolWindowControl.Mcp.cs:187-222`), with check events funneled to `OnDiffTreeCheckBoxClick` (`ToolWindows/MyToolWindowControl.xaml.cs:5209`).
  - Transcript area uses `StackPanel Transcript` and `ScrollViewer TranscriptScrollViewer` (chat updates in `AppendAssistantText`/`AppendExecText`).
  - Status & telemetry widgets updated via `UpdateTelemetryUi` (`ToolWindows/MyToolWindowControl.xaml.cs:23749`), `UpdateTokenUsage` (`ToolWindows/MyToolWindowControl.xaml.cs:23405`), and `UpdateStreamingIndicator` (`ToolWindows/MyToolWindowControl.xaml.cs:18781`).

## Cross-Partial Dependencies and Shared State
- 52 private fields declared in `ToolWindows/MyToolWindowControl.xaml.cs` are consumed by other partials (`scratch/mytoolwindowcontrol_cross_fields.md`). Key examples:
  - `_host` (`ToolWindows/MyToolWindowControl.xaml.cs:369`) drives authentication, exec, heartbeat, and working directory flows across six partials.
  - `_approvalQueue` / `_activeApproval` (`ToolWindows/MyToolWindowControl.xaml.cs:457-465`) underpin approval UX while logic lives in `MyToolWindowControl.Approvals.cs`.
  - `_options`, `_selectedModel`, `_selectedReasoning`, `_selectedApprovalMode` (lines 481, 761-777) link lifecycle, options, approvals, and windowing modules.
  - Working directory triad `_solutionService`, `_solutionEvents`, `_workingDir` (lines 561, 569, 489) shared with environment/subscription partials and lifecycle gating.
  - Diff/exec state dictionaries (`_diffTreeRoots`, `_execTurns`, `_mcpToolRuns` etc.) are mutated from specialized partials, making ordering and disposal delicate.
- Shared static state includes `MyToolWindowControl.Current` (`ToolWindows/MyToolWindowControl.xaml.cs:1297`) and cached ANSI brush tables (`ToolWindows/MyToolWindowControl.xaml.cs:15141,15237`).

## Duplicate or Shadowed Helpers
- JSON access helpers (`TryGetString` and friends) exist both in `ToolWindows/MyToolWindowControl.JsonHelpers.cs:11-103` and within `Core/DiffUtilities.cs:170-215`, duplicating responsibility.
- Logging helpers (`LogTelemetryAsync`, `LogAssistantTextAsync`, `LogAutoApprovalAsync`) are embedded in the control despite similar facilities under `CodexVS22.Core` telemetry abstractions; consolidation opportunity noted in `ToolWindows/MyToolWindowControl.Telemetry.cs:26-151` vs `ToolWindows/MyToolWindowControl.xaml.cs:25317-25453`.
- Multiple safe-invoke wrappers exist (`SafeInvokeAsync`, `SafeInvokeTupleAsync` in `ToolWindows/MyToolWindowControl.WorkingDirectory.Helpers.cs:145-189`) overlapping with inline `ThreadHelper.JoinableTaskFactory.RunAsync` usage elsewhere.

## External Integrations
- **Codex backend**: `CodexCliHost` (creation at `ToolWindows/MyToolWindowControl.xaml.cs:657`) plus approval/heartbeat helpers (`ToolWindows/MyToolWindowControl.Heartbeat.cs:18-159`).
- **Visual Studio services**: `VS.StatusBar`, `VS.Documents`, `VS.Solutions`, `VS.GetServiceAsync` (e.g. `ToolWindows/MyToolWindowControl.WorkingDirectory.Environment.cs:24`, `ToolWindows/MyToolWindowControl.xaml.cs:4577`, `ToolWindows/MyToolWindowControl.xaml.cs:7055`).
- **Diagnostics**: `DiagnosticsPane` logging permeates control (`ToolWindows/MyToolWindowControl.Approvals.cs:27`, `ToolWindows/MyToolWindowControl.Exec.cs:47`, etc.).
- **Threading**: `ThreadHelper.JoinableTaskFactory` and `Microsoft.VisualStudio.Threading` used for both UI marshaling and background dispatch.
- **EnvDTE / IVsSolution**: project and solution enumeration (`ToolWindows/MyToolWindowControl.WorkingDirectory.Projects.cs:17-146`, `ToolWindows/MyToolWindowControl.WorkingDirectory.Selection.cs:140-274`).
- **WPF / toolkit**: `Community.VisualStudio.Toolkit.VS`, `System.Windows.Controls` events, `Clipboard` interactions (`ToolWindows/MyToolWindowControl.xaml.cs:5365`).
- **Third-party**: `Newtonsoft.Json` for event parsing, `Regex` for exec chunk normalization (`ToolWindows/MyToolWindowControl.xaml.cs:26053`).

## Extraction Risk Hotspots
- Monolithic `HandleStdout` dispatcher (`ToolWindows/MyToolWindowControl.xaml.cs:1629`) intertwines CLI, diff, exec, MCP flows; splitting requires carefully staged event routing.
- Patch apply path (`ToolWindows/MyToolWindowControl.xaml.cs:8129-8129` and `8945`) mixes VS document APIs, file IO, telemetry, and approvals — high coupling and UI thread dependencies.
- Approval pipeline depends on synchronous UI lookups (`FindName`) and shared queues (`ToolWindows/MyToolWindowControl.Approvals.cs:70-163`), fragile under async refactor.
- Working directory subscriptions juggle VS UI contexts and CLI restarts (`ToolWindows/MyToolWindowControl.WorkingDirectory.Subscriptions.cs:16-216`), with static environment gates (`ToolWindows/MyToolWindowControl.Lifecycle.cs:10-74`).
- Heavy use of `async void` (30 sites) means exception propagation and cancellation will need redesign during modularization.

## Artifact Index
- `scratch/mytoolwindowcontrol_file_metrics.md` – raw line metrics.
- `scratch/mytoolwindowcontrol_member_inventory.md` – full exposure map with line references.
- `scratch/mytoolwindowcontrol_async_methods.md` & `scratch/mytoolwindowcontrol_async_threading.md` – async inventory + threading classification.
- `scratch/mytoolwindowcontrol_ui_events.md` – named UI elements and event handlers.
- `scratch/mytoolwindowcontrol_cross_fields.md` – cross-partial shared state usages.

